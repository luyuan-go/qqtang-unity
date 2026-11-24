using System;
using System.Collections.Generic;
using Qqtang.Core.Protocol;

namespace Qqtang.Core.Battle
{
    public enum BattleDirection : byte
    {
        None = 0,
        Right = 1,
        Up = 2,
        Left = 3,
        Down = 4
    }

    public enum ActorLifeState : byte
    {
        Active = 0,
        Trapped = 1,
        Dead = 2
    }

    public enum MovementStatusKind : byte
    {
        None = 0,
        Slow = 1,
        ForcedSlide = 2,
        Fast = 3
    }

    public enum OfflinePracticeMode : byte
    {
        Classic = 0,
        BossRaid = 1,
        BunCapture = 2,
        SculptureDefense = 3
    }

    [Flags]
    public enum BattleAudioCue : uint
    {
        None = 0,
        BombPlace = 1u << 0,
        BombExplode = 1u << 1,
        Pickup = 1u << 2,
        Trapped = 1u << 3,
        Rescued = 1u << 4,
        Eliminated = 1u << 5,
        WoodBreak = 1u << 6,
        WoodPush = 1u << 7,
        BombKick = 1u << 8,
        BunDelivery = 1u << 9,
        Damage = 1u << 10,
        Respawn = 1u << 11,
        Wave = 1u << 12
    }

    public struct NativeTransformationDef
    {
        public uint SceneId;
        public ushort AvatarRoleId;
        public string DisplayName;
        public uint DurationMs;
        public bool TraverseStaticTerrain;
        public bool CanCollectItems;
        public bool RecoverOnlyOnOpenCell;
        public bool ReverseDirection;
        public bool CanKickBomb;
        public bool CanPushBreakable;
        public byte SpeedRate;
        public byte HorizontalSpeedRate;
        public byte VerticalSpeedRate;
        public byte BombCapacity;
        public byte GrantedActionId;
        public byte GrantedActionCount;
    }

    public sealed class BattleFieldObject
    {
        public uint Id;
        public byte ActionId; // 41=Trap, 42=Banana(ForcedSlide), 43=Glue(Slow)
        public ushort OwnerId;
        public GridCell Cell;
        public HashSet<ushort> PassableBy = new HashSet<ushort>();
    }

    public sealed class BattleProjectile
    {
        public uint Id;
        public byte ActionId; // 44=Dart, 46=Axe
        public ushort OwnerId;
        public uint TargetBombId;
        public GridCell StartCell;
        public GridCell TargetCell;
        public uint CreatedAtMs;
        public uint ResolveAtMs;
    }

    public struct RoleCombatProfile
    {
        public byte BombCapacity, MaxBombCapacity;
        public byte BombPower, MaxBombPower;
        public byte SpeedRate, MaxSpeedRate;

        public RoleCombatProfile(byte cap, byte maxCap, byte pow, byte maxPow, byte spd, byte maxSpd)
        {
            BombCapacity = cap; MaxBombCapacity = maxCap;
            BombPower = pow; MaxBombPower = maxPow;
            SpeedRate = spd; MaxSpeedRate = maxSpd;
        }
    }

    public sealed class BattleActor
    {
        public ushort PlayerId;
        public byte RoleId;
        public byte TeamId;
        public bool IsLocal;
        public int PosX; // Pixel space: cell (r,c) center is (c*40 + 20, r*40 + 20)
        public int PosY;
        public uint MoveRemainder;
        public BattleDirection Facing = BattleDirection.Down;
        public bool Moving;
        public ActorLifeState State = ActorLifeState.Active;
        public uint TrappedElapsedMs;
        public uint OxygenMs = Rule1BattleSimulation.InitialOxygenMs;
        public byte BombCapacity, MaxBombCapacity;
        public byte BombPower, MaxBombPower;
        public byte SpeedRate, MaxSpeedRate;
        public ushort MoveSequence;
        public int KillCount;
        public int RescueCount;
        public int NeedleCount;           // Action 63 (Pickup 24)
        public int TrapCount;             // Action 41 (Pickup 21)
        public int BananaCount;           // Action 42 (Pickup 23)
        public int GlueCount;             // Action 43 (Pickup 25 x3)
        public int DartCount;             // Action 44 (Pickup 27 x2)
        public int AxeCount;              // Action 46 (Transformation 115 x9)
        public int RemoteDetonatorCount;  // Action 64 (Pickup 28)
        public int ShieldCount;           // Optional prepared prop / shield charge
        public bool CanKickBomb;
        public uint ShieldActiveUntilMs;
        public uint ActiveEmotionId;
        public uint EmotionExpireAtMs;

        // Movement status (Banana ForcedSlide / Glue Slow / Scene 47 Fast)
        public MovementStatusKind MovementStatus;
        public uint MovementStatusExpiresAtMs;
        public BattleDirection ForcedSlideDirection = BattleDirection.None;

        // Transformation / Avatar state (30s native duration + 3s post-transform protection)
        public uint TransformationSceneId;
        public ushort AvatarRoleId;
        public uint TransformationExpiresAtMs;
        public uint HiddenPickupReachExpiresAtMs;
        public uint SceneFourEffectExpiresAtMs;

        // Match accumulators & passive cosmetic mask
        public uint MatchSugar;
        public uint TreasureScore;
        public PassiveFunctionEffectMask PassiveEffects;
        public int[] BasicPickupDelta = new int[3]; // [0]=Cap(1), [1]=Pow(2), [2]=Spd(3) for death drops

        // Remote interpolation target
        public int TargetPosX;
        public int TargetPosY;
        public bool HasRemoteTarget;

        public bool IsInvulnerable(uint nowMs) => nowMs < ShieldActiveUntilMs;
        public bool IsTransformed => TransformationSceneId != 0;

        public int GetHeldActionCount(uint actionId)
        {
            switch (actionId)
            {
                case 41: return TrapCount;
                case 42: return BananaCount;
                case 43: return GlueCount;
                case 44: return DartCount;
                case 46: return AxeCount;
                case 63: return NeedleCount;
                case 64: return RemoteDetonatorCount;
                default: return 0;
            }
        }

        public void GrantHeldAction(uint actionId, int count)
        {
            if (count <= 0) return;
            switch (actionId)
            {
                case 41: TrapCount = Math.Min(9, TrapCount + count); break;
                case 42: BananaCount = Math.Min(9, BananaCount + count); break;
                case 43: GlueCount = Math.Min(9, GlueCount + count); break;
                case 44: DartCount = Math.Min(9, DartCount + count); break;
                case 46: AxeCount = Math.Min(9, AxeCount + count); break;
                case 63: NeedleCount = Math.Min(9, NeedleCount + count); break;
                case 64: RemoteDetonatorCount = Math.Min(9, RemoteDetonatorCount + count); break;
            }
        }

        public bool ConsumeHeldAction(uint actionId)
        {
            if (GetHeldActionCount(actionId) <= 0) return false;
            switch (actionId)
            {
                case 41: TrapCount--; return true;
                case 42: BananaCount--; return true;
                case 43: GlueCount--; return true;
                case 44: DartCount--; return true;
                case 46: AxeCount--; return true;
                case 63: NeedleCount--; return true;
                case 64: RemoteDetonatorCount--; return true;
                default: return false;
            }
        }

        public GridCell CurrentCell => new GridCell(
            Math.Max(0, PosY / Rule1BattleSimulation.CellSizePixels),
            Math.Max(0, PosX / Rule1BattleSimulation.CellSizePixels));
    }

    public sealed class ActiveBomb
    {
        public uint BombId;
        public ushort OwnerId;
        public GridCell Cell;
        public byte Power;
        public uint PlacedAtMs;
        public uint ExplodeAtMs;
        public bool RemoteOwned;
    }

    public sealed class ActiveFlame
    {
        public GridCell Cell;
        public ushort OwnerId;
        public uint ExpireAtMs;
    }

    public sealed class FieldPickup
    {
        public uint SceneId;
        public GridCell Cell;
        public bool Hidden;
        public bool Consumed;
    }

    public sealed class BattleBossEntity
    {
        public ushort BossId;
        public byte RoleId;
        public byte TeamId;
        public uint Label;
        public bool IsPve;
        public int PosX;
        public int PosY;
        public int Hp;
        public int MaxHp;
        public bool Alive = true;
        public uint LastDamagedMs;
        public string SpeechText = string.Empty;
        public uint SpeechExpireMs;

        public GridCell CurrentCell => new GridCell(
            Math.Max(0, PosY / Rule1BattleSimulation.CellSizePixels),
            Math.Max(0, PosX / Rule1BattleSimulation.CellSizePixels));
    }

    /// <summary>
    /// Exact client+0x522F0 3-step LCG from server/internal/game/battleengine/pickup_placement.go.
    /// </summary>
    public struct NativeMapRng
    {
        private uint _state;
        public NativeMapRng(uint seed) { _state = seed; }

        public uint Next()
        {
            unchecked
            {
                uint first = _state * 0x41C64E6Du + 0x3039u;
                uint second = first * 0x41C64E6Du + 0x3039u;
                uint third = second * 0x41C64E6Du + 0x3039u;
                _state = third;
                return ((((first >> 16) % 0x800u) << 10) ^ ((second >> 16) % 0x400u)) << 10 ^ ((third >> 16) % 0x400u);
            }
        }
    }

    /// <summary>
    /// Pure C# Rule-1 battle simulation aligned with server/internal/game/battleengine.
    /// Produces and consumes exact QQTang wire protocol battle events.
    /// </summary>
    public sealed class Rule1BattleSimulation
    {
        public const int CellSizePixels = 40;
        public const int ActorHalfSizePixels = 19;
        public const int CornerSlideTolerancePixels = 6;
        public const uint RoundStartClockMs = 3000;
        public const uint BombFuseMs = 3000;
        public const uint FlameDurationMs = 500;
        public const uint InitialOxygenMs = 6000;
        public const uint MovementStatusDurationMs = 10_000;
        public const uint TransformationDurationMs = 30_000;
        public const uint PostTransformProtectionMs = 3_000;
        public const uint ActionProjectileFlightMs = 1_000;

        public static bool TryGetTransformation(uint sceneId, out NativeTransformationDef def)
        {
            def = new NativeTransformationDef { SceneId = sceneId, DurationMs = TransformationDurationMs, CanCollectItems = true };
            switch (sceneId)
            {
                case 101: def.AvatarRoleId = 43; def.DisplayName = "变身·糖果精灵"; return true;
                case 104: def.AvatarRoleId = 41; def.DisplayName = "变身·穿墙飞鸭"; def.TraverseStaticTerrain = true; def.CanCollectItems = false; def.RecoverOnlyOnOpenCell = true; return true;
                case 107: def.AvatarRoleId = 45; def.DisplayName = "变身·疾风幽灵"; def.SpeedRate = 8; return true;
                case 108: def.AvatarRoleId = 46; def.DisplayName = "变身·糖泡大王"; def.BombCapacity = 8; return true;
                case 109: def.AvatarRoleId = 44; def.DisplayName = "变身·神力熊猫"; def.CanKickBomb = true; def.CanPushBreakable = true; return true;
                case 110: def.AvatarRoleId = 42; def.DisplayName = "变身·反向恶魔"; def.SpeedRate = 8; def.BombCapacity = 8; def.ReverseDirection = true; return true;
                case 114: def.AvatarRoleId = 54; def.DisplayName = "变身·横行螃蟹"; def.HorizontalSpeedRate = 5; def.VerticalSpeedRate = 2; return true;
                case 115: def.AvatarRoleId = 55; def.DisplayName = "变身·斧头帮"; def.GrantedActionId = 46; def.GrantedActionCount = 9; return true;
                default: return false;
            }
        }

        public static readonly ushort[] SpeedPixelsPerSecondByRate =
            { 0, 40, 72, 140, 160, 180, 204, 232, 268, 320, 520 };

        private static readonly Dictionary<byte, RoleCombatProfile> CombatProfiles = new Dictionary<byte, RoleCombatProfile>
        {
            [1] = new RoleCombatProfile(2, 6, 2, 8, 4, 7),
            [2] = new RoleCombatProfile(2, 6, 3, 9, 4, 6),
            [3] = new RoleCombatProfile(1, 5, 2, 9, 5, 7),
            [4] = new RoleCombatProfile(1, 7, 3, 9, 4, 7),
            [5] = new RoleCombatProfile(2, 7, 3, 8, 3, 7),
            [6] = new RoleCombatProfile(1, 6, 2, 7, 5, 8),
            [7] = new RoleCombatProfile(1, 8, 3, 9, 3, 8),
            [8] = new RoleCombatProfile(1, 7, 2, 6, 5, 8),
            [9] = new RoleCombatProfile(2, 6, 3, 7, 5, 8),
            [10] = new RoleCombatProfile(3, 8, 4, 9, 5, 8),
            [11] = new RoleCombatProfile(3, 8, 4, 9, 5, 8),
            [13] = new RoleCombatProfile(2, 8, 3, 7, 4, 6),
            [14] = new RoleCombatProfile(1, 7, 2, 6, 5, 8),
            [15] = new RoleCombatProfile(2, 7, 3, 9, 4, 8),
            [16] = new RoleCombatProfile(1, 8, 3, 9, 4, 8),
            [18] = new RoleCombatProfile(1, 6, 1, 6, 5, 8),
            [19] = new RoleCombatProfile(2, 8, 1, 6, 4, 7),
            [20] = new RoleCombatProfile(2, 6, 1, 5, 4, 6),
            [21] = new RoleCombatProfile(2, 8, 1, 5, 3, 6),
            [22] = new RoleCombatProfile(2, 6, 3, 7, 5, 8)
        };

