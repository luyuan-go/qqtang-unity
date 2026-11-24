using System;
using System.Collections.Generic;
using Qqtang.Core.Protocol;

namespace Qqtang.Core.State
{
    public enum ChatChannel
    {
        Lobby,
        Room,
        Kin,
        System
    }

    public sealed class ChatMessageEntry
    {
        public ChatChannel Channel;
        public string Sender = string.Empty;
        public string Content = string.Empty;
        public bool Highlight;
        public string Timestamp = string.Empty;
    }

    /// <summary>
    /// Centralized client state store fed by QqtangSessionClient responses and server notifications.
    /// UI panels subscribe to OnStateChanged or inspect StateVersion to re-render cleanly.
    /// </summary>
    public sealed class ClientStateStore
    {
        private const int MaxChatHistory = 120;

        public int StateVersion { get; private set; }
        public event Action OnStateChanged;
        public event Action<GameBeginData> OnGameBeginReceived;
        public event Action<GameOverData> OnGameOverReceived;
        public event Action<NotifyGameEventMessage> OnBattleGameEventReceived;
        public event Action<string> OnToastRequested;

        // Session / Account
        public bool IsLoggedIn { get; private set; }
        public uint Uin { get; private set; }
        public ushort PlayerId { get; private set; }
        public ushort SectionId { get; private set; } = 1;
        public string Nickname { get; private set; } = string.Empty;
        public byte Gender { get; private set; }
        public uint Identity { get; private set; }
        public uint Honor { get; private set; }
        public GameInfoData GameInfo { get; private set; } = new GameInfoData();
        public List<ItemInfoData> Inventory { get; } = new List<ItemInfoData>();
        public List<uint> AvailableBackgrounds { get; } = new List<uint>();

        // Lobby
        public List<RoomListEntryData> Rooms { get; } = new List<RoomListEntryData>();
        public List<PlayerListEntryData> OnlinePlayers { get; } = new List<PlayerListEntryData>();

        // Current Room
        public bool InRoom { get; private set; }
        public ushort RoomId { get; private set; }
        public string RoomName { get; set; } = string.Empty;
        public byte RoomFlag { get; private set; }
        public ushort MapId { get; private set; } = 201;
        public RoomGameType GameType { get; private set; }
        public ushort RoomOwnerId { get; private set; }
        public byte LocalSeatId { get; private set; }
        public byte LocalTeamId { get; private set; }
        public uint RoomBackgroundId { get; private set; }
        public EnterRoomSeatStatus[] SeatStatus { get; } = new EnterRoomSeatStatus[8];
        public List<RoomMemberData> RoomMembers { get; } = new List<RoomMemberData>();
        public bool IsRoomOwner => InRoom && RoomOwnerId == PlayerId;

        // Chats
        public List<ChatMessageEntry> LobbyChat { get; } = new List<ChatMessageEntry>();
        public List<ChatMessageEntry> RoomChat { get; } = new List<ChatMessageEntry>();
        public List<ChatMessageEntry> KinChat { get; } = new List<ChatMessageEntry>();

        // Pets
        public List<PetInfoData> Pets { get; } = new List<PetInfoData>();

        // Friends
        public List<uint> FriendUins { get; } = new List<uint>();
        public Dictionary<uint, FriendUpdateNotifyData> FriendDetails { get; } = new Dictionary<uint, FriendUpdateNotifyData>();
        public List<AddFriendRequestNotifyData> PendingFriendRequests { get; } = new List<AddFriendRequestNotifyData>();
        public FindFriendResponseData LastFindFriend { get; set; }

        // Kin
        public uint KinIndex { get; private set; }
        public KinBaseData KinBase { get; private set; }
        public List<KinMemberData> KinMembers { get; } = new List<KinMemberData>();
        public List<KinServerOperationData> PendingKinInvites { get; } = new List<KinServerOperationData>();
        public List<KinOrderInfoData> KinHonorTop { get; } = new List<KinOrderInfoData>();
        public List<KinOrderInfoData> KinActiveTop { get; } = new List<KinOrderInfoData>();

