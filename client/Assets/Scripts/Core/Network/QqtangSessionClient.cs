using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Qqtang.Core.Protocol;

namespace Qqtang.Core.Network
{
    public sealed class QqtangSessionClient : IDisposable
    {
        private TcpClient _tcp;
        private NetworkStream _stream;
        private UdpClient _udp;
        private IPEndPoint _udpServerEndPoint;
        private CancellationTokenSource _cts;
        private readonly SemaphoreSlim _tcpWriteLock = new SemaphoreSlim(1, 1);

        private int _outerSeq;
        private int _routeSeq;
        private int _udpPacketNumber;
        private uint _battleMsgSeq;
        private ushort _battleBatchIndex;
        private uint _clientTimeCounter = 1;

        private readonly ConcurrentQueue<DecodedLocalPacket> _incomingTcp = new ConcurrentQueue<DecodedLocalPacket>();
        private readonly ConcurrentQueue<GameplayBatch> _incomingUdpBatches = new ConcurrentQueue<GameplayBatch>();
        private readonly ConcurrentQueue<string> _diagnostics = new ConcurrentQueue<string>();

        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<DecodedLocalPacket>> _pendingResponses =
            new ConcurrentDictionary<ushort, TaskCompletionSource<DecodedLocalPacket>>();

        public uint Uin { get; private set; }
        public ushort PlayerId { get; private set; }
        public ushort SectionId { get; private set; } = 1;
        public string Nickname { get; private set; } = string.Empty;
        public byte Gender { get; private set; }
        public byte RoleId { get; private set; } = 1;
        public string ConnectedHost { get; private set; } = "127.0.0.1";
        public int ConnectedGamePort { get; private set; } = 18000;
        public int ShopPort { get; set; } = 18001;
        public bool IsConnected => _tcp != null && _tcp.Connected;
        public bool UdpPresenceConfirmed { get; private set; }
        public LegacyUdpEndpoint ObservedUdpEndpoint { get; private set; }
        public LoginResponseData LoginInfo { get; private set; }

        public async Task<LoginResponseData> ConnectAndLoginAsync(
            string host,
            int gamePort,
            uint uin,
            string password,
            byte roleId = 1,
            ushort sectionId = 1,
            CancellationToken ct = default)
        {
            Disconnect();
            ConnectedHost = host;
            ConnectedGamePort = gamePort;
            ShopPort = gamePort + 1;
            Uin = uin;
            RoleId = roleId;
            SectionId = sectionId;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);

            // Step 1: QQTAUTH1 challenge-response on the game TCP listener
            _tcp = new TcpClient { NoDelay = true };
            await _tcp.ConnectAsync(host, gamePort).ConfigureAwait(false);
            _stream = _tcp.GetStream();

            await WriteRawFrameAsync(AccountAuthCodec.MarshalHello(uin), _cts.Token).ConfigureAwait(false);
            byte[] challengeFrame = await ReadRawFrameAsync(_stream, _cts.Token).ConfigureAwait(false);
            AccountAuthMessage challenge = AccountAuthCodec.Unmarshal(challengeFrame);
            if (challenge.Type == AccountAuthMessageType.Result && !challenge.Accepted)
                throw new InvalidOperationException($"Account auth rejected: {challenge.Reason}");
            if (challenge.Type != AccountAuthMessageType.Challenge)
                throw new InvalidOperationException($"Expected QQTAUTH1 Challenge, got {challenge.Type}.");

            byte[] proof = AccountAuthCodec.ComputeClientProof(password, challenge.Salt, challenge.Iterations, challenge.Nonce, uin);
            await WriteRawFrameAsync(AccountAuthCodec.MarshalProof(uin, proof), _cts.Token).ConfigureAwait(false);
            byte[] resultFrame = await ReadRawFrameAsync(_stream, _cts.Token).ConfigureAwait(false);
            AccountAuthMessage authResult = AccountAuthCodec.Unmarshal(resultFrame);
            if (authResult.Type != AccountAuthMessageType.Result || !authResult.Accepted)
                throw new InvalidOperationException($"Account login failed: {authResult.Reason}");

            Nickname = string.IsNullOrEmpty(authResult.Nickname) ? $"P{uin}" : authResult.Nickname;
            Gender = authResult.Gender;

            // Start TCP receive loop before sending Game Login
            _ = Task.Run(() => TcpReceiveLoopAsync(_cts.Token));

            // Step 2: Game Login (0x0064) on the same TCP connection
            byte[] loginPayload = GameRequestBuilder.BuildLoginPayload(uin, Nickname, authResult.Gender, sectionId, roleId, NextClientTime());
            DecodedLocalPacket loginReply = await SendRequestAsync(GameCommands.Login, 2, sectionId, loginPayload, _cts.Token).ConfigureAwait(false);
            LoginInfo = LoginResponseData.Parse(loginReply.Payload);
            if (LoginInfo.ResultId != 0)
                throw new InvalidOperationException($"Game login rejected: result={LoginInfo.ResultId} reason={LoginInfo.Reason}");

            PlayerId = LoginInfo.PlayerId;
            SectionId = LoginInfo.SectionId;

            // Step 3: Bind QQTPPP UDP socket and register Type-1 presence
            IPAddress serverIp = (await Dns.GetHostAddressesAsync(host).ConfigureAwait(false))[0];
            foreach (var addr in await Dns.GetHostAddressesAsync(host).ConfigureAwait(false))
            {
                if (addr.AddressFamily == AddressFamily.InterNetwork)
                {
                    serverIp = addr;
                    break;
                }
            }
            _udpServerEndPoint = new IPEndPoint(serverIp, gamePort);
            _udp = new UdpClient(new IPEndPoint(IPAddress.Any, 0));
            _ = Task.Run(() => UdpReceiveLoopAsync(_cts.Token));
            await SendUdpPresenceAsync().ConfigureAwait(false);

            return LoginInfo;
        }