        public uint GameId { get; private set; }
        public ushort LocalPlayerId { get; private set; }
        public ushort ArbitratorPlayerId { get; private set; }
        public uint ElapsedMs { get; private set; }
        public uint RoundDurationMs { get; private set; }
        public bool IsFinished { get; private set; }
        public LoadedMapData Map { get; private set; }

        public List<BattleActor> Actors { get; } = new List<BattleActor>();
        public List<ActiveBomb> Bombs { get; } = new List<ActiveBomb>();
        public List<ActiveFlame> Flames { get; } = new List<ActiveFlame>();
        public List<FieldPickup> Pickups { get; } = new List<FieldPickup>();
        public List<BattleFieldObject> FieldObjects { get; } = new List<BattleFieldObject>();
        public List<BattleProjectile> Projectiles { get; } = new List<BattleProjectile>();
        public List<BattleBossEntity> Bosses { get; } = new List<BattleBossEntity>();
        public ushort BunCarrierPlayerId { get; private set; }
        public byte BunTeamId { get; private set; }
        public GridCell OfflineBunCell { get; private set; }
        public Dictionary<byte, int> TeamBunScores { get; } = new Dictionary<byte, int>();
        public Dictionary<byte, int> TankBaseHpByTeam { get; } = new Dictionary<byte, int>();
        public Dictionary<byte, int> SculptureProgressByTeam { get; } = new Dictionary<byte, int>();
        public bool IsOfflineSoloMode { get; set; }
        public OfflinePracticeMode OfflineMode { get; set; } = OfflinePracticeMode.Classic;
        public GameOverData OfflineSettlement { get; private set; }
        public BattleAudioCue StepAudioCues { get; private set; }
        public bool AllBossesDefeated => Bosses.Count > 0 && Bosses.TrueForAll(b => !b.Alive);

        // Outgoing wire events produced during Step()
        public List<(uint schemaId, byte[] body)> OutgoingPeerEvents { get; } = new List<(uint, byte[])>();
        public List<(ushort schemaId, byte[] body)> OutgoingTcpEvents { get; } = new List<(ushort, byte[])>();

        private uint _nextBombId = 1;
        private uint _nextFieldObjectId = 1;
        private uint _nextProjectileId = 1;
        private uint _offlineBossSkillNextMs = 7000;
        private ulong _deathDropRng = 0x9E3779B97F4A7C15UL;
        private ushort _pushSequence = 1;
        private BattleDirection _lastSentMoveDir = BattleDirection.None;
        private bool _lastSentMoving;
        private uint _lastMoveBroadcastMs;
        private readonly HashSet<(ushort owner, uint placedAt, byte r, byte c)> _seenRemoteBombs = new HashSet<(ushort, uint, byte, byte)>();
        private readonly Dictionary<uint, ushort> _uinToPlayerId = new Dictionary<uint, ushort>();

        public void RegisterParticipantUin(uint uin, ushort playerId)
        {
            if (uin != 0 && playerId != 0) _uinToPlayerId[uin] = playerId;
        }

        public bool TryUseWorldItem(uint itemId)
        {
            BattleActor local = LocalActor;
            if (local == null || IsFinished || ElapsedMs < RoundStartClockMs) return false;

            if (itemId == 63) // Needle self-rescue while trapped
            {
                if (local.State != ActorLifeState.Trapped || !local.ConsumeHeldAction(63)) return false;
                local.State = ActorLifeState.Active;
                local.TrappedElapsedMs = 0;
                local.ShieldActiveUntilMs = ElapsedMs + 800;
                StepAudioCues |= BattleAudioCue.Rescued;
            }
            else if (itemId == 64) // Remote detonate own active bombs
            {
                if (local.State != ActorLifeState.Active || !local.ConsumeHeldAction(64)) return false;
                foreach (var b in Bombs)
                    if (b.OwnerId == local.PlayerId && b.ExplodeAtMs > ElapsedMs)
                        b.ExplodeAtMs = ElapsedMs;
            }
            else if (itemId == 41 || itemId == 42 || itemId == 43) // Ground Field Object: 41=Trap, 42=Banana, 43=Glue
            {
                if (local.State != ActorLifeState.Active) return false;
                GridCell cell = local.CurrentCell;
                if (!Map.InBounds(cell.Row, cell.Col)) return false;
                var tile = Map.GetCell(cell.Row, cell.Col);
                if (tile.Collision != CellCollision.Open || tile.MapElementOccupied) return false;
                if (!local.ConsumeHeldAction(itemId)) return false;
                PlaceFieldObject((byte)itemId, local.PlayerId, cell);
            }
            else if (itemId == 44 || itemId == 46) // Dart (44) / Axe (46) remote bomb-detonation projectile
            {
                if (local.State != ActorLifeState.Active) return false;
                if (!TryFindProjectileTarget(local, out GridCell targetCell, out uint targetBombId)) return false;
                if (!local.ConsumeHeldAction(itemId)) return false;
                Projectiles.Add(new BattleProjectile
                {
                    Id = _nextProjectileId++,
                    ActionId = (byte)itemId,
                    OwnerId = local.PlayerId,
                    TargetBombId = targetBombId,
                    StartCell = local.CurrentCell,
                    TargetCell = targetCell,
                    CreatedAtMs = ElapsedMs,
                    ResolveAtMs = ElapsedMs + ActionProjectileFlightMs
                });
                StepAudioCues |= BattleAudioCue.Wave;
            }
            else
            {
                return false;
            }

            var ev = new WorldUseItemEventData
            {
                PlayerId = local.PlayerId,
                ClientTime = ElapsedMs,
                ItemId = itemId,
                PosX = (ushort)Math.Max(0, local.PosX),
                PosY = (ushort)Math.Max(0, local.PosY)
            };
            byte[] body = ev.Marshal();
            OutgoingPeerEvents.Add((ExtendedGameEventSchemas.RequestUseItem, body));
            OutgoingTcpEvents.Add((ExtendedGameEventSchemas.RequestUseItem, body));
            return true;
        }

        private void PlaceFieldObject(byte actionId, ushort ownerId, GridCell cell)
        {
            FieldObjects.RemoveAll(f => f.Cell.Equals(cell));
            var obj = new BattleFieldObject
            {
                Id = _nextFieldObjectId++,
                ActionId = actionId,
                OwnerId = ownerId,
                Cell = cell
            };
            foreach (var a in Actors)
                if (a.State != ActorLifeState.Dead && a.CurrentCell.Equals(cell))
                    obj.PassableBy.Add(a.PlayerId);
            FieldObjects.Add(obj);
        }

        private bool TryFindProjectileTarget(BattleActor actor, out GridCell lastCell, out uint targetBombId)
        {
            lastCell = actor.CurrentCell;
            targetBombId = 0;
            BattleDirection dir = actor.Facing == BattleDirection.None ? BattleDirection.Down : actor.Facing;
            var (dr, dc) = DirectionDelta(dir);
            if (dr == 0 && dc == 0) return false;
            int maxDist = Math.Max(Map.Width, Map.Height);
            bool reachable = false;
            GridCell cur = actor.CurrentCell;
            for (int step = 1; step < maxDist; step++)
            {
                cur = new GridCell(cur.Row + dr, cur.Col + dc);
                if (!Map.InBounds(cur.Row, cur.Col)) break;
                var tile = Map.GetCell(cur.Row, cur.Col);
                if (!tile.FlamePassable && tile.Collision != CellCollision.Open) break;
                lastCell = cur;
                reachable = true;
                var bomb = Bombs.Find(b => b.Cell.Equals(cur));
                if (bomb != null)
                {
                    targetBombId = bomb.BombId;
                    return true;
                }
            }
            return reachable;
        }

        public void TriggerLocalEmotion(uint localUin, uint emotionId)
        {
            BattleActor local = LocalActor;
            if (local == null || emotionId == 0) return;
            local.ActiveEmotionId = emotionId;
            local.EmotionExpireAtMs = ElapsedMs + 3000;
            if (localUin != 0)
            {
                var ev = new UseEmotionEventData { Uin = localUin, Time = ElapsedMs, EmotionId = emotionId };
                OutgoingTcpEvents.Add((ExtendedGameEventSchemas.NotifyUseEmotion, ev.Marshal()));
            }
        }

        public bool TryKickAdjacentBomb(BattleDirection dir)
        {
            BattleActor local = LocalActor;
            bool canKick = local != null && (local.CanKickBomb || (TryGetTransformation(local.TransformationSceneId, out var td) && td.CanKickBomb));
            if (local == null || local.State != ActorLifeState.Active || !canKick || dir == BattleDirection.None)
                return false;
            var (dr, dc) = DirectionDelta(dir);
            GridCell from = new GridCell(local.CurrentCell.Row + dr, local.CurrentCell.Col + dc);
            ActiveBomb bomb = Bombs.Find(b => b.Cell.Equals(from));
            if (bomb == null) return false;

            GridCell to = from;
            while (true)
            {
                GridCell next = new GridCell(to.Row + dr, to.Col + dc);
                if (!Map.InBounds(next.Row, next.Col)) break;
                if (Map.GetCell(next.Row, next.Col).Collision != CellCollision.Open) break;
                if (HasBombAt(next)) break;
                to = next;
            }
            if (to.Equals(from)) return false;

            bomb.Cell = to;
            StepAudioCues |= BattleAudioCue.BombKick;
            var msg = new MoveBombEventData
            {
                PlayerId = local.PlayerId,
                ClientTime = (ushort)(ElapsedMs & 0xFFFF),
                FromRow = (ushort)from.Row,
                FromCol = (ushort)from.Col,
                ToRow = (ushort)to.Row,
                ToCol = (ushort)to.Col,
                Direction = ToNativeDirectionCode(dir),
                BombPlayerId = bomb.OwnerId,
                BombTime = bomb.PlacedAtMs,
                BombPower = bomb.Power
            };
            byte[] body = msg.Marshal();
            OutgoingPeerEvents.Add((ExtendedGameEventSchemas.RequestMoveBomb, body));
            OutgoingTcpEvents.Add((ExtendedGameEventSchemas.RequestMoveBomb, body));
            return true;
        }

        public bool TryPushAdjacentMapElement(BattleDirection dir)
        {
            BattleActor local = LocalActor;
            if (local == null || local.State != ActorLifeState.Active || dir == BattleDirection.None) return false;
            var (dr, dc) = DirectionDelta(dir);
            GridCell src = new GridCell(local.CurrentCell.Row + dr, local.CurrentCell.Col + dc);
            GridCell dst = new GridCell(src.Row + dr, src.Col + dc);
            if (!Map.InBounds(src.Row, src.Col) || !Map.InBounds(dst.Row, dst.Col)) return false;
            var srcTile = Map.GetCell(src.Row, src.Col);
            var dstTile = Map.GetCell(dst.Row, dst.Col);
            bool pandaPush = TryGetTransformation(local.TransformationSceneId, out var td) && td.CanPushBreakable && srcTile.Collision == CellCollision.Breakable;
            if ((!srcTile.Pushable && !pandaPush) || dstTile.Collision != CellCollision.Open || HasBombAt(dst)) return false;

            ApplyMapElementSlide(src, dst);
            StepAudioCues |= BattleAudioCue.WoodPush;
            var req = new MoveMapElementEventData
            {
                PlayerId = local.PlayerId,
                ClientTime = ElapsedMs,
                ElementId = srcTile.MapElementId != 0 ? srcTile.MapElementId : 1u,
                Row = (ushort)src.Row,
                Col = (ushort)src.Col,
                Direction = ToNativeDirectionCode(dir),
                Sequence = _pushSequence++
            };
            OutgoingPeerEvents.Add((ExtendedGameEventSchemas.RequestMoveMapElement, req.MarshalRequest()));
            OutgoingTcpEvents.Add((ExtendedGameEventSchemas.RequestMoveMapElement, req.MarshalRequest()));
            return true;
        }

        private void ApplyMapElementSlide(GridCell src, GridCell dst)
        {
            if (!Map.InBounds(src.Row, src.Col) || !Map.InBounds(dst.Row, dst.Col)) return;
            int si = src.Row * Map.Width + src.Col;
            int di = dst.Row * Map.Width + dst.Col;
            var s = Map.Cells[si];
            var d = Map.Cells[di];
            d.Collision = s.Collision;
            d.FlamePassable = s.FlamePassable;
            d.Pushable = s.Pushable;
            d.MapElementOccupied = s.MapElementOccupied;
            d.Durability = s.Durability;
            d.MapElementId = s.MapElementId;
            d.VisualSceneElementId = s.VisualSceneElementId;
            s.Collision = CellCollision.Open;
            s.FlamePassable = true;
            s.Pushable = false;
            s.MapElementOccupied = false;
            s.Durability = 0;
            s.MapElementId = 0;
            s.VisualSceneElementId = 0;
            Map.Cells[si] = s;
            Map.Cells[di] = d;
        }