        // Marriage & Wedding
        public MarriageInfoResponseData Marriage { get; private set; }
        public List<SparkNotifyData> PendingSparks { get; } = new List<SparkNotifyData>();
        public List<WeddingConfirmNotifyData> PendingWeddings { get; } = new List<WeddingConfirmNotifyData>();
        public ushort RoomWeddingModeId { get; private set; }

        // Hidden Treasure Room
        public TreasureRoomResponseData ActiveTreasureRoom { get; set; }

        // Battle
        public bool InBattle { get; private set; }
        public GameBeginData CurrentGameBegin { get; private set; }
        public GameOverData LastGameOver { get; private set; }
        public event Action<GameBeginData> OnNextMapStageReceived;

        public void ResetAll()
        {
            IsLoggedIn = false;
            Uin = 0;
            PlayerId = 0;
            Nickname = string.Empty;
            GameInfo = new GameInfoData();
            Inventory.Clear();
            Rooms.Clear();
            OnlinePlayers.Clear();
            ClearRoomInternal();
            LobbyChat.Clear();
            RoomChat.Clear();
            KinChat.Clear();
            Pets.Clear();
            FriendUins.Clear();
            FriendDetails.Clear();
            PendingFriendRequests.Clear();
            LastFindFriend = null;
            KinIndex = 0;
            KinBase = null;
            KinMembers.Clear();
            PendingKinInvites.Clear();
            Marriage = null;
            PendingSparks.Clear();
            InBattle = false;
            CurrentGameBegin = null;
            LastGameOver = null;
            NotifyChanged();
        }

        public void ApplyLogin(LoginResponseData login, uint uin, string nickname, byte gender)
        {
            IsLoggedIn = true;
            Uin = uin;
            PlayerId = login.PlayerId;
            SectionId = login.SectionId;
            Nickname = nickname;
            Gender = gender;
            Identity = login.Identity;
            Honor = login.Honor;
            GameInfo = login.GameInfo ?? new GameInfoData();
            Inventory.Clear();
            Inventory.AddRange(login.Items);
            NotifyChanged();
        }

        public void ApplyRoomList(RoomListResponseData list)
        {
            if (list == null) return;
            Rooms.Clear();
            Rooms.AddRange(list.Rooms);
            NotifyChanged();
        }

        public void ApplyPlayerList(PlayerListResponseData list)
        {
            if (list == null) return;
            OnlinePlayers.Clear();
            OnlinePlayers.AddRange(list.Players);
            NotifyChanged();
        }

        public void ApplyEnterRoom(EnterRoomResponseData enter, string fallbackName = null)
        {
            if (enter == null || enter.ResultId != 0) return;
            InRoom = true;
            RoomId = enter.RoomId;
            if (!string.IsNullOrEmpty(fallbackName))
                RoomName = fallbackName;
            else
            {
                var found = Rooms.Find(r => r.RoomId == enter.RoomId);
                RoomName = found != null ? found.Name : $"房间 #{enter.RoomId}";
            }
            LocalSeatId = enter.LocalSeatId;
            LocalTeamId = enter.LocalTeamId;
            MapId = enter.MapId;
            RoomFlag = enter.RoomFlag;
            RoomOwnerId = enter.RoomOwnerId;
            GameType = enter.GameType;
            Array.Copy(enter.SeatStatus, SeatStatus, 8);
            RoomMembers.Clear();
            RoomMembers.AddRange(enter.Players);
            RoomChat.Clear();
            LastGameOver = null;
            NotifyChanged();
        }

        public void ApplyJoinRoom(JoinRoomResponseData join)
        {
            if (join == null || join.ResultId != 0) return;
            InRoom = true;
            RoomId = join.RoomId;
            RoomName = string.IsNullOrEmpty(join.RoomName) ? $"房间 #{join.RoomId}" : join.RoomName;
            LocalSeatId = join.LocalSeatId;
            LocalTeamId = join.LocalTeamId;
            MapId = join.MapId;
            RoomFlag = join.RoomFlag;
            RoomOwnerId = join.RoomOwnerId;
            GameType = join.GameType;
            RoomBackgroundId = join.BackgroundId;
            Array.Copy(join.SeatStatus, SeatStatus, 8);
            RoomMembers.Clear();
            RoomMembers.AddRange(join.Players);
            RoomChat.Clear();
            LastGameOver = null;
            NotifyChanged();
        }

