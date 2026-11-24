using System;
using System.Collections.Generic;
using System.Text;

namespace Qqtang.Core.Protocol
{
    public static class ExtendedGameCommands
    {
        public const ushort FindFriend = 0x0067;
        public const ushort ChatAcrossSection = 0x006C;
        public const ushort SectionChat = 0x006E;
        public const ushort KickOffPlayer = 0x006F;
        public const ushort KickOffRoomNotify = 0x0074;
        public const ushort UseItemInRoom = 0x007D;
        public const ushort UseItemInRoomNotify = 0x007E;
        public const ushort GetGameMoney = 0x008B;
        public const ushort SectionChatNotify = 0x0092;
        public const ushort GameMoneyNotify = 0x0094;
        public const ushort AcrossSectionChatNotify = 0x0095;
        public const ushort PointsRefreshNotify = 0x009A;
        public const ushort PlayerStatusNotify = 0x00A0;
        public const ushort FriendList = 0x00A2;
        public const ushort AddFriend = 0x00A3;
        public const ushort RemoveFriend = 0x00A4;
        public const ushort FriendUpdateNotify = 0x00A5;
        public const ushort AnswerFriend = 0x00A6;
        public const ushort CreateKin = 0x00B5;
        public const ushort FetchKinBase = 0x00B6;
        public const ushort FetchKinMemberList = 0x00B7;
        public const ushort UpdateKinTitle = 0x00B8;
        public const ushort OperateKin = 0x00BA;
        public const ushort ServerOperateKin = 0x00BC;
        public const ushort ServerOperateKinNotify = 0x00BD;
        public const ushort KinChat = 0x00BE;
        public const ushort KinChatNotify = 0x00BF;
        public const ushort KickKinMember = 0x00C0;
        public const ushort ExitKin = 0x00C1;
        public const ushort DismissKin = 0x00C2;
        public const ushort KinEventNotify = 0x00C4;
        public const ushort AssignKinAuthority = 0x00C5;
        public const ushort SetKinBadge = 0x00C7;
        public const ushort FetchKinTop = 0x00CB;
        public const ushort CombineForge = 0x00CF;
        public const ushort ChangeRoomType = 0x00D0;
        public const ushort ChangeRoomTypeNotify = 0x00D1;
        public const ushort PlayerPets = 0x00D4;
        public const ushort HandlePet = 0x00D5;
        public const ushort PlayerItemAddNotify = 0x00D9;
        public const ushort BreakEgg = 0x00FE;
        public const ushort BackgroundList = 0x00FF;
        public const ushort UseBackground = 0x0100;
        public const ushort UseBackgroundNotify = 0x0101;
        public const ushort RequestSpark = 0x0104;
        public const ushort NotifySpark = 0x0105;
        public const ushort AnswerSpark = 0x0106;
        public const ushort MarriageInfo = 0x0107;
        public const ushort ModifyLoveWord = 0x0108;
        public const ushort Divorce = 0x0109;
        public const ushort StartWedding = 0x010A;
        public const ushort WeddingConfirmNotify = 0x010B;
        public const ushort AnswerWedding = 0x010C;
        public const ushort ChangeWeddingMode = 0x010D;
        public const ushort ChangeWeddingModeNotify = 0x010E;
        public const ushort SetKinDeclaration = 0x0140;
        public const ushort SetKinNotification = 0x0141;
        public const ushort ShopBuy = 0x0259;
        public const ushort GetConfigFile = 0x0266;
        public const ushort Forge = 0x0353;
        public const ushort ShopList = 0x035E;
    }

    public static class ExtendedGameEventSchemas
    {
        public const ushort RequestUseItem = 0x0FAF;
        public const ushort NotifyPlayerUseItem = 0x0FB0;
        public const ushort RequestMoveMapElement = 0x0FB2;
        public const ushort NotifyMapElementMoved = 0x0FB3;
        public const ushort RequestMoveBomb = 0x0FB4;
        public const ushort NotifyPlayerRelive = 0x0FBA;
        public const ushort RequestPreparedUseProp = 0x1174;
        public const ushort NotifyPreparedUseProp = 0x1175;
        public const ushort NotifyUseEmotion = 0x1194;
        public const ushort NotifyPlayerMoveBomb = 0x139C;
    }

    [Flags]
    public enum ModifyRoomFlags : uint
    {
        None = 0,
        NameChanged = 1u << 0,
        PasswordPresent = 1u << 1,
        PasswordChanged = 1u << 2,
        PasswordEnabled = 1u << 3
    }

    public enum PetEventId : uint
    {
        Feed = 1,
        Activate = 2,
        Deactivate = 3,
        Release = 4,
        Rename = 5,
        LearnSkill = 6,
        Adopt = 7
    }

    public enum ShopPaymentMethod : ushort
    {
        QCoin = 1,
        GameMoney = 3,
        VNet = 4,
        KubiGem = 6,
        QPoint = 7
    }

    public static class KinConstants
    {
        public const uint OperationAccepted = 0;
        public const uint OperationRejected = 0xFFFFFFFFu;
        public const uint EventMemberJoined = 1;
        public const uint EventFamilyDismissed = 5;
        public const uint EventMemberRemoved = 8;
        public const uint EventUpdateAnnouncement = 9;
    }

    public static class LegacyChatTextHelper
    {
        // GBK bytes for "说:" -> 0xCB 0xB5 0x3A
        private static readonly byte[] GbkSayColon = { 0xCB, 0xB5, 0x3A };
        private static readonly byte[] Utf8SayColon = Encoding.UTF8.GetBytes("说:");

        public static (string nickname, string content) SplitNamedChat(byte[] raw)
        {
            if (raw == null || raw.Length == 0)
                return (string.Empty, string.Empty);

            int idx = FindSequence(raw, GbkSayColon);
            int sepLen = GbkSayColon.Length;
            if (idx < 0)
            {
                idx = FindSequence(raw, Utf8SayColon);
                sepLen = Utf8SayColon.Length;
            }
            if (idx < 0)
            {
                return (string.Empty, LegacyTextEncoding.DecodeNullTerminated(raw));
            }
            string nick = LegacyTextEncoding.DecodeBytes(raw, 0, idx);
            string body = LegacyTextEncoding.DecodeBytes(raw, idx + sepLen, raw.Length - idx - sepLen);
            return (nick, body);
        }