        public static RoleCombatProfile GetRoleProfile(byte roleId) =>
            CombatProfiles.TryGetValue(roleId, out var p) ? p : new RoleCombatProfile(2, 6, 2, 8, 4, 7);

        public void Initialize(GameBeginData beginData, LoadedMapData sourceMap, ushort localPlayerId, bool usesFreeRule)
        {
            GameId = beginData.GameId;
            LocalPlayerId = localPlayerId;
            ArbitratorPlayerId = beginData.ArbitratorPlayerId;
            ElapsedMs = 0;
            RoundDurationMs = beginData.GameTimeMs > 0 ? beginData.GameTimeMs : 240_000u;
            IsFinished = false;
            Map = sourceMap.Clone();
            Actors.Clear();
            Bombs.Clear();
            Flames.Clear();
            Pickups.Clear();
            FieldObjects.Clear();
            Projectiles.Clear();
            Bosses.Clear();
            BunCarrierPlayerId = 0;
            BunTeamId = 0;
            OfflineBunCell = new GridCell(Map.Height / 2, Map.Width / 2);
            TeamBunScores.Clear();
            TankBaseHpByTeam.Clear();
            SculptureProgressByTeam.Clear();
            OfflineSettlement = null;
            StepAudioCues = BattleAudioCue.None;
            OutgoingPeerEvents.Clear();
            OutgoingTcpEvents.Clear();
            _seenRemoteBombs.Clear();
            _nextBombId = 1;
            _nextFieldObjectId = 1;
            _nextProjectileId = 1;
            _offlineBossSkillNextMs = 7000;
            _deathDropRng = (ulong)beginData.SpawnSeed ^ 0x9E3779B97F4A7C15UL;
            _lastSentMoveDir = BattleDirection.None;
            _lastSentMoving = false;
            _lastMoveBroadcastMs = 0;

            GridCell[] spawns = ComputeNativeSpawns(beginData.SpawnSeed, usesFreeRule, Map.SpawnGroupA, Map.SpawnGroupB, beginData.Players);
            for (int i = 0; i < beginData.Players.Count; i++)
            {
                var p = beginData.Players[i];
                var profile = GetRoleProfile(p.RoleId);
                GridCell spawn = spawns[i];
                int px = spawn.Col * CellSizePixels + CellSizePixels / 2;
                int py = spawn.Row * CellSizePixels + CellSizePixels / 2;
                Actors.Add(new BattleActor
                {
                    PlayerId = p.PlayerId,
                    RoleId = p.RoleId,
                    TeamId = p.TeamId,
                    IsLocal = p.PlayerId == localPlayerId,
                    PosX = px,
                    PosY = py,
                    TargetPosX = px,
                    TargetPosY = py,
                    BombCapacity = profile.BombCapacity,
                    MaxBombCapacity = profile.MaxBombCapacity,
                    BombPower = profile.BombPower,
                    MaxBombPower = profile.MaxBombPower,
                    SpeedRate = profile.SpeedRate,
                    MaxSpeedRate = profile.MaxSpeedRate
                });
            }

            // Place hidden wall pickups deterministically from ItemSeed + GAME_BEGIN.NewItems (or Items fallback)
            var dropSource = beginData.NewItems.Count > 0 ? beginData.NewItems : beginData.Items;
            PlaceHiddenPickups(beginData.ItemSeed, Map.HiddenItemCells, dropSource);
        }

        public void ConfigureOfflineScenario(OfflinePracticeMode mode)
        {
            OfflineMode = mode;
            if (!IsOfflineSoloMode) return;
            if (mode == OfflinePracticeMode.BossRaid)
            {
                Bosses.Clear();
                int cr = Map.Height / 2, cc = Map.Width / 2;
                // Clear center 3x3 so Boss can patrol smoothly
                for (int r = Math.Max(1, cr - 1); r <= Math.Min(Map.Height - 2, cr + 1); r++)
                {
                    for (int c = Math.Max(1, cc - 1); c <= Math.Min(Map.Width - 2, cc + 1); c++)
                    {
                        int idx = r * Map.Width + c;
                        var cell = Map.Cells[idx];
                        cell.Collision = CellCollision.Open;
                        cell.FlamePassable = true;
                        Map.Cells[idx] = cell;
                    }
                }
                Bosses.Add(new BattleBossEntity
                {
                    BossId = 30001, RoleId = 3, TeamId = 9, Label = 101, IsPve = true,
                    Hp = 2500, MaxHp = 2500, Alive = true,
                    PosX = cc * CellSizePixels + CellSizePixels / 2,
                    PosY = cr * CellSizePixels + CellSizePixels / 2,
                    SpeechText = "何方小道，敢闯灵葫妖阵！", SpeechExpireMs = 6500
                });
            }
            else if (mode == OfflinePracticeMode.BunCapture)
            {
                TeamBunScores[1] = 0;
                TeamBunScores[2] = 0;
                OfflineBunCell = new GridCell(Map.Height / 2, Map.Width / 2);
                int idx = OfflineBunCell.Row * Map.Width + OfflineBunCell.Col;
                var tile = Map.Cells[idx];
                tile.Collision = CellCollision.Open;
                tile.FlamePassable = true;
                Map.Cells[idx] = tile;
            }
            else if (mode == OfflinePracticeMode.SculptureDefense)
            {
                TankBaseHpByTeam[1] = 1000;
                TankBaseHpByTeam[2] = 1000;
                SculptureProgressByTeam[1] = 0;
                SculptureProgressByTeam[2] = 0;
            }
        }

        public static GridCell[] ComputeNativeSpawns(
            uint spawnSeed,
            bool usesFreeRule,
            List<GridCell> groupA,
            List<GridCell> groupB,
            List<PlayerGameInfoData> players)
        {
            var result = new GridCell[players.Count];
            bool useTeams = !usesFreeRule && groupB.Count > 0 && players.Count % 2 == 0;
            if (useTeams)
            {
                byte teamA = players[0].TeamId;
                byte teamB = 0;
                int countA = 0, countB = 0;
                foreach (var p in players)
                {
                    if (p.TeamId == teamA) countA++;
                    else if (teamB == 0 || p.TeamId == teamB) { teamB = p.TeamId; countB++; }
                    else { useTeams = false; break; }
                }
                if (teamB == 0 || countA != countB || countA > groupA.Count || countB > groupB.Count)
                    useTeams = false;

                if (useTeams)
                {
                    var selA = SampleSpawnPool(spawnSeed, groupA, countA);
                    var selB = SampleSpawnPool(spawnSeed, groupB, countB);
                    int ia = 0, ib = 0;
                    for (int i = 0; i < players.Count; i++)
                        result[i] = players[i].TeamId == teamA ? selA[ia++] : selB[ib++];
                    return result;
                }
            }

            // Free / interleaved pool
            var interleaved = new List<GridCell>(groupA.Count + groupB.Count);
            int shared = Math.Min(groupA.Count, groupB.Count);
            for (int i = 0; i < shared; i++)
            {
                interleaved.Add(groupA[i]);
                interleaved.Add(groupB[i]);
            }
            for (int i = shared; i < groupA.Count; i++) interleaved.Add(groupA[i]);
            for (int i = shared; i < groupB.Count; i++) interleaved.Add(groupB[i]);

            for (int i = 0; i < players.Count; i++)
                result[i] = i < interleaved.Count ? interleaved[i] : new GridCell(0, i);
            return result;
        }

        private static List<GridCell> SampleSpawnPool(uint seed, List<GridCell> source, int count)
        {
            var pool = new List<GridCell>(source);
            var rng = new NativeMapRng(seed);
            var selected = new List<GridCell>(count);
            int remaining = pool.Count;
            while (selected.Count < count && remaining > 0)
            {
                int idx = (int)(rng.Next() % (uint)remaining);
                selected.Add(pool[idx]);
                remaining--;
                pool[idx] = pool[remaining];
            }
            return selected;
        }

        private void PlaceHiddenPickups(uint itemSeed, List<GridCell> candidates, List<GameItemTypeData> newItems)
        {
            if (candidates.Count == 0 || newItems.Count == 0) return;
            var shuffled = new List<GridCell>(candidates);
            var rng = new NativeMapRng(itemSeed);
            uint count = (uint)shuffled.Count;
            for (int i = 0; i < shuffled.Count; i++)
            {
                int a = (int)(rng.Next() % count);
                int b = (int)(rng.Next() % count);
                (shuffled[a], shuffled[b]) = (shuffled[b], shuffled[a]);
            }
            int cursor = 0;
            foreach (var item in newItems)
            {
                for (int q = 0; q < item.Quantity && cursor < shuffled.Count; q++)
                {
                    Pickups.Add(new FieldPickup { SceneId = item.ItemId, Cell = shuffled[cursor++], Hidden = true });
                }
            }
        }

        public BattleActor GetActor(ushort playerId)
        {
            for (int i = 0; i < Actors.Count; i++)
                if (Actors[i].PlayerId == playerId) return Actors[i];
            return null;
        }

        public BattleActor LocalActor => GetActor(LocalPlayerId);

        public void StepOfflineBot(ushort botPlayerId, uint deltaMs, BattleDirection moveInput, bool placeBomb)
        {
            if (IsFinished || ElapsedMs < RoundStartClockMs) return;
            BattleActor bot = GetActor(botPlayerId);
            if (bot == null || bot.IsLocal || bot.State != ActorLifeState.Active) return;

            bool moving = moveInput != BattleDirection.None;
            if (moving)
            {
                bot.Facing = moveInput;
                ushort speedPx = SpeedPixelsPerSecondByRate[Math.Min((int)bot.SpeedRate, SpeedPixelsPerSecondByRate.Length - 1)];
                uint num = speedPx * deltaMs + bot.MoveRemainder;
                int dist = (int)Math.Min(40u, num / 1000u);
                bot.MoveRemainder = num % 1000u;
                if (dist > 0) MoveActorPixels(bot, moveInput, dist);
            }
            else
            {
                bot.MoveRemainder = 0;
            }
            bot.Moving = moving;
            if (placeBomb) TryPlaceBombForActor(bot, emitWire: false);
        }

        private void TryPlaceBombForActor(BattleActor actor, bool emitWire)
        {
            GridCell cell = actor.CurrentCell;
            if (!Map.InBounds(cell.Row, cell.Col)) return;
            var tile = Map.GetCell(cell.Row, cell.Col);
            if (tile.Collision != CellCollision.Open || tile.MapElementOccupied) return;
            if (HasBombAt(cell)) return;

            int activeOwned = 0;
            foreach (var b in Bombs) if (b.OwnerId == actor.PlayerId) activeOwned++;
            if (activeOwned >= actor.BombCapacity) return;

            uint bombId = _nextBombId++;
            Bombs.Add(new ActiveBomb
            {
                BombId = bombId,
                OwnerId = actor.PlayerId,
                Cell = cell,
                Power = actor.BombPower,
                PlacedAtMs = ElapsedMs,
                ExplodeAtMs = ElapsedMs + BombFuseMs + 1,
                RemoteOwned = false
            });

            if (emitWire)
            {
                var msg = new PlayerUseBombMessage
                {
                    PlayerId = actor.PlayerId,
                    ClientTime = ElapsedMs,
                    BombId = bombId,
                    Row = (byte)cell.Row,
                    Column = (byte)cell.Col,
                    Power = actor.BombPower,
                    Property = 0
                };
                OutgoingPeerEvents.Add((GameEventSchemas.PlayerUseBomb, msg.Marshal()));
            }
        }

        public void Step(uint deltaMs, BattleDirection localMoveInput, bool localPlaceBomb)
        {
            OutgoingPeerEvents.Clear();
            OutgoingTcpEvents.Clear();
            StepAudioCues = BattleAudioCue.None;
            if (IsFinished) return;

            ElapsedMs += deltaMs;

            // Expire old flames
            for (int i = Flames.Count - 1; i >= 0; i--)
            {
                if (ElapsedMs >= Flames[i].ExpireAtMs) Flames.RemoveAt(i);
            }

            // Before RoundStartClockMs (3000ms Ready/Go), actors cannot act
            if (ElapsedMs >= RoundStartClockMs)
            {
                ExpireActorStatusesAndTransformations();
                ResolveActionProjectiles();
                ReleaseFieldObjectPassThrough();

                // 1. Update local player movement & bomb placement (handling Demon reverse-controls & Banana forced slide)
                BattleActor local = LocalActor;
                if (local != null && local.State == ActorLifeState.Active)
                {
                    BattleDirection effectiveInput = TransformLocalInput(local, localMoveInput);
                    StepLocalMovement(local, deltaMs, effectiveInput);
                    if (localPlaceBomb) TryPlaceLocalBomb(local);
                }

                // 2. Interpolate remote actors toward their advertised network target
                for (int i = 0; i < Actors.Count; i++)
                {
                    BattleActor actor = Actors[i];
                    if (actor.IsLocal || actor.State == ActorLifeState.Dead) continue;
                    StepRemoteActorInterpolation(actor, deltaMs);
                }

                // 3. Explode due bombs (local or arbitrator-owned)
                StepDueBombs();

                // 4. Evaluate field object contacts (Trap 41 / Banana 42 / Glue 43), flame hits, pickups, trap/rescue/kill contact, oxygen timers & modes
                ResolveFieldObjectContacts();
                StepCombatAndPickups(deltaMs);
            }
        }

