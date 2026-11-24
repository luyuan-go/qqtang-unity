using System;
using System.Collections.Generic;

namespace Qqtang.Core.Protocol
{
    public static class TreasureCommands
    {
        public const ushort EnterTreasure = 0x00AB;
        public const ushort GetTreasureItem = 0x00AC;
        public const ushort LeaveTreasure = 0x00AD;
        public const ushort TreasureTimeout = 0x00AE;
    }

    public static class ModeEventSchemas
    {
        public const ushort RequestGetBun = 0x0FB6;
        public const ushort NotifyPlayerGetBun = 0x0FB7;
        public const ushort RequestPutBun = 0x0FB8;
        public const ushort NotifyPlayerPutBun = 0x0FB9;
        public const ushort NotifyMapElementExploded = 0x0FBE;
        public const ushort NotifyItemExploded = 0x0FBF;
        public const ushort RequestEatBomb = 0x10E2;
        public const ushort NotifyPlayerEatBomb = 0x10E3;
        public const ushort PlayerBeHarmed = 0x10F4;
        public const ushort PlayerThrowBomb = 0x1159;
        public const ushort RequestPushMapElement = 0x115E;
        public const ushort NotifyPushMapElement = 0x115F;
        public const ushort NotifyPlayerTrapped = 0x1166;
        public const ushort NotifyNpcUseSkill = 0x1169;
        public const ushort NotifyNpcTalk = 0x116A;
        public const ushort NotifyNpcDropItem = 0x116B;
        public const ushort RequestPreparedUseProp = 0x1174;
        public const ushort NotifyPreparedUseProp = 0x1175;
        public const ushort NotifyGameNextMap = 0x1177;
        public const ushort RequestGameNextMap = 0x1178;
        public const ushort ResponseGameNextMap = 0x1179;
        public const ushort CreateNpcBoss = 0x1389;
        public const ushort CreatePveNpcBoss = 0x138A;
        public const ushort RequestTankBaseHp = 0x15B3;
    }

    // --- Hidden Treasure Room (0x00AB..0x00AE) ---

    public sealed class TreasureRoomResponseData
    {
        public ushort ResultId;
        public uint MapId;
        public List<ItemInfoData> Items = new List<ItemInfoData>();