        public async Task SendUdpPresenceAsync()
        {
            if (_udp == null || _udpServerEndPoint == null || PlayerId == 0) return;
            var localEp = (IPEndPoint)_udp.Client.LocalEndPoint;
            var advertised = new LegacyUdpEndpoint
            {
                IPv4 = new byte[] { 127, 0, 0, 1 },
                Port = (ushort)localEp.Port
            };
            uint pktNum = (uint)Interlocked.Increment(ref _udpPacketNumber);
            byte[] dgram = QqtPppUdpCodec.EncodePresence(pktNum, PlayerId, Uin, advertised);
            await _udp.SendAsync(dgram, dgram.Length, _udpServerEndPoint).ConfigureAwait(false);
        }

        public async Task SendHelloAsync(CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildHelloPayload(Uin, NextClientTime());
            await SendRequestAsync(GameCommands.Hello, 2, SectionId, payload, ct).ConfigureAwait(false);
        }

        public async Task<RoomListResponseData> RefreshRoomListAsync(byte gameType = 0, byte gameMode = 0, CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildRoomListPayload(Uin, NextClientTime(), 2, 1, 50, gameMode, gameType);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.RoomList, 2, SectionId, payload, ct).ConfigureAwait(false);
            return RoomListResponseData.Parse(reply.Payload);
        }

        public async Task<ushort> CreateRoomAsync(string roomName, RoomPropertyFlag flag = RoomPropertyFlag.Standard, RoomGameType gameType = RoomGameType.CompetitiveNoItem, string password = "", CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildCreateRoomPayload(Uin, NextClientTime(), roomName, flag, gameType, password);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.CreateRoom, 2, SectionId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            ushort resId = r.ReadUInt16();
            ushort roomId = r.ReadUInt16();
            if (resId != 0) throw new InvalidOperationException($"CreateRoom failed: result={resId}");
            return roomId;
        }

        public async Task<EnterRoomResponseData> EnterRoomAsync(ushort roomId, byte roleId = 0, string password = "", CancellationToken ct = default)
        {
            byte effectiveRole = roleId != 0 ? roleId : RoleId;
            byte[] payload = GameRequestBuilder.BuildEnterRoomPayload(Uin, NextClientTime(), roomId, effectiveRole, password);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.EnterRoom, 2, SectionId, payload, ct).ConfigureAwait(false);
            return EnterRoomResponseData.Parse(reply.Payload);
        }

        public async Task LeaveRoomAsync(ushort roomId, CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildLeaveRoomPayload(Uin, NextClientTime());
            await SendRequestAsync(GameCommands.LeaveRoom, 3, roomId, payload, ct).ConfigureAwait(false);
        }

        public async Task SelectMapAsync(ushort roomId, ushort mapId, RoomGameType gameType = RoomGameType.CompetitiveNoItem, CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildModifyRoomInfoPayload(Uin, NextClientTime(), mapId, gameType);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.ModifyRoomInfo, 3, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            ushort res = r.ReadUInt16();
            if (res != 0) throw new InvalidOperationException($"SelectMap failed: result={res}");
        }

        public async Task ChangeTeamAsync(ushort roomId, byte teamId, CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildChangeTeamPayload(Uin, NextClientTime(), teamId);
            await SendRequestAsync(GameCommands.ChangeTeam, 3, roomId, payload, ct).ConfigureAwait(false);
        }

        public async Task ChangeRoleAsync(ushort roomId, byte roleId, CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildChangeRolePayload(Uin, NextClientTime(), roleId);
            await SendRequestAsync(GameCommands.ChangeRole, 3, roomId, payload, ct).ConfigureAwait(false);
            RoleId = roleId;
        }

        public async Task SetReadyAsync(ushort roomId, bool ready, CancellationToken ct = default)
        {
            ushort cmd = ready ? GameCommands.Ready : GameCommands.CancelReady;
            byte[] payload = GameRequestBuilder.BuildReadyPayload(Uin, NextClientTime());
            await SendRequestAsync(cmd, 3, roomId, payload, ct).ConfigureAwait(false);
        }

        public async Task SetItemStatusAsync(uint itemId, byte newStatus, byte newRoleId = 0, CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildItemStatusChangePayload(Uin, NextClientTime(), itemId, newStatus, newRoleId);
            await SendRequestAsync(GameCommands.ItemStatusChange, 2, SectionId, payload, ct).ConfigureAwait(false);
        }

        public async Task<ushort> StartGameAsync(ushort roomId, CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildStartGamePayload(Uin, NextClientTime());
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.StartGame, 3, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task SendUdpOkAsync(ushort destPlayerId, uint destUin, bool readyMode = true, CancellationToken ct = default)
        {
            byte[] info;
            if (readyMode)
            {
                // Non-zero mode (1) + 4-byte logical address + 2-byte port tells server/AI native peer is ready.
                var ep = ObservedUdpEndpoint.IPv4 != null && ObservedUdpEndpoint.Port != 0
                    ? ObservedUdpEndpoint
                    : new LegacyUdpEndpoint { IPv4 = new byte[] { 127, 0, 0, 1 }, Port = (ushort)((IPEndPoint)_udp.Client.LocalEndPoint).Port };
                info = new byte[7];
                info[0] = 1;
                Buffer.BlockCopy(ep.IPv4, 0, info, 1, 4);
                info[5] = (byte)(ep.Port >> 8);
                info[6] = (byte)ep.Port;
            }
            else
            {
                info = new byte[] { 0 };
            }
            byte[] payload = GameRequestBuilder.BuildTransferUdpOkPayload(Uin, NextClientTime(), destPlayerId, destUin, info);
            await SendOneWayAsync(GameCommands.RequestUdpOk, 4, 0xFFFF, payload, ct).ConfigureAwait(false);
        }

        public async Task SendGameEventTcpAsync(ushort roomId, ushort schema, byte[] body, CancellationToken ct = default)
        {
            byte[] payload = GameRequestBuilder.BuildGameEventRequestPayload(Uin, NextClientTime(), schema, body);
            await SendOneWayAsync(GameCommands.GameEventRequest, 3, roomId, payload, ct).ConfigureAwait(false);
        }

        // --- Extended Lobby, Room, Chat, Economy, Craft, Pet, Friend, Kin, Marriage APIs ---

        public async Task LogoutAsync(CancellationToken ct = default)
        {
            if (!IsConnected) return;
            try
            {
                byte[] payload = ExtendedRequestBuilder.BuildLogoutPayload(Uin, NextClientTime());
                await SendRequestAsync(GameCommands.Logout, 2, SectionId, payload, ct).ConfigureAwait(false);
            }
            catch { }
            finally
            {
                Disconnect();
            }
        }

        public async Task<PlayerListResponseData> RefreshPlayerListAsync(ushort startPlayerId = 0, ushort number = 50, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildPlayerListPayload(Uin, NextClientTime(), startPlayerId, number);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.PlayerList, 2, SectionId, payload, ct).ConfigureAwait(false);
            return PlayerListResponseData.Parse(reply.Payload);
        }

        public async Task<FindFriendResponseData> FindFriendAsync(uint targetUin, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildFindFriendPayload(Uin, NextClientTime(), targetUin);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.FindFriend, 2, SectionId, payload, ct).ConfigureAwait(false);
            return FindFriendResponseData.Parse(reply.Payload);
        }

        public async Task<GameMoneyResponseData> GetGameMoneyAsync(CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildGetGameMoneyPayload(Uin, NextClientTime());
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.GetGameMoney, 2, SectionId, payload, ct).ConfigureAwait(false);
            return GameMoneyResponseData.Parse(reply.Payload);
        }

        public async Task<ushort> SendRoomChatAsync(ushort roomId, string content, ushort dstPlayerId = 0xFFFF, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildRoomChatPayload(Uin, NextClientTime(), dstPlayerId, content);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.RoomChat, 3, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task<ushort> SendSectionChatAsync(string content, bool smallBugle = false, CancellationToken ct = default)
        {
            uint dstUin = smallBugle ? 1u : 0u;
            byte[] payload = ExtendedRequestBuilder.BuildSectionChatPayload(Uin, NextClientTime(), 0xFFFF, dstUin, content);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.SectionChat, 2, SectionId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task<JoinRoomResponseData> QuickJoinRoomAsync(RoomGameType gameType = RoomGameType.CompetitiveNoItem, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildJoinRoomPayload(Uin, NextClientTime(), gameType);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.JoinRoom, 2, SectionId, payload, ct).ConfigureAwait(false);
            return JoinRoomResponseData.Parse(reply.Payload);
        }

        public async Task<ushort> ModifyRoomAsync(
            ushort roomId, string roomName, RoomPropertyFlag roomFlag, string password,
            RoomGameType gameType, bool nameChanged = true, bool passwordChanged = false, CancellationToken ct = default)
        {
            ModifyRoomFlags flags = ModifyRoomFlags.None;
            if (nameChanged) flags |= ModifyRoomFlags.NameChanged;
            if ((roomFlag & RoomPropertyFlag.Password) != 0 && !string.IsNullOrEmpty(password))
                flags |= ModifyRoomFlags.PasswordPresent | ModifyRoomFlags.PasswordEnabled;
            if (passwordChanged) flags |= ModifyRoomFlags.PasswordChanged;

            string effectivePassword = (roomFlag & RoomPropertyFlag.Password) != 0 ? (password ?? string.Empty) : string.Empty;
            byte[] payload = ExtendedRequestBuilder.BuildModifyRoomPayload(Uin, NextClientTime(), flags, roomName, roomFlag, effectivePassword, gameType);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.ModifyRoom, 3, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task<ushort> ChangeRoomTypeAsync(ushort roomId, RoomGameType gameType, uint continueId = 0, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildChangeRoomTypePayload(Uin, NextClientTime(), gameType, continueId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.ChangeRoomType, 3, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task<ushort> SetSeatStatusAsync(ushort roomId, byte seatId, bool locked, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildSetSeatStatusPayload(Uin, NextClientTime(), seatId, locked);
            DecodedLocalPacket reply = await SendRequestAsync(GameCommands.SetSeatStatus, 3, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task<ushort> KickOffPlayerAsync(ushort roomId, ushort targetPlayerId, uint targetUin, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildKickOffPlayerPayload(Uin, NextClientTime(), targetPlayerId, targetUin);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.KickOffPlayer, 3, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task<ushort> UseItemInRoomAsync(ushort roomId, uint itemId, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildUseItemInRoomPayload(Uin, NextClientTime(), itemId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.UseItemInRoom, 3, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task<BackgroundListResponseData> GetBackgroundListAsync(CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildBackgroundListPayload(Uin, NextClientTime());
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.BackgroundList, 4, 0xFFFF, payload, ct).ConfigureAwait(false);
            return BackgroundListResponseData.Parse(reply.Payload);
        }

        public async Task<ushort> UseBackgroundAsync(ushort roomId, uint backgroundItemId, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildUseBackgroundPayload(Uin, NextClientTime(), backgroundItemId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.UseBackground, 4, roomId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            r.ReadUInt32(); // UIN
            r.ReadUInt32(); // Time
            return r.ReadUInt16();
        }

        // Shop uses a short-lived auxiliary TCP connection to the dedicated shop listener (gamePort + 1).
        public async Task<ShopListResponseData> QueryShopListAsync(uint clientShopVersion = 848, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildShopListPayload(Uin, NextClientTime(), 848, 1, clientShopVersion);
            DecodedLocalPacket reply = await SendShopSocketRequestAsync(ExtendedGameCommands.ShopList, payload, ct).ConfigureAwait(false);
            return ShopListResponseData.Parse(reply.Payload);
        }

        public async Task<ShopBuyResponseData> BuyCommodityAsync(
            uint commodityId, ShopPaymentMethod payType = ShopPaymentMethod.GameMoney, uint catalogVersion = 848, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildShopBuyPayload(Uin, NextClientTime(), commodityId, payType, 2, 848, catalogVersion, 0);
            DecodedLocalPacket reply = await SendShopSocketRequestAsync(ExtendedGameCommands.ShopBuy, payload, ct).ConfigureAwait(false);
            return ShopBuyResponseData.Parse(reply.Payload);
        }

        private async Task<DecodedLocalPacket> SendShopSocketRequestAsync(ushort command, byte[] payload, CancellationToken ct)
        {
            using (var shopTcp = new TcpClient { NoDelay = true })
            {
                await shopTcp.ConnectAsync(ConnectedHost, ShopPort).ConfigureAwait(false);
                using (var stream = shopTcp.GetStream())
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                    uint outerSeq = (uint)Interlocked.Increment(ref _outerSeq);
                    ushort routeSeq = (ushort)Interlocked.Increment(ref _routeSeq);
                    byte[] frame = LocalPacketCodec.EncodeLocalPacket(outerSeq, routeSeq, Uin, command, 2, SectionId, payload);
                    await stream.WriteAsync(frame, 0, frame.Length, timeoutCts.Token).ConfigureAwait(false);
                    await stream.FlushAsync(timeoutCts.Token).ConfigureAwait(false);
                    byte[] rawReply = await ReadRawFrameAsync(stream, timeoutCts.Token).ConfigureAwait(false);
                    return LocalPacketCodec.DecodeLocalPacket(rawReply);
                }
            }
        }

        public async Task<CombineForgeResponseData> CombineForgeAsync(byte type, uint itemId, uint fromItemId, IReadOnlyList<uint> materialIds, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildCombineForgePayload(Uin, NextClientTime(), type, itemId, fromItemId, materialIds);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.CombineForge, 2, SectionId, payload, ct).ConfigureAwait(false);
            return CombineForgeResponseData.Parse(reply.Payload);
        }

        public async Task<ForgeResponseData> ForgeAvatarAsync(byte type, uint itemId, uint materialId, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildForgePayload(Uin, NextClientTime(), type, itemId, materialId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.Forge, 2, SectionId, payload, ct).ConfigureAwait(false);
            return ForgeResponseData.Parse(reply.Payload);
        }

        public async Task<BreakEggResponseData> BreakEggAsync(uint hammerId, uint eggId, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildBreakEggPayload(Uin, NextClientTime(), hammerId, eggId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.BreakEgg, 2, SectionId, payload, ct).ConfigureAwait(false);
            return BreakEggResponseData.Parse(reply.Payload);
        }

        public async Task<PlayerPetsResponseData> RefreshPetsAsync(CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildPlayerPetsPayload(Uin, NextClientTime());
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.PlayerPets, 2, SectionId, payload, ct).ConfigureAwait(false);
            return PlayerPetsResponseData.Parse(reply.Payload);
        }

        public async Task<HandlePetResponseData> HandlePetAsync(PetEventId eventId, uint petId, string parameter = "", CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildHandlePetPayload(Uin, NextClientTime(), eventId, petId, parameter);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.HandlePet, 2, SectionId, payload, ct).ConfigureAwait(false);
            return HandlePetResponseData.Parse(reply.Payload);
        }

        public async Task<FriendListResponseData> RefreshFriendsAsync(CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildFriendListPayload(Uin, NextClientTime());
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.FriendList, 2, SectionId, payload, ct).ConfigureAwait(false);
            return FriendListResponseData.Parse(reply.Payload);
        }

        public async Task AddFriendAsync(uint targetUin, string word = "你好，交个朋友吧", CancellationToken ct = default)
        {
            // Server sends no immediate response on success (forwards 0x00A3 to target; rejects via 0x00A6).
            byte[] payload = ExtendedRequestBuilder.BuildAddFriendPayload(Uin, NextClientTime(), targetUin, word);
            await SendOneWayAsync(ExtendedGameCommands.AddFriend, 2, SectionId, payload, ct).ConfigureAwait(false);
        }

        public async Task<RemoveFriendResponseData> RemoveFriendAsync(uint targetUin, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildRemoveFriendPayload(Uin, NextClientTime(), targetUin);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.RemoveFriend, 2, SectionId, payload, ct).ConfigureAwait(false);
            return RemoveFriendResponseData.Parse(reply.Payload);
        }

        public async Task<AnswerFriendResultData> AnswerFriendAsync(uint requesterUin, bool accepted, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildAnswerFriendPayload(accepted, requesterUin, NextClientTime(), Uin);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.AnswerFriend, 2, SectionId, payload, ct).ConfigureAwait(false);
            return AnswerFriendResultData.Parse(reply.Payload);
        }

        public async Task<CreateKinResponseData> CreateKinAsync(string name, string declaration, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildCreateKinPayload(Uin, NextClientTime(), name, declaration, SectionId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.CreateKin, 2, SectionId, payload, ct).ConfigureAwait(false);
            return CreateKinResponseData.Parse(reply.Payload);
        }

        public async Task<KinBaseData> FetchKinBaseAsync(uint kinIndex, bool isMember = true, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildFetchKinBasePayload(Uin, NextClientTime(), kinIndex, isMember ? 1u : 0u);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.FetchKinBase, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinBaseData.ParseFetchBaseResponse(reply.Payload);
        }

        public async Task<FetchKinMembersResponseData> FetchKinMembersAsync(uint kinIndex, uint listUpdate = 0, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildFetchKinMembersPayload(Uin, NextClientTime(), kinIndex, listUpdate);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.FetchKinMemberList, 2, SectionId, payload, ct).ConfigureAwait(false);
            return FetchKinMembersResponseData.Parse(reply.Payload);
        }

        public async Task<KinOperationResponseData> InviteToKinAsync(uint kinIndex, uint targetUin, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildKinOperationPayload(Uin, NextClientTime(), kinIndex, targetUin, 0, 0);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.OperateKin, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinOperationResponseData.Parse(reply.Payload);
        }

        public async Task AnswerKinInviteAsync(uint inviterUin, uint kinIndex, bool accepted, CancellationToken ct = default)
        {
            // One-way answer; server replies with ServerOperateKinNotify (0x00BD) follow-up.
            byte[] payload = ExtendedRequestBuilder.BuildKinServerOperationAnswerPayload(inviterUin, NextClientTime(), kinIndex, Uin, accepted);
            await SendOneWayAsync(ExtendedGameCommands.ServerOperateKin, 2, SectionId, payload, ct).ConfigureAwait(false);
        }

        public async Task SendKinChatAsync(uint kinIndex, string message, CancellationToken ct = default)
        {
            // One-way request; server echoes KinChatNotify (0x00BF) to all online kin members including sender.
            byte[] payload = ExtendedRequestBuilder.BuildKinChatPayload(Uin, NextClientTime(), kinIndex, message);
            await SendOneWayAsync(ExtendedGameCommands.KinChat, 2, SectionId, payload, ct).ConfigureAwait(false);
        }

        public async Task<KinSimpleResultData> KickKinMemberAsync(uint kinIndex, uint dstUin, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildKickKinMemberPayload(Uin, NextClientTime(), dstUin, kinIndex);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.KickKinMember, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinSimpleResultData.ParseTarget(reply.Payload);
        }

        public async Task<KinSimpleResultData> ExitKinAsync(uint kinIndex, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildExitKinPayload(Uin, NextClientTime(), kinIndex);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.ExitKin, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinSimpleResultData.ParseSelf(reply.Payload, false);
        }

        public async Task<KinSimpleResultData> DismissKinAsync(uint kinIndex, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildDismissKinPayload(Uin, NextClientTime(), kinIndex, 0);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.DismissKin, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinSimpleResultData.ParseSelf(reply.Payload, true);
        }

        public async Task<KinSimpleResultData> SetKinDeclarationAsync(uint kinIndex, string declaration, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildSetKinDeclarationPayload(Uin, NextClientTime(), kinIndex, declaration);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.SetKinDeclaration, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinSimpleResultData.ParseText(reply.Payload, false);
        }

        public async Task<KinSimpleResultData> SetKinNotificationAsync(uint kinIndex, string notification, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildSetKinNotificationPayload(Uin, NextClientTime(), kinIndex, notification);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.SetKinNotification, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinSimpleResultData.ParseText(reply.Payload, true);
        }

        public async Task<FetchKinTopResponseData> FetchKinTopAsync(uint kinIndex, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildFetchKinTopPayload(Uin, NextClientTime(), kinIndex);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.FetchKinTop, 2, SectionId, payload, ct).ConfigureAwait(false);
            return FetchKinTopResponseData.Parse(reply.Payload);
        }

        public async Task<KinAuthorityResultData> AssignKinAuthorityAsync(uint kinIndex, uint dstUin, uint authorityId, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildAssignKinAuthorityPayload(kinIndex, Uin, NextClientTime(), dstUin, authorityId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.AssignKinAuthority, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinAuthorityResultData.Parse(reply.Payload);
        }

        public async Task<KinBadgeResultData> SetKinBadgeAsync(uint kinIndex, uint badgeId, uint definedBadgeId = 0, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildSetKinBadgePayload(kinIndex, Uin, NextClientTime(), badgeId, definedBadgeId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.SetKinBadge, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinBadgeResultData.Parse(reply.Payload);
        }

        public async Task<KinSimpleResultData> UpdateKinTitleAsync(uint kinIndex, string title, CancellationToken ct = default)
        {
            string formatted = FormatKinAuthorityTitles(title);
            byte[] payload = ExtendedRequestBuilder.BuildUpdateKinTitlePayload(Uin, NextClientTime(), kinIndex, formatted);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.UpdateKinTitle, 2, SectionId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            r.ReadUInt32(); // UIN
            var res = new KinSimpleResultData
            {
                Result = r.ReadUInt16(),
                KinIndex = kinIndex
            };
            if (r.Remaining >= 2)
            {
                ushort textLen = r.ReadUInt16();
                if (textLen > 0 && r.Remaining >= textLen) r.Skip(textLen);
            }
            if (r.Remaining >= 2)
            {
                ushort reasonLen = r.ReadUInt16();
                if (reasonLen > 0 && r.Remaining >= reasonLen)
                    res.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(reasonLen));
            }
            return res;
        }

        private static string FormatKinAuthorityTitles(string input)
        {
            if (!string.IsNullOrEmpty(input) && input.IndexOf('\x07') >= 0) return input;
            string[] defaults = { "掌门", "副掌门", "长老", "精英", "弟子" };
            string[] raw = string.IsNullOrWhiteSpace(input)
                ? defaults
                : input.Split(new[] { ',', '，', '/', '|' }, StringSplitOptions.RemoveEmptyEntries);
            var parts = new string[5];
            for (int i = 0; i < 5; i++)
            {
                string p = (i < raw.Length && !string.IsNullOrWhiteSpace(raw[i])) ? raw[i].Trim() : defaults[i];
                if (p.Length > 5) p = p.Substring(0, 5); // <= 10 GBK bytes guaranteed
                parts[i] = p;
            }
            return string.Join("\x07", parts) + "\x07";
        }

        public async Task<AcrossSectionChatResponseData> SendAcrossSectionChatAsync(string content, uint dstUin = 0, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildAcrossSectionChatPayload(Uin, NextClientTime(), 0, 0xFFFF, dstUin, content);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.ChatAcrossSection, 2, SectionId, payload, ct).ConfigureAwait(false);
            return AcrossSectionChatResponseData.Parse(reply.Payload);
        }

        public async Task<uint> CheckConfigFilesAsync(CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildGetConfigFilePayload(Uin, NextClientTime());
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.GetConfigFile, 4, 0xFFFF, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.Remaining >= 4 ? r.ReadUInt32() : 0u;
        }

        public async Task<SparkResponseData> RequestSparkAsync(uint targetUin, string word, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildSparkPayload(Uin, targetUin, word);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.RequestSpark, 2, SectionId, payload, ct).ConfigureAwait(false);
            return SparkResponseData.Parse(reply.Payload);
        }

        public async Task<AnswerSparkResultData> AnswerSparkAsync(uint proposerUin, bool accepted, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildAnswerSparkPayload(Uin, proposerUin, accepted);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.AnswerSpark, 2, SectionId, payload, ct).ConfigureAwait(false);
            return AnswerSparkResultData.Parse(reply.Payload);
        }

        public async Task<MarriageInfoResponseData> GetMarriageInfoAsync(uint targetUin = 0, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildMarriageInfoPayload(Uin, targetUin == 0 ? Uin : targetUin);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.MarriageInfo, 2, SectionId, payload, ct).ConfigureAwait(false);
            return MarriageInfoResponseData.Parse(reply.Payload);
        }

        public async Task<MarriageSimpleResultData> ModifyLoveWordAsync(uint spouseUin, string loveWord, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildModifyLoveWordPayload(Uin, spouseUin, loveWord);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.ModifyLoveWord, 2, SectionId, payload, ct).ConfigureAwait(false);
            return MarriageSimpleResultData.Parse(reply.Payload);
        }

        public async Task<MarriageSimpleResultData> DivorceAsync(uint spouseUin, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildDivorcePayload(Uin, spouseUin);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.Divorce, 2, SectionId, payload, ct).ConfigureAwait(false);
            return MarriageSimpleResultData.Parse(reply.Payload);
        }

        public async Task SendUseEmotionAsync(ushort roomId, uint emotionId, uint gameTimeMs, CancellationToken ct = default)
        {
            var body = new UseEmotionEventData { Uin = Uin, Time = gameTimeMs, EmotionId = emotionId }.Marshal();
            await SendGameEventTcpAsync(roomId, ExtendedGameEventSchemas.NotifyUseEmotion, body, ct).ConfigureAwait(false);
        }

        // --- Wedding Ceremony, Hidden Treasure Room, Adventure NextMap, Prepared Props ---

        public async Task<StartWeddingResponseData> StartWeddingAsync(uint targetUin, ushort weddingModeId, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildStartWeddingPayload(Uin, targetUin, weddingModeId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.StartWedding, 2, SectionId, payload, ct).ConfigureAwait(false);
            return StartWeddingResponseData.Parse(reply.Payload);
        }

        public async Task<AnswerWeddingResultData> AnswerWeddingAsync(uint proposerUin, bool accepted, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildAnswerWeddingPayload(Uin, proposerUin, accepted);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.AnswerWedding, 2, SectionId, payload, ct).ConfigureAwait(false);
            return AnswerWeddingResultData.Parse(reply.Payload);
        }

        public async Task<ChangeWeddingModeResponseData> ChangeWeddingModeAsync(ushort weddingModeId, CancellationToken ct = default)
        {
            byte[] payload = ModeRequestBuilder.BuildChangeWeddingModePayload(Uin, weddingModeId);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.ChangeWeddingMode, 2, SectionId, payload, ct).ConfigureAwait(false);
            return ChangeWeddingModeResponseData.Parse(reply.Payload);
        }

        public async Task<TreasureRoomResponseData> EnterTreasureRoomAsync(CancellationToken ct = default)
        {
            byte[] payload = ModeRequestBuilder.BuildTreasureRoomPayload(Uin, NextClientTime());
            DecodedLocalPacket reply = await SendRequestAsync(TreasureCommands.EnterTreasure, 2, SectionId, payload, ct).ConfigureAwait(false);
            return TreasureRoomResponseData.Parse(reply.Payload);
        }

        public async Task<ushort> GetTreasureItemAsync(uint itemId, CancellationToken ct = default)
        {
            byte[] payload = ModeRequestBuilder.BuildTreasureItemPayload(Uin, NextClientTime(), itemId);
            DecodedLocalPacket reply = await SendRequestAsync(TreasureCommands.GetTreasureItem, 2, SectionId, payload, ct).ConfigureAwait(false);
            var r = new BigEndianReader(reply.Payload);
            return r.ReadUInt16();
        }

        public async Task LeaveTreasureRoomAsync(CancellationToken ct = default)
        {
            byte[] payload = ModeRequestBuilder.BuildTreasureRoomPayload(Uin, NextClientTime());
            await SendOneWayAsync(TreasureCommands.LeaveTreasure, 2, SectionId, payload, ct).ConfigureAwait(false);
        }

        public async Task SendAdventureNextMapAsync(ushort roomId, uint continueId, uint nextMapId, uint gameTimeMs, CancellationToken ct = default)
        {
            var req = new GameNextMapRequestData { PlayerId = PlayerId, ClientTime = gameTimeMs, ContinueId = continueId, NextMapId = nextMapId };
            await SendGameEventTcpAsync(roomId, ModeEventSchemas.RequestGameNextMap, req.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task SendPreparedUsePropAsync(ushort roomId, uint itemId, ushort posX, ushort posY, CancellationToken ct = default)
        {
            var ev = new PreparedUsePropEventData { PlayerId = PlayerId, ItemId = itemId, PosX = posX, PosY = posY };
            await SendGameEventTcpAsync(roomId, ModeEventSchemas.RequestPreparedUseProp, ev.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task SendBunActionAsync(ushort roomId, ushort schema, ushort posX, ushort posY, byte bunId, byte bunTeamId, uint gameTimeMs, CancellationToken ct = default)
        {
            var ev = new BunActionEventData { PlayerId = PlayerId, ClientTime = gameTimeMs, PosX = posX, PosY = posY, BunId = bunId, BunTeamId = bunTeamId };
            await SendGameEventTcpAsync(roomId, schema, ev.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task SendTankBaseHpAsync(ushort roomId, ushort teamId, short changeHp, uint gameTimeMs, CancellationToken ct = default)
        {
            var ev = new TankBaseHpEventData { PlayerId = PlayerId, Time = gameTimeMs, TeamId = teamId, ChangeHp = changeHp };
            await SendGameEventTcpAsync(roomId, ModeEventSchemas.RequestTankBaseHp, ev.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task SendWrestleAsync(ushort roomId, byte skillType, byte firstTeam, byte playerPos, uint gameTimeMs, CancellationToken ct = default)
        {
            var ev = new WrestleActionEventData { SkillType = skillType, FirstTeam = firstTeam, PlayerPos = playerPos, PlayerId = PlayerId, Time = gameTimeMs };
            await SendGameEventTcpAsync(roomId, ModeEventSchemas.RequestWrestle, ev.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task SendLaunchMachineAsync(ushort roomId, byte firstTeam, byte roleId, uint gameTimeMs, CancellationToken ct = default)
        {
            var ev = new MachineLaunchEventData { FirstTeam = firstTeam, RoleId = roleId, PlayerId = PlayerId, Time = gameTimeMs };
            await SendGameEventTcpAsync(roomId, ModeEventSchemas.RequestLaunchMachine, ev.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task SendCancelUsePropAsync(ushort roomId, uint propId, uint gameTimeMs, CancellationToken ct = default)
        {
            var ev = new CancelUsePropEventData { PlayerId = PlayerId, PropId = propId, Time = gameTimeMs };
            await SendGameEventTcpAsync(roomId, ModeEventSchemas.RequestCancelUseProp, ev.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task SendPushBoxRequestAsync(ushort roomId, byte direction, byte power, byte[] element8, uint gameTimeMs, CancellationToken ct = default)
        {
            var ev = new PushMapElementRequestData { PlayerId = PlayerId, Direction = direction, Power = power, Time = gameTimeMs, Element = element8 ?? new byte[8] };
            await SendGameEventTcpAsync(roomId, ModeEventSchemas.RequestPushMapElement, ev.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task SendDestroyBoxRequestAsync(ushort roomId, ushort posX, ushort posY, byte[] element8, uint gameTimeMs, CancellationToken ct = default)
        {
            var ev = new DestroyBoxRequestData { PlayerId = PlayerId, Time = gameTimeMs, PosX = posX, PosY = posY, Element = element8 ?? new byte[8] };
            await SendGameEventTcpAsync(roomId, ModeEventSchemas.RequestDestroyBox, ev.Marshal(), ct).ConfigureAwait(false);
        }

        public async Task<KinSimpleResultData> SetKinAuthorityTextAsync(uint kinIndex, string titleText, CancellationToken ct = default)
        {
            byte[] payload = ExtendedRequestBuilder.BuildSetKinNotificationPayload(Uin, NextClientTime(), kinIndex, titleText);
            DecodedLocalPacket reply = await SendRequestAsync(ExtendedGameCommands.SetKinAuthority, 2, SectionId, payload, ct).ConfigureAwait(false);
            return KinSimpleResultData.ParseText(reply.Payload, true);
        }

        public async Task SendPeerGameplayAsync(
            uint gameId,
            uint gameTimeMs,
            IReadOnlyList<LegacyUdpMulticastTarget> peers,
            IReadOnlyList<(uint schemaId, byte[] body)> events,
            bool mirrorReliableTcp = false,
            CancellationToken ct = default)
        {
            if (peers == null || peers.Count == 0 || events == null || events.Count == 0) return;

            var pkg = new GameplayDataPackage
            {
                PlayerId = PlayerId,
                Time = gameTimeMs,
                GameId = gameId
            };
            foreach (var ev in events)
            {
                uint seq = ++_battleMsgSeq;
                pkg.MessageIndexes.Add(seq);
                pkg.Messages.Add(new BattleMessageData
                {
                    Time = gameTimeMs,
                    DataId = ev.schemaId,
                    Sequence = seq,
                    Data = ev.body,
                    GameTime = gameTimeMs,
                    Flag = 0
                });
            }

            if (_udp != null && _udpServerEndPoint != null)
            {
                var batch = new GameplayBatch
                {
                    GameId = gameId,
                    FirstIndex = 0,
                    Entries = new List<GameplayBatchEntry>
                    {
                        new GameplayBatchEntry { Index = ++_battleBatchIndex, Package = pkg }
                    }
                };
                byte[] encrypted = batch.EncodeEncrypted();
                uint pktNum = (uint)Interlocked.Increment(ref _udpPacketNumber);
                byte[] dgram = QqtPppUdpCodec.EncodeMulticast(pktNum, PlayerId, Uin, peers, encrypted);
                await _udp.SendAsync(dgram, dgram.Length, _udpServerEndPoint).ConfigureAwait(false);
            }

            if (mirrorReliableTcp && _stream != null)
            {
                var recipientIds = new List<ushort>(peers.Count);
                foreach (var p in peers) recipientIds.Add(p.PlayerId);
                uint outerSeq = (uint)Interlocked.Increment(ref _outerSeq);
                ushort routeSeq = (ushort)Interlocked.Increment(ref _routeSeq);
                byte[] fastFrame = LocalPacketCodec.EncodeRoomFastGameplayPacket(
                    outerSeq, routeSeq, Uin, recipientIds, new[] { pkg.Marshal() }, gameTimeMs);
                await WriteRawFrameAsync(fastFrame, ct).ConfigureAwait(false);
            }
        }

        public bool TryDequeueTcpPacket(out DecodedLocalPacket packet) => _incomingTcp.TryDequeue(out packet);
        public bool TryDequeueUdpBatch(out GameplayBatch batch) => _incomingUdpBatches.TryDequeue(out batch);
        public bool TryDequeueDiagnostic(out string msg) => _diagnostics.TryDequeue(out msg);

        private async Task<DecodedLocalPacket> SendRequestAsync(ushort command, ushort route, ushort sectionId, byte[] payload, CancellationToken ct)
        {
            var tcs = new TaskCompletionSource<DecodedLocalPacket>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingResponses[command] = tcs;
            try
            {
                await SendOneWayAsync(command, route, sectionId, payload, ct).ConfigureAwait(false);
                using (var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    timeoutCts.CancelAfter(TimeSpan.FromSeconds(10));
                    using (timeoutCts.Token.Register(() => tcs.TrySetCanceled()))
                    {
                        return await tcs.Task.ConfigureAwait(false);
                    }
                }
            }
            finally
            {
                _pendingResponses.TryRemove(command, out _);
            }
        }

        private async Task SendOneWayAsync(ushort command, ushort route, ushort sectionId, byte[] payload, CancellationToken ct)
        {
            uint outerSeq = (uint)Interlocked.Increment(ref _outerSeq);
            ushort routeSeq = (ushort)Interlocked.Increment(ref _routeSeq);
            byte[] frame = LocalPacketCodec.EncodeLocalPacket(outerSeq, routeSeq, Uin, command, route, sectionId, payload);
            await WriteRawFrameAsync(frame, ct).ConfigureAwait(false);
        }

        private async Task WriteRawFrameAsync(byte[] frame, CancellationToken ct)
        {
            await _tcpWriteLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (_stream == null) throw new InvalidOperationException("TCP stream is not connected.");
                await _stream.WriteAsync(frame, 0, frame.Length, ct).ConfigureAwait(false);
                await _stream.FlushAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                _tcpWriteLock.Release();
            }
        }

        private async Task TcpReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _stream != null)
                {
                    byte[] raw = await ReadRawFrameAsync(_stream, ct).ConfigureAwait(false);
                    DecodedLocalPacket decoded = LocalPacketCodec.DecodeLocalPacket(raw);

                    // Correlated response has non-zero RouteSequence; unsolicited server notifications have RouteSequence == 0.
                    if (decoded.RouteSequence != 0 && _pendingResponses.TryRemove(decoded.Command, out var tcs))
                    {
                        tcs.TrySetResult(decoded);
                    }
                    else
                    {
                        _incomingTcp.Enqueue(decoded);
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                _diagnostics.Enqueue($"TCP loop ended: {ex.Message}");
                foreach (var kv in _pendingResponses)
                    kv.Value.TrySetException(ex);
            }
        }

        private async Task UdpReceiveLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!ct.IsCancellationRequested && _udp != null)
                {
                    UdpReceiveResult res = await _udp.ReceiveAsync().ConfigureAwait(false);
                    byte[] data = res.Buffer;
                    if (data.Length == QqtPppUdpCodec.PeerHandshakeSize)
                    {
                        // Reply to 0x20 peer probe with 0x21 so peers/AI advance smoothly
                        if (data[2] == QqtPppUdpCodec.PeerHandshakeRequest && ObservedUdpEndpoint.IPv4 != null)
                        {
                            byte[] resp = QqtPppUdpCodec.EncodePeerHandshake(QqtPppUdpCodec.PeerHandshakeResponse, Uin, ObservedUdpEndpoint);
                            await _udp.SendAsync(resp, resp.Length, res.RemoteEndPoint).ConfigureAwait(false);
                        }
                        continue;
                    }
                    if (data.Length < QqtPppUdpCodec.ControlHeaderSize) continue;

                    try
                    {
                        LegacyUdpControlPacket pkt = QqtPppUdpCodec.DecodeControlPacket(data);
                        if (pkt.Type == LegacyUdpMessageType.Presence && pkt.Presence.HasValue)
                        {
                            ObservedUdpEndpoint = pkt.Presence.Value;
                            UdpPresenceConfirmed = true;
                        }
                        else if (pkt.Type == LegacyUdpMessageType.Multicast && pkt.MulticastData != null)
                        {
                            GameplayBatch batch = GameplayBatch.DecodeEncrypted(pkt.MulticastData);
                            _incomingUdpBatches.Enqueue(batch);
                        }
                    }
                    catch (Exception parseEx)
                    {
                        _diagnostics.Enqueue($"UDP packet ignored: {parseEx.Message}");
                    }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                if (!ct.IsCancellationRequested)
                    _diagnostics.Enqueue($"UDP loop ended: {ex.Message}");
            }
        }

        private static async Task<byte[]> ReadRawFrameAsync(Stream stream, CancellationToken ct)
        {
            byte[] header = new byte[4];
            await ReadExactAsync(stream, header, 0, 4, ct).ConfigureAwait(false);
            uint totalLen = QqTea.ReadUInt32BE(header, 0);
            if (totalLen < 4 || totalLen > 1024 * 1024)
                throw new InvalidOperationException($"Invalid TCP frame length {totalLen}.");
            byte[] frame = new byte[totalLen];
            Buffer.BlockCopy(header, 0, frame, 0, 4);
            if (totalLen > 4)
                await ReadExactAsync(stream, frame, 4, (int)totalLen - 4, ct).ConfigureAwait(false);
            return frame;
        }

        private static async Task ReadExactAsync(Stream stream, byte[] buf, int offset, int count, CancellationToken ct)
        {
            int readTotal = 0;
            while (readTotal < count)
            {
                int n = await stream.ReadAsync(buf, offset + readTotal, count - readTotal, ct).ConfigureAwait(false);
                if (n <= 0) throw new EndOfStreamException("Connection closed by server.");
                readTotal += n;
            }
        }

        private uint NextClientTime() => _clientTimeCounter++;

        public void Disconnect()
        {
            try { _cts?.Cancel(); } catch { }
            try { _stream?.Dispose(); } catch { }
            try { _tcp?.Close(); } catch { }
            try { _udp?.Close(); } catch { }
            _cts = null;
            _stream = null;
            _tcp = null;
            _udp = null;
            UdpPresenceConfirmed = false;
        }

        public void Dispose()
        {
            Disconnect();
            _tcpWriteLock.Dispose();
        }
    }
}