        public void ClearRoom()
        {
            ClearRoomInternal();
            NotifyChanged();
        }

        public void ExitBattleToRoom()
        {
            InBattle = false;
            CurrentGameBegin = null;
            LastGameOver = null;
            foreach (var m in RoomMembers) m.Status = 0;
            NotifyChanged();
        }

        public void UpdateMoney(uint newMoney)
        {
            if (GameInfo != null) GameInfo.Money = newMoney;
            NotifyChanged();
        }

        public void ApplyPets(PlayerPetsResponseData resp)
        {
            if (resp == null) return;
            Pets.Clear();
            Pets.AddRange(resp.Pets);
            NotifyChanged();
        }

        public void ApplySinglePetUpdate(PetInfoData pet, PetEventId ev)
        {
            if (pet == null || pet.PetId == 0) return;
            int idx = Pets.FindIndex(p => p.PetId == pet.PetId);
            if (ev == PetEventId.Release)
            {
                if (idx >= 0) Pets.RemoveAt(idx);
            }
            else if (idx >= 0)
            {
                Pets[idx] = pet;
            }
            else
            {
                Pets.Add(pet);
            }
            if (ev == PetEventId.Activate)
            {
                foreach (var p in Pets) p.State = (ushort)(p.PetId == pet.PetId ? 1 : 2);
                if (GameInfo != null) GameInfo.PetId = pet.PetId;
            }
            else if (ev == PetEventId.Deactivate && GameInfo != null && GameInfo.PetId == pet.PetId)
            {
                GameInfo.PetId = 0;
            }
            NotifyChanged();
        }

        public void ApplyFriendList(FriendListResponseData resp)
        {
            if (resp == null || resp.ResultId != 0) return;
            FriendUins.Clear();
            FriendUins.AddRange(resp.FriendUins);
            NotifyChanged();
        }

        public void ApplyKinBase(KinBaseData kin)
        {
            if (kin == null) return;
            KinIndex = kin.Index;
            KinBase = kin;
            NotifyChanged();
        }

        public void ApplyKinMembers(FetchKinMembersResponseData resp)
        {
            if (resp == null) return;
            KinMembers.Clear();
            KinMembers.AddRange(resp.Members);
            NotifyChanged();
        }

        public void ApplyKinTop(FetchKinTopResponseData resp)
        {
            if (resp == null) return;
            KinHonorTop.Clear();
            KinHonorTop.AddRange(resp.HonorTop);
            KinActiveTop.Clear();
            KinActiveTop.AddRange(resp.ActiveTop);
            NotifyChanged();
        }

        public void ClearKin()
        {
            KinIndex = 0;
            KinBase = null;
            KinMembers.Clear();
            KinChat.Clear();
            NotifyChanged();
        }

        public void ApplyMarriageInfo(MarriageInfoResponseData info)
        {
            Marriage = info;
            NotifyChanged();
        }

        public void SetInventoryItemStatusLocal(ushort itemId, byte newStatus, byte newRoleId)
        {
            var item = Inventory.Find(i => i.ItemId == itemId);
            if (item != null)
            {
                item.Status = newStatus;
                if (newRoleId > 0) item.RoleId = newRoleId;
                NotifyChanged();
            }
        }

        public void UpsertInventoryItems(IReadOnlyList<ItemInfoData> items)
        {
            if (items == null) return;
            foreach (var it in items)
            {
                int idx = Inventory.FindIndex(x => x.ItemId == it.ItemId);
                if (it.Quantity == 0)
                {
                    if (idx >= 0) Inventory.RemoveAt(idx);
                }
                else if (idx >= 0)
                {
                    Inventory[idx] = it;
                }
                else
                {
                    Inventory.Add(it);
                }
            }
            NotifyChanged();
        }