        public static TreasureRoomResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new TreasureRoomResponseData
            {
                ResultId = r.ReadUInt16(),
                MapId = r.ReadUInt32()
            };
            ushort count = r.ReadUInt16();
            for (int i = 0; i < count && r.Remaining >= ItemInfoData.WireSize; i++)
                d.Items.Add(ItemInfoData.ReadFrom(ref r));
            return d;
        }
    }

    // --- Wedding Ceremony (0x010A..0x010E) ---

    public sealed class StartWeddingResponseData
    {
        public uint Uin;
        public uint TargetUin;
        public string Nickname = string.Empty;
        public string TargetNickname = string.Empty;
        public ushort Result;
        public string Word = string.Empty;
        public ushort WeddingModeId;

        public static StartWeddingResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new StartWeddingResponseData
            {
                Uin = r.ReadUInt32(),
                TargetUin = r.ReadUInt32(),
                Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20)),
                TargetNickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20)),
                Result = r.ReadUInt16()
            };
            byte len = r.ReadByte();
            if (len > 0 && r.Remaining >= len) d.Word = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            if (r.Remaining >= 2) d.WeddingModeId = r.ReadUInt16();
            return d;
        }
    }

    public sealed class WeddingConfirmNotifyData
    {
        public uint ProposerUin;
        public uint TargetUin;
        public string Nickname = string.Empty;
        public string TargetNickname = string.Empty;
        public string Word = string.Empty;

        public static WeddingConfirmNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new WeddingConfirmNotifyData
            {
                ProposerUin = r.ReadUInt32(),
                TargetUin = r.ReadUInt32(),
                Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20)),
                TargetNickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20))
            };
            byte len = r.ReadByte();
            if (len > 0 && r.Remaining >= len) d.Word = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class AnswerWeddingResultData
    {
        public uint Uin;
        public uint TargetUin;
        public string Nickname = string.Empty;
        public string TargetNickname = string.Empty;
        public ushort Result;
        public string Word = string.Empty;

        public static AnswerWeddingResultData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new AnswerWeddingResultData
            {
                Uin = r.ReadUInt32(),
                TargetUin = r.ReadUInt32(),
                Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20)),
                TargetNickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20)),
                Result = r.ReadUInt16()
            };
            byte len = r.ReadByte();
            if (len > 0 && r.Remaining >= len) d.Word = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class ChangeWeddingModeResponseData
    {
        public uint Uin;
        public ushort Result;
        public string Word = string.Empty;

        public static ChangeWeddingModeResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var d = new ChangeWeddingModeResponseData
            {
                Uin = r.ReadUInt32(),
                Result = r.ReadUInt16()
            };
            byte len = r.ReadByte();
            if (len > 0 && r.Remaining >= len) d.Word = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class ChangeWeddingModeNotifyData
    {
        public uint Uin;
        public ushort WeddingModeId; // 0 = Western, 1 = Chinese

        public static ChangeWeddingModeNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new ChangeWeddingModeNotifyData
            {
                Uin = r.ReadUInt32(),
                WeddingModeId = r.ReadUInt16()
            };
        }
    }

    // --- Bosses, Adventure, Bun, Prepared Props ---

    public sealed class BossItemDropData
    {
        public uint ItemId;
        public ushort ItemCount;
        public ushort DropTime;
    }

    public sealed class CompetitiveBossInfoData
    {
        public uint Label;
        public ushort BossId;
        public byte RoleId;
        public byte TeamId;
        public uint AiType;
        public ushort Row;
        public ushort Col;
        public ushort Hp;
        public uint Rate;
        public uint Bubble;
        public ushort Power;
        public List<BossItemDropData> NormalItems = new List<BossItemDropData>();
        public List<uint> Skills = new List<uint>();
    }

    public sealed class CreateNpcBossEventData
    {
        public List<CompetitiveBossInfoData> Bosses = new List<CompetitiveBossInfoData>();
        public uint Time;

        public static CreateNpcBossEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new CreateNpcBossEventData();
            uint count = r.ReadUInt32();
            for (uint i = 0; i < count && r.Remaining >= 28; i++)
            {
                var b = new CompetitiveBossInfoData
                {
                    Label = r.ReadUInt32(),
                    BossId = r.ReadUInt16(),
                    RoleId = r.ReadByte(),
                    TeamId = r.ReadByte(),
                    AiType = r.ReadUInt32(),
                    Row = r.ReadUInt16(),
                    Col = r.ReadUInt16(),
                    Hp = r.ReadUInt16(),
                    Rate = r.ReadUInt32(),
                    Bubble = r.ReadUInt32(),
                    Power = r.ReadUInt16()
                };
                ushort normalCount = r.ReadUInt16();
                for (int k = 0; k < normalCount; k++)
                    b.NormalItems.Add(new BossItemDropData { ItemId = r.ReadUInt32(), ItemCount = r.ReadUInt16(), DropTime = r.ReadUInt16() });
                ushort outfitCount = r.ReadUInt16();
                if (outfitCount > 0) r.Skip(outfitCount * 8);
                ushort skillCount = r.ReadUInt16();
                for (int k = 0; k < skillCount; k++) b.Skills.Add(r.ReadUInt32());
                uint metaLen = r.ReadUInt32();
                if (metaLen > 0) r.Skip((int)metaLen);
                d.Bosses.Add(b);
            }
            if (r.Remaining >= 4) d.Time = r.ReadUInt32();
            return d;
        }
    }

    public sealed class PveBossGroupData
    {
        public ushort BossId;
        public byte BossCount;
        public List<BossItemDropData> NormalItems = new List<BossItemDropData>();
    }

    public sealed class CreatePveNpcBossEventData
    {
        public List<PveBossGroupData> Groups = new List<PveBossGroupData>();

        public static CreatePveNpcBossEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new CreatePveNpcBossEventData();
            uint count = r.ReadUInt32();
            for (uint i = 0; i < count && r.Remaining >= 5; i++)
            {
                var g = new PveBossGroupData
                {
                    BossId = r.ReadUInt16(),
                    BossCount = r.ReadByte()
                };
                ushort itemCount = r.ReadUInt16();
                for (int k = 0; k < itemCount && r.Remaining >= 8; k++)
                    g.NormalItems.Add(new BossItemDropData { ItemId = r.ReadUInt32(), ItemCount = r.ReadUInt16(), DropTime = r.ReadUInt16() });
                d.Groups.Add(g);
            }
            return d;
        }
    }

    public sealed class EntityHarmedEventData
    {
        public ushort ObjectId;
        public uint Time;
        public ushort PosX;
        public ushort PosY;
        public bool IsAvatar;
        public ushort LossHp;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(13);
            b.WriteUInt16(ObjectId);
            b.WriteUInt32(Time);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            b.WriteByte((byte)(IsAvatar ? 1 : 0));
            b.WriteUInt16(LossHp);
            return b.ToArray();
        }

        public static EntityHarmedEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new EntityHarmedEventData
            {
                ObjectId = r.ReadUInt16(),
                Time = r.ReadUInt32(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16(),
                IsAvatar = r.ReadByte() != 0,
                LossHp = r.ReadUInt16()
            };
        }
    }

    public sealed class BunActionEventData
    {
        public ushort PlayerId;
        public uint ClientTime;
        public ushort PosX;
        public ushort PosY;
        public byte BunId;
        public byte BunTeamId;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(ClientTime);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            b.WriteByte(BunId);
            b.WriteByte(BunTeamId);
            return b.ToArray();
        }

        public static BunActionEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new BunActionEventData
            {
                PlayerId = r.ReadUInt16(),
                ClientTime = r.ReadUInt32(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16(),
                BunId = r.ReadByte(),
                BunTeamId = r.ReadByte()
            };
        }
    }

    public sealed class NpcUseSkillEventData
    {
        public ushort ObjectId;
        public uint Time;
        public ushort PosX;
        public ushort PosY;
        public ushort SkillId;
        public List<(uint sceneId, byte row, byte col)> Items = new List<(uint, byte, byte)>();

        public static NpcUseSkillEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new NpcUseSkillEventData
            {
                ObjectId = r.ReadUInt16(),
                Time = r.ReadUInt32(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16(),
                SkillId = r.ReadUInt16()
            };
            byte count = r.ReadByte();
            for (int i = 0; i < count && r.Remaining >= 6; i++)
                d.Items.Add((r.ReadUInt32(), r.ReadByte(), r.ReadByte()));
            return d;
        }
    }

    public sealed class NpcTalkEventData
    {
        public ushort ObjectId;
        public string Words = string.Empty;

        public static NpcTalkEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new NpcTalkEventData { ObjectId = r.ReadUInt16() };
            ushort len = r.ReadUInt16();
            if (len > 0 && r.Remaining >= len)
                d.Words = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(len));
            return d;
        }
    }

    public sealed class PreparedUsePropEventData
    {
        public ushort PlayerId;
        public uint ItemId;
        public ushort PosX;
        public ushort PosY;
        public uint Flag1, Flag2, Flag3, Flag4;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(26);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(ItemId);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            b.WriteUInt32(Flag1);
            b.WriteUInt32(Flag2);
            b.WriteUInt32(Flag3);
            b.WriteUInt32(Flag4);
            return b.ToArray();
        }

        public static PreparedUsePropEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PreparedUsePropEventData
            {
                PlayerId = r.ReadUInt16(),
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

    public sealed class GameNextMapRequestData
    {
        public ushort PlayerId;
        public uint ClientTime;
        public uint ContinueId;
        public uint NextMapId;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(14);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(ClientTime);
            b.WriteUInt32(ContinueId);
            b.WriteUInt32(NextMapId);
            return b.ToArray();
        }
    }

    public static class ModeRequestBuilder
    {
        public static byte[] BuildTreasureRoomPayload(uint uin, uint time)
        {
            var b = new BigEndianBuffer(8);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            return b.ToArray();
        }

        public static byte[] BuildTreasureItemPayload(uint uin, uint time, uint itemId)
        {
            var b = new BigEndianBuffer(12);
            b.WriteUInt32(uin);
            b.WriteUInt32(time);
            b.WriteUInt32(itemId);
            return b.ToArray();
        }

        public static byte[] BuildChangeWeddingModePayload(uint uin, ushort weddingModeId)
        {
            var b = new BigEndianBuffer(6);
            b.WriteUInt32(uin);
            b.WriteUInt16(weddingModeId);
            return b.ToArray();
        }
    }
}