        private static int FindSequence(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i <= haystack.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j]) { match = false; break; }
                }
                if (match) return i;
            }
            return -1;
        }
    }

    // --- Lobby / Profile / Economy ---

    public sealed class PlayerListEntryData
    {
        public uint Uin;
        public string Nickname = string.Empty;
        public ushort PlayerId;
        public byte Gender;
        public byte IconId;
        public uint Identity;
        public GameInfoData GameInfo;
        public uint KinIndex;
        public string KinName = string.Empty;
        public uint Honor;

        public static PlayerListEntryData ReadPlayerInfo(ref BigEndianReader r)
        {
            var e = new PlayerListEntryData
            {
                Uin = r.ReadUInt32(),
                Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20)),
                PlayerId = r.ReadUInt16(),
                Gender = r.ReadByte(),
                IconId = r.ReadByte(),
                Identity = r.ReadUInt32(),
                GameInfo = GameInfoData.ReadFrom(ref r)
            };
            byte extCount = r.ReadByte();
            if (extCount > 0) r.Skip(extCount * 4);
            e.KinIndex = r.ReadUInt32();
            ushort kinNameLen = r.ReadUInt16();
            if (kinNameLen > 0) e.KinName = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(kinNameLen));
            r.Skip(8); // KinFlagID
            return e;
        }
    }

    public sealed class PlayerListResponseData
    {
        public ushort ResultId;
        public List<PlayerListEntryData> Players = new List<PlayerListEntryData>();

        public static PlayerListResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var resp = new PlayerListResponseData { ResultId = r.ReadUInt16() };
            byte count = r.ReadByte();
            for (int i = 0; i < count; i++)
                resp.Players.Add(PlayerListEntryData.ReadPlayerInfo(ref r));
            for (int i = 0; i < count && r.Remaining >= 8; i++)
            {
                resp.Players[i].Honor = r.ReadUInt32();
                r.ReadUInt32(); // Attach1
            }
            for (int i = 0; i < count && r.Remaining >= 1; i++)
            {
                byte pCount = r.ReadByte();
                if (pCount > 0 && r.Remaining >= pCount * 11) r.Skip(pCount * 11);
            }
            return resp;
        }
    }

    public sealed class FindFriendResponseData
    {
        public ushort ResultId;
        public uint FriendUin;
        public string PlayerName = string.Empty;
        public ushort PlayerId;
        public byte Gender;
        public byte IconId;
        public uint Identity;
        public uint ServerId;
        public ushort DialogId;
        public ushort SectionId;
        public ushort RoomId;
        public string RoomName = string.Empty;
        public bool Online;
        public GameInfoData GameInfo;
        public List<ItemInfoData> Items = new List<ItemInfoData>();
        public uint KinIndex;
        public string KinName = string.Empty;
        public uint Honor;

        public static FindFriendResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var resp = new FindFriendResponseData { ResultId = r.ReadUInt16() };
            if (resp.ResultId != 0 || r.Remaining < 20) return resp;
            resp.FriendUin = r.ReadUInt32();
            resp.PlayerName = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20));
            resp.PlayerId = r.ReadUInt16();
            resp.Gender = r.ReadByte();
            resp.IconId = r.ReadByte();
            resp.Identity = r.ReadUInt32();
            resp.ServerId = r.ReadUInt32();
            resp.DialogId = r.ReadUInt16();
            resp.SectionId = r.ReadUInt16();
            resp.RoomId = r.ReadUInt16();
            resp.RoomName = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20));
            resp.Online = r.ReadByte() != 0;
            resp.GameInfo = GameInfoData.ReadFrom(ref r);
            ushort itemCount = r.ReadUInt16();
            for (int i = 0; i < itemCount; i++)
                resp.Items.Add(ItemInfoData.ReadFrom(ref r));
            resp.KinIndex = r.ReadUInt32();
            ushort kinNameLen = r.ReadUInt16();
            if (kinNameLen > 0) resp.KinName = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(kinNameLen));
            r.Skip(8); // KinFlagID
            resp.Honor = r.ReadUInt32();
            return resp;
        }
    }

    public sealed class GameMoneyResponseData
    {
        public ushort ResultId;
        public uint Money;
        public uint Uin;

        public static GameMoneyResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new GameMoneyResponseData
            {
                ResultId = r.ReadUInt16(),
                Money = r.ReadUInt32(),
                Uin = r.ReadUInt32()
            };
        }
    }

    public sealed class GameMoneyNotifyData
    {
        public ushort ResultId;
        public byte MoneyType;
        public uint Money;
        public uint Uin;

        public static GameMoneyNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new GameMoneyNotifyData
            {
                ResultId = r.ReadUInt16(),
                MoneyType = r.ReadByte(),
                Money = r.ReadUInt32(),
                Uin = r.ReadUInt32()
            };
        }
    }

    public sealed class PlayerItemAddNotifyData
    {
        public uint Uin;
        public uint Time;
        public uint SourceUin;
        public uint CommodityId;
        public List<ItemInfoData> Items = new List<ItemInfoData>();

        public static PlayerItemAddNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new PlayerItemAddNotifyData
            {
                Uin = r.ReadUInt32(),
                Time = r.ReadUInt32(),
                SourceUin = r.ReadUInt32(),
                CommodityId = r.ReadUInt32()
            };
            ushort count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
                d.Items.Add(ItemInfoData.ReadFrom(ref r));
            return d;
        }
    }

    // --- Chat ---

    public sealed class RoomChatNotifyData
    {
        public ushort SourcePlayerId;
        public ushort DestinationPlayerId;
        public bool IsSystem;
        public string Nickname = string.Empty;
        public string Content = string.Empty;

        public static RoomChatNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new RoomChatNotifyData
            {
                SourcePlayerId = r.ReadUInt16(),
                DestinationPlayerId = r.ReadUInt16()
            };
            ushort len = r.ReadUInt16();
            byte[] msg = r.ReadBytes(len);
            if (d.SourcePlayerId == 0xFFFF)
            {
                d.IsSystem = true;
                d.Nickname = "系统";
                d.Content = LegacyTextEncoding.DecodeNullTerminated(msg);
            }
            else
            {
                var (nick, body) = LegacyChatTextHelper.SplitNamedChat(msg);
                d.Nickname = nick;
                d.Content = body;
            }
            return d;
        }
    }

    public sealed class SectionChatNotifyData
    {
        public ushort SourcePlayerId;
        public ushort DestinationPlayerId;
        public string Nickname = string.Empty;
        public string Content = string.Empty;
        public uint Uin;
        public uint Identity;
        public bool IsSmallBugle => (Identity & 0x4000u) != 0;
        public uint XEffectId;
        public uint Point;

        public static SectionChatNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new SectionChatNotifyData
            {
                SourcePlayerId = r.ReadUInt16(),
                DestinationPlayerId = r.ReadUInt16()
            };
            ushort len = r.ReadUInt16();
            byte[] msg = r.ReadBytes(len);
            var (nick, body) = LegacyChatTextHelper.SplitNamedChat(msg);
            d.Nickname = nick;
            d.Content = body;
            if (r.Remaining >= 16)
            {
                d.Uin = r.ReadUInt32();
                d.Identity = r.ReadUInt32();
                d.XEffectId = r.ReadUInt32();
                d.Point = r.ReadUInt32();
            }
            return d;
        }
    }

    public sealed class AcrossSectionChatNotifyData
    {
        public uint SourceUin;
        public string Content = string.Empty;
        public string Nickname = string.Empty;

        public static AcrossSectionChatNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            uint srcUin = r.ReadUInt32();
            ushort len = r.ReadUInt16();
            string content = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            string nick = r.Remaining >= 32
                ? LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(32))
                : $"UIN:{srcUin}";
            return new AcrossSectionChatNotifyData { SourceUin = srcUin, Content = content, Nickname = nick };
        }
    }

    // --- Room Management ---

    public sealed class JoinRoomResponseData
    {
        public ushort ResultId;
        public ushort RoomId;
        public string RoomName = string.Empty;
        public byte LocalTeamId;
        public byte LocalSeatId;
        public ushort MapId;
        public byte RoomFlag;
        public ushort RoomOwnerId;
        public EnterRoomSeatStatus[] SeatStatus = new EnterRoomSeatStatus[8];
        public List<RoomMemberData> Players = new List<RoomMemberData>();
        public RoomGameType GameType;
        public uint BackgroundId;

        public static JoinRoomResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var resp = new JoinRoomResponseData
            {
                ResultId = r.ReadUInt16(),
                RoomId = r.ReadUInt16()
            };
            if (resp.ResultId != 0) return resp;

            resp.RoomName = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20));
            byte localTeamSeat = r.ReadByte();
            resp.LocalTeamId = (byte)(localTeamSeat >> 4);
            resp.LocalSeatId = (byte)(localTeamSeat & 0x0F);
            resp.MapId = r.ReadUInt16();
            resp.RoomFlag = r.ReadByte();
            resp.RoomOwnerId = r.ReadUInt16();
            byte playerCount = r.ReadByte();
            for (int i = 0; i < 8; i++)
                resp.SeatStatus[i] = (EnterRoomSeatStatus)r.ReadByte();
            for (int i = 0; i < playerCount; i++)
                resp.Players.Add(RoomMemberData.ReadFrom(ref r));
            r.Skip(playerCount * 12); // Attach
            resp.GameType = (RoomGameType)r.ReadByte();
            resp.BackgroundId = r.ReadUInt32();
            // SpouseUINs (4*playerCount) and PatternPoints follow; WeddingModeID is omitted in JOIN_ROOM_OLD.
            return resp;
        }
    }

    public sealed class ModifyRoomNotifyData
    {
        public ModifyRoomFlags Flags;
        public string RoomName = string.Empty;
        public RoomPropertyFlag RoomFlag;
        public string Password = string.Empty;

        public static ModifyRoomNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new ModifyRoomNotifyData
            {
                Flags = (ModifyRoomFlags)r.ReadUInt32(),
                RoomName = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20)),
                RoomFlag = (RoomPropertyFlag)r.ReadByte(),
                Password = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(16))
            };
        }
    }

    public sealed class ChangeRoomTypeNotifyData
    {
        public RoomGameType GameType;
        public uint ContinueId;

        public static ChangeRoomTypeNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new ChangeRoomTypeNotifyData
            {
                GameType = (RoomGameType)r.ReadByte(),
                ContinueId = r.ReadUInt32()
            };
        }
    }

    public sealed class SetSeatStatusNotifyData
    {
        public byte SeatId;
        public byte Status; // 1 = open, 2 = locked
        public bool IsLocked => Status == 2;

        public static SetSeatStatusNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new SetSeatStatusNotifyData { SeatId = r.ReadByte(), Status = r.ReadByte() };
        }
    }

    public sealed class KickOffRoomNotifyData
    {
        public ushort ReasonId;
        public ushort RoomId;
        public uint PlayerUin;
        public string Attach = string.Empty;

        public static KickOffRoomNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new KickOffRoomNotifyData
            {
                ReasonId = r.ReadUInt16(),
                RoomId = r.ReadUInt16(),
                PlayerUin = r.ReadUInt32()
            };
            byte len = r.ReadByte();
            if (len > 0 && r.Remaining >= len)
                d.Attach = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class UseItemInRoomNotifyData
    {
        public uint Uin;
        public ushort DialogCode;
        public uint ItemId;

        public static UseItemInRoomNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new UseItemInRoomNotifyData
            {
                Uin = r.ReadUInt32(),
                DialogCode = r.ReadUInt16(),
                ItemId = r.ReadUInt32()
            };
        }
    }

    public sealed class BackgroundListResponseData
    {
        public uint Uin;
        public uint Time;
        public List<uint> BackgroundIds = new List<uint>();

        public static BackgroundListResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new BackgroundListResponseData
            {
                Uin = r.ReadUInt32(),
                Time = r.ReadUInt32()
            };
            uint count = r.ReadUInt32();
            for (uint i = 0; i < count && r.Remaining >= 4; i++)
                d.BackgroundIds.Add(r.ReadUInt32());
            return d;
        }
    }

    public sealed class UseBackgroundNotifyData
    {
        public uint Uin;
        public uint Time;
        public uint ItemId;

        public static UseBackgroundNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new UseBackgroundNotifyData
            {
                Uin = r.ReadUInt32(),
                Time = r.ReadUInt32(),
                ItemId = r.ReadUInt32()
            };
        }
    }

    // --- Shop, Craft, Forge, BreakEgg ---

    public sealed class ShopListResponseData
    {
        public ushort ResultId;
        public ushort DownloadResultId;
        public uint LatestShopListVersion;
        public uint RealFileLength;
        public uint RealZipFileLength;
        public byte BufferSequence;
        public byte[] FileData = Array.Empty<byte>();

        public static ShopListResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new ShopListResponseData
            {
                ResultId = r.ReadUInt16(),
                DownloadResultId = r.ReadUInt16(),
                LatestShopListVersion = r.ReadUInt32(),
                RealFileLength = r.ReadUInt32(),
                RealZipFileLength = r.ReadUInt32(),
                BufferSequence = r.ReadByte()
            };
            ushort len = r.ReadUInt16();
            if (len > 0 && r.Remaining >= len) d.FileData = r.ReadBytes(len);
            return d;
        }
    }

    public sealed class ShopBuyResponseData
    {
        public ushort ResultId;
        public string ResultString = string.Empty;
        public ushort DealType;
        public ShopPaymentMethod PayType;
        public uint PayMoney;
        public uint CommodityId;
        public List<uint> ItemIds = new List<uint>();
        public uint TicketLeft;

        public static ShopBuyResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new ShopBuyResponseData
            {
                ResultId = r.ReadUInt16(),
                ResultString = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(128)),
                DealType = r.ReadUInt16(),
                PayType = (ShopPaymentMethod)r.ReadUInt16(),
                PayMoney = r.ReadUInt32(),
                CommodityId = r.ReadUInt32()
            };
            ushort count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
                d.ItemIds.Add(r.ReadUInt32());
            if (r.Remaining >= 4) d.TicketLeft = r.ReadUInt32();
            return d;
        }
    }

    public sealed class CombineForgeResponseData
    {
        public ushort ResultId;
        public byte Type;
        public uint ItemId;
        public uint FromItemId;
        public List<uint> MaterialIds = new List<uint>();
        public string Reason = string.Empty;

        public static CombineForgeResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new CombineForgeResponseData
            {
                ResultId = r.ReadUInt16(),
                Type = r.ReadByte(),
                ItemId = r.ReadUInt32(),
                FromItemId = r.ReadUInt32()
            };
            ushort matCount = r.ReadUInt16();
            for (int i = 0; i < matCount; i++) d.MaterialIds.Add(r.ReadUInt32());
            if (r.Remaining >= 2)
            {
                ushort reasonLen = r.ReadUInt16();
                if (reasonLen > 0 && r.Remaining >= reasonLen)
                    d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(reasonLen));
            }
            return d;
        }
    }

    public sealed class ForgeResponseData
    {
        public ushort ResultId;
        public byte Type;
        public uint ItemId;
        public uint MaterialId;
        public byte Effect;
        public byte Color;
        public string Reason = string.Empty;

        public static ForgeResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new ForgeResponseData
            {
                ResultId = r.ReadUInt16(),
                Type = r.ReadByte(),
                ItemId = r.ReadUInt32(),
                MaterialId = r.ReadUInt32(),
                Effect = r.ReadByte(),
                Color = r.ReadByte()
            };
            if (r.Remaining >= 2)
            {
                ushort reasonLen = r.ReadUInt16();
                if (reasonLen > 0 && r.Remaining >= reasonLen)
                    d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(reasonLen));
            }
            return d;
        }
    }

    public sealed class BreakEggResponseData
    {
        public ushort ResultId;
        public uint ShowItemId;
        public string AttachInfo = string.Empty;

        public static BreakEggResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new BreakEggResponseData
            {
                ResultId = r.ReadUInt16(),
                ShowItemId = r.ReadUInt32()
            };
            if (r.Remaining >= 1)
            {
                byte len = r.ReadByte();
                if (len > 0 && r.Remaining >= len)
                    d.AttachInfo = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            }
            return d;
        }
    }

    // --- Pets ---

    public sealed class PetInfoData
    {
        public uint PetId;
        public uint PetTypeId;
        public uint Experience;
        public uint Loyalty;
        public ushort Level;
        public ushort Mood;
        public ushort State; // 1 = active, 2 = inactive
        public string Name = string.Empty;
        public byte[] Skills = Array.Empty<byte>();
        public bool IsActive => State == 1;

        public static PetInfoData ReadCompact(ref BigEndianReader r)
        {
            var p = new PetInfoData
            {
                PetId = r.ReadUInt32(),
                PetTypeId = r.ReadUInt32(),
                Experience = r.ReadUInt32(),
                Loyalty = r.ReadUInt32(),
                Level = r.ReadUInt16(),
                Mood = r.ReadUInt16(),
                State = r.ReadUInt16(),
                Name = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(12))
            };
            uint skillCount = r.ReadUInt32();
            if (skillCount > 0 && r.Remaining >= (int)skillCount)
                p.Skills = r.ReadBytes((int)skillCount);
            return p;
        }
    }

    public sealed class PlayerPetsResponseData
    {
        public List<PetInfoData> Pets = new List<PetInfoData>();

        public static PlayerPetsResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new PlayerPetsResponseData();
            if (r.Remaining < 1) return d;
            byte baseCount = r.ReadByte();
            for (int i = 0; i < baseCount; i++)
                d.Pets.Add(PetInfoData.ReadCompact(ref r));
            if (r.Remaining >= 1)
            {
                byte extraCount = r.ReadByte();
                for (int i = 0; i < extraCount; i++)
                    d.Pets.Add(PetInfoData.ReadCompact(ref r));
            }
            return d;
        }
    }

    public sealed class HandlePetResponseData
    {
        public ushort ResultId;
        public PetEventId EventId;
        public string Reason = string.Empty;
        public PetInfoData Pet;

        public static HandlePetResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new HandlePetResponseData
            {
                ResultId = r.ReadUInt16(),
                EventId = (PetEventId)r.ReadUInt32()
            };
            ushort reasonLen = r.ReadUInt16();
            if (reasonLen > 0 && r.Remaining >= reasonLen)
                d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(reasonLen));
            if (r.Remaining >= 38)
                d.Pet = PetInfoData.ReadCompact(ref r);
            return d;
        }
    }

    // --- Friends ---

    public sealed class FriendListResponseData
    {
        public ushort ResultId;
        public uint OwnerUin;
        public List<uint> FriendUins = new List<uint>();

        public static FriendListResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new FriendListResponseData
            {
                ResultId = r.ReadUInt16(),
                OwnerUin = r.ReadUInt32()
            };
            ushort maxCount = r.ReadUInt16();
            ushort count = r.ReadUInt16();
            int slots = Math.Min((int)maxCount, r.Remaining / 4);
            for (int i = 0; i < slots; i++)
            {
                uint fUin = r.ReadUInt32();
                if (i < count && fUin != 0)
                    d.FriendUins.Add(fUin);
            }
            return d;
        }
    }

    public sealed class AddFriendRequestNotifyData
    {
        public uint RequesterUin;
        public uint Time;
        public uint TargetUin;
        public string Word = string.Empty;

        public static AddFriendRequestNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new AddFriendRequestNotifyData
            {
                RequesterUin = r.ReadUInt32(),
                Time = r.ReadUInt32(),
                TargetUin = r.ReadUInt32()
            };
            ushort len = r.ReadUInt16();
            if (len > 0 && r.Remaining >= len)
                d.Word = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class RemoveFriendResponseData
    {
        public ushort ResultId;
        public uint Uin;
        public uint TargetUin;

        public static RemoveFriendResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new RemoveFriendResponseData
            {
                ResultId = r.ReadUInt16(),
                Uin = r.ReadUInt32(),
                TargetUin = r.ReadUInt32()
            };
        }
    }

    public sealed class AnswerFriendResultData
    {
        public ushort ResultId;
        public uint RequesterUin;
        public uint Time;
        public uint ResponderUin;

        public static AnswerFriendResultData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new AnswerFriendResultData
            {
                ResultId = r.ReadUInt16(),
                RequesterUin = r.ReadUInt32(),
                Time = r.ReadUInt32(),
                ResponderUin = r.ReadUInt32()
            };
        }
    }

    public sealed class FriendUpdateNotifyData
    {
        public uint OwnerUin;
        public uint FriendUin;
        public uint Point;
        public bool Online;
        public string Nickname = string.Empty;
        public byte Gender;
        public uint Identity;
        public uint ExtPoint;

        public static FriendUpdateNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new FriendUpdateNotifyData
            {
                OwnerUin = r.ReadUInt32(),
                FriendUin = r.ReadUInt32(),
                Point = r.ReadUInt32()
            };
            r.ReadByte(); // extCount
            r.Skip(30 * 4); // fixed 30 external item IDs
            d.Online = r.ReadByte() != 0;
            d.Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20));
            d.Gender = r.ReadByte();
            d.Identity = r.ReadUInt32();
            d.ExtPoint = r.ReadUInt32();
            return d;
        }
    }

    // --- Kin (家族) ---

    public sealed class CreateKinResponseData
    {
        public uint Uin;
        public uint KinIndex;
        public uint ErrorNo;
        public string Reason = string.Empty;

        public static CreateKinResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new CreateKinResponseData
            {
                Uin = r.ReadUInt32(),
                KinIndex = r.ReadUInt32(),
                ErrorNo = r.ReadUInt32()
            };
            if (r.Remaining >= 2)
            {
                ushort len = r.ReadUInt16();
                if (len > 0 && r.Remaining >= len)
                    d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            }
            return d;
        }
    }

    public sealed class KinBaseData
    {
        public uint Index;
        public uint OwnerUin;
        public uint CreatedUnix;
        public uint DismissUnix;
        public uint Status;
        public uint Grade;
        public uint FlagIndex;
        public uint FlagId;
        public ushort MemberCount;
        public string Name = string.Empty;
        public string Declaration = string.Empty;
        public string Title = string.Empty;
        public uint Section;
        public uint BaseUpdate;
        public uint ListUpdate;
        public string Notification = string.Empty;
        public uint Honor;
        public uint ActivePoint;

        public static KinBaseData ReadFrom(ref BigEndianReader r)
        {
            var k = new KinBaseData
            {
                Index = r.ReadUInt32(),
                OwnerUin = r.ReadUInt32(),
                CreatedUnix = r.ReadUInt32(),
                DismissUnix = r.ReadUInt32(),
                Status = r.ReadUInt32(),
                Grade = r.ReadUInt32()
            };
            k.FlagIndex = r.ReadUInt32();
            k.FlagId = r.ReadUInt32();
            k.MemberCount = r.ReadUInt16();
            ushort declLen = r.ReadUInt16();
            ushort titleLen = r.ReadUInt16();
            k.Name = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(17));
            if (declLen > 0) k.Declaration = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(declLen));
            if (titleLen > 0) k.Title = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(titleLen));
            k.Section = r.ReadUInt32();
            k.BaseUpdate = r.ReadUInt32();
            k.ListUpdate = r.ReadUInt32();
            ushort notifLen = r.ReadUInt16();
            if (notifLen > 0) k.Notification = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(notifLen));
            k.Honor = r.ReadUInt32();
            k.ActivePoint = r.ReadUInt32();
            // 6 trailing order/last-period uint32 fields
            if (r.Remaining >= 24) r.Skip(24);
            return k;
        }

        public static KinBaseData ParseFetchBaseResponse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            r.ReadUInt32(); // UIN
            return ReadFrom(ref r);
        }
    }

    public sealed class KinMemberData
    {
        public uint Uin;
        public string Nickname = string.Empty;
        public uint JoinedUnix;
        public uint Status;
        public uint StatusTime;
        public uint Grade;
        public uint OnlineTime;
        public uint LastLoginTime;
        public uint Honor;
        public uint ActivePoint;
        public bool IsOnline => Status != 0;
    }

    public sealed class FetchKinMembersResponseData
    {
        public uint Uin;
        public uint ListUpdate;
        public ushort Result;
        public List<KinMemberData> Members = new List<KinMemberData>();

        public static FetchKinMembersResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new FetchKinMembersResponseData
            {
                Uin = r.ReadUInt32(),
                ListUpdate = r.ReadUInt32(),
                Result = r.ReadUInt16()
            };
            ushort count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                var m = new KinMemberData { Uin = r.ReadUInt32() };
                ushort nickLen = r.ReadUInt16();
                if (nickLen > 0) m.Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(nickLen));
                m.JoinedUnix = r.ReadUInt32();
                m.Status = r.ReadUInt32();
                m.StatusTime = r.ReadUInt32();
                m.Grade = r.ReadUInt32();
                m.OnlineTime = r.ReadUInt32();
                m.LastLoginTime = r.ReadUInt32();
                d.Members.Add(m);
            }
            for (int i = 0; i < count && r.Remaining >= 8; i++)
            {
                d.Members[i].Honor = r.ReadUInt32();
                d.Members[i].ActivePoint = r.ReadUInt32();
            }
            return d;
        }
    }

    public sealed class KinOperationResponseData
    {
        public uint Uin;
        public uint Time;
        public uint KinIndex;
        public uint DstUin;
        public uint Para;
        public uint Reserve;
        public string Reason = string.Empty;

        public static KinOperationResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new KinOperationResponseData
            {
                Uin = r.ReadUInt32(),
                Time = r.ReadUInt32(),
                KinIndex = r.ReadUInt32(),
                DstUin = r.ReadUInt32(),
                Para = r.ReadUInt32(),
                Reserve = r.ReadUInt32()
            };
            if (r.Remaining >= 2)
            {
                ushort len = r.ReadUInt16();
                if (len > 0 && r.Remaining >= len)
                    d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            }
            return d;
        }
    }

    public sealed class KinServerOperationData
    {
        public uint InviterUin;
        public uint Time;
        public uint KinIndex;
        public uint ResponderUin;
        public uint Para;
        public uint Reserve;
        public KinBaseData Kin;

        public static KinServerOperationData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new KinServerOperationData
            {
                InviterUin = r.ReadUInt32(),
                Time = r.ReadUInt32(),
                KinIndex = r.ReadUInt32(),
                ResponderUin = r.ReadUInt32(),
                Para = r.ReadUInt32(),
                Reserve = r.ReadUInt32()
            };
            if (r.Remaining >= 50) d.Kin = KinBaseData.ReadFrom(ref r);
            return d;
        }
    }

    public sealed class KinChatNotifyData
    {
        public uint Uin;
        public uint KinIndex;
        public string Message = string.Empty;

        public static KinChatNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new KinChatNotifyData
            {
                Uin = r.ReadUInt32(),
                KinIndex = r.ReadUInt32()
            };
            uint len = r.ReadUInt32();
            if (len > 0 && r.Remaining >= (int)len)
                d.Message = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes((int)len));
            return d;
        }
    }

    public sealed class KinEventNotifyData
    {
        public uint EventType;
        public uint KinIndex;
        public uint Uin;
        public string Nickname = string.Empty;
        public uint AttachUin;
        public string AttachNickname = string.Empty;
        public string Description = string.Empty;

        public static KinEventNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new KinEventNotifyData
            {
                EventType = r.ReadUInt32(),
                KinIndex = r.ReadUInt32(),
                Uin = r.ReadUInt32()
            };
            ushort nickLen = r.ReadUInt16();
            if (nickLen > 0) d.Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(nickLen));
            d.AttachUin = r.ReadUInt32();
            ushort attachLen = r.ReadUInt16();
            if (attachLen > 0) d.AttachNickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(attachLen));
            ushort descLen = r.ReadUInt16();
            if (descLen > 0) d.Description = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(descLen));
            return d;
        }
    }

    public sealed class KinSimpleResultData
    {
        public ushort Result;
        public uint KinIndex;
        public string Reason = string.Empty;

        public static KinSimpleResultData ParseSelf(byte[] payload, bool hasValue)
        {
            var r = new BigEndianReader(payload);
            var d = new KinSimpleResultData { Result = r.ReadUInt16() };
            r.ReadUInt32(); // UIN
            d.KinIndex = r.ReadUInt32();
            if (hasValue && r.Remaining >= 4) r.ReadUInt32();
            if (r.Remaining >= 2)
            {
                ushort len = r.ReadUInt16();
                if (len > 0 && r.Remaining >= len)
                    d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            }
            return d;
        }

        public static KinSimpleResultData ParseTarget(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new KinSimpleResultData { Result = r.ReadUInt16() };
            r.ReadUInt32(); // UIN
            r.ReadUInt32(); // DstUIN
            d.KinIndex = r.ReadUInt32();
            if (r.Remaining >= 2)
            {
                ushort len = r.ReadUInt16();
                if (len > 0 && r.Remaining >= len)
                    d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            }
            return d;
        }

        public static KinSimpleResultData ParseText(byte[] payload, bool indexFirst)
        {
            var r = new BigEndianReader(payload);
            var d = new KinSimpleResultData { Result = r.ReadUInt16() };
            if (indexFirst) { d.KinIndex = r.ReadUInt32(); r.ReadUInt32(); }
            else { r.ReadUInt32(); d.KinIndex = r.ReadUInt32(); }
            ushort textLen = r.ReadUInt16();
            if (textLen > 0 && r.Remaining >= textLen) r.Skip(textLen);
            if (r.Remaining >= 2)
            {
                ushort reasonLen = r.ReadUInt16();
                if (reasonLen > 0 && r.Remaining >= reasonLen)
                    d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(reasonLen));
            }
            return d;
        }
    }

    public sealed class KinOrderInfoData
    {
        public uint KinIndex;
        public string Name = string.Empty;
        public uint Value;
        public ushort Order;
        public ushort BeforeOrder;

        public static KinOrderInfoData ReadFrom(ref BigEndianReader r)
        {
            var e = new KinOrderInfoData { KinIndex = r.ReadUInt32() };
            byte len = r.ReadByte();
            if (len > 0) e.Name = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            e.Value = r.ReadUInt32();
            e.Order = r.ReadUInt16();
            e.BeforeOrder = r.ReadUInt16();
            return e;
        }
    }

    public sealed class FetchKinTopResponseData
    {
        public ushort Result;
        public List<KinOrderInfoData> HonorTop = new List<KinOrderInfoData>();
        public List<KinOrderInfoData> ActiveTop = new List<KinOrderInfoData>();

        public static FetchKinTopResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new FetchKinTopResponseData { Result = r.ReadUInt16() };
            r.ReadUInt32(); // orderTime
            r.ReadUInt32(); // uin
            r.ReadUInt32(); // orderTime
            for (int i = 0; i < 20 && r.Remaining >= 13; i++)
            {
                var entry = KinOrderInfoData.ReadFrom(ref r);
                if (entry.KinIndex != 0) d.HonorTop.Add(entry);
            }
            for (int i = 0; i < 20 && r.Remaining >= 13; i++)
            {
                var entry = KinOrderInfoData.ReadFrom(ref r);
                if (entry.KinIndex != 0) d.ActiveTop.Add(entry);
            }
            return d;
        }
    }

    // --- Marriage (伴侣) ---

    public sealed class SparkResponseData
    {
        public ushort Result;
        public uint Uin;
        public uint TargetUin;
        public string Word = string.Empty;

        public static SparkResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new SparkResponseData
            {
                Result = r.ReadUInt16(),
                Uin = r.ReadUInt32(),
                TargetUin = r.ReadUInt32()
            };
            byte len = r.ReadByte();
            if (len > 0 && r.Remaining >= len) d.Word = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class SparkNotifyData
    {
        public uint ProposerUin;
        public uint TargetUin;
        public string Nickname = string.Empty;
        public string Word = string.Empty;

        public static SparkNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new SparkNotifyData
            {
                ProposerUin = r.ReadUInt32(),
                TargetUin = r.ReadUInt32(),
                Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20))
            };
            byte len = r.ReadByte();
            if (len > 0 && r.Remaining >= len) d.Word = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class AnswerSparkResultData
    {
        public ushort Result;
        public uint ProposerUin;
        public uint ResponderUin;
        public string Word = string.Empty;

        public static AnswerSparkResultData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new AnswerSparkResultData
            {
                Result = r.ReadUInt16(),
                ProposerUin = r.ReadUInt32(),
                ResponderUin = r.ReadUInt32()
            };
            byte len = r.ReadByte();
            if (len > 0 && r.Remaining >= len) d.Word = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class MarriageInfoResponseData
    {
        public ushort Result;
        public uint TargetUin;
        public uint MarriageUin;
        public uint Loyalty;
        public ushort Level;
        public uint LevelValue;
        public uint Age;
        public string LoveWord = string.Empty;
        public string Nickname = string.Empty;
        public string SpouseNickname = string.Empty;
        public uint RingId;
        public bool IsMarried => Result == 0 && MarriageUin != 0;

        public static MarriageInfoResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new MarriageInfoResponseData
            {
                Result = r.ReadUInt16(),
                TargetUin = r.ReadUInt32(),
                MarriageUin = r.ReadUInt32(),
                Loyalty = r.ReadUInt32(),
                Level = r.ReadUInt16(),
                LevelValue = r.ReadUInt32(),
                Age = r.ReadUInt32()
            };
            ushort wordLen = r.ReadUInt16();
            if (wordLen > 0 && r.Remaining >= wordLen)
                d.LoveWord = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(wordLen));
            if (r.Remaining >= 44)
            {
                d.Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20));
                d.SpouseNickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20));
                d.RingId = r.ReadUInt32();
            }
            return d;
        }
    }

    public sealed class MarriageSimpleResultData
    {
        public uint Uin;
        public uint TargetUin;
        public ushort Result;

        public static MarriageSimpleResultData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new MarriageSimpleResultData
            {
                Uin = r.ReadUInt32(),
                TargetUin = r.ReadUInt32(),
                Result = r.ReadUInt16()
            };
        }
    }

    // --- Extended Battle Events ---

    public sealed class WorldUseItemEventData
    {
        public ushort PlayerId;
        public uint ClientTime;
        public uint ItemId; // 63 = Needle, 41/42/43 = FieldObject, 44/46 = Projectile, 64 = RemoteDetonate
        public ushort PosX;
        public ushort PosY;
        public uint Flag1;
        public uint Flag2;
        public uint Flag3;
        public uint Flag4;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(30);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(ClientTime);
            b.WriteUInt32(ItemId);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            b.WriteUInt32(Flag1);
            b.WriteUInt32(Flag2);
            b.WriteUInt32(Flag3);
            b.WriteUInt32(Flag4);
            return b.ToArray();
        }

        public static WorldUseItemEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new WorldUseItemEventData
            {
                PlayerId = r.ReadUInt16(),
                ClientTime = r.ReadUInt32(),
                ItemId = r.ReadUInt32(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16(),
                Flag1 = r.ReadUInt32(),
                Flag2 = r.ReadUInt32(),
                Flag3 = r.ReadUInt32(),
                Flag4 = r.ReadUInt32()
            };
        }
    }

    public sealed class MoveMapElementEventData
    {
        public ushort PlayerId;
        public uint ClientTime;
        public uint ElementId;
        public ushort Row;
        public ushort Col;
        public byte Direction;
        public ushort Sequence;

        public byte[] MarshalRequest()
        {
            var b = new BigEndianBuffer(15);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(ClientTime);
            b.WriteUInt32(ElementId);
            b.WriteByte((byte)Row);
            b.WriteByte((byte)Col);
            b.WriteByte(Direction);
            b.WriteUInt16(Sequence);
            return b.ToArray();
        }

        public byte[] MarshalMoved()
        {
            var b = new BigEndianBuffer(17);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(ClientTime);
            b.WriteUInt32(ElementId);
            b.WriteUInt16(Row);
            b.WriteUInt16(Col);
            b.WriteByte(Direction);
            b.WriteUInt16(Sequence);
            return b.ToArray();
        }

        public static MoveMapElementEventData ParseRequest(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new MoveMapElementEventData
            {
                PlayerId = r.ReadUInt16(),
                ClientTime = r.ReadUInt32(),
                ElementId = r.ReadUInt32(),
                Row = r.ReadByte(),
                Col = r.ReadByte(),
                Direction = r.ReadByte(),
                Sequence = r.ReadUInt16()
            };
        }

        public static MoveMapElementEventData ParseMoved(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new MoveMapElementEventData
            {
                PlayerId = r.ReadUInt16(),
                ClientTime = r.ReadUInt32(),
                ElementId = r.ReadUInt32(),
                Row = r.ReadUInt16(),
                Col = r.ReadUInt16(),
                Direction = r.ReadByte(),
                Sequence = r.ReadUInt16()
            };
        }
    }

    public sealed class MoveBombEventData
    {
        public ushort PlayerId;
        public ushort ClientTime;
        public ushort FromRow;
        public ushort FromCol;
        public ushort ToRow;
        public ushort ToCol;
        public byte Direction;
        public ushort BombPlayerId;
        public uint BombTime;
        public byte BombPower;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(20);
            b.WriteUInt16(PlayerId);
            b.WriteUInt16(ClientTime);
            b.WriteUInt16(FromRow);
            b.WriteUInt16(FromCol);
            b.WriteUInt16(ToRow);
            b.WriteUInt16(ToCol);
            b.WriteByte(Direction);
            b.WriteUInt16(BombPlayerId);
            b.WriteUInt32(BombTime);
            b.WriteByte(BombPower);
            return b.ToArray();
        }

        public static MoveBombEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new MoveBombEventData
            {
                PlayerId = r.ReadUInt16(),
                ClientTime = r.ReadUInt16(),
                FromRow = r.ReadUInt16(),
                FromCol = r.ReadUInt16(),
                ToRow = r.ReadUInt16(),
                ToCol = r.ReadUInt16(),
                Direction = r.ReadByte(),
                BombPlayerId = r.ReadUInt16(),
                BombTime = r.ReadUInt32(),
                BombPower = r.ReadByte()
            };
        }
    }

    public sealed class PlayerReliveEventData
    {
        public ushort PlayerId;
        public uint ClientTime;
        public byte Row;
        public byte Col;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(8);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(ClientTime);
            b.WriteByte(Row);
            b.WriteByte(Col);
            return b.ToArray();
        }

        public static PlayerReliveEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PlayerReliveEventData
            {
                PlayerId = r.ReadUInt16(),
                ClientTime = r.ReadUInt32(),
                Row = r.ReadByte(),
                Col = r.ReadByte()
            };
        }
    }

    public sealed class UseEmotionEventData
    {
        public uint Uin;
        public uint Time;
        public uint EmotionId;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(Uin);
            b.WriteUInt32(Time);
            b.WriteUInt32(EmotionId);
            return b.ToArray();
        }

        public static UseEmotionEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new UseEmotionEventData
            {
                Uin = r.ReadUInt32(),
                Time = r.ReadUInt32(),
                EmotionId = r.ReadUInt32()
            };
        }
    }

    public sealed class DispatchedGameItem
    {
        public byte Row;
        public byte Col;
        public byte ItemType;
        public byte SubType;
        public uint ItemId;
    }

    public sealed class DispatchItemEventData
    {
        public uint Time;
        public List<DispatchedGameItem> Items = new List<DispatchedGameItem>();

        public static DispatchItemEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new DispatchItemEventData { Time = r.ReadUInt32() };
            byte count = r.ReadByte();
            for (int i = 0; i < count && r.Remaining >= 8; i++)
            {
                // GAME_ITEM wire size = 8 bytes (Row, Col, Type, SubType, u32 ID or similar)
                d.Items.Add(new DispatchedGameItem
                {
                    Row = r.ReadByte(),
                    Col = r.ReadByte(),
                    ItemType = r.ReadByte(),
                    SubType = r.ReadByte(),
                    ItemId = r.ReadUInt32()
                });
            }
            return d;
        }
    }

    // --- Extended Request Builders ---

    public static class ExtendedRequestBuilder
    {
        public static byte[] BuildLogoutPayload(uint uin, uint time)
        {
            var b = new BigEndianBuffer(9);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteByte(0); // StatisticItems count
            return b.ToArray();
        }

        public static byte[] BuildPlayerListPayload(uint uin, uint time, ushort startPlayerId = 0, ushort number = 50)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt16(startPlayerId);
            b.WriteUInt16(number);
            return b.ToArray();
        }

        public static byte[] BuildFindFriendPayload(uint uin, uint time, uint friendUin)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(friendUin);
            return b.ToArray();
        }

        public static byte[] BuildGetGameMoneyPayload(uint uin, uint time)
        {
            var b = new BigEndianBuffer(8);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            return b.ToArray();
        }

        public static byte[] BuildRoomChatPayload(uint uin, uint time, ushort dstPlayerId, string content)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(content, 500);
            var b = new BigEndianBuffer(12 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt16(dstPlayerId);
            b.WriteUInt16((ushort)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildSectionChatPayload(uint uin, uint time, ushort dstPlayerId, uint dstUin, string content)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(content, 500);
            var b = new BigEndianBuffer(16 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt16(dstPlayerId);
            b.WriteUInt32(dstUin);
            b.WriteUInt16((ushort)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildAcrossSectionChatPayload(uint uin, uint time, uint serviceId, ushort dstPlayerId, uint dstUin, string content)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(content, 500);
            var b = new BigEndianBuffer(20 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(serviceId);
            b.WriteUInt16(dstPlayerId);
            b.WriteUInt32(dstUin);
            b.WriteUInt16((ushort)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildJoinRoomPayload(uint uin, uint time, RoomGameType gameType)
        {
            var b = new BigEndianBuffer(9);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteByte((byte)gameType);
            return b.ToArray();
        }

        public static byte[] BuildModifyRoomPayload(
            uint uin, uint time, ModifyRoomFlags flags, string roomName,
            RoomPropertyFlag roomFlag, string password, RoomGameType gameType, uint continueId = 0)
        {
            var b = new BigEndianBuffer(54);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32((uint)flags);
            b.WriteFixedSlot(LegacyTextEncoding.EncodeSlot(roomName, 20), 20);
            b.WriteByte((byte)roomFlag);
            b.WriteFixedSlot(LegacyTextEncoding.EncodeSlot(password, 16), 16);
            b.WriteByte((byte)gameType);
            b.WriteUInt32(continueId);
            return b.ToArray();
        }

        public static byte[] BuildChangeRoomTypePayload(uint uin, uint time, RoomGameType gameType, uint continueId = 0)
        {
            var b = new BigEndianBuffer(13);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteByte((byte)gameType);
            b.WriteUInt32(continueId);
            return b.ToArray();
        }

        public static byte[] BuildSetSeatStatusPayload(uint uin, uint time, byte seatId, bool locked)
        {
            var b = new BigEndianBuffer(10);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteByte(seatId);
            b.WriteByte((byte)(locked ? 2 : 1));
            return b.ToArray();
        }

        public static byte[] BuildKickOffPlayerPayload(uint uin, uint time, ushort targetPlayerId, uint targetUin)
        {
            var b = new BigEndianBuffer(14);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt16(targetPlayerId);
            b.WriteUInt32(targetUin);
            return b.ToArray();
        }

        public static byte[] BuildUseItemInRoomPayload(uint uin, uint time, uint itemId)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(itemId);
            return b.ToArray();
        }

        public static byte[] BuildBackgroundListPayload(uint uin, uint time)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(0);
            return b.ToArray();
        }

        public static byte[] BuildUseBackgroundPayload(uint uin, uint time, uint itemId)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(itemId);
            return b.ToArray();
        }

        public static byte[] BuildShopListPayload(uint uin, uint time, uint clientVer = 848, uint csVer = 1, uint shopListVer = 848)
        {
            var b = new BigEndianBuffer(20);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(clientVer);
            b.WriteUInt32(csVer);
            b.WriteUInt32(shopListVer);
            return b.ToArray();
        }

        public static byte[] BuildShopBuyPayload(
            uint uin, uint time, uint commodityId,
            ShopPaymentMethod payType = ShopPaymentMethod.GameMoney,
            ushort dealType = 2, uint clientVer = 848, uint commodityVer = 848, ushort agreeMixed = 0)
        {
            var b = new BigEndianBuffer(26);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(commodityId);
            b.WriteUInt16(dealType);
            b.WriteUInt16((ushort)payType);
            b.WriteUInt32(clientVer);
            b.WriteUInt32(commodityVer);
            b.WriteUInt16(agreeMixed);
            return b.ToArray();
        }

        public static byte[] BuildCombineForgePayload(uint uin, uint time, byte type, uint itemId, uint fromItemId, IReadOnlyList<uint> materialIds)
        {
            int count = materialIds?.Count ?? 0;
            var b = new BigEndianBuffer(19 + count * 4);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteByte(type);
            b.WriteUInt32(itemId);
            b.WriteUInt32(fromItemId);
            b.WriteUInt16((ushort)count);
            for (int i = 0; i < count; i++) b.WriteUInt32(materialIds[i]);
            return b.ToArray();
        }

        public static byte[] BuildForgePayload(uint uin, uint time, byte type, uint itemId, uint materialId)
        {
            var b = new BigEndianBuffer(17);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteByte(type);
            b.WriteUInt32(itemId);
            b.WriteUInt32(materialId);
            return b.ToArray();
        }

        public static byte[] BuildBreakEggPayload(uint uin, uint time, uint hammerId, uint eggId)
        {
            var b = new BigEndianBuffer(16);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(hammerId);
            b.WriteUInt32(eggId);
            return b.ToArray();
        }

        public static byte[] BuildPlayerPetsPayload(uint uin, uint time)
        {
            var b = new BigEndianBuffer(8);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            return b.ToArray();
        }

        public static byte[] BuildHandlePetPayload(uint uin, uint time, PetEventId eventId, uint petId, string parameter = "")
        {
            var b = new BigEndianBuffer(36);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32((uint)eventId);
            b.WriteUInt32(petId);
            b.WriteFixedSlot(LegacyTextEncoding.EncodeSlot(parameter, 20), 20);
            return b.ToArray();
        }

        public static byte[] BuildFriendListPayload(uint uin, uint time)
        {
            var b = new BigEndianBuffer(8);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            return b.ToArray();
        }

        public static byte[] BuildAddFriendPayload(uint uin, uint time, uint targetUin, string word)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(word, 512);
            var b = new BigEndianBuffer(14 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(targetUin);
            b.WriteUInt16((ushort)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildRemoveFriendPayload(uint uin, uint time, uint targetUin)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(targetUin);
            return b.ToArray();
        }

        public static byte[] BuildAnswerFriendPayload(bool accepted, uint requesterUin, uint time, uint responderUin)
        {
            var b = new BigEndianBuffer(14);
            b.WriteUInt16((ushort)(accepted ? 0 : 1));
            b.WriteUInt32(requesterUin);
            b.WriteUInt32(time);
            b.WriteUInt32(responderUin);
            return b.ToArray();
        }

        public static byte[] BuildCreateKinPayload(uint uin, uint time, string name, string declaration, uint section = 1)
        {
            byte[] decl = LegacyTextEncoding.EncodeSlot(declaration, 257);
            var b = new BigEndianBuffer(37 + decl.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(0); // Status
            b.WriteUInt16(0); // Reserve
            b.WriteUInt16((ushort)decl.Length);
            b.WriteFixedSlot(LegacyTextEncoding.EncodeSlot(name, 17), 17);
            b.WriteBytes(decl);
            b.WriteUInt32(section);
            return b.ToArray();
        }

        public static byte[] BuildFetchKinBasePayload(uint uin, uint time, uint kinIndex, uint isMember = 1)
        {
            var b = new BigEndianBuffer(16);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(isMember);
            return b.ToArray();
        }

        public static byte[] BuildFetchKinMembersPayload(uint uin, uint time, uint kinIndex, uint listUpdate = 0)
        {
            var b = new BigEndianBuffer(16);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(listUpdate);
            return b.ToArray();
        }

        public static byte[] BuildKinOperationPayload(uint uin, uint time, uint kinIndex, uint dstUin, uint para = 0, uint reserve = 0)
        {
            var b = new BigEndianBuffer(24);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(dstUin);
            b.WriteUInt32(para);
            b.WriteUInt32(reserve);
            return b.ToArray();
        }

        public static byte[] BuildKinServerOperationAnswerPayload(uint inviterUin, uint time, uint kinIndex, uint responderUin, bool accepted)
        {
            var b = new BigEndianBuffer(24);
            b.WriteUInt32(inviterUin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(responderUin);
            b.WriteUInt32(accepted ? KinConstants.OperationAccepted : KinConstants.OperationRejected);
            b.WriteUInt32(0);
            return b.ToArray();
        }

        public static byte[] BuildKinChatPayload(uint uin, uint time, uint kinIndex, string message)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(message, 512);
            var b = new BigEndianBuffer(16 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            b.WriteUInt32((uint)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildKickKinMemberPayload(uint uin, uint time, uint dstUin, uint kinIndex)
        {
            var b = new BigEndianBuffer(16);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(dstUin);
            b.WriteUInt32(kinIndex);
            return b.ToArray();
        }

        public static byte[] BuildExitKinPayload(uint uin, uint time, uint kinIndex)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            return b.ToArray();
        }

        public static byte[] BuildDismissKinPayload(uint uin, uint time, uint kinIndex, uint value = 0)
        {
            var b = new BigEndianBuffer(16);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(value);
            return b.ToArray();
        }

        public static byte[] BuildSetKinDeclarationPayload(uint uin, uint time, uint kinIndex, string declaration)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(declaration, 257);
            var b = new BigEndianBuffer(14 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            b.WriteUInt16((ushort)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildSetKinNotificationPayload(uint uin, uint time, uint kinIndex, string notification)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(notification, 257);
            var b = new BigEndianBuffer(14 + text.Length);
            // SetKinNotificationCommand (0x0141) uses indexFirst = true: KinIndex, UIN, Time, Len, Text
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt16((ushort)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildFetchKinTopPayload(uint uin, uint time, uint kinIndex)
        {
            var b = new BigEndianBuffer(24);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(0); // ClientOrderTime
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(0); // HonorOrder
            b.WriteUInt32(0); // ActivePointOrder
            return b.ToArray();
        }

        public static byte[] BuildSparkPayload(uint uin, uint targetUin, string word)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(word, 200);
            var b = new BigEndianBuffer(9 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(targetUin);
            b.WriteByte((byte)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildAnswerSparkPayload(uint responderUin, uint proposerUin, bool accepted)
        {
            var b = new BigEndianBuffer(10);
            b.WriteUInt32(responderUin);
            b.WriteUInt32(proposerUin);
            b.WriteUInt16((ushort)(accepted ? 0 : 1));
            return b.ToArray();
        }

        public static byte[] BuildMarriageInfoPayload(uint uin, uint targetUin)
        {
            var b = new BigEndianBuffer(8);
            b.WriteUInt32(uin);
            b.WriteUInt32(targetUin);
            return b.ToArray();
        }

        public static byte[] BuildModifyLoveWordPayload(uint uin, uint spouseUin, string loveWord)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(loveWord, 200);
            var b = new BigEndianBuffer(10 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(spouseUin);
            b.WriteUInt16((ushort)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildDivorcePayload(uint uin, uint spouseUin)
        {
            var b = new BigEndianBuffer(8);
            b.WriteUInt32(uin);
            b.WriteUInt32(spouseUin);
            return b.ToArray();
        }

        public static byte[] BuildStartWeddingPayload(uint uin, uint targetUin, ushort weddingModeId)
        {
            var b = new BigEndianBuffer(10);
            b.WriteUInt32(uin);
            b.WriteUInt32(targetUin);
            b.WriteUInt16(weddingModeId);
            return b.ToArray();
        }

        public static byte[] BuildAnswerWeddingPayload(uint uin, uint targetUin, bool accepted)
        {
            var b = new BigEndianBuffer(10);
            b.WriteUInt32(uin);
            b.WriteUInt32(targetUin);
            b.WriteUInt16((ushort)(accepted ? 0 : 1));
            return b.ToArray();
        }

        public static byte[] BuildAssignKinAuthorityPayload(uint kinIndex, uint uin, uint time, uint dstUin, uint authorityId)
        {
            var b = new BigEndianBuffer(20);
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(dstUin);
            b.WriteUInt32(authorityId);
            return b.ToArray();
        }

        public static byte[] BuildSetKinBadgePayload(uint kinIndex, uint uin, uint time, uint badgeId, uint definedBadgeId = 0)
        {
            var b = new BigEndianBuffer(20);
            b.WriteUInt32(kinIndex);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(badgeId);
            b.WriteUInt32(definedBadgeId);
            return b.ToArray();
        }

        public static byte[] BuildUpdateKinTitlePayload(uint uin, uint time, uint kinIndex, string title)
        {
            byte[] text = LegacyTextEncoding.EncodeSlot(title, 200);
            var b = new BigEndianBuffer(14 + text.Length);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(kinIndex);
            b.WriteUInt16((ushort)text.Length);
            b.WriteBytes(text);
            return b.ToArray();
        }

        public static byte[] BuildGetConfigFilePayload(uint uin, uint time)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(0); // 0 reports -> server responds with 0-count current status
            return b.ToArray();
        }
    }

    public sealed class KinBadgeResultData
    {
        public ushort Result;
        public uint KinIndex;
        public uint Uin;
        public uint BadgeId;
        public uint DefinedBadgeId;

        public static KinBadgeResultData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new KinBadgeResultData
            {
                Result = r.ReadUInt16(),
                KinIndex = r.ReadUInt32(),
                Uin = r.ReadUInt32(),
                BadgeId = r.ReadUInt32(),
                DefinedBadgeId = r.ReadUInt32()
            };
        }
    }

    public sealed class KinAuthorityResultData
    {
        public ushort Result;
        public uint KinIndex;
        public uint Uin;
        public uint DstUin;
        public uint AuthorityId;
        public string Reason = string.Empty;

        public static KinAuthorityResultData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new KinAuthorityResultData
            {
                Result = r.ReadUInt16(),
                KinIndex = r.ReadUInt32(),
                Uin = r.ReadUInt32(),
                DstUin = r.ReadUInt32(),
                AuthorityId = r.ReadUInt32()
            };
            if (r.Remaining >= 2)
            {
                ushort len = r.ReadUInt16();
                if (len > 0 && r.Remaining >= len)
                    d.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            }
            return d;
        }
    }

    public sealed class AcrossSectionChatResponseData
    {
        public ushort ResultId;
        public uint DestinationUin;

        public static AcrossSectionChatResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new AcrossSectionChatResponseData
            {
                ResultId = r.ReadUInt16(),
                DestinationUin = r.Remaining >= 4 ? r.ReadUInt32() : 0
            };
        }
    }
}
