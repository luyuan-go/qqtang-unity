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
        public byte RoleId { get; private set; } = 1;
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