        private BattleDirection TransformLocalInput(BattleActor actor, BattleDirection raw)
        {
            if (actor.MovementStatus == MovementStatusKind.ForcedSlide && actor.ForcedSlideDirection != BattleDirection.None)
                return actor.ForcedSlideDirection;
            if (TryGetTransformation(actor.TransformationSceneId, out var td) && td.ReverseDirection)
            {
                switch (raw)
                {
                    case BattleDirection.Up: return BattleDirection.Down;
                    case BattleDirection.Down: return BattleDirection.Up;
                    case BattleDirection.Left: return BattleDirection.Right;
                    case BattleDirection.Right: return BattleDirection.Left;
                }
            }
            return raw;
        }

        private byte GetEffectiveSpeedRate(BattleActor actor, BattleDirection dir)
        {
            if (actor.MovementStatus == MovementStatusKind.Slow) return 2;
            if (actor.MovementStatus == MovementStatusKind.ForcedSlide) return 10;
            if (actor.MovementStatus == MovementStatusKind.Fast) return 8;
            if (TryGetTransformation(actor.TransformationSceneId, out var td))
            {
                if ((dir == BattleDirection.Left || dir == BattleDirection.Right) && td.HorizontalSpeedRate > 0)
                    return td.HorizontalSpeedRate;
                if ((dir == BattleDirection.Up || dir == BattleDirection.Down) && td.VerticalSpeedRate > 0)
                    return td.VerticalSpeedRate;
                if (td.SpeedRate > 0) return td.SpeedRate;
            }
            return actor.SpeedRate;
        }

        private byte GetEffectiveBombCapacity(BattleActor actor)
        {
            if (TryGetTransformation(actor.TransformationSceneId, out var td) && td.BombCapacity > 0)
                return td.BombCapacity;
            return actor.BombCapacity;
        }

        private void ExpireActorStatusesAndTransformations()
        {
            foreach (var actor in Actors)
            {
                if (actor.MovementStatus != MovementStatusKind.None &&
                    actor.MovementStatus != MovementStatusKind.ForcedSlide &&
                    actor.MovementStatusExpiresAtMs > 0 && ElapsedMs >= actor.MovementStatusExpiresAtMs)
                {
                    actor.MovementStatus = MovementStatusKind.None;
                    actor.MovementStatusExpiresAtMs = 0;
                    actor.MoveRemainder = 0;
                }
                if (actor.TransformationSceneId != 0 && ElapsedMs >= actor.TransformationExpiresAtMs)
                {
                    bool canRecover = true;
                    if (TryGetTransformation(actor.TransformationSceneId, out var td) && td.RecoverOnlyOnOpenCell)
                    {
                        var c = actor.CurrentCell;
                        canRecover = Map.InBounds(c.Row, c.Col) && Map.GetCell(c.Row, c.Col).Collision == CellCollision.Open;
                    }
                    if (canRecover)
                    {
                        EndTransformation(actor, emitWire: actor.IsLocal || LocalPlayerId == ArbitratorPlayerId);
                    }
                }
            }
        }

        private void EndTransformation(BattleActor actor, bool emitWire)
        {
            if (actor.TransformationSceneId == 0) return;
            if (TryGetTransformation(actor.TransformationSceneId, out var prev) && prev.GrantedActionId == 46)
                actor.AxeCount = 0;
            actor.TransformationSceneId = 0;
            actor.AvatarRoleId = 0;
            actor.TransformationExpiresAtMs = 0;
            actor.ShieldActiveUntilMs = Math.Max(actor.ShieldActiveUntilMs, ElapsedMs + PostTransformProtectionMs);
            if (emitWire)
            {
                var rec = new AvatarRecoveryEventData
                {
                    PlayerId = actor.PlayerId,
                    Time = ElapsedMs,
                    PosX = (ushort)Math.Max(0, actor.PosX),
                    PosY = (ushort)Math.Max(0, actor.PosY)
                };
                byte[] body = rec.Marshal();
                OutgoingPeerEvents.Add((ModeEventSchemas.NotifyRecoverAvatar, body));
                OutgoingTcpEvents.Add((ModeEventSchemas.NotifyRecoverAvatar, body));
            }
        }

        private void ResolveActionProjectiles()
        {
            for (int i = Projectiles.Count - 1; i >= 0; i--)
            {
                var proj = Projectiles[i];
                if (proj.ResolveAtMs > ElapsedMs) continue;
                Projectiles.RemoveAt(i);
                if (proj.TargetBombId != 0)
                {
                    var bomb = Bombs.Find(b => b.BombId == proj.TargetBombId && b.Cell.Equals(proj.TargetCell));
                    if (bomb != null) bomb.ExplodeAtMs = ElapsedMs;
                }
            }
        }

        private void ReleaseFieldObjectPassThrough()
        {
            foreach (var obj in FieldObjects)
            {
                if (obj.PassableBy.Count == 0) continue;
                obj.PassableBy.RemoveWhere(pid =>
                {
                    var a = GetActor(pid);
                    return a == null || a.State == ActorLifeState.Dead || !a.CurrentCell.Equals(obj.Cell);
                });
            }
        }

        private void ResolveFieldObjectContacts()
        {
            for (int i = FieldObjects.Count - 1; i >= 0; i--)
            {
                var obj = FieldObjects[i];
                bool consumed = false;
                foreach (var actor in Actors)
                {
                    if (actor.State != ActorLifeState.Active || actor.IsInvulnerable(ElapsedMs)) continue;
                    if (!actor.CurrentCell.Equals(obj.Cell) || obj.PassableBy.Contains(actor.PlayerId)) continue;
                    if (!IsOfflineSoloMode && !actor.IsLocal) continue;

                    if (obj.ActionId == 41) // Capture Trap
                    {
                        if (actor.IsTransformed)
                        {
                            EndTransformation(actor, emitWire: actor.IsLocal);
                        }
                        else
                        {
                            actor.State = ActorLifeState.Trapped;
                            actor.TrappedElapsedMs = 0;
                            actor.Moving = false;
                            StepAudioCues |= BattleAudioCue.Trapped;
                            if (actor.IsLocal)
                            {
                                var hitMsg = new PlayerExplodedMessage
                                {
                                    PlayerId = actor.PlayerId, ClientTime = ElapsedMs,
                                    PosX = (ushort)actor.PosX, PosY = (ushort)actor.PosY, IsAvatar = false
                                };
                                byte[] body = hitMsg.Marshal();
                                OutgoingPeerEvents.Add((GameEventSchemas.PlayerBeExploded, body));
                                OutgoingTcpEvents.Add((GameEventSchemas.PlayerBeExploded, body));
                            }
                        }
                        consumed = true;
                        break;
                    }
                    else if (obj.ActionId == 42) // Banana Peel -> Forced Slide
                    {
                        actor.MovementStatus = MovementStatusKind.ForcedSlide;
                        actor.ForcedSlideDirection = actor.Facing == BattleDirection.None ? BattleDirection.Down : actor.Facing;
                        actor.MovementStatusExpiresAtMs = 0;
                        actor.MoveRemainder = 0;
                        StepAudioCues |= BattleAudioCue.BombKick;
                        consumed = true;
                        break;
                    }
                    else if (obj.ActionId == 43) // Slow Glue -> 10s SpeedRate 2
                    {
                        actor.MovementStatus = MovementStatusKind.Slow;
                        actor.MovementStatusExpiresAtMs = ElapsedMs + MovementStatusDurationMs;
                        actor.MoveRemainder = 0;
                        StepAudioCues |= BattleAudioCue.Damage;
                        consumed = true;
                        break;
                    }
                }
                if (consumed) FieldObjects.RemoveAt(i);
            }
        }

        private void StepLocalMovement(BattleActor local, uint deltaMs, BattleDirection dir)
        {
            bool moving = dir != BattleDirection.None;
            byte effectiveRate = GetEffectiveSpeedRate(local, dir);
            if (moving)
            {
                local.Facing = dir;
                ushort speedPx = SpeedPixelsPerSecondByRate[Math.Min((int)effectiveRate, SpeedPixelsPerSecondByRate.Length - 1)];
                uint num = speedPx * deltaMs + local.MoveRemainder;
                int dist = (int)Math.Min(40u, num / 1000u);
                local.MoveRemainder = num % 1000u;
                int prevX = local.PosX, prevY = local.PosY;
                if (dist > 0) MoveActorPixels(local, dir, dist);
                // Clear ForcedSlide when hitting a wall/bomb
                if (local.MovementStatus == MovementStatusKind.ForcedSlide && dist > 0 && local.PosX == prevX && local.PosY == prevY)
                {
                    local.MovementStatus = MovementStatusKind.None;
                    local.ForcedSlideDirection = BattleDirection.None;
                }
            }
            else
            {
                local.MoveRemainder = 0;
            }
            local.Moving = moving;

            bool stateChanged = moving != _lastSentMoving || (moving && dir != _lastSentMoveDir);
            bool heartbeatDue = moving && (ElapsedMs - _lastMoveBroadcastMs >= 100);
            if (stateChanged || heartbeatDue)
            {
                if (stateChanged) local.MoveSequence++;
                _lastSentMoving = moving;
                _lastSentMoveDir = dir;
                _lastMoveBroadcastMs = ElapsedMs;

                byte walk = ToNativeWalkByte(local.Facing, moving, local.MovementStatus == MovementStatusKind.ForcedSlide);
                var moveMsg = new PlayerMoveMessage
                {
                    PlayerId = local.PlayerId,
                    SeqReply = 0,
                    Entries = new List<PlayerMoveEntryData>
                    {
                        new PlayerMoveEntryData
                        {
                            PlayerId = local.PlayerId,
                            Move = new PlayerMoveSequenceData
                            {
                                Sequence = local.MoveSequence,
                                TimeStamp = ElapsedMs,
                                CurrentPosX = (ushort)Math.Max(0, local.PosX),
                                CurrentPosY = (ushort)Math.Max(0, local.PosY),
                                CornerPosX = 0,
                                CornerPosY = 0,
                                EndPosX = (ushort)Math.Max(0, local.PosX),
                                EndPosY = (ushort)Math.Max(0, local.PosY),
                                WalkAndDirection = walk,
                                Speed = effectiveRate
                            }
                        }
                    }
                };
                OutgoingPeerEvents.Add((GameEventSchemas.PlayerMove, moveMsg.Marshal()));
            }
        }

        private void StepRemoteActorInterpolation(BattleActor actor, uint deltaMs)
        {
            if (!actor.HasRemoteTarget) return;
            int dx = actor.TargetPosX - actor.PosX;
            int dy = actor.TargetPosY - actor.PosY;
            if (dx == 0 && dy == 0) return;

            ushort speedPx = SpeedPixelsPerSecondByRate[Math.Min((int)actor.SpeedRate, SpeedPixelsPerSecondByRate.Length - 1)];
            int maxStep = Math.Max(1, (int)(speedPx * deltaMs / 1000u) + 2);
            if (Math.Abs(dx) <= maxStep) actor.PosX = actor.TargetPosX;
            else actor.PosX += Math.Sign(dx) * maxStep;

            if (Math.Abs(dy) <= maxStep) actor.PosY = actor.TargetPosY;
            else actor.PosY += Math.Sign(dy) * maxStep;
        }

        private void MoveActorPixels(BattleActor actor, BattleDirection dir, int distance)
        {
            int dx = dir == BattleDirection.Right ? 1 : (dir == BattleDirection.Left ? -1 : 0);
            int dy = dir == BattleDirection.Down ? 1 : (dir == BattleDirection.Up ? -1 : 0);

            int startX = actor.PosX;
            int startY = actor.PosY;
            int maxX = Map.Width * CellSizePixels - 1;
            int maxY = Map.Height * CellSizePixels - 1;

            int candX = Clamp(startX + dx * distance, ActorHalfSizePixels, maxX - ActorHalfSizePixels);
            int candY = Clamp(startY + dy * distance, ActorHalfSizePixels, maxY - ActorHalfSizePixels);

            if (IsPositionWalkable(actor, startX, startY, candX, candY, dir))
            {
                actor.PosX = candX;
                actor.PosY = candY;
                return;
            }

            if (actor.IsLocal && (TryKickAdjacentBomb(dir) || TryPushAdjacentMapElement(dir)))
                return;

            // Corner slide assistance (6px tolerance like native FUN_005cfc10)
            if (dx != 0)
            {
                int cellCenterY = (startY / CellSizePixels) * CellSizePixels + CellSizePixels / 2;
                int offset = cellCenterY - startY;
                if (offset != 0 && Math.Abs(offset) <= CornerSlideTolerancePixels * 3)
                {
                    int slideY = startY + Math.Sign(offset) * Math.Min(distance, Math.Abs(offset));
                    if (IsPositionWalkable(actor, startX, startY, startX, slideY, offset > 0 ? BattleDirection.Down : BattleDirection.Up))
                        actor.PosY = slideY;
                }
            }
            else if (dy != 0)
            {
                int cellCenterX = (startX / CellSizePixels) * CellSizePixels + CellSizePixels / 2;
                int offset = cellCenterX - startX;
                if (offset != 0 && Math.Abs(offset) <= CornerSlideTolerancePixels * 3)
                {
                    int slideX = startX + Math.Sign(offset) * Math.Min(distance, Math.Abs(offset));
                    if (IsPositionWalkable(actor, startX, startY, slideX, startY, offset > 0 ? BattleDirection.Right : BattleDirection.Left))
                        actor.PosX = slideX;
                }
            }
        }