        public bool HasActiveItem(ushort itemId)
        {
            var it = Inventory.Find(i => i.ItemId == itemId);
            return it != null && it.Quantity > 0 && it.Status == 1;
        }

        public bool OwnsItem(ushort itemId)
        {
            var it = Inventory.Find(i => i.ItemId == itemId);
            return it != null && it.Quantity > 0;
        }

        public void PushToast(string text)
        {
            if (!string.IsNullOrEmpty(text))
                OnToastRequested?.Invoke(text);
        }

        /// <summary>
        /// Routes any unsolicited TCP notification (or echoed command) into the store.
        /// </summary>
        public void ApplyServerPacket(DecodedLocalPacket pkt)
        {
            if (pkt == null) return;
            try
            {
                switch (pkt.Command)
                {
                    case GameCommands.RoomPush:
                        MergeRoomPush(RoomListResponseData.ParseRoomPush(pkt.Payload));
                        return;

                    case GameCommands.EnterRoomNotify:
                    {
                        var (member, roomId) = EnterRoomResponseData.ParseEnterRoomNotify(pkt.Payload);
                        if (InRoom && roomId == RoomId)
                        {
                            RoomMembers.RemoveAll(m => m.PlayerId == member.PlayerId);
                            RoomMembers.Add(member);
                            if (member.SeatId >= 1 && member.SeatId <= 8)
                                SeatStatus[member.SeatId - 1] = EnterRoomSeatStatus.Occupied;
                            NotifyChanged();
                        }
                        return;
                    }

                    case GameCommands.LeaveRoomNotify:
                    {
                        var left = LeaveRoomNotifyData.Parse(pkt.Payload);
                        var removed = RoomMembers.Find(m => m.PlayerId == left.PlayerId);
                        if (removed != null)
                        {
                            if (removed.SeatId >= 1 && removed.SeatId <= 8)
                                SeatStatus[removed.SeatId - 1] = EnterRoomSeatStatus.Open;
                            RoomMembers.Remove(removed);
                        }
                        if (left.NewRoomOwnerId != 0) RoomOwnerId = left.NewRoomOwnerId;
                        if (left.MapId != 0) MapId = left.MapId;
                        NotifyChanged();
                        return;
                    }

                    case GameCommands.ModifyRoomInfoNotify:
                    {
                        var r = new BigEndianReader(pkt.Payload);
                        MapId = r.ReadUInt16();
                        if (r.Remaining >= 1) GameType = (RoomGameType)r.ReadByte();
                        NotifyChanged();
                        return;
                    }

                    case GameCommands.ModifyRoomNotify:
                    {
                        var mod = ModifyRoomNotifyData.Parse(pkt.Payload);
                        if ((mod.Flags & ModifyRoomFlags.NameChanged) != 0 && !string.IsNullOrEmpty(mod.RoomName))
                            RoomName = mod.RoomName;
                        RoomFlag = (byte)mod.RoomFlag;
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.ChangeRoomTypeNotify:
                    {
                        var ch = ChangeRoomTypeNotifyData.Parse(pkt.Payload);
                        GameType = ch.GameType;
                        NotifyChanged();
                        return;
                    }

                    case GameCommands.ChangeTeamNotify:
                    {
                        var r = new BigEndianReader(pkt.Payload);
                        r.ReadUInt32(); // UIN
                        ushort pid = r.ReadUInt16();
                        byte team = r.ReadByte();
                        var m = RoomMembers.Find(x => x.PlayerId == pid);
                        if (m != null) m.TeamId = team;
                        if (pid == PlayerId) LocalTeamId = team;
                        NotifyChanged();
                        return;
                    }

                    case GameCommands.ChangeRoleNotify:
                    {
                        var r = new BigEndianReader(pkt.Payload);
                        r.ReadUInt32(); // UIN
                        ushort pid = r.ReadUInt16();
                        byte role = r.ReadByte();
                        var m = RoomMembers.Find(x => x.PlayerId == pid);
                        if (m != null && m.GameInfo != null) m.GameInfo.RoleId = role;
                        if (pid == PlayerId && GameInfo != null) GameInfo.RoleId = role;
                        NotifyChanged();
                        return;
                    }

                    case GameCommands.ReadyStateNotify:
                    {
                        var r = new BigEndianReader(pkt.Payload);
                        r.ReadUInt32(); // UIN
                        ushort pid = r.ReadUInt16();
                        byte st = r.ReadByte();
                        var m = RoomMembers.Find(x => x.PlayerId == pid);
                        if (m != null) m.Status = st;
                        NotifyChanged();
                        return;
                    }

                    case GameCommands.SetSeatStatusNotify:
                    {
                        var seat = SetSeatStatusNotifyData.Parse(pkt.Payload);
                        if (seat.SeatId >= 1 && seat.SeatId <= 8)
                            SeatStatus[seat.SeatId - 1] = seat.IsLocked ? EnterRoomSeatStatus.Locked : EnterRoomSeatStatus.Open;
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.KickOffRoomNotify:
                    {
                        var kick = KickOffRoomNotifyData.Parse(pkt.Payload);
                        if (kick.PlayerUin == Uin)
                        {
                            InBattle = false;
                            ClearRoomInternal();
                            PushToast("已被房主移出房间");
                            NotifyChanged();
                        }
                        return;
                    }

                    case ExtendedGameCommands.UseItemInRoomNotify:
                    {
                        var use = UseItemInRoomNotifyData.Parse(pkt.Payload);
                        AppendChat(RoomChat, ChatChannel.System, "系统", $"玩家 {use.Uin} 使用了房间道具 #{use.ItemId}");
                        return;
                    }

                    case ExtendedGameCommands.UseBackgroundNotify:
                    {
                        var bg = UseBackgroundNotifyData.Parse(pkt.Payload);
                        RoomBackgroundId = bg.ItemId;
                        NotifyChanged();
                        return;
                    }

                    case GameCommands.RoomChatNotify:
                    {
                        var chat = RoomChatNotifyData.Parse(pkt.Payload);
                        AppendChat(RoomChat, chat.IsSystem ? ChatChannel.System : ChatChannel.Room, chat.Nickname, chat.Content, chat.IsSystem);
                        return;
                    }

                    case ExtendedGameCommands.SectionChatNotify:
                    {
                        var chat = SectionChatNotifyData.Parse(pkt.Payload);
                        AppendChat(LobbyChat, ChatChannel.Lobby, chat.Nickname, chat.Content, chat.IsSmallBugle);
                        return;
                    }

                    case ExtendedGameCommands.AcrossSectionChatNotify:
                    {
                        var chat = AcrossSectionChatNotifyData.Parse(pkt.Payload);
                        AppendChat(LobbyChat, ChatChannel.Lobby, $"[跨区]{chat.Nickname}", chat.Content, true);
                        return;
                    }

                    case ExtendedGameCommands.GameMoneyNotify:
                    {
                        var m = GameMoneyNotifyData.Parse(pkt.Payload);
                        if (m.Uin == Uin && GameInfo != null)
                        {
                            GameInfo.Money = m.Money;
                            NotifyChanged();
                        }
                        return;
                    }

                    case ExtendedGameCommands.PlayerItemAddNotify:
                    {
                        var upd = PlayerItemAddNotifyData.Parse(pkt.Payload);
                        if (upd.Uin == Uin)
                            UpsertInventoryItems(upd.Items);
                        return;
                    }

                    case ExtendedGameCommands.PlayerPets:
                        ApplyPets(PlayerPetsResponseData.Parse(pkt.Payload));
                        return;

                    case ExtendedGameCommands.FriendList:
                        ApplyFriendList(FriendListResponseData.Parse(pkt.Payload));
                        return;

                    case ExtendedGameCommands.AddFriend:
                    {
                        var req = AddFriendRequestNotifyData.Parse(pkt.Payload);
                        PendingFriendRequests.RemoveAll(x => x.RequesterUin == req.RequesterUin);
                        PendingFriendRequests.Add(req);
                        PushToast($"收到来自 {req.RequesterUin} 的好友申请");
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.FriendUpdateNotify:
                    {
                        var f = FriendUpdateNotifyData.Parse(pkt.Payload);
                        if (!FriendUins.Contains(f.FriendUin)) FriendUins.Add(f.FriendUin);
                        FriendDetails[f.FriendUin] = f;
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.AnswerFriend:
                    {
                        var ans = AnswerFriendResultData.Parse(pkt.Payload);
                        PushToast(ans.ResultId == 0
                            ? $"好友请求已通过 ({ans.RequesterUin} <-> {ans.ResponderUin})"
                            : $"好友请求被拒绝或失败");
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.CreateKin:
                    {
                        // Login kin session restore pushes CreateKin response with KinIndex
                        var ck = CreateKinResponseData.Parse(pkt.Payload);
                        if (ck.ErrorNo == 0 && ck.KinIndex > 0)
                        {
                            KinIndex = ck.KinIndex;
                            NotifyChanged();
                        }
                        return;
                    }

                    case ExtendedGameCommands.ServerOperateKin:
                    {
                        var inv = KinServerOperationData.Parse(pkt.Payload);
                        PendingKinInvites.RemoveAll(x => x.KinIndex == inv.KinIndex && x.InviterUin == inv.InviterUin);
                        PendingKinInvites.Add(inv);
                        PushToast($"收到家族 [{inv.Kin?.Name ?? inv.KinIndex.ToString()}] 邀请");
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.ServerOperateKinNotify:
                    {
                        var res = KinServerOperationData.Parse(pkt.Payload);
                        if (res.Para == KinConstants.OperationAccepted && res.Kin != null)
                        {
                            KinIndex = res.Kin.Index;
                            KinBase = res.Kin;
                            PushToast($"已加入家族 [{res.Kin.Name}]");
                            NotifyChanged();
                        }
                        return;
                    }

                    case ExtendedGameCommands.KinChatNotify:
                    {
                        var kc = KinChatNotifyData.Parse(pkt.Payload);
                        var member = KinMembers.Find(x => x.Uin == kc.Uin);
                        string sender = member != null && !string.IsNullOrEmpty(member.Nickname) ? member.Nickname : kc.Uin.ToString();
                        AppendChat(KinChat, ChatChannel.Kin, sender, kc.Message);
                        return;
                    }

                    case ExtendedGameCommands.KinEventNotify:
                    {
                        var ev = KinEventNotifyData.Parse(pkt.Payload);
                        if (ev.EventType == KinConstants.EventFamilyDismissed ||
                            (ev.EventType == KinConstants.EventMemberRemoved && ev.AttachUin == Uin))
                        {
                            ClearKin();
                        }
                        if (!string.IsNullOrEmpty(ev.Description))
                        {
                            AppendChat(KinChat, ChatChannel.System, "家族", ev.Description, true);
                            PushToast(ev.Description);
                        }
                        return;
                    }

                    case ExtendedGameCommands.NotifySpark:
                    {
                        var sp = SparkNotifyData.Parse(pkt.Payload);
                        PendingSparks.RemoveAll(x => x.ProposerUin == sp.ProposerUin);
                        PendingSparks.Add(sp);
                        PushToast($"收到来自 {sp.Nickname}({sp.ProposerUin}) 的求婚！");
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.AnswerSpark:
                    {
                        var ans = AnswerSparkResultData.Parse(pkt.Payload);
                        PushToast(ans.Result == 0 ? "求婚成功，喜结良缘！" : "对方婉拒了求婚。");
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.Divorce:
                    {
                        var div = MarriageSimpleResultData.Parse(pkt.Payload);
                        if (div.Result == 0)
                        {
                            Marriage = null;
                            PushToast("已解除伴侣关系");
                            NotifyChanged();
                        }
                        return;
                    }

                    case ExtendedGameCommands.WeddingConfirmNotify:
                    {
                        var wc = WeddingConfirmNotifyData.Parse(pkt.Payload);
                        PendingWeddings.RemoveAll(w => w.ProposerUin == wc.ProposerUin);
                        PendingWeddings.Add(wc);
                        PushToast($"收到来自 {wc.Nickname} 的婚礼仪式邀请！");
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.AnswerWedding:
                    {
                        var aw = AnswerWeddingResultData.Parse(pkt.Payload);
                        PushToast(aw.Result == 0 ? $"婚礼仪式正式开启！{aw.Word}" : $"婚礼仪式已取消：{aw.Word}");
                        NotifyChanged();
                        return;
                    }

                    case ExtendedGameCommands.ChangeWeddingModeNotify:
                    {
                        var wm = ChangeWeddingModeNotifyData.Parse(pkt.Payload);
                        RoomWeddingModeId = wm.WeddingModeId;
                        PushToast(wm.WeddingModeId == 1 ? "婚礼场景已切换：中式礼堂" : "婚礼场景已切换：西式教堂");
                        NotifyChanged();
                        return;
                    }

                    case TreasureCommands.TreasureTimeout:
                    {
                        ActiveTreasureRoom = null;
                        PushToast("藏宝室限时已到，自动返回房间");
                        NotifyChanged();
                        return;
                    }

                    case GameCommands.GameBeginNotify:
                    {
                        var begin = GameBeginData.ParseLengthPrefixed(pkt.Payload);
                        CurrentGameBegin = begin;
                        LastGameOver = null;
                        InBattle = true;
                        NotifyChanged();
                        OnGameBeginReceived?.Invoke(begin);
                        return;
                    }

                    case GameCommands.GameEventNotify:
                    {
                        var ev = NotifyGameEventMessage.Parse(pkt.Payload);
                        if (ev.Schema == GameEventSchemas.NotifyGameOver)
                        {
                            LastGameOver = GameOverData.Parse(ev.Body);
                            NotifyChanged();
                            OnGameOverReceived?.Invoke(LastGameOver);
                        }
                        else if (ev.Schema == ModeEventSchemas.NotifyGameNextMap)
                        {
                            var nextStage = GameBeginData.Parse(ev.Body);
                            CurrentGameBegin = nextStage;
                            NotifyChanged();
                            OnNextMapStageReceived?.Invoke(nextStage);
                        }
                        else
                        {
                            OnBattleGameEventReceived?.Invoke(ev);
                        }
                        return;
                    }
                }
            }
            catch
            {
                // Ignore malformed/unknown optional server pushes rather than breaking main loop.
            }
        }

        private void MergeRoomPush(List<RoomListEntryData> pushed)
        {
            foreach (var entry in pushed)
            {
                int idx = Rooms.FindIndex(r => r.RoomId == entry.RoomId);
                if (entry.CurrentPlayers == 0 && entry.Capacity == 0)
                {
                    if (idx >= 0) Rooms.RemoveAt(idx);
                }
                else if (idx >= 0)
                {
                    Rooms[idx] = entry;
                }
                else
                {
                    Rooms.Add(entry);
                }
            }
            Rooms.Sort((a, b) => a.RoomId.CompareTo(b.RoomId));
            NotifyChanged();
        }

        private void ClearRoomInternal()
        {
            InRoom = false;
            RoomId = 0;
            RoomName = string.Empty;
            RoomOwnerId = 0;
            RoomMembers.Clear();
            Array.Clear(SeatStatus, 0, SeatStatus.Length);
        }

        private void AppendChat(List<ChatMessageEntry> list, ChatChannel channel, string sender, string content, bool highlight = false)
        {
            list.Add(new ChatMessageEntry
            {
                Channel = channel,
                Sender = string.IsNullOrEmpty(sender) ? "玩家" : sender,
                Content = content ?? string.Empty,
                Highlight = highlight,
                Timestamp = DateTime.Now.ToString("HH:mm:ss")
            });
            if (list.Count > MaxChatHistory)
                list.RemoveAt(0);
            NotifyChanged();
        }

        private void NotifyChanged()
        {
            StateVersion++;
            OnStateChanged?.Invoke();
        }
    }
}
