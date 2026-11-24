using System;
using System.Collections.Generic;

namespace Qqtang.Core.Protocol
{
    public static class GameCommands
    {
        public const ushort Login = 0x0064;
        public const ushort Logout = 0x0065;
        public const ushort RoomList = 0x0066;
        public const ushort EnterRoom = 0x0068;
        public const ushort LeaveRoom = 0x0069;
        public const ushort PlayerList = 0x006A;
        public const ushort CreateRoom = 0x006B;
        public const ushort RoomChat = 0x006D;
        public const ushort ChangeTeam = 0x0070;
        public const ushort ChangeRole = 0x0071;
        public const ushort Ready = 0x0072;
        public const ushort CancelReady = 0x0073;
        public const ushort ModifyRoomInfo = 0x0078;
        public const ushort ModifyRoomInfoNotify = 0x0079;
        public const ushort ChangeTeamNotify = 0x007A;
        public const ushort ChangeRoleNotify = 0x007B;
        public const ushort ReadyStateNotify = 0x007C;
        public const ushort SetSeatStatus = 0x007F;
        public const ushort SetSeatStatusNotify = 0x0080;
        public const ushort StartGame = 0x0082;
        public const ushort GameEventRequest = 0x0083;
        public const ushort GameEventNotify = 0x0084;
        public const ushort ItemStatusChange = 0x0085;
        public const ushort RequestUdpOk = 0x0086;
        public const ushort NotifyUdpOk = 0x0087;
        public const ushort Hello = 0x0088;
        public const ushort GameBeginNotify = 0x0089;
        public const ushort JoinRoom = 0x008A;
        public const ushort RoomPush = 0x008C;
        public const ushort EnterRoomNotify = 0x008D;
        public const ushort LeaveRoomNotify = 0x008E;
        public const ushort RoomChatNotify = 0x0093;
        public const ushort ModifyRoom = 0x0098;
        public const ushort ModifyRoomNotify = 0x0099;
    }

    public static class GameEventSchemas
    {
        public const ushort NotifyGameBegin = 0x0FA1;
        public const ushort PlayerMove = 0x0FA2;
        public const ushort PlayerUseBomb = 0x0FA3;
        public const ushort NotifyBombExplode = 0x0FA4;
        public const ushort NotifyPlayerUseBomb = 0x138B;
        public const ushort PlayerBeExploded = 0x0FA5;
        public const ushort NotifyPlayerExploded = 0x0FA6;
        public const ushort NotifyPlayerDie = 0x0FA7;
        public const ushort RequestKillPlayer = 0x0FA8;
        public const ushort NotifyPlayerKilled = 0x0FA9;
        public const ushort RequestSavePlayer = 0x0FAA;
        public const ushort NotifyPlayerSaved = 0x0FAB;
        public const ushort RequestGetItem = 0x0FAC;
        public const ushort NotifyPlayerGetItem = 0x0FAD;
        public const ushort NotifyDispatchItem = 0x0FAE;
        public const ushort NotifyGameOver = 0x0FBB;
    }

    public static class SpecialItemIds
    {
        public const ushort SinglePlayerAdventureCard = 99;
        public const ushort SinglePlayerBossCard = 30098;
        public const ushort CompetitiveAiCard = 30099;
    }

    [Flags]
    public enum RoomListFlag : byte
    {
        InMatch = 0,
        Preparing = 1 << 0,
        Password = 1 << 1,
        FreeRule = 1 << 2,
        Vip = 1 << 3
    }

    [Flags]
    public enum RoomPropertyFlag : byte
    {
        Standard = 0,
        Password = 1 << 0,
        FreeRule = 1 << 1
    }

    public enum RoomGameType : byte
    {
        CompetitiveNoItem = 0,
        CompetitiveItem = 1,
        Adventure = 2,
        CompetitiveLoot = 3,
        Chat = 4
    }

    public enum EnterRoomSeatStatus : byte
    {
        Locked = 0,
        Open = 1,
        Occupied = 2
    }

    public enum GameResultCode : byte
    {
        Loss = 0,
        Draw = 1,
        Win = 2
    }

    public sealed class GameInfoData
    {
        public const int WireSize = 47;
        public uint WinNum;
        public uint LossNum;
        public uint EqualNum;
        public uint OrgId;
        public uint Point;
        public uint Money;
        public ushort Degree;
        public byte RoleId;
        public uint PetId;
        public uint ExtWinNum;
        public uint ExtLossNum;
        public uint ExtEqualNum;
        public uint ExtPoint;

        public static GameInfoData ReadFrom(ref BigEndianReader r) => new GameInfoData
        {
            WinNum = r.ReadUInt32(),
            LossNum = r.ReadUInt32(),
            EqualNum = r.ReadUInt32(),
            OrgId = r.ReadUInt32(),
            Point = r.ReadUInt32(),
            Money = r.ReadUInt32(),
            Degree = r.ReadUInt16(),
            RoleId = r.ReadByte(),
            PetId = r.ReadUInt32(),
            ExtWinNum = r.ReadUInt32(),
            ExtLossNum = r.ReadUInt32(),
            ExtEqualNum = r.ReadUInt32(),
            ExtPoint = r.ReadUInt32()
        };
    }