        private bool IsPositionWalkable(BattleActor actor, int oldX, int oldY, int newX, int newY, BattleDirection dir)
        {
            bool flyOverStatic = TryGetTransformation(actor.TransformationSceneId, out var td) && td.TraverseStaticTerrain;
            GridCell oldCell = new GridCell(oldY / CellSizePixels, oldX / CellSizePixels);
            int minCol = (newX - ActorHalfSizePixels) / CellSizePixels;
            int maxCol = (newX + ActorHalfSizePixels) / CellSizePixels;
            int minRow = (newY - ActorHalfSizePixels) / CellSizePixels;
            int maxRow = (newY + ActorHalfSizePixels) / CellSizePixels;

            for (int r = minRow; r <= maxRow; r++)
            {
                for (int c = minCol; c <= maxCol; c++)
                {
                    if (!Map.InBounds(r, c)) return false;
                    var cell = Map.GetCell(r, c);
                    if (!flyOverStatic && cell.Collision != CellCollision.Open) return false;

                    // Dynamic bomb collision: allow leaving the bomb cell we are already standing on, block entering another bomb cell
                    var gc = new GridCell(r, c);
                    if (!gc.Equals(oldCell) && HasBombAt(gc)) return false;
                }
            }
            return true;
        }

        private void TryPlaceLocalBomb(BattleActor local)
        {
            GridCell cell = local.CurrentCell;
            if (!Map.InBounds(cell.Row, cell.Col)) return;
            var tile = Map.GetCell(cell.Row, cell.Col);
            if (tile.Collision != CellCollision.Open || tile.MapElementOccupied) return;
            if (HasBombAt(cell)) return;

            int activeOwned = 0;
            foreach (var b in Bombs) if (b.OwnerId == local.PlayerId) activeOwned++;
            if (activeOwned >= GetEffectiveBombCapacity(local)) return;

            uint bombId = _nextBombId++;
            Bombs.Add(new ActiveBomb
            {
                BombId = bombId,
                OwnerId = local.PlayerId,
                Cell = cell,
                Power = local.BombPower,
                PlacedAtMs = ElapsedMs,
                ExplodeAtMs = ElapsedMs + BombFuseMs + 1,
                RemoteOwned = false
            });
            StepAudioCues |= BattleAudioCue.BombPlace;

            var msg = new PlayerUseBombMessage
            {
                PlayerId = local.PlayerId,
                ClientTime = ElapsedMs,
                BombId = bombId,
                Row = (byte)cell.Row,
                Column = (byte)cell.Col,
                Power = local.BombPower,
                Property = (byte)(ElapsedMs < local.SceneFourEffectExpiresAtMs ? 0x40 : 0)
            };
            OutgoingPeerEvents.Add((GameEventSchemas.PlayerUseBomb, msg.Marshal()));
        }

        private void StepDueBombs()
        {
            // Only detonate locally when we are arbitrator or for locally owned bombs
            var due = new Queue<ActiveBomb>();
            var scheduledIds = new HashSet<uint>();
            foreach (var b in Bombs)
            {
                if (b.ExplodeAtMs <= ElapsedMs && (!b.RemoteOwned || LocalPlayerId == ArbitratorPlayerId))
                {
                    due.Enqueue(b);
                    scheduledIds.Add(b.BombId);
                }
            }
            if (due.Count == 0) return;

            var explodeMsg = new BombExplodeMessage { PlayerId = LocalPlayerId, ClientTime = ElapsedMs };
            var destroyedWalls = new HashSet<GridCell>();

            while (due.Count > 0)
            {
                ActiveBomb bomb = due.Dequeue();
                byte rMin = (byte)bomb.Cell.Row, rMax = (byte)bomb.Cell.Row;
                byte cMin = (byte)bomb.Cell.Col, cMax = (byte)bomb.Cell.Col;
                AddFlame(bomb.Cell, bomb.OwnerId);

                int[] dr = { -1, 1, 0, 0 };
                int[] dc = { 0, 0, -1, 1 };
                for (int d = 0; d < 4; d++)
                {
                    for (int step = 1; step <= bomb.Power; step++)
                    {
                        int nr = bomb.Cell.Row + dr[d] * step;
                        int nc = bomb.Cell.Col + dc[d] * step;
                        if (!Map.InBounds(nr, nc)) break;
                        var tile = Map.GetCell(nr, nc);
                        var targetCell = new GridCell(nr, nc);

                        if (tile.Collision == CellCollision.Solid) break;
                        if (tile.Collision == CellCollision.Breakable)
                        {
                            if (destroyedWalls.Add(targetCell))
                            {
                                BreakWall(targetCell, explodeMsg);
                            }
                            break;
                        }

                        AddFlame(targetCell, bomb.OwnerId);
                        if (nr < rMin) rMin = (byte)nr;
                        if (nr > rMax) rMax = (byte)nr;
                        if (nc < cMin) cMin = (byte)nc;
                        if (nc > cMax) cMax = (byte)nc;

                        // Chain detonation
                        foreach (var other in Bombs)
                        {
                            if (!scheduledIds.Contains(other.BombId) && other.Cell.Equals(targetCell))
                            {
                                scheduledIds.Add(other.BombId);
                                due.Enqueue(other);
                            }
                        }
                        if (!tile.FlamePassable) break;
                    }
                }

                explodeMsg.Bombs.Add(new ExplodedBombEntry
                {
                    PlayerId = bomb.OwnerId,
                    ClientTime = bomb.PlacedAtMs,
                    BombId = (byte)Math.Max(1u, bomb.BombId & 0xFFu),
                    Row = (byte)bomb.Cell.Row,
                    Column = (byte)bomb.Cell.Col,
                    RowMin = rMin,
                    RowMax = rMax,
                    ColumnMin = cMin,
                    ColumnMax = cMax
                });
            }

            Bombs.RemoveAll(b => scheduledIds.Contains(b.BombId));
            StepAudioCues |= BattleAudioCue.BombExplode;
            if (destroyedWalls.Count > 0) StepAudioCues |= BattleAudioCue.WoodBreak;

            // Burn exposed ground pickups caught in flames (excluding walls just opened in this tick) -> 0x0FBF
            var burnedItems = new ItemsExplodedEventData { PlayerId = LocalPlayerId, Time = ElapsedMs };
            foreach (var p in Pickups)
            {
                if (p.Hidden || p.Consumed || destroyedWalls.Contains(p.Cell)) continue;
                if (HasFlameAt(p.Cell))
                {
                    p.Consumed = true;
                    burnedItems.Items.Add((p.SceneId, (byte)p.Cell.Row, (byte)p.Cell.Col));
                }
            }
            if (burnedItems.Items.Count > 0 && LocalPlayerId == ArbitratorPlayerId)
            {
                byte[] burnBody = burnedItems.Marshal();
                OutgoingPeerEvents.Add((ModeEventSchemas.NotifyItemExploded, burnBody));
                OutgoingTcpEvents.Add((ModeEventSchemas.NotifyItemExploded, burnBody));
            }

            byte[] body = explodeMsg.Marshal();
            OutgoingPeerEvents.Add((GameEventSchemas.NotifyBombExplode, body));
            OutgoingTcpEvents.Add((GameEventSchemas.NotifyBombExplode, body));
        }

        private void BreakWall(GridCell cell, BombExplodeMessage explodeMsg)
        {
            int idx = cell.Row * Map.Width + cell.Col;
            var tile = Map.Cells[idx];
            if (tile.Durability > 1)
            {
                tile.Durability--;
                Map.Cells[idx] = tile;
                return;
            }
            ushort elemId = (ushort)(tile.MapElementId != 0 ? tile.MapElementId : 1);
            explodeMsg.MapElems.Add(new ExplodedMapElementEntry
            {
                MapElementId = elemId,
                Row = (byte)cell.Row,
                Column = (byte)cell.Col
            });

            tile.Collision = CellCollision.Open;
            tile.FlamePassable = true;
            tile.MapElementOccupied = false;
            tile.Durability = 0;
            tile.VisualSceneElementId = 0;
            Map.Cells[idx] = tile;

            // Reveal any hidden pickup at this cell
            foreach (var p in Pickups)
            {
                if (p.Hidden && !p.Consumed && p.Cell.Equals(cell))
                    p.Hidden = false;
            }
        }

        private void StepCombatAndPickups(uint deltaMs)
        {
            BattleActor local = LocalActor;
            if (local == null) return;

            // 1. Local flame hit check -> strips transformation OR Traps (0x0FA5)
            if (local.State == ActorLifeState.Active && !local.IsInvulnerable(ElapsedMs) && HasFlameAt(local.CurrentCell))
            {
                if (local.IsTransformed)
                {
                    EndTransformation(local, emitWire: true);
                    StepAudioCues |= BattleAudioCue.Damage;
                }
                else
                {
                    local.State = ActorLifeState.Trapped;
                    local.TrappedElapsedMs = 0;
                    local.Moving = false;
                    StepAudioCues |= BattleAudioCue.Trapped;
                    var hitMsg = new PlayerExplodedMessage
                    {
                        PlayerId = local.PlayerId,
                        ClientTime = ElapsedMs,
                        PosX = (ushort)local.PosX,
                        PosY = (ushort)local.PosY,
                        IsAvatar = false
                    };
                    byte[] body = hitMsg.Marshal();
                    OutgoingPeerEvents.Add((GameEventSchemas.PlayerBeExploded, body));
                    OutgoingTcpEvents.Add((GameEventSchemas.PlayerBeExploded, body));
                }
            }

            // 2. Local pickup collection (blocked if Duck transformation cannot collect items)
            bool localCanCollect = !TryGetTransformation(local.TransformationSceneId, out var localTd) || localTd.CanCollectItems;
            if (local.State == ActorLifeState.Active && localCanCollect)
            {
                GridCell lc = local.CurrentCell;
                foreach (var pickup in Pickups)
                {
                    if (pickup.Hidden || pickup.Consumed || !pickup.Cell.Equals(lc)) continue;
                    pickup.Consumed = true;
                    ApplyPickupEffect(local, pickup.SceneId, pickup.Cell);
                    StepAudioCues |= BattleAudioCue.Pickup;
                    var itemMsg = new PlayerItemEventMessage
                    {
                        PlayerId = local.PlayerId,
                        ClientTime = ElapsedMs,
                        ItemId = pickup.SceneId,
                        PosX = (ushort)local.PosX,
                        PosY = (ushort)local.PosY
                    };
                    OutgoingPeerEvents.Add((GameEventSchemas.RequestGetItem, itemMsg.Marshal()));
                    if (LocalPlayerId == ArbitratorPlayerId)
                        OutgoingPeerEvents.Add((GameEventSchemas.NotifyPlayerGetItem, itemMsg.Marshal()));
                }
            }

            // 3. Contact rescue/kill with trapped actors
            if (local.State == ActorLifeState.Active)
            {
                foreach (var other in Actors)
                {
                    if (other.PlayerId == local.PlayerId || other.State != ActorLifeState.Trapped) continue;
                    if (Math.Abs(local.PosX - other.PosX) <= 28 && Math.Abs(local.PosY - other.PosY) <= 28)
                    {
                        var inter = new PlayerInteractionMessage
                        {
                            PlayerId = local.PlayerId,
                            ClientTime = ElapsedMs,
                            DestinationPlayerId = other.PlayerId,
                            PosX = (ushort)other.PosX,
                            PosY = (ushort)other.PosY
                        };
                        if (other.TeamId == local.TeamId)
                        {
                            other.State = ActorLifeState.Active;
                            other.TrappedElapsedMs = 0;
                            local.RescueCount++;
                            StepAudioCues |= BattleAudioCue.Rescued;
                            OutgoingPeerEvents.Add((GameEventSchemas.RequestSavePlayer, inter.Marshal()));
                        }
                        else
                        {
                            other.State = ActorLifeState.Dead;
                            local.KillCount++;
                            StepAudioCues |= BattleAudioCue.Eliminated;
                            if (IsOfflineSoloMode) ScatterDeathDrops(other);
                            OutgoingPeerEvents.Add((GameEventSchemas.RequestKillPlayer, inter.Marshal()));
                            OutgoingTcpEvents.Add((GameEventSchemas.RequestKillPlayer, inter.Marshal()));
                        }
                    }
                }
            }

            // 4. Trapped oxygen countdown -> Death (0x0FA7) + Death Drops
            if (local.State == ActorLifeState.Trapped)
            {
                local.TrappedElapsedMs += deltaMs;
                if (local.TrappedElapsedMs >= local.OxygenMs)
                {
                    local.State = ActorLifeState.Dead;
                    StepAudioCues |= BattleAudioCue.Eliminated;
                    var drops = ScatterDeathDrops(local);
                    var dieMsg = new PlayerDeathMessage
                    {
                        PlayerId = local.PlayerId,
                        ClientTime = ElapsedMs,
                        PosX = (ushort)local.PosX,
                        PosY = (ushort)local.PosY
                    };
                    foreach (var d in drops)
                        dieMsg.Items.Add(new GameItemCell { ItemId = d.SceneId, Row = (byte)d.Cell.Row, Column = (byte)d.Cell.Col });
                    byte[] body = dieMsg.Marshal();
                    OutgoingPeerEvents.Add((GameEventSchemas.NotifyPlayerDie, body));
                    OutgoingTcpEvents.Add((GameEventSchemas.NotifyPlayerDie, body));
                }
            }

            // 5. Boss / PVE NPC flame damage check -> PlayerBeHarmed (0x10F4) & NotifyPlayerDie (0x0FA7)
            for (int i = 0; i < Bosses.Count; i++)
            {
                var boss = Bosses[i];
                if (!boss.Alive || ElapsedMs - boss.LastDamagedMs < 600) continue;
                if (!HasFlameAt(boss.CurrentCell)) continue;
                boss.LastDamagedMs = ElapsedMs;
                const ushort loss = 500;
                boss.Hp = Math.Max(0, boss.Hp - loss);
                StepAudioCues |= BattleAudioCue.Damage;
                var harmed = new EntityHarmedEventData
                {
                    ObjectId = boss.BossId, Time = ElapsedMs,
                    PosX = (ushort)Math.Max(0, boss.PosX), PosY = (ushort)Math.Max(0, boss.PosY),
                    IsAvatar = false, LossHp = loss
                };
                OutgoingTcpEvents.Add((ModeEventSchemas.PlayerBeHarmed, harmed.Marshal()));
                if (boss.Hp <= 0)
                {
                    boss.Alive = false;
                    StepAudioCues |= BattleAudioCue.Eliminated;
                    var die = new PlayerDeathMessage
                    {
                        PlayerId = boss.BossId, ClientTime = ElapsedMs,
                        PosX = (ushort)Math.Max(0, boss.PosX), PosY = (ushort)Math.Max(0, boss.PosY)
                    };
                    OutgoingTcpEvents.Add((GameEventSchemas.NotifyPlayerDie, die.Marshal()));
                }
            }

            // 6. Offline Solo Practice: simulate full combat/pickups/modes/settlement for bots & bosses
            if (IsOfflineSoloMode)
            {
                StepOfflineBotsCombatAndSettlement(deltaMs);
            }
        }

