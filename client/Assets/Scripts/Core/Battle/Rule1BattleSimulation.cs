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

        // Remote interpolation target
        public int TargetPosX;
        public int TargetPosY;
        public bool HasRemoteTarget;

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

        // Outgoing wire events produced during Step()
        public List<(uint schemaId, byte[] body)> OutgoingPeerEvents { get; } = new List<(uint, byte[])>();
        public List<(ushort schemaId, byte[] body)> OutgoingTcpEvents { get; } = new List<(ushort, byte[])>();

        private uint _nextBombId = 1;
        private BattleDirection _lastSentMoveDir = BattleDirection.None;
        private bool _lastSentMoving;
        private uint _lastMoveBroadcastMs;
        private readonly HashSet<(ushort owner, uint placedAt, byte r, byte c)> _seenRemoteBombs = new HashSet<(ushort, uint, byte, byte)>();

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
            OutgoingPeerEvents.Clear();
            OutgoingTcpEvents.Clear();
            _seenRemoteBombs.Clear();
            _nextBombId = 1;
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

            // Place hidden wall pickups deterministically from ItemSeed + GAME_BEGIN.NewItems
            PlaceHiddenPickups(beginData.ItemSeed, Map.HiddenItemCells, beginData.NewItems);
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

        public void Step(uint deltaMs, BattleDirection localMoveInput, bool localPlaceBomb)
        {
            OutgoingPeerEvents.Clear();
            OutgoingTcpEvents.Clear();
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
                // 1. Update local player movement & bomb placement
                BattleActor local = LocalActor;
                if (local != null && local.State == ActorLifeState.Active)
                {
                    StepLocalMovement(local, deltaMs, localMoveInput);
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

                // 4. Evaluate flame hits, pickups, trap/rescue/kill contact, and oxygen timers
                StepCombatAndPickups(deltaMs);
            }
        }

        private void StepLocalMovement(BattleActor local, uint deltaMs, BattleDirection dir)
        {
            bool moving = dir != BattleDirection.None;
            if (moving)
            {
                local.Facing = dir;
                ushort speedPx = SpeedPixelsPerSecondByRate[Math.Min((int)local.SpeedRate, SpeedPixelsPerSecondByRate.Length - 1)];
                uint num = speedPx * deltaMs + local.MoveRemainder;
                int dist = (int)Math.Min(40u, num / 1000u);
                local.MoveRemainder = num % 1000u;
                if (dist > 0) MoveActorPixels(local, dir, dist);
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

                byte walk = ToNativeWalkByte(local.Facing, moving, false);
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
                                Speed = local.SpeedRate
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
                    if (cell.Collision != CellCollision.Open) return false;

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
            if (activeOwned >= local.BombCapacity) return;

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

            var msg = new PlayerUseBombMessage
            {
                PlayerId = local.PlayerId,
                ClientTime = ElapsedMs,
                BombId = bombId,
                Row = (byte)cell.Row,
                Column = (byte)cell.Col,
                Power = local.BombPower,
                Property = 0
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

            // 1. Local flame hit check -> Trapped (0x0FA5)
            if (local.State == ActorLifeState.Active && HasFlameAt(local.CurrentCell))
            {
                local.State = ActorLifeState.Trapped;
                local.TrappedElapsedMs = 0;
                local.Moving = false;
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

            // 2. Local pickup collection
            if (local.State == ActorLifeState.Active)
            {
                GridCell lc = local.CurrentCell;
                foreach (var pickup in Pickups)
                {
                    if (pickup.Hidden || pickup.Consumed || !pickup.Cell.Equals(lc)) continue;
                    pickup.Consumed = true;
                    ApplyPickupEffect(local, pickup.SceneId);
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
                            OutgoingPeerEvents.Add((GameEventSchemas.RequestSavePlayer, inter.Marshal()));
                        }
                        else
                        {
                            other.State = ActorLifeState.Dead;
                            local.KillCount++;
                            OutgoingPeerEvents.Add((GameEventSchemas.RequestKillPlayer, inter.Marshal()));
                            OutgoingTcpEvents.Add((GameEventSchemas.RequestKillPlayer, inter.Marshal()));
                        }
                    }
                }
            }

            // 4. Trapped oxygen countdown -> Death (0x0FA7)
            if (local.State == ActorLifeState.Trapped)
            {
                local.TrappedElapsedMs += deltaMs;
                if (local.TrappedElapsedMs >= local.OxygenMs)
                {
                    local.State = ActorLifeState.Dead;
                    var dieMsg = new PlayerDeathMessage
                    {
                        PlayerId = local.PlayerId,
                        ClientTime = ElapsedMs,
                        PosX = (ushort)local.PosX,
                        PosY = (ushort)local.PosY
                    };
                    byte[] body = dieMsg.Marshal();
                    OutgoingPeerEvents.Add((GameEventSchemas.NotifyPlayerDie, body));
                    OutgoingTcpEvents.Add((GameEventSchemas.NotifyPlayerDie, body));
                }
            }
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
                        ApplyPickupEffect(getter, got.ItemId);
                    }
                    break;

                case GameEventSchemas.NotifyGameOver:
                    IsFinished = true;
                    break;
            }
        }

        private static void ApplyPickupEffect(BattleActor actor, uint sceneId)
        {
            switch (sceneId)
            {
                case 1: actor.BombCapacity = (byte)Math.Min(actor.MaxBombCapacity, actor.BombCapacity + 1); break;
                case 2: actor.BombPower = (byte)Math.Min(actor.MaxBombPower, actor.BombPower + 1); break;
                case 3: actor.SpeedRate = (byte)Math.Min(actor.MaxSpeedRate, actor.SpeedRate + 1); break;
                case 6: actor.BombCapacity = actor.MaxBombCapacity; break;
                case 7: actor.BombPower = actor.MaxBombPower; break;
                case 8: actor.SpeedRate = actor.MaxSpeedRate; break;
                case 47: actor.SpeedRate = (byte)Math.Max((int)actor.SpeedRate, 8); break;
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