    public sealed class ItemInfoData
    {
        public const int WireSize = 18;
        public ushort ItemId;
        public uint Quantity;
        public byte Status;
        public byte RoleId;
        public byte Effect;
        public byte Color;
        public uint BuyTime;
        public uint AvailPeriod;

        public static ItemInfoData ReadFrom(ref BigEndianReader r) => new ItemInfoData
        {
            ItemId = r.ReadUInt16(),
            Quantity = r.ReadUInt32(),
            Status = r.ReadByte(),
            RoleId = r.ReadByte(),
            Effect = r.ReadByte(),
            Color = r.ReadByte(),
            BuyTime = r.ReadUInt32(),
            AvailPeriod = r.ReadUInt32()
        };
    }

    public sealed class LoginResponseData
    {
        public ushort ResultId;
        public ushort PlayerId;
        public uint Uin;
        public uint Identity;
        public ushort SectionId;
        public byte[] KeyGameData;
        public ushort NumOfRoom;
        public ushort MinRoomId;
        public GameInfoData GameInfo;
        public List<ItemInfoData> Items = new List<ItemInfoData>();
        public byte SectionMode;
        public uint ExtraSectionModeInfo;
        public uint SectionIdentity;
        public string Reason = string.Empty;
        public uint Honor;

        public static LoginResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var resp = new LoginResponseData
            {
                ResultId = r.ReadUInt16(),
                PlayerId = r.ReadUInt16(),
                Uin = r.ReadUInt32(),
                Identity = r.ReadUInt32(),
                SectionId = r.ReadUInt16()
            };
            byte keyLen = r.ReadByte();
            byte[] keySlot = r.ReadBytes(32);
            resp.KeyGameData = new byte[Math.Min((int)keyLen, 32)];
            Buffer.BlockCopy(keySlot, 0, resp.KeyGameData, 0, resp.KeyGameData.Length);
            resp.NumOfRoom = r.ReadUInt16();
            resp.MinRoomId = r.ReadUInt16();
            resp.GameInfo = GameInfoData.ReadFrom(ref r);
            ushort itemCount = r.ReadUInt16();
            for (int i = 0; i < itemCount; i++)
                resp.Items.Add(ItemInfoData.ReadFrom(ref r));
            resp.SectionMode = r.ReadByte();
            resp.ExtraSectionModeInfo = r.ReadUInt32();
            resp.SectionIdentity = r.ReadUInt32();
            byte reasonLen = r.ReadByte();
            resp.Reason = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(reasonLen));
            resp.Honor = r.ReadUInt32();
            // TaskCount, TaskGrade, TaskGameCount, TaskGameFinished + PatternPoints
            r.Skip(8);
            byte patternNum = r.ReadByte();
            r.Skip(patternNum * 11);
            return resp;
        }
    }

    public sealed class RoomListEntryData
    {
        public string Name = string.Empty;
        public ushort RoomId;
        public RoomListFlag RoomFlag;
        public ushort MapId;
        public byte CurrentPlayers;
        public byte Capacity;
        public RoomGameType GameType;
        public uint ContinueId;

        public static RoomListEntryData ReadFrom(ref BigEndianReader r)
        {
            byte nameLen = r.ReadByte();
            string name = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(nameLen));
            ushort roomId = r.ReadUInt16();
            RoomListFlag flag = (RoomListFlag)r.ReadByte();
            ushort mapId = r.ReadUInt16();
            byte pop = r.ReadByte();
            RoomGameType gameType = (RoomGameType)r.ReadByte();
            uint continueId = r.ReadUInt32();
            return new RoomListEntryData
            {
                Name = name,
                RoomId = roomId,
                RoomFlag = flag,
                MapId = mapId,
                CurrentPlayers = (byte)(pop >> 4),
                Capacity = (byte)(pop & 0x0F),
                GameType = gameType,
                ContinueId = continueId
            };
        }
    }

    public sealed class RoomListResponseData
    {
        public ushort ResultId;
        public byte Operation;
        public List<RoomListEntryData> Rooms = new List<RoomListEntryData>();
        public byte GameMode;
        public ushort StartRoomId;

        public static RoomListResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var resp = new RoomListResponseData
            {
                ResultId = r.ReadUInt16(),
                Operation = r.ReadByte()
            };
            ushort count = r.ReadUInt16();
            for (int i = 0; i < count; i++)
                resp.Rooms.Add(RoomListEntryData.ReadFrom(ref r));
            resp.GameMode = r.ReadByte();
            resp.StartRoomId = r.ReadUInt16();
            return resp;
        }

        public static List<RoomListEntryData> ParseRoomPush(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            ushort count = r.ReadUInt16();
            var list = new List<RoomListEntryData>(count);
            for (int i = 0; i < count; i++)
                list.Add(RoomListEntryData.ReadFrom(ref r));
            return list;
        }
    }

    public sealed class RoomMemberData
    {
        public uint Uin;
        public string Nickname = string.Empty;
        public ushort PlayerId;
        public byte TeamId;
        public byte SeatId;
        public byte Status; // 1 = ready
        public byte Gender;
        public byte IconId;
        public uint Identity;
        public GameInfoData GameInfo;
        public List<ItemInfoData> Items = new List<ItemInfoData>();

        public static RoomMemberData ReadFrom(ref BigEndianReader r)
        {
            var m = new RoomMemberData();
            m.Uin = r.ReadUInt32();
            m.Nickname = LegacyTextEncoding.DecodeNullTerminated(r.ReadBytes(20));
            m.PlayerId = r.ReadUInt16();
            byte teamSeat = r.ReadByte();
            m.TeamId = (byte)(teamSeat >> 4);
            m.SeatId = (byte)(teamSeat & 0x0F);
            m.Status = r.ReadByte();
            m.Gender = r.ReadByte();
            m.IconId = r.ReadByte();
            m.Identity = r.ReadUInt32();
            m.GameInfo = GameInfoData.ReadFrom(ref r);
            ushort itemCount = r.ReadUInt16();
            for (int i = 0; i < itemCount; i++)
                m.Items.Add(ItemInfoData.ReadFrom(ref r));
            r.ReadUInt32(); // KinIndex
            ushort kinNameLen = r.ReadUInt16();
            r.Skip(kinNameLen);
            r.Skip(8); // KinFlagID
            // PetBaseInfo: 38-byte header with SkillCount at [34..38] + SkillCount bytes
            byte[] petHead = r.ReadBytes(38);
            uint petSkillCount = QqTea.ReadUInt32BE(petHead, 34);
            if (petSkillCount > 0) r.Skip((int)petSkillCount);
            return m;
        }
    }

    public sealed class EnterRoomResponseData
    {
        public ushort ResultId;
        public ushort RoomId;
        public byte LocalTeamId;
        public byte LocalSeatId;
        public ushort MapId;
        public byte RoomFlag;
        public ushort RoomOwnerId;
        public EnterRoomSeatStatus[] SeatStatus = new EnterRoomSeatStatus[8];
        public List<RoomMemberData> Players = new List<RoomMemberData>();
        public RoomGameType GameType;

        public static EnterRoomResponseData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var resp = new EnterRoomResponseData
            {
                ResultId = r.ReadUInt16(),
                RoomId = r.ReadUInt16()
            };
            if (resp.ResultId != 0) return resp;

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
            // Attach (12 bytes * playerCount)
            r.Skip(playerCount * 12);
            resp.GameType = (RoomGameType)r.ReadByte();
            r.ReadUInt32(); // BackgroundID
            r.Skip(playerCount * 4); // SpouseUIN
            r.ReadUInt16(); // WeddingModeID
            for (int i = 0; i < playerCount; i++)
            {
                byte pCount = r.ReadByte();
                r.Skip(pCount * 11);
            }
            return resp;
        }

        public static (RoomMemberData member, ushort roomId) ParseEnterRoomNotify(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            var member = RoomMemberData.ReadFrom(ref r);
            ushort roomId = r.ReadUInt16();
            return (member, roomId);
        }
    }

    public sealed class LeaveRoomNotifyData
    {
        public ushort PlayerId;
        public ushort NewRoomOwnerId;
        public ushort NewArbitratorId;
        public ushort MapId;

        public static LeaveRoomNotifyData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return new LeaveRoomNotifyData
            {
                PlayerId = r.ReadUInt16(),
                NewRoomOwnerId = r.ReadUInt16(),
                NewArbitratorId = r.ReadUInt16(),
                MapId = r.ReadUInt16()
            };
        }
    }

    public sealed class GameItemTypeData
    {
        public uint ItemId;
        public short Quantity;
    }

    public sealed class PlayerGameInfoData
    {
        public ushort PlayerId;
        public byte RoleId;
        public byte TeamId;
        public uint DelayTime;
        public uint ExtPoint;
        public List<GameItemTypeData> NewItems = new List<GameItemTypeData>();
    }

    public sealed class GameBeginData
    {
        public uint GameId;
        public uint MapId;
        public uint SpawnSeed;
        public uint ItemSeed;
        public ushort ArbitratorPlayerId;
        public List<PlayerGameInfoData> Players = new List<PlayerGameInfoData>();
        public List<GameItemTypeData> Items = new List<GameItemTypeData>();
        public byte ReportFlag;
        public byte[] MapHash;
        public List<GameItemTypeData> NewItems = new List<GameItemTypeData>();
        public int ContinueId;
        public byte[] FileHash;
        public uint GameTimeMs;

        public static GameBeginData ParseLengthPrefixed(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            ushort declared = r.ReadUInt16();
            if (declared != r.Remaining)
                throw new InvalidOperationException($"GAME_BEGIN_DATA declared {declared} != remaining {r.Remaining}.");
            return ParseCore(ref r);
        }

        public static GameBeginData Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            return ParseCore(ref r);
        }

        private static GameBeginData ParseCore(ref BigEndianReader r)
        {
            var data = new GameBeginData
            {
                GameId = r.ReadUInt32(),
                MapId = r.ReadUInt32(),
                SpawnSeed = r.ReadUInt32(),
                ItemSeed = r.ReadUInt32(),
                ArbitratorPlayerId = r.ReadUInt16()
            };
            byte playerCount = r.ReadByte();
            for (int i = 0; i < playerCount; i++)
            {
                var p = new PlayerGameInfoData
                {
                    PlayerId = r.ReadUInt16(),
                    RoleId = r.ReadByte(),
                    TeamId = r.ReadByte(),
                    DelayTime = r.ReadUInt32(),
                    ExtPoint = r.ReadUInt32()
                };
                byte pItemCount = r.ReadByte();
                for (int j = 0; j < pItemCount; j++)
                    p.NewItems.Add(new GameItemTypeData { ItemId = r.ReadUInt32(), Quantity = (short)r.ReadUInt16() });
                data.Players.Add(p);
            }
            byte itemCount = r.ReadByte();
            for (int i = 0; i < itemCount; i++)
                data.Items.Add(new GameItemTypeData { ItemId = r.ReadUInt32(), Quantity = (short)r.ReadUInt16() });
            data.ReportFlag = r.ReadByte();
            data.MapHash = r.ReadBytes(32);
            byte newItemCount = r.ReadByte();
            for (int i = 0; i < newItemCount; i++)
                data.NewItems.Add(new GameItemTypeData { ItemId = r.ReadUInt32(), Quantity = (short)r.ReadUInt16() });
            data.ContinueId = (int)r.ReadUInt32();
            data.FileHash = r.ReadBytes(16);
            data.GameTimeMs = r.ReadUInt32();
            return data;
        }
    }

    public sealed class BattleMessageData
    {
        public uint Time;
        public uint DataId;
        public uint Sequence;
        public byte[] Data = Array.Empty<byte>();
        public uint GameTime;
        public uint Flag;
    }

    public sealed class GameplayDataPackage
    {
        public ushort PlayerId;
        public uint Time;
        public uint GameId;
        public List<uint> MessageIndexes = new List<uint>();
        public List<BattleMessageData> Messages = new List<BattleMessageData>();

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(64);
            w.WriteUInt16(PlayerId);
            w.WriteUInt32(Time);
            w.WriteUInt32(GameId);
            w.WriteByte((byte)Messages.Count);
            for (int i = 0; i < Messages.Count; i++)
                w.WriteUInt32(i < MessageIndexes.Count ? MessageIndexes[i] : Messages[i].Sequence);
            foreach (var m in Messages)
            {
                w.WriteUInt32(m.Time);
                w.WriteUInt32(m.DataId);
                w.WriteUInt16((ushort)m.Data.Length);
                w.WriteUInt32(m.Sequence);
                w.WriteBytes(m.Data);
                w.WriteUInt32(m.GameTime);
                w.WriteUInt32(m.Flag);
            }
            return w.ToArray();
        }

        public static GameplayDataPackage Parse(byte[] bytes)
        {
            var r = new BigEndianReader(bytes);
            var pkg = new GameplayDataPackage
            {
                PlayerId = r.ReadUInt16(),
                Time = r.ReadUInt32(),
                GameId = r.ReadUInt32()
            };
            byte count = r.ReadByte();
            for (int i = 0; i < count; i++)
                pkg.MessageIndexes.Add(r.ReadUInt32());
            for (int i = 0; i < count; i++)
            {
                uint time = r.ReadUInt32();
                uint dataId = r.ReadUInt32();
                ushort dataLen = r.ReadUInt16();
                uint seq = r.ReadUInt32();
                byte[] data = r.ReadBytes(dataLen);
                uint gameTime = r.ReadUInt32();
                uint flag = r.ReadUInt32();
                pkg.Messages.Add(new BattleMessageData
                {
                    Time = time, DataId = dataId, Sequence = seq,
                    Data = data, GameTime = gameTime, Flag = flag
                });
            }
            return pkg;
        }
    }

    public sealed class GameplayBatchEntry
    {
        public ushort Index;
        public GameplayDataPackage Package;
    }

    public sealed class GameplayBatch
    {
        public uint GameId;
        public ushort FirstIndex;
        public List<GameplayBatchEntry> Entries = new List<GameplayBatchEntry>();

        public byte[] EncodeEncrypted()
        {
            var w = new BigEndianBuffer(128);
            w.WriteByte((byte)Entries.Count);
            w.WriteUInt32LE(GameId);
            w.WriteUInt16(FirstIndex);
            foreach (var entry in Entries)
            {
                byte[] compact = entry.Package.Marshal();
                w.WriteUInt16(entry.Index);
                w.WriteUInt16((ushort)compact.Length);
                w.WriteBytes(compact);
            }
            return QqTea.Encrypt(w.ToArray(), QqTea.QqtPppGameplayKey);
        }

        public static GameplayBatch DecodeEncrypted(byte[] ciphertext)
        {
            byte[] plain = QqTea.Decrypt(ciphertext, QqTea.QqtPppGameplayKey);
            var r = new BigEndianReader(plain);
            byte count = r.ReadByte();
            var batch = new GameplayBatch
            {
                GameId = r.ReadUInt32LE(),
                FirstIndex = r.ReadUInt16()
            };
            for (int i = 0; i < count; i++)
            {
                ushort idx = r.ReadUInt16();
                ushort len = r.ReadUInt16();
                byte[] compact = r.ReadBytes(len);
                batch.Entries.Add(new GameplayBatchEntry
                {
                    Index = idx,
                    Package = GameplayDataPackage.Parse(compact)
                });
            }
            return batch;
        }
    }

    public struct PlayerMoveSequenceData
    {
        public ushort Sequence;
        public uint TimeStamp;
        public ushort CurrentPosX;
        public ushort CurrentPosY;
        public ushort CornerPosX;
        public ushort CornerPosY;
        public ushort EndPosX;
        public ushort EndPosY;
        public byte WalkAndDirection; // low 2 bits: 0=R,1=U,2=L,3=D; 0x10=moving; 0x20=forced
        public byte Speed;
    }

    public struct PlayerMoveEntryData
    {
        public ushort PlayerId;
        public PlayerMoveSequenceData Move;
    }

    public sealed class PlayerMoveMessage
    {
        public ushort PlayerId;
        public uint SeqReply;
        public List<PlayerMoveEntryData> Entries = new List<PlayerMoveEntryData>();

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(7 + Entries.Count * 22);
            w.WriteUInt16(PlayerId);
            w.WriteByte((byte)Entries.Count);
            w.WriteUInt32(SeqReply);
            foreach (var e in Entries) w.WriteUInt16(e.PlayerId);
            foreach (var e in Entries)
            {
                w.WriteUInt16(e.Move.Sequence);
                w.WriteUInt32(e.Move.TimeStamp);
                w.WriteUInt16(e.Move.CurrentPosX);
                w.WriteUInt16(e.Move.CurrentPosY);
                w.WriteUInt16(e.Move.CornerPosX);
                w.WriteUInt16(e.Move.CornerPosY);
                w.WriteUInt16(e.Move.EndPosX);
                w.WriteUInt16(e.Move.EndPosY);
                w.WriteByte(e.Move.WalkAndDirection);
                w.WriteByte(e.Move.Speed);
            }
            return w.ToArray();
        }

        public static PlayerMoveMessage Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var msg = new PlayerMoveMessage { PlayerId = r.ReadUInt16() };
            byte count = r.ReadByte();
            msg.SeqReply = r.ReadUInt32();
            ushort[] ids = new ushort[count];
            for (int i = 0; i < count; i++) ids[i] = r.ReadUInt16();
            for (int i = 0; i < count; i++)
            {
                msg.Entries.Add(new PlayerMoveEntryData
                {
                    PlayerId = ids[i],
                    Move = new PlayerMoveSequenceData
                    {
                        Sequence = r.ReadUInt16(),
                        TimeStamp = r.ReadUInt32(),
                        CurrentPosX = r.ReadUInt16(),
                        CurrentPosY = r.ReadUInt16(),
                        CornerPosX = r.ReadUInt16(),
                        CornerPosY = r.ReadUInt16(),
                        EndPosX = r.ReadUInt16(),
                        EndPosY = r.ReadUInt16(),
                        WalkAndDirection = r.ReadByte(),
                        Speed = r.ReadByte()
                    }
                });
            }
            return msg;
        }
    }

    public struct PlayerUseBombMessage
    {
        public ushort PlayerId;
        public uint ClientTime;
        public uint BombId;
        public byte Row;
        public byte Column;
        public ushort Power;
        public byte Property;

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(15);
            w.WriteUInt16(PlayerId);
            w.WriteUInt32(ClientTime);
            w.WriteUInt32(BombId);
            w.WriteByte(Row);
            w.WriteByte(Column);
            w.WriteUInt16(Power);
            w.WriteByte(Property);
            return w.ToArray();
        }

        public static PlayerUseBombMessage Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PlayerUseBombMessage
            {
                PlayerId = r.ReadUInt16(),
                ClientTime = r.ReadUInt32(),
                BombId = r.ReadUInt32(),
                Row = r.ReadByte(),
                Column = r.ReadByte(),
                Power = r.ReadUInt16(),
                Property = r.ReadByte()
            };
        }
    }

    public struct ExplodedBombEntry
    {
        public ushort PlayerId;
        public uint ClientTime;
        public byte BombId;
        public byte Row, Column, RowMin, RowMax, ColumnMin, ColumnMax;
    }

    public struct ExplodedMapElementEntry
    {
        public ushort MapElementId;
        public byte Row, Column;
    }

    public struct GameItemCell
    {
        public uint ItemId;
        public byte Row, Column;
    }

    public sealed class BombExplodeMessage
    {
        public ushort PlayerId;
        public uint ClientTime;
        public List<ExplodedBombEntry> Bombs = new List<ExplodedBombEntry>();
        public List<ExplodedMapElementEntry> MapElems = new List<ExplodedMapElementEntry>();
        public List<GameItemCell> Items = new List<GameItemCell>();

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(32);
            w.WriteUInt16(PlayerId);
            w.WriteUInt32(ClientTime);
            w.WriteByte((byte)Bombs.Count);
            foreach (var b in Bombs)
            {
                w.WriteUInt16(b.PlayerId);
                w.WriteUInt32(b.ClientTime);
                w.WriteByte(b.BombId);
                w.WriteByte(b.Row);
                w.WriteByte(b.Column);
                w.WriteByte(b.RowMin);
                w.WriteByte(b.RowMax);
                w.WriteByte(b.ColumnMin);
                w.WriteByte(b.ColumnMax);
            }
            w.WriteByte((byte)MapElems.Count);
            foreach (var e in MapElems)
            {
                w.WriteUInt16(e.MapElementId);
                w.WriteByte(e.Row);
                w.WriteByte(e.Column);
            }
            w.WriteByte((byte)Items.Count);
            foreach (var it in Items)
            {
                w.WriteUInt32(it.ItemId);
                w.WriteByte(it.Row);
                w.WriteByte(it.Column);
            }
            return w.ToArray();
        }

        public static BombExplodeMessage Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var msg = new BombExplodeMessage { PlayerId = r.ReadUInt16(), ClientTime = r.ReadUInt32() };
            byte bc = r.ReadByte();
            for (int i = 0; i < bc; i++)
            {
                msg.Bombs.Add(new ExplodedBombEntry
                {
                    PlayerId = r.ReadUInt16(), ClientTime = r.ReadUInt32(),
                    BombId = r.ReadByte(), Row = r.ReadByte(), Column = r.ReadByte(),
                    RowMin = r.ReadByte(), RowMax = r.ReadByte(),
                    ColumnMin = r.ReadByte(), ColumnMax = r.ReadByte()
                });
            }
            byte mc = r.ReadByte();
            for (int i = 0; i < mc; i++)
                msg.MapElems.Add(new ExplodedMapElementEntry { MapElementId = r.ReadUInt16(), Row = r.ReadByte(), Column = r.ReadByte() });
            byte ic = r.ReadByte();
            for (int i = 0; i < ic; i++)
                msg.Items.Add(new GameItemCell { ItemId = r.ReadUInt32(), Row = r.ReadByte(), Column = r.ReadByte() });
            return msg;
        }
    }

    public struct PlayerExplodedMessage
    {
        public ushort PlayerId;
        public uint ClientTime;
        public ushort PosX;
        public ushort PosY;
        public bool IsAvatar;

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(11);
            w.WriteUInt16(PlayerId);
            w.WriteUInt32(ClientTime);
            w.WriteUInt16(PosX);
            w.WriteUInt16(PosY);
            w.WriteByte((byte)(IsAvatar ? 1 : 0));
            return w.ToArray();
        }

        public static PlayerExplodedMessage Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PlayerExplodedMessage
            {
                PlayerId = r.ReadUInt16(), ClientTime = r.ReadUInt32(),
                PosX = r.ReadUInt16(), PosY = r.ReadUInt16(), IsAvatar = r.ReadByte() != 0
            };
        }
    }

    public struct PlayerInteractionMessage
    {
        public ushort PlayerId;
        public uint ClientTime;
        public ushort DestinationPlayerId;
        public ushort PosX;
        public ushort PosY;

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(12);
            w.WriteUInt16(PlayerId);
            w.WriteUInt32(ClientTime);
            w.WriteUInt16(DestinationPlayerId);
            w.WriteUInt16(PosX);
            w.WriteUInt16(PosY);
            return w.ToArray();
        }

        public static PlayerInteractionMessage Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PlayerInteractionMessage
            {
                PlayerId = r.ReadUInt16(), ClientTime = r.ReadUInt32(),
                DestinationPlayerId = r.ReadUInt16(), PosX = r.ReadUInt16(), PosY = r.ReadUInt16()
            };
        }
    }

    public sealed class PlayerDeathMessage
    {
        public ushort PlayerId;
        public uint ClientTime;
        public ushort PosX;
        public ushort PosY;
        public List<GameItemCell> Items = new List<GameItemCell>();

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(11 + Items.Count * 6);
            w.WriteUInt16(PlayerId);
            w.WriteUInt32(ClientTime);
            w.WriteUInt16(PosX);
            w.WriteUInt16(PosY);
            w.WriteByte((byte)Items.Count);
            foreach (var it in Items)
            {
                w.WriteUInt32(it.ItemId);
                w.WriteByte(it.Row);
                w.WriteByte(it.Column);
            }
            return w.ToArray();
        }

        public static PlayerDeathMessage Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var msg = new PlayerDeathMessage
            {
                PlayerId = r.ReadUInt16(), ClientTime = r.ReadUInt32(),
                PosX = r.ReadUInt16(), PosY = r.ReadUInt16()
            };
            byte count = r.ReadByte();
            for (int i = 0; i < count; i++)
                msg.Items.Add(new GameItemCell { ItemId = r.ReadUInt32(), Row = r.ReadByte(), Column = r.ReadByte() });
            return msg;
        }
    }

    public struct PlayerItemEventMessage
    {
        public ushort PlayerId;
        public uint ClientTime;
        public uint ItemId;
        public ushort PosX;
        public ushort PosY;

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(14);
            w.WriteUInt16(PlayerId);
            w.WriteUInt32(ClientTime);
            w.WriteUInt32(ItemId);
            w.WriteUInt16(PosX);
            w.WriteUInt16(PosY);
            return w.ToArray();
        }

        public static PlayerItemEventMessage Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            return new PlayerItemEventMessage
            {
                PlayerId = r.ReadUInt16(), ClientTime = r.ReadUInt32(),
                ItemId = r.ReadUInt32(), PosX = r.ReadUInt16(), PosY = r.ReadUInt16()
            };
        }
    }

    public sealed class GameResultEntry
    {
        public ushort PlayerId;
        public GameResultCode Result;
        public uint Remark;
        public uint Point;
        public List<(uint count, uint score)> Fields = new List<(uint, uint)>();
    }

    public sealed class GameOverData
    {
        public uint Time;
        public List<GameResultEntry> Results = new List<GameResultEntry>();
        public byte GameMode;

        public byte[] Marshal()
        {
            var w = new BigEndianBuffer(64);
            w.WriteUInt32(Time);
            w.WriteByte((byte)Results.Count);
            foreach (var res in Results)
            {
                w.WriteUInt16(res.PlayerId);
                w.WriteByte((byte)res.Result);
                w.WriteUInt32(res.Remark);
                w.WriteUInt32(res.Point);
                w.WriteByte((byte)res.Fields.Count);
                foreach (var f in res.Fields) w.WriteUInt32(f.count);
                foreach (var f in res.Fields) w.WriteUInt32(f.score);
            }
            w.WriteByte(GameMode);
            return w.ToArray();
        }

        public static GameOverData Parse(byte[] body)
        {
            var r = new BigEndianReader(body);
            var data = new GameOverData { Time = r.ReadUInt32() };
            byte count = r.ReadByte();
            for (int i = 0; i < count; i++)
            {
                var entry = new GameResultEntry
                {
                    PlayerId = r.ReadUInt16(),
                    Result = (GameResultCode)r.ReadByte(),
                    Remark = r.ReadUInt32(),
                    Point = r.ReadUInt32()
                };
                byte fc = r.ReadByte();
                uint[] counts = new uint[fc];
                for (int j = 0; j < fc; j++) counts[j] = r.ReadUInt32();
                for (int j = 0; j < fc; j++) entry.Fields.Add((counts[j], r.ReadUInt32()));
                data.Results.Add(entry);
            }
            data.GameMode = r.ReadByte();
            return data;
        }
    }

    public sealed class NotifyGameEventMessage
    {
        public ushort RoomId;
        public uint GameDataSequence;
        public ushort Schema;
        public byte[] Body = Array.Empty<byte>();

        public static NotifyGameEventMessage Parse(byte[] payload)
        {
            var r = new BigEndianReader(payload);
            ushort roomId = r.ReadUInt16();
            uint seq = r.ReadUInt32();
            uint gameDataLen = r.ReadUInt32();
            ushort schema = (ushort)r.ReadUInt32LE();
            byte[] body = r.ReadBytes((int)gameDataLen - 4);
            return new NotifyGameEventMessage { RoomId = roomId, GameDataSequence = seq, Schema = schema, Body = body };
        }
    }

    public static class GameRequestBuilder
    {
        public static byte[] BuildLoginPayload(uint uin, string nickname, byte gender, ushort sectionId, byte roleId, uint clientTime = 1)
        {
            var w = new BigEndianBuffer(53);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteFixedSlot(LegacyTextEncoding.EncodeSlot(nickname, 20), 20);
            w.WriteByte(gender);
            w.WriteByte(0);           // IconID
            w.WriteUInt16(0);         // PalDialogID
            w.WriteUInt16(sectionId);
            w.WriteUInt32(0);         // AttachIdentity
            w.WriteByte(roleId);
            w.WriteUInt32(0x00000502);// ClientVersion
            w.WriteUInt16(0);         // ConfigFiles count
            w.WriteUInt32(0);         // ClientType
            w.WriteUInt32(0);         // CSVersion
            return w.ToArray();
        }

        public static byte[] BuildHelloPayload(uint uin, uint clientTime)
        {
            var w = new BigEndianBuffer(10);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteUInt16(0); // Info length
            return w.ToArray();
        }

        public static byte[] BuildRoomListPayload(uint uin, uint clientTime, byte operation = 2, ushort startRoomId = 1, ushort number = 50, byte gameMode = 0, byte gameType = 0)
        {
            var w = new BigEndianBuffer(15);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteByte(operation);
            w.WriteUInt16(startRoomId);
            w.WriteUInt16(number);
            w.WriteByte(gameMode);
            w.WriteByte(gameType);
            return w.ToArray();
        }

        public static byte[] BuildCreateRoomPayload(uint uin, uint clientTime, string roomName, RoomPropertyFlag flag, RoomGameType gameType, string password = "")
        {
            var w = new BigEndianBuffer(50);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteFixedSlot(LegacyTextEncoding.EncodeSlot(roomName, 19), 20);
            bool hasPass = !string.IsNullOrEmpty(password);
            RoomPropertyFlag effectiveFlag = hasPass ? (flag | RoomPropertyFlag.Password) : (flag & ~RoomPropertyFlag.Password);
            w.WriteByte((byte)effectiveFlag);
            w.WriteFixedSlot(hasPass ? LegacyTextEncoding.EncodeSlot(password, 15) : null, 16);
            w.WriteByte((byte)gameType);
            w.WriteUInt32(0); // ContinueID
            return w.ToArray();
        }

        public static byte[] BuildEnterRoomPayload(uint uin, uint clientTime, ushort roomId, byte roleId, string password = "")
        {
            var w = new BigEndianBuffer(27);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteUInt16(roomId);
            w.WriteByte(roleId);
            w.WriteFixedSlot(string.IsNullOrEmpty(password) ? null : LegacyTextEncoding.EncodeSlot(password, 15), 16);
            return w.ToArray();
        }

        public static byte[] BuildLeaveRoomPayload(uint uin, uint clientTime)
        {
            var w = new BigEndianBuffer(8);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            return w.ToArray();
        }

        public static byte[] BuildModifyRoomInfoPayload(uint uin, uint clientTime, ushort mapId, RoomGameType gameType, uint continueId = 0)
        {
            var w = new BigEndianBuffer(15);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteUInt16(mapId);
            w.WriteByte((byte)gameType);
            w.WriteUInt32(continueId);
            return w.ToArray();
        }

        public static byte[] BuildChangeTeamPayload(uint uin, uint clientTime, byte teamId)
        {
            var w = new BigEndianBuffer(10);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteUInt16(teamId);
            return w.ToArray();
        }

        public static byte[] BuildChangeRolePayload(uint uin, uint clientTime, byte roleId)
        {
            var w = new BigEndianBuffer(10);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteUInt16(roleId);
            return w.ToArray();
        }

        public static byte[] BuildReadyPayload(uint uin, uint clientTime)
        {
            var w = new BigEndianBuffer(8);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            return w.ToArray();
        }

        public static byte[] BuildItemStatusChangePayload(uint uin, uint clientTime, uint itemId, byte newStatus, byte newRoleId = 0)
        {
            var w = new BigEndianBuffer(16);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            w.WriteUInt16(1);
            w.WriteUInt32(itemId);
            w.WriteByte(newStatus);
            w.WriteByte(newRoleId);
            return w.ToArray();
        }

        public static byte[] BuildStartGamePayload(uint uin, uint clientTime)
        {
            var w = new BigEndianBuffer(8);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTime);
            return w.ToArray();
        }

        public static byte[] BuildTransferUdpOkPayload(uint sourceUin, uint clientTime, ushort destPlayerId, uint destUin, byte[] info)
        {
            var w = new BigEndianBuffer(16 + (info?.Length ?? 0));
            w.WriteUInt32(sourceUin);
            w.WriteUInt32(clientTime);
            w.WriteUInt16(destPlayerId);
            w.WriteUInt32(destUin);
            w.WriteUInt16((ushort)(info?.Length ?? 0));
            if (info != null && info.Length > 0) w.WriteBytes(info);
            return w.ToArray();
        }

        public static byte[] BuildGameEventRequestPayload(uint uin, uint clientTag, ushort schema, byte[] body)
        {
            int bodyLen = body?.Length ?? 0;
            var w = new BigEndianBuffer(14 + bodyLen);
            w.WriteUInt32(uin);
            w.WriteUInt32(clientTag);
            w.WriteUInt16((ushort)(4 + bodyLen));
            w.WriteUInt32LE(schema);
            if (bodyLen > 0) w.WriteBytes(body);
            return w.ToArray();
        }
    }
}