        private List<FieldPickup> ScatterDeathDrops(BattleActor victim)
        {
            var sceneIds = new List<uint>(16);
            for (int i = 0; i < 3; i++)
            {
                int cnt = Clamp(victim.BasicPickupDelta[i], 0, 10);
                for (int k = 0; k < cnt && sceneIds.Count < 16; k++) sceneIds.Add((uint)(i + 1));
                victim.BasicPickupDelta[i] = 0;
            }
            var actionMap = new (uint actionId, uint sceneId)[] { (41, 21), (42, 23), (63, 24), (43, 25), (44, 27), (64, 28) };
            foreach (var (aid, sid) in actionMap)
            {
                int cnt = victim.GetHeldActionCount(aid);
                for (int k = 0; k < cnt && sceneIds.Count < 16; k++) sceneIds.Add(sid);
            }
            victim.TrapCount = victim.BananaCount = victim.GlueCount = victim.DartCount = victim.AxeCount = victim.NeedleCount = victim.RemoteDetonatorCount = 0;
            if (sceneIds.Count == 0) return new List<FieldPickup>();

            var openCells = new List<GridCell>();
            for (int r = 0; r < Map.Height; r++)
            {
                for (int c = 0; c < Map.Width; c++)
                {
                    var gc = new GridCell(r, c);
                    if (Map.GetCell(r, c).Collision != CellCollision.Open) continue;
                    if (HasBombAt(gc) || Pickups.Exists(p => !p.Consumed && p.Cell.Equals(gc))) continue;
                    openCells.Add(gc);
                }
            }
            var drops = new List<FieldPickup>();
            for (int i = 0; i < sceneIds.Count && openCells.Count > 0; i++)
            {
                _deathDropRng += 0x9E3779B97F4A7C15UL;
                ulong z = _deathDropRng;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                int idx = (int)((z ^ (z >> 31)) % (ulong)openCells.Count);
                GridCell cell = openCells[idx];
                openCells.RemoveAt(idx);
                var fp = new FieldPickup { SceneId = sceneIds[i], Cell = cell, Hidden = false };
                Pickups.Add(fp);
                drops.Add(fp);
            }
            return drops;
        }

        private void StepOfflineBotsCombatAndSettlement(uint deltaMs)
        {
            foreach (var actor in Actors)
            {
                if (actor.IsLocal || actor.State == ActorLifeState.Dead) continue;
                if (actor.State == ActorLifeState.Active && !actor.IsInvulnerable(ElapsedMs) && HasFlameAt(actor.CurrentCell))
                {
                    actor.State = ActorLifeState.Trapped;
                    actor.TrappedElapsedMs = 0;
                    actor.Moving = false;
                }
                if (actor.State == ActorLifeState.Active)
                {
                    GridCell ac = actor.CurrentCell;
                    foreach (var p in Pickups)
                    {
                        if (!p.Hidden && !p.Consumed && p.Cell.Equals(ac))
                        {
                            p.Consumed = true;
                            ApplyPickupEffect(actor, p.SceneId, p.Cell);
                        }
                    }
                    foreach (var other in Actors)
                    {
                        if (other.PlayerId == actor.PlayerId || other.State != ActorLifeState.Trapped) continue;
                        if (Math.Abs(actor.PosX - other.PosX) <= 28 && Math.Abs(actor.PosY - other.PosY) <= 28)
                        {
                            if (other.TeamId == actor.TeamId) { other.State = ActorLifeState.Active; other.TrappedElapsedMs = 0; actor.RescueCount++; }
                            else { other.State = ActorLifeState.Dead; actor.KillCount++; ScatterDeathDrops(other); }
                        }
                    }
                }
                else if (actor.State == ActorLifeState.Trapped)
                {
                    actor.TrappedElapsedMs += deltaMs;
                    if (actor.TrappedElapsedMs >= actor.OxygenMs) { actor.State = ActorLifeState.Dead; ScatterDeathDrops(actor); }
                }
            }

            // Offline Boss Raid AI: patrol & cast periodic cross-flame skills
            if (OfflineMode == OfflinePracticeMode.BossRaid)
            {
                if (ElapsedMs >= _offlineBossSkillNextMs)
                {
                    _offlineBossSkillNextMs = ElapsedMs + 5500;
                    foreach (var b in Bosses)
                    {
                        if (!b.Alive) continue;
                        b.SpeechText = "灵焰焚天！";
                        b.SpeechExpireMs = ElapsedMs + 2400;
                        GridCell bc = b.CurrentCell;
                        for (int s = -2; s <= 2; s++)
                        {
                            var hCell = new GridCell(bc.Row, bc.Col + s);
                            var vCell = new GridCell(bc.Row + s, bc.Col);
                            if (Map.InBounds(hCell.Row, hCell.Col) && Map.GetCell(hCell.Row, hCell.Col).Collision == CellCollision.Open) AddFlame(hCell, b.BossId);
                            if (Map.InBounds(vCell.Row, vCell.Col) && Map.GetCell(vCell.Row, vCell.Col).Collision == CellCollision.Open) AddFlame(vCell, b.BossId);
                        }
                        StepAudioCues |= BattleAudioCue.Wave;
                    }
                }
                if (AllBossesDefeated || (LocalActor != null && LocalActor.State == ActorLifeState.Dead) || ElapsedMs >= RoundDurationMs)
                {
                    ConcludeOfflineMatch(AllBossesDefeated ? (byte)1 : (byte)9);
                    return;
                }
                return;
            }

            // Offline Bun Capture mode: grab center bun -> return to corner base to score (first to 3 wins)
            if (OfflineMode == OfflinePracticeMode.BunCapture)
            {
                if (BunCarrierPlayerId != 0)
                {
                    var carrier = GetActor(BunCarrierPlayerId);
                    if (carrier == null || carrier.State != ActorLifeState.Active)
                    {
                        BunCarrierPlayerId = 0;
                    }
                    else
                    {
                        GridCell homeBase = carrier.TeamId == 1 ? new GridCell(0, 0) : new GridCell(Map.Height - 1, Map.Width - 1);
                        if (Math.Abs(carrier.CurrentCell.Row - homeBase.Row) + Math.Abs(carrier.CurrentCell.Col - homeBase.Col) <= 2)
                        {
                            BunCarrierPlayerId = 0;
                            TeamBunScores.TryGetValue(carrier.TeamId, out int sc);
                            TeamBunScores[carrier.TeamId] = sc + 1;
                            StepAudioCues |= BattleAudioCue.BunDelivery;
                            if (sc + 1 >= 3) { ConcludeOfflineMatch(carrier.TeamId); return; }
                        }
                    }
                }
                else
                {
                    foreach (var a in Actors)
                    {
                        if (a.State == ActorLifeState.Active && a.CurrentCell.Equals(OfflineBunCell))
                        {
                            BunCarrierPlayerId = a.PlayerId;
                            BunTeamId = a.TeamId;
                            StepAudioCues |= BattleAudioCue.Pickup;
                            break;
                        }
                    }
                }
            }

            // Offline Sculpture/Base Defense: flames near top-left (Team 1) or bottom-right (Team 2) damage enemy base
            if (OfflineMode == OfflinePracticeMode.SculptureDefense)
            {
                var base1 = new GridCell(1, 1);
                var base2 = new GridCell(Map.Height - 2, Map.Width - 2);
                if (HasFlameAt(base1) && TankBaseHpByTeam.TryGetValue(1, out int hp1) && hp1 > 0)
                {
                    TankBaseHpByTeam[1] = Math.Max(0, hp1 - 10);
                    StepAudioCues |= BattleAudioCue.Damage;
                }
                if (HasFlameAt(base2) && TankBaseHpByTeam.TryGetValue(2, out int hp2) && hp2 > 0)
                {
                    TankBaseHpByTeam[2] = Math.Max(0, hp2 - 10);
                    StepAudioCues |= BattleAudioCue.Damage;
                }
                if (TankBaseHpByTeam.TryGetValue(1, out int c1) && c1 <= 0) { ConcludeOfflineMatch(2); return; }
                if (TankBaseHpByTeam.TryGetValue(2, out int c2) && c2 <= 0) { ConcludeOfflineMatch(1); return; }
            }

            // Determine match end if <= 1 team remains alive or time elapsed
            var aliveTeams = new HashSet<byte>();
            foreach (var a in Actors) if (a.State != ActorLifeState.Dead) aliveTeams.Add(a.TeamId);
            if (aliveTeams.Count <= 1 || ElapsedMs >= RoundDurationMs)
            {
                byte winTeam = 0;
                if (aliveTeams.Count == 1)
                    foreach (var t in aliveTeams) winTeam = t;
                ConcludeOfflineMatch(winTeam);
            }
        }

        private void ConcludeOfflineMatch(byte winTeam)
        {
            IsFinished = true;
            var over = new GameOverData { Time = ElapsedMs, GameMode = (byte)OfflineMode };
            foreach (var a in Actors)
            {
                GameResultCode code = winTeam == 0
                    ? GameResultCode.Draw
                    : (a.TeamId == winTeam ? GameResultCode.Win : GameResultCode.Loss);
                var entry = new GameResultEntry
                {
                    PlayerId = a.PlayerId,
                    Result = code,
                    Point = (uint)(code == GameResultCode.Win ? 200 : (code == GameResultCode.Draw ? 50 : 20)),
                    Remark = (uint)a.KillCount
                };
                entry.Fields.Add(((uint)a.KillCount, (uint)(a.KillCount * 100)));
                entry.Fields.Add(((uint)a.RescueCount, (uint)(a.RescueCount * 80)));
                entry.Fields.Add((a.MatchSugar, a.MatchSugar));
                over.Results.Add(entry);
            }
            OfflineSettlement = over;
        }

