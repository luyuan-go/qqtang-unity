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
        public const ushort NotifyChangeArbitrator = 0x0FB1;
        public const ushort RequestGetBun = 0x0FB6;
        public const ushort NotifyPlayerGetBun = 0x0FB7;
        public const ushort RequestPutBun = 0x0FB8;
        public const ushort NotifyPlayerPutBun = 0x0FB9;
        public const ushort NotifyMapElementExploded = 0x0FBE;
        public const ushort NotifyItemExploded = 0x0FBF;
        public const ushort NotifyRecoverAvatar = 0x10E0;
        public const ushort NotifyDispatchBomb = 0x10E1;
        public const ushort RequestEatBomb = 0x10E2;
        public const ushort NotifyPlayerEatBomb = 0x10E3;
        public const ushort NotifyPlayerLeave = 0x10EC;
        public const ushort RequestLaunchMachine = 0x10EF;
        public const ushort NotifyMachineFree = 0x10F0;
        public const ushort NotifyMachineAngry = 0x10F1;
        public const ushort NotifyMachineGrace = 0x10F2;
        public const ushort NotifyMachineCool = 0x10F3;
        public const ushort PlayerBeHarmed = 0x10F4;
        public const ushort RequestCancelUseProp = 0x10F5;
        public const ushort NotifyCancelUseProp = 0x10F6;
        public const ushort NotifyPropTrigger = 0x10F7;
        public const ushort PlayerThrowBomb = 0x1159;
        public const ushort NotifyProduceItem = 0x115A;
        public const ushort NotifyWrestleSkill = 0x115B;
        public const ushort RequestWrestle = 0x115C;
        public const ushort NotifyMachineMove = 0x115D;
        public const ushort RequestPushMapElement = 0x115E;
        public const ushort NotifyPushMapElement = 0x115F;
        public const ushort NotifyMapElementKnock = 0x1160;
        public const ushort NotifyMapElementStop = 0x1161;
        public const ushort NotifyPlayerKnocked = 0x1162;
        public const ushort RequestDestroyBox = 0x1163;
        public const ushort PlayerKnocked = 0x1164;
        public const ushort BoxStopped = 0x1165;
        public const ushort NotifyPlayerTrapped = 0x1166;
        public const ushort NotifyFire = 0x1167;
        public const ushort NotifyGenerateBox = 0x1168;
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

    // --- Additional Native Rule & Battle Event Codecs (Phase 4) ---

    public sealed class AvatarRecoveryEventData
    {
        public ushort PlayerId;
        public uint Time;
        public ushort PosX;
        public ushort PosY;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(10);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            return b.ToArray();
        }

        public static AvatarRecoveryEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new AvatarRecoveryEventData
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16()
            };
        }
    }

    public sealed class DispatchBombEventData
    {
        public ushort PlayerId;
        public uint Time;
        public List<(uint sceneId, byte prop, byte row, byte col)> Bombs = new List<(uint, byte, byte, byte)>();
        public List<(uint itemId, byte row, byte col)> Items = new List<(uint, byte, byte)>();

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(8 + Bombs.Count * 7 + Items.Count * 6);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            b.WriteByte((byte)Bombs.Count);
            foreach (var bm in Bombs)
            {
                b.WriteUInt32(bm.sceneId);
                b.WriteByte(bm.prop);
                b.WriteByte(bm.row);
                b.WriteByte(bm.col);
            }
            b.WriteByte((byte)Items.Count);
            foreach (var it in Items)
            {
                b.WriteUInt32(it.itemId);
                b.WriteByte(it.row);
                b.WriteByte(it.col);
            }
            return b.ToArray();
        }

        public static DispatchBombEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new DispatchBombEventData
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32()
            };
            byte bCount = r.ReadByte();
            for (int i = 0; i < bCount && r.Remaining >= 7; i++)
                d.Bombs.Add((r.ReadUInt32(), r.ReadByte(), r.ReadByte(), r.ReadByte()));
            if (r.Remaining >= 1)
            {
                byte iCount = r.ReadByte();
                for (int i = 0; i < iCount && r.Remaining >= 6; i++)
                    d.Items.Add((r.ReadUInt32(), r.ReadByte(), r.ReadByte()));
            }
            return d;
        }
    }

    public sealed class ItemsExplodedEventData
    {
        public ushort PlayerId;
        public uint Time;
        public List<(uint itemId, byte row, byte col)> Items = new List<(uint, byte, byte)>();

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(7 + Items.Count * 6);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            b.WriteByte((byte)Items.Count);
            foreach (var it in Items)
            {
                b.WriteUInt32(it.itemId);
                b.WriteByte(it.row);
                b.WriteByte(it.col);
            }
            return b.ToArray();
        }

        public static ItemsExplodedEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new ItemsExplodedEventData { PlayerId = r.ReadUInt16(), Time = r.ReadUInt32() };
            byte count = r.ReadByte();
            for (int i = 0; i < count && r.Remaining >= 6; i++)
                d.Items.Add((r.ReadUInt32(), r.ReadByte(), r.ReadByte()));
            return d;
        }
    }

    public sealed class EatBombActionData
    {
        public ushort PlayerId;
        public ushort Time;
        public uint PlayerAvatar;
        public ushort PosX;
        public ushort PosY;
        public ushort BombPlayerId;
        public uint BombTime;
        public byte BombRow;
        public byte BombCol;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(20);
            b.WriteUInt16(PlayerId);
            b.WriteUInt16(Time);
            b.WriteUInt32(PlayerAvatar);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            b.WriteUInt16(BombPlayerId);
            b.WriteUInt32(BombTime);
            b.WriteByte(BombRow);
            b.WriteByte(BombCol);
            return b.ToArray();
        }

        public static EatBombActionData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new EatBombActionData
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt16(),
                PlayerAvatar = r.ReadUInt32(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16(),
                BombPlayerId = r.ReadUInt16(),
                BombTime = r.ReadUInt32(),
                BombRow = r.ReadByte(),
                BombCol = r.ReadByte()
            };
        }
    }

    [Flags]
    public enum PassiveFunctionEffectMask : byte
    {
        None = 0,
        Lightning = 1 << 0,
        SwordArc = 1 << 1,
        KickImpact = 1 << 2
    }

    public sealed class PassiveFunctionProjectionData
    {
        public byte Rule;
        public ushort PlayerId;
        public PassiveFunctionEffectMask Effects;
        public bool Reset;

        public static bool IsProjectionBody(byte[] body) =>
            body != null && body.Length >= 10 && body[0] == 0xFE && body[1] == (byte)'Q' && body[2] == (byte)'F';

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(12);
            b.WriteByte(0xFE);
            b.WriteByte((byte)'Q');
            b.WriteByte((byte)'F');
            b.WriteByte((byte)(Rule | (Reset ? 0x80 : 0)));
            b.WriteUInt16(PlayerId);
            b.WriteUInt32((uint)Effects);
            b.WriteByte(0);
            b.WriteByte(0);
            return b.ToArray();
        }

        public static PassiveFunctionProjectionData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            r.Skip(3); // FE Q F
            byte rawRule = r.ReadByte();
            return new PassiveFunctionProjectionData
            {
                Rule = (byte)(rawRule & 0x7F),
                Reset = (rawRule & 0x80) != 0,
                PlayerId = r.ReadUInt16(),
                Effects = (PassiveFunctionEffectMask)(r.ReadUInt32() & 0xFF)
            };
        }
    }

    public sealed class KickBombActionData
    {
        public byte FromRow;
        public byte FromCol;
        public byte DestRow;
        public byte DestCol;
        public byte BombProp;
        public byte Direction;
        public byte Power;
        public uint Time;
        public ushort PlayerId;
        public ushort BombPower;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(13);
            b.WriteByte((byte)((FromRow << 4) | (FromCol & 0x0F)));
            b.WriteByte((byte)((DestRow << 4) | (DestCol & 0x0F)));
            b.WriteByte(BombProp);
            b.WriteByte(Direction);
            b.WriteByte(Power);
            b.WriteUInt32(Time);
            b.WriteUInt16(PlayerId);
            b.WriteUInt16(BombPower);
            return b.ToArray();
        }

        public static KickBombActionData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            byte src = r.ReadByte();
            byte dst = r.ReadByte();
            return new KickBombActionData
            {
                FromRow = (byte)(src >> 4),
                FromCol = (byte)(src & 0x0F),
                DestRow = (byte)(dst >> 4),
                DestCol = (byte)(dst & 0x0F),
                BombProp = r.ReadByte(),
                Direction = r.ReadByte(),
                Power = r.ReadByte(),
                Time = r.ReadUInt32(),
                PlayerId = r.ReadUInt16(),
                BombPower = r.ReadUInt16()
            };
        }
    }

    public sealed class ProduceItemEventData
    {
        public byte Row;
        public byte Col;
        public uint ItemId;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(5);
            b.WriteByte((byte)((Row << 4) | (Col & 0x0F)));
            b.WriteUInt32(ItemId);
            return b.ToArray();
        }

        public static ProduceItemEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            byte rc = r.ReadByte();
            return new ProduceItemEventData { Row = (byte)(rc >> 4), Col = (byte)(rc & 0x0F), ItemId = r.ReadUInt32() };
        }
    }

    public sealed class WrestleActionEventData
    {
        public byte SkillType;
        public byte FirstTeam;
        public byte PlayerPos;
        public ushort PlayerId;
        public uint Time;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(9);
            b.WriteByte(SkillType);
            b.WriteByte(FirstTeam);
            b.WriteByte(PlayerPos);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            return b.ToArray();
        }

        public static WrestleActionEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new WrestleActionEventData
            {
                SkillType = r.ReadByte(),
                FirstTeam = r.ReadByte(),
                PlayerPos = r.ReadByte(),
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32()
            };
        }
    }

    public sealed class NpcDropItemEventData
    {
        public ushort ObjectId;
        public ushort PosX;
        public ushort PosY;
        public List<(uint itemId, byte row, byte col)> Items = new List<(uint, byte, byte)>();

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(7 + Items.Count * 6);
            b.WriteUInt16(ObjectId);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            b.WriteByte((byte)Items.Count);
            foreach (var it in Items)
            {
                b.WriteUInt32(it.itemId);
                b.WriteByte(it.row);
                b.WriteByte(it.col);
            }
            return b.ToArray();
        }

        public static NpcDropItemEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new NpcDropItemEventData
            {
                ObjectId = r.ReadUInt16(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16()
            };
            byte count = r.ReadByte();
            for (int i = 0; i < count && r.Remaining >= 6; i++)
                d.Items.Add((r.ReadUInt32(), r.ReadByte(), r.ReadByte()));
            return d;
        }
    }

    public sealed class TankBaseHpEventData
    {
        public ushort PlayerId;
        public uint Time;
        public ushort TeamId;
        public short ChangeHp;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(10);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            b.WriteUInt16(TeamId);
            b.WriteUInt16((ushort)ChangeHp);
            return b.ToArray();
        }

        public static TankBaseHpEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new TankBaseHpEventData
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32(),
                TeamId = r.ReadUInt16(),
                ChangeHp = (short)r.ReadUInt16()
            };
        }
    }

    public sealed class PlayerTimedStateEventData
    {
        public ushort PlayerId;
        public uint Time;
        public byte Value;

        public static PlayerTimedStateEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PlayerTimedStateEventData
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32(),
                Value = r.Remaining >= 1 ? r.ReadByte() : (byte)0
            };
        }
    }

    public sealed class ChangeArbitratorEventData
    {
        public ushort NewArbitratorPlayerId;

        public static ChangeArbitratorEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new ChangeArbitratorEventData { NewArbitratorPlayerId = r.Remaining >= 2 ? r.ReadUInt16() : (ushort)0 };
        }
    }

    public sealed class PlayerLeaveBattleEventData
    {
        public ushort PlayerId;

        public static PlayerLeaveBattleEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PlayerLeaveBattleEventData { PlayerId = r.Remaining >= 2 ? r.ReadUInt16() : (ushort)0 };
        }
    }

    // --- Machine World (0x10EF..0x10F3, 0x115D) ---

    public sealed class MachineLaunchEventData
    {
        public byte FirstTeam;
        public byte RoleId;
        public ushort PlayerId;
        public uint Time;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(8);
            b.WriteByte(FirstTeam);
            b.WriteByte(RoleId);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            return b.ToArray();
        }

        public static MachineLaunchEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new MachineLaunchEventData
            {
                FirstTeam = r.ReadByte(),
                RoleId = r.ReadByte(),
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32()
            };
        }
    }

    public sealed class MachineBombStateEventData
    {
        public ushort PlayerId;
        public uint Time;
        public List<(uint sceneId, byte prop, byte row, byte col)> Bombs = new List<(uint, byte, byte, byte)>();

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(7 + Bombs.Count * 7);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            b.WriteByte((byte)Bombs.Count);
            foreach (var bm in Bombs)
            {
                b.WriteUInt32(bm.sceneId);
                b.WriteByte(bm.prop);
                b.WriteByte(bm.row);
                b.WriteByte(bm.col);
            }
            return b.ToArray();
        }

        public static MachineBombStateEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new MachineBombStateEventData
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32()
            };
            byte count = r.ReadByte();
            for (int i = 0; i < count && r.Remaining >= 7; i++)
                d.Bombs.Add((r.ReadUInt32(), r.ReadByte(), r.ReadByte(), r.ReadByte()));
            return d;
        }
    }

    // --- Box-Push Rule 8 (0x115E..0x1163, 0x1167..0x1168, 0x0FBE) ---

    public sealed class PushMapElementRequestData
    {
        public ushort PlayerId;
        public byte Direction;
        public byte Power;
        public uint Time;
        public byte[] Element = new byte[8];

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(16);
            b.WriteUInt16(PlayerId);
            b.WriteByte(Direction);
            b.WriteByte(Power);
            b.WriteUInt32(Time);
            b.WriteFixedSlot(Element, 8);
            return b.ToArray();
        }

        public static PushMapElementRequestData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PushMapElementRequestData
            {
                PlayerId = r.ReadUInt16(),
                Direction = r.ReadByte(),
                Power = r.ReadByte(),
                Time = r.ReadUInt32(),
                Element = r.ReadBytes(8)
            };
        }
    }

    public sealed class PushMapElementNotifyData
    {
        public ushort PlayerId;
        public byte Direction;
        public byte Power;
        public byte DestinationRow;
        public byte DestinationCol;
        public uint CurrentX;
        public uint CurrentY;
        public uint Time;
        public byte[] Element = new byte[8];

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(26);
            b.WriteUInt16(PlayerId);
            b.WriteByte(Direction);
            b.WriteByte(Power);
            b.WriteByte(DestinationRow);
            b.WriteByte(DestinationCol);
            b.WriteUInt32(CurrentX);
            b.WriteUInt32(CurrentY);
            b.WriteUInt32(Time);
            b.WriteFixedSlot(Element, 8);
            return b.ToArray();
        }

        public static PushMapElementNotifyData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PushMapElementNotifyData
            {
                PlayerId = r.ReadUInt16(),
                Direction = r.ReadByte(),
                Power = r.ReadByte(),
                DestinationRow = r.ReadByte(),
                DestinationCol = r.ReadByte(),
                CurrentX = r.ReadUInt32(),
                CurrentY = r.ReadUInt32(),
                Time = r.ReadUInt32(),
                Element = r.ReadBytes(8)
            };
        }
    }

    public sealed class DestroyBoxRequestData
    {
        public ushort PlayerId;
        public uint Time;
        public ushort PosX;
        public ushort PosY;
        public byte[] Element = new byte[8];

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(18);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            b.WriteFixedSlot(Element, 8);
            return b.ToArray();
        }

        public static DestroyBoxRequestData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new DestroyBoxRequestData
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16(),
                Element = r.ReadBytes(8)
            };
        }
    }

    public sealed class BoxWireData
    {
        public byte Row;
        public byte Col;
        public byte StopRow;
        public byte StopCol;
        public uint MapElementId;
        public uint UniqueId;

        public void WriteTo(BigEndianBuffer b)
        {
            b.WriteByte(Row);
            b.WriteByte(Col);
            b.WriteByte(StopRow);
            b.WriteByte(StopCol);
            b.WriteUInt32(MapElementId);
            b.WriteUInt32(UniqueId);
        }

        public static BoxWireData ReadFrom(ref BigEndianReader r) => new BoxWireData
        {
            Row = r.ReadByte(),
            Col = r.ReadByte(),
            StopRow = r.ReadByte(),
            StopCol = r.ReadByte(),
            MapElementId = r.ReadUInt32(),
            UniqueId = r.ReadUInt32()
        };
    }

    public sealed class MapElementStopEventData
    {
        public uint Time;
        public List<BoxWireData> Boxes = new List<BoxWireData>();

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(5 + Boxes.Count * 12);
            b.WriteByte((byte)Boxes.Count);
            b.WriteUInt32(Time);
            foreach (var box in Boxes) box.WriteTo(b);
            return b.ToArray();
        }

        public static MapElementStopEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            byte count = r.ReadByte();
            var d = new MapElementStopEventData { Time = r.ReadUInt32() };
            for (int i = 0; i < count && r.Remaining >= 12; i++)
                d.Boxes.Add(BoxWireData.ReadFrom(ref r));
            return d;
        }
    }

    public sealed class GenerateBoxesEventData
    {
        public List<byte[]> Elements = new List<byte[]>();

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(1 + Elements.Count * 8);
            b.WriteByte((byte)Elements.Count);
            foreach (var el in Elements) b.WriteFixedSlot(el, 8);
            return b.ToArray();
        }

        public static GenerateBoxesEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            byte count = r.ReadByte();
            var d = new GenerateBoxesEventData();
            for (int i = 0; i < count && r.Remaining >= 8; i++)
                d.Elements.Add(r.ReadBytes(8));
            return d;
        }
    }

    public sealed class PlayerKnockedEventData
    {
        public ushort PlayerId;
        public uint Time;
        public ushort PosX;
        public ushort PosY;
        public bool IsAvatar;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(11);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            b.WriteUInt16(PosX);
            b.WriteUInt16(PosY);
            b.WriteByte((byte)(IsAvatar ? 1 : 0));
            return b.ToArray();
        }

        public static PlayerKnockedEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PlayerKnockedEventData
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32(),
                PosX = r.ReadUInt16(),
                PosY = r.ReadUInt16(),
                IsAvatar = r.ReadByte() != 0
            };
        }
    }

    public sealed class MapElementsExplodedEventData
    {
        public ushort PlayerId;
        public uint Time;
        public List<byte[]> Elements = new List<byte[]>();

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(7 + Elements.Count * 8);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(Time);
            b.WriteByte((byte)Elements.Count);
            foreach (var el in Elements) b.WriteFixedSlot(el, 8);
            return b.ToArray();
        }

        public static MapElementsExplodedEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new MapElementsExplodedEventData { PlayerId = r.ReadUInt16(), Time = r.ReadUInt32() };
            byte count = r.ReadByte();
            for (int i = 0; i < count && r.Remaining >= 8; i++)
                d.Elements.Add(r.ReadBytes(8));
            return d;
        }
    }

    // --- Prepared Prop Cancel & Trigger (0x10F5..0x10F7) ---

    public sealed class CancelUsePropEventData
    {
        public ushort PlayerId;
        public uint PropId;
        public uint Time;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(10);
            b.WriteUInt16(PlayerId);
            b.WriteUInt32(PropId);
            b.WriteUInt32(Time);
            return b.ToArray();
        }

        public static CancelUsePropEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new CancelUsePropEventData { PlayerId = r.ReadUInt16(), PropId = r.ReadUInt32(), Time = r.ReadUInt32() };
        }
    }

    public sealed class PropTriggerEventData
    {
        public uint PropGuid;
        public uint GameTime;
        public ushort UserId;
        public uint PosX;
        public uint PosY;
        public byte Dir;
        public List<(ushort playerId, uint posX, uint posY, byte dir)> AffectedPlayers = new List<(ushort, uint, uint, byte)>();
        public uint TypeId;

        public byte[] Marshal()
        {
            var b = new BigEndianBuffer(24 + AffectedPlayers.Count * 11);
            b.WriteUInt32(PropGuid);
            b.WriteUInt32(GameTime);
            b.WriteUInt16(UserId);
            b.WriteUInt32(PosX);
            b.WriteUInt32(PosY);
            b.WriteByte(Dir);
            b.WriteByte((byte)AffectedPlayers.Count);
            foreach (var a in AffectedPlayers)
            {
                b.WriteUInt16(a.playerId);
                b.WriteUInt32(a.posX);
                b.WriteUInt32(a.posY);
                b.WriteByte(a.dir);
            }
            b.WriteUInt32(TypeId);
            return b.ToArray();
        }

        public static PropTriggerEventData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var d = new PropTriggerEventData
            {
                PropGuid = r.ReadUInt32(),
                GameTime = r.ReadUInt32(),
                UserId = r.ReadUInt16(),
                PosX = r.ReadUInt32(),
                PosY = r.ReadUInt32(),
                Dir = r.ReadByte()
            };
            byte count = r.ReadByte();
            for (int i = 0; i < count && r.Remaining >= 11; i++)
                d.AffectedPlayers.Add((r.ReadUInt16(), r.ReadUInt32(), r.ReadUInt32(), r.ReadByte()));
            if (r.Remaining >= 4) d.TypeId = r.ReadUInt32();
            return d;
        }
    }
}