        public void ApplyRemoteMessage(ushort senderPlayerId, uint schemaId, byte[] body)
        {
            if (senderPlayerId == LocalPlayerId) return;

            switch (schemaId)
            {
                case GameEventSchemas.PlayerMove:
                    var move = PlayerMoveMessage.Parse(body);
                    foreach (var entry in move.Entries)
                    {
                        BattleActor actor = GetActor(entry.PlayerId);
                        if (actor == null || actor.IsLocal) continue;
                        actor.PosX = entry.Move.CurrentPosX;
                        actor.PosY = entry.Move.CurrentPosY;
                        actor.TargetPosX = entry.Move.EndPosX;
                        actor.TargetPosY = entry.Move.EndPosY;
                        actor.HasRemoteTarget = true;
                        actor.Moving = (entry.Move.WalkAndDirection & 0x10) != 0;
                        actor.Facing = FromNativeWalkByte(entry.Move.WalkAndDirection);
                        if (entry.Move.Speed > 0 && entry.Move.Speed < SpeedPixelsPerSecondByRate.Length)
                            actor.SpeedRate = entry.Move.Speed;
                    }
                    break;

                case GameEventSchemas.PlayerUseBomb:
                case GameEventSchemas.NotifyPlayerUseBomb:
                    var placed = PlayerUseBombMessage.Parse(body);
                    var key = (placed.PlayerId, placed.ClientTime, placed.Row, placed.Column);
                    if (!_seenRemoteBombs.Add(key)) return;
                    Bombs.Add(new ActiveBomb
                    {
                        BombId = _nextBombId++,
                        OwnerId = placed.PlayerId,
                        Cell = new GridCell(placed.Row, placed.Column),
                        Power = (byte)Math.Max(1, (int)placed.Power),
                        PlacedAtMs = placed.ClientTime,
                        ExplodeAtMs = Math.Max(ElapsedMs, placed.ClientTime) + BombFuseMs + 1,
                        RemoteOwned = true
                    });
                    break;

                case GameEventSchemas.NotifyBombExplode:
                    var exploded = BombExplodeMessage.Parse(body);
                    foreach (var b in exploded.Bombs)
                    {
                        Bombs.RemoveAll(x => x.Cell.Row == b.Row && x.Cell.Col == b.Column);
                        for (int r = b.RowMin; r <= b.RowMax; r++) AddFlame(new GridCell(r, b.Column), b.PlayerId);
                        for (int c = b.ColumnMin; c <= b.ColumnMax; c++) AddFlame(new GridCell(b.Row, c), b.PlayerId);
                    }
                    foreach (var me in exploded.MapElems)
                    {
                        if (!Map.InBounds(me.Row, me.Column)) continue;
                        int idx = me.Row * Map.Width + me.Column;
                        var tile = Map.Cells[idx];
                        tile.Collision = CellCollision.Open;
                        tile.FlamePassable = true;
                        tile.MapElementOccupied = false;
                        tile.Durability = 0;
                        tile.VisualSceneElementId = 0;
                        Map.Cells[idx] = tile;
                        foreach (var p in Pickups)
                            if (p.Hidden && !p.Consumed && p.Cell.Row == me.Row && p.Cell.Col == me.Column)
                                p.Hidden = false;
                    }
                    break;

                case GameEventSchemas.PlayerBeExploded:
                case GameEventSchemas.NotifyPlayerExploded:
                    var hit = PlayerExplodedMessage.Parse(body);
                    var hitActor = GetActor(hit.PlayerId);
                    if (hitActor != null && hitActor.State == ActorLifeState.Active)
                    {
                        hitActor.State = ActorLifeState.Trapped;
                        hitActor.TrappedElapsedMs = 0;
                        hitActor.PosX = hit.PosX;
                        hitActor.PosY = hit.PosY;
                    }
                    break;

                case GameEventSchemas.NotifyPlayerSaved:
                case GameEventSchemas.RequestSavePlayer:
                    var saved = PlayerInteractionMessage.Parse(body);
                    var savedActor = GetActor(saved.DestinationPlayerId);
                    if (savedActor != null && savedActor.State == ActorLifeState.Trapped)
                    {
                        savedActor.State = ActorLifeState.Active;
                        savedActor.TrappedElapsedMs = 0;
                    }
                    break;

                case GameEventSchemas.RequestKillPlayer:
                case GameEventSchemas.NotifyPlayerKilled:
                    var killed = PlayerInteractionMessage.Parse(body.Length >= 12 ? SubSlice(body, 0, 12) : body);
                    var deadActor = GetActor(killed.DestinationPlayerId);
                    if (deadActor != null) deadActor.State = ActorLifeState.Dead;
                    break;

                case GameEventSchemas.NotifyPlayerDie:
                    var died = PlayerDeathMessage.Parse(body);
                    var dActor = GetActor(died.PlayerId);
                    if (dActor != null) dActor.State = ActorLifeState.Dead;
                    foreach (var drop in died.Items)
                    {
                        var dc = new GridCell(drop.Row, drop.Column);
                        if (Map.InBounds(dc.Row, dc.Col) && drop.ItemId != 0)
                        {
                            Pickups.RemoveAll(p => !p.Consumed && p.Cell.Equals(dc));
                            Pickups.Add(new FieldPickup { SceneId = drop.ItemId, Cell = dc, Hidden = false });
                        }
                    }
                    break;

                case GameEventSchemas.NotifyPlayerGetItem:
                    var got = PlayerItemEventMessage.Parse(body);
                    var getter = GetActor(got.PlayerId);
                    if (getter != null)
                    {
                        GridCell gc = new GridCell(got.PosY / CellSizePixels, got.PosX / CellSizePixels);
                        foreach (var p in Pickups)
                        {
                            if (!p.Consumed && p.Cell.Equals(gc) && p.SceneId == got.ItemId)
                            {
                                p.Consumed = true;
                                break;
                            }
                        }
                        ApplyPickupEffect(getter, got.ItemId, gc);
                    }
                    break;

                case GameEventSchemas.NotifyGameOver:
                    IsFinished = true;
                    break;

                case ExtendedGameEventSchemas.RequestUseItem:
                case ExtendedGameEventSchemas.NotifyPlayerUseItem:
                {
                    var use = WorldUseItemEventData.Parse(body);
                    var uActor = GetActor(use.PlayerId);
                    if (uActor != null)
                    {
                        if (use.ItemId == 63 && uActor.State == ActorLifeState.Trapped)
                        {
                            uActor.State = ActorLifeState.Active;
                            uActor.TrappedElapsedMs = 0;
                            uActor.ShieldActiveUntilMs = ElapsedMs + 800;
                        }
                        else if (use.ItemId == 64)
                        {
                            foreach (var b in Bombs)
                                if (b.OwnerId == uActor.PlayerId && b.ExplodeAtMs > ElapsedMs)
                                    b.ExplodeAtMs = ElapsedMs;
                        }
                        else if (use.ItemId == 41 || use.ItemId == 42 || use.ItemId == 43)
                        {
                            var cell = new GridCell(use.PosY / CellSizePixels, use.PosX / CellSizePixels);
                            if (Map.InBounds(cell.Row, cell.Col))
                                PlaceFieldObject((byte)use.ItemId, uActor.PlayerId, cell);
                        }
                        else if (use.ItemId == 44 || use.ItemId == 46)
                        {
                            if (TryFindProjectileTarget(uActor, out GridCell tCell, out uint tBomb))
                            {
                                Projectiles.Add(new BattleProjectile
                                {
                                    Id = _nextProjectileId++, ActionId = (byte)use.ItemId, OwnerId = uActor.PlayerId,
                                    TargetBombId = tBomb, StartCell = uActor.CurrentCell, TargetCell = tCell,
                                    CreatedAtMs = ElapsedMs, ResolveAtMs = ElapsedMs + ActionProjectileFlightMs
                                });
                            }
                        }
                    }
                    break;
                }

                case ExtendedGameEventSchemas.RequestMoveBomb:
                case ExtendedGameEventSchemas.NotifyPlayerMoveBomb:
                {
                    var mb = MoveBombEventData.Parse(body);
                    var bomb = Bombs.Find(b => b.Cell.Row == mb.FromRow && b.Cell.Col == mb.FromCol);
                    if (bomb != null && Map.InBounds(mb.ToRow, mb.ToCol))
                        bomb.Cell = new GridCell(mb.ToRow, mb.ToCol);
                    break;
                }

                case ExtendedGameEventSchemas.RequestMoveMapElement:
                {
                    var me = MoveMapElementEventData.ParseRequest(body);
                    var (dr, dc) = DirectionDelta(FromNativeDirectionCode(me.Direction));
                    ApplyMapElementSlide(new GridCell(me.Row, me.Col), new GridCell(me.Row + dr, me.Col + dc));
                    break;
                }

                case ExtendedGameEventSchemas.NotifyMapElementMoved:
                {
                    var me = MoveMapElementEventData.ParseMoved(body);
                    var (dr, dc) = DirectionDelta(FromNativeDirectionCode(me.Direction));
                    ApplyMapElementSlide(new GridCell(me.Row, me.Col), new GridCell(me.Row + dr, me.Col + dc));
                    break;
                }

                case ExtendedGameEventSchemas.NotifyPlayerRelive:
                {
                    var rel = PlayerReliveEventData.Parse(body);
                    var rActor = GetActor(rel.PlayerId);
                    if (rActor != null)
                    {
                        rActor.State = ActorLifeState.Active;
                        rActor.TrappedElapsedMs = 0;
                        rActor.PosX = rel.Col * CellSizePixels + CellSizePixels / 2;
                        rActor.PosY = rel.Row * CellSizePixels + CellSizePixels / 2;
                        rActor.TargetPosX = rActor.PosX;
                        rActor.TargetPosY = rActor.PosY;
                        rActor.ShieldActiveUntilMs = ElapsedMs + 2000;
                    }
                    break;
                }

                case GameEventSchemas.NotifyDispatchItem:
                {
                    var disp = DispatchItemEventData.Parse(body);
                    foreach (var it in disp.Items)
                    {
                        if (Map.InBounds(it.Row, it.Col))
                            Pickups.Add(new FieldPickup { SceneId = it.ItemId != 0 ? it.ItemId : it.ItemType, Cell = new GridCell(it.Row, it.Col), Hidden = false });
                    }
                    break;
                }

                case ExtendedGameEventSchemas.NotifyUseEmotion:
                {
                    var emo = UseEmotionEventData.Parse(body);
                    if (_uinToPlayerId.TryGetValue(emo.Uin, out ushort pid))
                    {
                        var eActor = GetActor(pid);
                        if (eActor != null)
                        {
                            eActor.ActiveEmotionId = emo.EmotionId;
                            eActor.EmotionExpireAtMs = ElapsedMs + 3000;
                        }
                    }
                    break;
                }

                case ModeEventSchemas.CreateNpcBoss:
                {
                    var spawned = CreateNpcBossEventData.Parse(body);
                    foreach (var b in spawned.Bosses)
                    {
                        int r = b.Row > 0 && b.Row < Map.Height ? b.Row : Map.Height / 2;
                        int c = b.Col > 0 && b.Col < Map.Width ? b.Col : Map.Width / 2;
                        int maxHp = b.Hp > 0 ? b.Hp : 3000;
                        Bosses.RemoveAll(x => x.BossId == b.BossId);
                        Bosses.Add(new BattleBossEntity
                        {
                            BossId = b.BossId, RoleId = b.RoleId, TeamId = b.TeamId, Label = b.Label,
                            IsPve = false, Hp = maxHp, MaxHp = maxHp, Alive = true,
                            PosX = c * CellSizePixels + CellSizePixels / 2,
                            PosY = r * CellSizePixels + CellSizePixels / 2
                        });
                    }
                    break;
                }

                case ModeEventSchemas.CreatePveNpcBoss:
                {
                    var pve = CreatePveNpcBossEventData.Parse(body);
                    ushort nextObj = 30001;
                    int slot = 0;
                    foreach (var g in pve.Groups)
                    {
                        for (int k = 0; k < g.BossCount; k++)
                        {
                            int r = Math.Min(Map.Height - 2, 2 + (slot * 2) % Math.Max(1, Map.Height - 3));
                            int c = Math.Min(Map.Width - 2, 2 + (slot * 3) % Math.Max(1, Map.Width - 3));
                            slot++;
                            ushort id = nextObj++;
                            Bosses.Add(new BattleBossEntity
                            {
                                BossId = id, RoleId = (byte)(1 + (g.BossId % 4)), TeamId = 9, Label = g.BossId,
                                IsPve = true, Hp = 1500, MaxHp = 1500, Alive = true,
                                PosX = c * CellSizePixels + CellSizePixels / 2,
                                PosY = r * CellSizePixels + CellSizePixels / 2
                            });
                        }
                    }
                    break;
                }

                case ModeEventSchemas.PlayerBeHarmed:
                {
                    var h = EntityHarmedEventData.Parse(body);
                    var boss = Bosses.Find(x => x.BossId == h.ObjectId);
                    if (boss != null && boss.Alive)
                    {
                        boss.Hp = Math.Max(0, boss.Hp - (h.LossHp > 0 ? h.LossHp : 500));
                        if (boss.Hp <= 0) boss.Alive = false;
                    }
                    break;
                }

                case ModeEventSchemas.NotifyNpcUseSkill:
                {
                    var sk = NpcUseSkillEventData.Parse(body);
                    foreach (var it in sk.Items)
                    {
                        if (Map.InBounds(it.row, it.col))
                            AddFlame(new GridCell(it.row, it.col), sk.ObjectId);
                    }
                    break;
                }

                case ModeEventSchemas.NotifyNpcTalk:
                {
                    var talk = NpcTalkEventData.Parse(body);
                    var boss = Bosses.Find(x => x.BossId == talk.ObjectId);
                    if (boss != null) { boss.SpeechText = talk.Words; boss.SpeechExpireMs = ElapsedMs + 3500; }
                    break;
                }

                case ModeEventSchemas.RequestGetBun:
                case ModeEventSchemas.NotifyPlayerGetBun:
                {
                    var bun = BunActionEventData.Parse(body);
                    BunCarrierPlayerId = bun.PlayerId;
                    BunTeamId = bun.BunTeamId;
                    break;
                }

                case ModeEventSchemas.RequestPutBun:
                case ModeEventSchemas.NotifyPlayerPutBun:
                {
                    var bun = BunActionEventData.Parse(body);
                    if (BunCarrierPlayerId == bun.PlayerId) BunCarrierPlayerId = 0;
                    var actor = GetActor(bun.PlayerId);
                    if (actor != null)
                    {
                        TeamBunScores.TryGetValue(actor.TeamId, out int cur);
                        TeamBunScores[actor.TeamId] = cur + 1;
                    }
                    break;
                }

                case ModeEventSchemas.NotifyPreparedUseProp:
                {
                    var prop = PreparedUsePropEventData.Parse(body);
                    var pActor = GetActor(prop.PlayerId);
                    if (pActor != null)
                    {
                        if (pActor.State == ActorLifeState.Dead)
                        {
                            pActor.State = ActorLifeState.Active;
                            pActor.ShieldActiveUntilMs = ElapsedMs + 2000;
                            StepAudioCues |= BattleAudioCue.Respawn;
                        }
                        else if (pActor.State == ActorLifeState.Trapped)
                        {
                            pActor.State = ActorLifeState.Active;
                            pActor.TrappedElapsedMs = 0;
                            StepAudioCues |= BattleAudioCue.Rescued;
                        }
                        else
                        {
                            pActor.ShieldActiveUntilMs = ElapsedMs + 2500;
                        }
                    }
                    break;
                }

                case ModeEventSchemas.NotifyChangeArbitrator:
                {
                    var arb = ChangeArbitratorEventData.Parse(body);
                    if (arb.NewArbitratorPlayerId != 0) ArbitratorPlayerId = arb.NewArbitratorPlayerId;
                    break;
                }

                case ModeEventSchemas.NotifyPlayerLeave:
                {
                    var lv = PlayerLeaveBattleEventData.Parse(body);
                    var leaving = GetActor(lv.PlayerId);
                    if (leaving != null) leaving.State = ActorLifeState.Dead;
                    if (BunCarrierPlayerId == lv.PlayerId) BunCarrierPlayerId = 0;
                    break;
                }

                case ModeEventSchemas.NotifyItemExploded:
                {
                    var ie = ItemsExplodedEventData.Parse(body);
                    foreach (var it in ie.Items)
                    {
                        var gc = new GridCell(it.row, it.col);
                        foreach (var p in Pickups)
                            if (!p.Consumed && p.Cell.Equals(gc) && (it.itemId == 0 || p.SceneId == it.itemId))
                                p.Consumed = true;
                    }
                    break;
                }

                case ModeEventSchemas.NotifyRecoverAvatar:
                {
                    var rec = AvatarRecoveryEventData.Parse(body);
                    var rActor = GetActor(rec.PlayerId);
                    if (rActor != null) EndTransformation(rActor, emitWire: false);
                    break;
                }

                case ModeEventSchemas.NotifyDispatchBomb:
                {
                    var db = DispatchBombEventData.Parse(body);
                    foreach (var bm in db.Bombs)
                    {
                        var cell = new GridCell(bm.row, bm.col);
                        if (Map.InBounds(cell.Row, cell.Col) && !HasBombAt(cell))
                        {
                            Bombs.Add(new ActiveBomb
                            {
                                BombId = _nextBombId++, OwnerId = db.PlayerId, Cell = cell, Power = 2,
                                PlacedAtMs = db.Time, ExplodeAtMs = ElapsedMs + BombFuseMs, RemoteOwned = true
                            });
                        }
                    }
                    foreach (var it in db.Items)
                    {
                        var cell = new GridCell(it.row, it.col);
                        if (Map.InBounds(cell.Row, cell.Col) && it.itemId != 0)
                            Pickups.Add(new FieldPickup { SceneId = it.itemId, Cell = cell, Hidden = false });
                    }
                    break;
                }

                case ModeEventSchemas.RequestEatBomb:
                case ModeEventSchemas.NotifyPlayerEatBomb:
                {
                    var eat = EatBombActionData.Parse(body);
                    Bombs.RemoveAll(b => b.Cell.Row == eat.BombRow && b.Cell.Col == eat.BombCol);
                    break;
                }

                case ModeEventSchemas.PlayerThrowBomb:
                {
                    if (PassiveFunctionProjectionData.IsProjectionBody(body))
                    {
                        var proj = PassiveFunctionProjectionData.Parse(body);
                        if (proj.Reset)
                            foreach (var a in Actors) a.PassiveEffects = PassiveFunctionEffectMask.None;
                        var target = GetActor(proj.PlayerId);
                        if (target != null) target.PassiveEffects |= proj.Effects;
                    }
                    else if (body.Length >= 13)
                    {
                        var kick = KickBombActionData.Parse(body);
                        var bomb = Bombs.Find(b => b.Cell.Row == kick.FromRow && b.Cell.Col == kick.FromCol);
                        if (bomb != null && Map.InBounds(kick.DestRow, kick.DestCol))
                        {
                            bomb.Cell = new GridCell(kick.DestRow, kick.DestCol);
                            StepAudioCues |= BattleAudioCue.BombKick;
                        }
                    }
                    break;
                }

                case ModeEventSchemas.NotifyProduceItem:
                {
                    var prod = ProduceItemEventData.Parse(body);
                    var cell = new GridCell(prod.Row, prod.Col);
                    if (Map.InBounds(cell.Row, cell.Col) && prod.ItemId != 0)
                        Pickups.Add(new FieldPickup { SceneId = prod.ItemId, Cell = cell, Hidden = false });
                    break;
                }

                case ModeEventSchemas.NotifyNpcDropItem:
                {
                    var drop = NpcDropItemEventData.Parse(body);
                    foreach (var it in drop.Items)
                    {
                        var cell = new GridCell(it.row, it.col);
                        if (Map.InBounds(cell.Row, cell.Col) && it.itemId != 0)
                            Pickups.Add(new FieldPickup { SceneId = it.itemId, Cell = cell, Hidden = false });
                    }
                    break;
                }

                case ModeEventSchemas.RequestTankBaseHp:
                {
                    var hp = TankBaseHpEventData.Parse(body);
                    byte tid = (byte)hp.TeamId;
                    TankBaseHpByTeam.TryGetValue(tid, out int cur);
                    if (cur == 0 && hp.ChangeHp < 0) cur = 1000;
                    TankBaseHpByTeam[tid] = Math.Max(0, cur + hp.ChangeHp);
                    StepAudioCues |= BattleAudioCue.Damage;
                    break;
                }

                case ModeEventSchemas.NotifyPlayerTrapped:
                {
                    var ts = PlayerTimedStateEventData.Parse(body);
                    var ta = GetActor(ts.PlayerId);
                    if (ta != null && ta.State == ActorLifeState.Active)
                    {
                        ta.State = ActorLifeState.Trapped;
                        ta.TrappedElapsedMs = 0;
                    }
                    break;
                }
            }
        }

        private void ApplyPickupEffect(BattleActor actor, uint sceneId, GridCell pickupCell)
        {
            // 1. Native transformations (101, 104, 107, 108, 109, 110, 114, 115)
            if (TryGetTransformation(sceneId, out var td))
            {
                if (TryGetTransformation(actor.TransformationSceneId, out var prev) && prev.GrantedActionId == 46)
                    actor.AxeCount = 0;
                actor.ShieldActiveUntilMs = 0;
                actor.TransformationSceneId = sceneId;
                actor.AvatarRoleId = td.AvatarRoleId;
                actor.TransformationExpiresAtMs = ElapsedMs + td.DurationMs;
                if (td.GrantedActionId != 0)
                    actor.GrantHeldAction(td.GrantedActionId, td.GrantedActionCount);
                return;
            }

            // 2. Native battle action item pickups (21/23/24/25/27/28)
            switch (sceneId)
            {
                case 1:
                    actor.BasicPickupDelta[0]++;
                    actor.BombCapacity = (byte)Math.Min(actor.MaxBombCapacity, actor.BombCapacity + 1);
                    return;
                case 2:
                    actor.BasicPickupDelta[1]++;
                    actor.BombPower = (byte)Math.Min(actor.MaxBombPower, actor.BombPower + 1);
                    return;
                case 3:
                    actor.BasicPickupDelta[2]++;
                    actor.SpeedRate = (byte)Math.Min(actor.MaxSpeedRate, actor.SpeedRate + 1);
                    return;
                case 4: actor.SceneFourEffectExpiresAtMs = Math.Max(actor.SceneFourEffectExpiresAtMs, ElapsedMs + 30_000u); return;
                case 5:
                {
                    // Deterministic client+0x1CD489 random +/-1 attribute
                    var rng = new NativeMapRng((uint)(pickupCell.Row * 15 + pickupCell.Col));
                    uint attr = rng.Next() % 3;
                    int delta = (rng.Next() & 1) == 1 ? 1 : -1;
                    if (attr == 0) { actor.BasicPickupDelta[0] += delta; actor.BombCapacity = (byte)Clamp(actor.BombCapacity + delta, 1, actor.MaxBombCapacity); }
                    else if (attr == 1) { actor.BasicPickupDelta[1] += delta; actor.BombPower = (byte)Clamp(actor.BombPower + delta, 1, actor.MaxBombPower); }
                    else { actor.BasicPickupDelta[2] += delta; actor.SpeedRate = (byte)Clamp(actor.SpeedRate + delta, 1, actor.MaxSpeedRate); }
                    return;
                }
                case 6: actor.BombCapacity = actor.MaxBombCapacity; return;
                case 7: actor.BombPower = actor.MaxBombPower; return;
                case 8: actor.SpeedRate = actor.MaxSpeedRate; return;
                case 21: actor.GrantHeldAction(41, 1); return; // Trap
                case 23: actor.GrantHeldAction(42, 1); return; // Banana Peel
                case 24: actor.GrantHeldAction(63, 1); return; // Needle
                case 25: actor.GrantHeldAction(43, 3); return; // Slow Glue x3
                case 26:
                case 62: actor.OxygenMs += 1000; return;       // Oxygen bottle
                case 27: actor.GrantHeldAction(44, 2); return; // Dart x2
                case 28: actor.GrantHeldAction(64, 1); return; // Remote Detonator
                case 45: actor.CanKickBomb = true; return;     // Kick Shoes
                case 47: actor.MovementStatus = MovementStatusKind.Fast; actor.MovementStatusExpiresAtMs = ElapsedMs + MovementStatusDurationMs; return;
                case 61: actor.HiddenPickupReachExpiresAtMs = Math.Max(actor.HiddenPickupReachExpiresAtMs, ElapsedMs + 10_000u); return;
                case 63: actor.GrantHeldAction(63, 1); return;
                case 64: actor.GrantHeldAction(64, 1); return;
                case 81: actor.MatchSugar += 50; return;
                case 82: actor.MatchSugar += 100; return;
                case 83: actor.MatchSugar += 200; return;
                case 84:
                case 94: actor.MatchSugar += 500; return;
                case 85:
                case 93: actor.MatchSugar += 1000; return;
                case 150: actor.TreasureScore += 1; return;
                case 151: actor.TreasureScore += 2; return;
                case 152: actor.TreasureScore += 3; return;
            }
        }

        private static (int dr, int dc) DirectionDelta(BattleDirection dir)
        {
            switch (dir)
            {
                case BattleDirection.Up: return (-1, 0);
                case BattleDirection.Down: return (1, 0);
                case BattleDirection.Left: return (0, -1);
                case BattleDirection.Right: return (0, 1);
                default: return (0, 0);
            }
        }

        private static byte ToNativeDirectionCode(BattleDirection dir)
        {
            switch (dir)
            {
                case BattleDirection.Right: return 0;
                case BattleDirection.Up: return 1;
                case BattleDirection.Left: return 2;
                default: return 3;
            }
        }

        private static BattleDirection FromNativeDirectionCode(byte code)
        {
            switch (code & 0x03)
            {
                case 0: return BattleDirection.Right;
                case 1: return BattleDirection.Up;
                case 2: return BattleDirection.Left;
                default: return BattleDirection.Down;
            }
        }

        private void AddFlame(GridCell cell, ushort ownerId)
        {
            Flames.Add(new ActiveFlame { Cell = cell, OwnerId = ownerId, ExpireAtMs = ElapsedMs + FlameDurationMs });
        }

        private bool HasBombAt(GridCell cell)
        {
            for (int i = 0; i < Bombs.Count; i++)
                if (Bombs[i].Cell.Equals(cell)) return true;
            return false;
        }

        private bool HasFlameAt(GridCell cell)
        {
            for (int i = 0; i < Flames.Count; i++)
                if (Flames[i].Cell.Equals(cell)) return true;
            return false;
        }

        public static byte ToNativeWalkByte(BattleDirection dir, bool moving, bool forced)
        {
            byte d;
            switch (dir)
            {
                case BattleDirection.Right: d = 0; break;
                case BattleDirection.Up: d = 1; break;
                case BattleDirection.Left: d = 2; break;
                default: d = 3; break;
            }
            if (moving) d |= 0x10;
            if (forced) d |= 0x20;
            return d;
        }

        public static BattleDirection FromNativeWalkByte(byte walk)
        {
            switch (walk & 0x03)
            {
                case 0: return BattleDirection.Right;
                case 1: return BattleDirection.Up;
                case 2: return BattleDirection.Left;
                default: return BattleDirection.Down;
            }
        }

        private static int Clamp(int v, int min, int max) => v < min ? min : (v > max ? max : v);

        private static byte[] SubSlice(byte[] src, int off, int len)
        {
            byte[] dst = new byte[len];
            Buffer.BlockCopy(src, off, dst, 0, len);
            return dst;
        }
    }
}
