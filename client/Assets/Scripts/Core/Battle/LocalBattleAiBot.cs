using System;
using System.Collections.Generic;

namespace Qqtang.Core.Battle
{
    /// <summary>
    /// Tactical local AI bot for Offline Solo Practice mode.
    /// Computes active bomb blast danger grid, evades blasts, collects nearby pickups, and bombs breakable walls/opponents safely.
    /// </summary>
    public sealed class LocalBattleAiBot
    {
        private readonly ushort _playerId;
        private readonly Random _rng;
        private BattleDirection _cachedDir = BattleDirection.None;
        private uint _nextDecisionMs;

        private static readonly BattleDirection[] CardinalDirs =
        {
            BattleDirection.Right, BattleDirection.Up, BattleDirection.Left, BattleDirection.Down
        };

        public LocalBattleAiBot(ushort playerId, int seed)
        {
            _playerId = playerId;
            _rng = new Random(seed ^ (playerId * 997));
        }

        public (BattleDirection move, bool placeBomb) Decide(Rule1BattleSimulation sim)
        {
            var me = sim.GetActor(_playerId);
            if (me == null || me.State != ActorLifeState.Active || sim.ElapsedMs < Rule1BattleSimulation.RoundStartClockMs)
                return (BattleDirection.None, false);

            var map = sim.Map;
            bool[,] danger = BuildDangerGrid(sim);
            GridCell cell = me.CurrentCell;

            // 1. If currently standing in danger (bomb ray or active flame), immediately escape toward nearest safe cell
            if (InBounds(map, cell) && danger[cell.Row, cell.Col])
            {
                BattleDirection escape = FindBfsDirection(sim, cell, c => !danger[c.Row, c.Col], allowCurrentDanger: true);
                _cachedDir = escape;
                _nextDecisionMs = sim.ElapsedMs + 120;
                return (escape, false);
            }

            // 2. Throttle strategic replanning so motion stays smooth
            if (sim.ElapsedMs < _nextDecisionMs)
            {
                if (_cachedDir != BattleDirection.None)
                {
                    GridCell next = StepCell(cell, _cachedDir);
                    if (IsWalkableCell(sim, next) && !danger[next.Row, next.Col])
                        return (_cachedDir, false);
                }
            }
            _nextDecisionMs = sim.ElapsedMs + 220;

            // 3. If adjacent to an enemy or breakable wall AND we have an escape cell after placing a bomb, drop bomb!
            if (CanPlaceMoreBombs(sim, me) && !HasBombAt(sim, cell))
            {
                bool targetAdj = HasAdjacentEnemyOrBreakable(sim, me, cell);
                if (targetAdj && HasSafeEscapeAfterBomb(sim, danger, cell, me.BombPower))
                {
                    _cachedDir = BattleDirection.None;
                    return (BattleDirection.None, true);
                }
            }

            // 4. Chase trapped enemy to finish, or trapped ally to rescue
            foreach (var other in sim.Actors)
            {
                if (other.PlayerId == me.PlayerId || other.State != ActorLifeState.Trapped) continue;
                var dir = FindBfsDirection(sim, cell, c => c.Equals(other.CurrentCell), allowCurrentDanger: false, danger);
                if (dir != BattleDirection.None)
                {
                    _cachedDir = dir;
                    return (dir, false);
                }
            }

            // 5. Seek nearest visible pickup
            var pickupDir = FindBfsDirection(sim, cell, c => HasVisiblePickupAt(sim, c), allowCurrentDanger: false, danger);
            if (pickupDir != BattleDirection.None)
            {
                _cachedDir = pickupDir;
                return (pickupDir, false);
            }

            // 6. Seek nearest cell adjacent to a breakable wall or opponent
            var huntDir = FindBfsDirection(sim, cell, c => HasAdjacentEnemyOrBreakable(sim, me, c), allowCurrentDanger: false, danger);
            if (huntDir != BattleDirection.None)
            {
                _cachedDir = huntDir;
                return (huntDir, false);
            }

            // 7. Random safe wander
            var safeDirs = new List<BattleDirection>(4);
            foreach (var d in CardinalDirs)
            {
                var n = StepCell(cell, d);
                if (IsWalkableCell(sim, n) && !danger[n.Row, n.Col]) safeDirs.Add(d);
            }
            _cachedDir = safeDirs.Count > 0 ? safeDirs[_rng.Next(safeDirs.Count)] : BattleDirection.None;
            return (_cachedDir, false);
        }

        private static bool[,] BuildDangerGrid(Rule1BattleSimulation sim)
        {
            var map = sim.Map;
            var danger = new bool[map.Height, map.Width];
            foreach (var f in sim.Flames)
                if (InBounds(map, f.Cell)) danger[f.Cell.Row, f.Cell.Col] = true;
            foreach (var b in sim.Bombs)
                MarkBombDanger(map, danger, b.Cell, b.Power);
            foreach (var obj in sim.FieldObjects)
                if (InBounds(map, obj.Cell)) danger[obj.Cell.Row, obj.Cell.Col] = true;
            return danger;
        }

        private static void MarkBombDanger(LoadedMapData map, bool[,] danger, GridCell origin, int power)
        {
            if (InBounds(map, origin)) danger[origin.Row, origin.Col] = true;
            int[] dr = { -1, 1, 0, 0 };
            int[] dc = { 0, 0, -1, 1 };
            for (int d = 0; d < 4; d++)
            {
                for (int s = 1; s <= power; s++)
                {
                    int nr = origin.Row + dr[d] * s;
                    int nc = origin.Col + dc[d] * s;
                    if (!map.InBounds(nr, nc)) break;
                    var tile = map.GetCell(nr, nc);
                    if (tile.Collision != CellCollision.Open) break;
                    danger[nr, nc] = true;
                    if (!tile.FlamePassable) break;
                }
            }
        }

        private static bool HasSafeEscapeAfterBomb(Rule1BattleSimulation sim, bool[,] baseDanger, GridCell bombCell, byte power)
        {
            var map = sim.Map;
            var hypo = (bool[,])baseDanger.Clone();
            MarkBombDanger(map, hypo, bombCell, power);
            var escape = FindBfsDirection(sim, bombCell, c => !hypo[c.Row, c.Col], allowCurrentDanger: true, maxDepth: 6);
            return escape != BattleDirection.None;
        }

        private static BattleDirection FindBfsDirection(
            Rule1BattleSimulation sim, GridCell start, Func<GridCell, bool> isGoal,
            bool allowCurrentDanger, bool[,] avoidDanger = null, int maxDepth = 14)
        {
            var map = sim.Map;
            var visited = new bool[map.Height, map.Width];
            var queue = new Queue<(GridCell cell, BattleDirection firstDir, int depth)>();
            visited[start.Row, start.Col] = true;

            foreach (var d in CardinalDirs)
            {
                GridCell n = StepCell(start, d);
                if (!IsWalkableCell(sim, n)) continue;
                if (!allowCurrentDanger && avoidDanger != null && avoidDanger[n.Row, n.Col]) continue;
                visited[n.Row, n.Col] = true;
                if (isGoal(n)) return d;
                queue.Enqueue((n, d, 1));
            }

            while (queue.Count > 0)
            {
                var (cur, firstDir, depth) = queue.Dequeue();
                if (depth >= maxDepth) continue;
                foreach (var d in CardinalDirs)
                {
                    GridCell n = StepCell(cur, d);
                    if (!IsWalkableCell(sim, n) || visited[n.Row, n.Col]) continue;
                    if (!allowCurrentDanger && avoidDanger != null && avoidDanger[n.Row, n.Col]) continue;
                    visited[n.Row, n.Col] = true;
                    if (isGoal(n)) return firstDir;
                    queue.Enqueue((n, firstDir, depth + 1));
                }
            }
            return BattleDirection.None;
        }

        private static bool HasAdjacentEnemyOrBreakable(Rule1BattleSimulation sim, BattleActor me, GridCell cell)
        {
            foreach (var d in CardinalDirs)
            {
                GridCell n = StepCell(cell, d);
                if (!sim.Map.InBounds(n.Row, n.Col)) continue;
                if (sim.Map.GetCell(n.Row, n.Col).Collision == CellCollision.Breakable) return true;
            }
            foreach (var other in sim.Actors)
            {
                if (other.PlayerId == me.PlayerId || other.TeamId == me.TeamId || other.State == ActorLifeState.Dead) continue;
                int dist = Math.Abs(other.CurrentCell.Row - cell.Row) + Math.Abs(other.CurrentCell.Col - cell.Col);
                if (dist <= Math.Min(3, (int)me.BombPower)) return true;
            }
            return false;
        }

        private static bool CanPlaceMoreBombs(Rule1BattleSimulation sim, BattleActor actor)
        {
            int active = 0;
            foreach (var b in sim.Bombs) if (b.OwnerId == actor.PlayerId) active++;
            return active < actor.BombCapacity;
        }

        private static bool HasBombAt(Rule1BattleSimulation sim, GridCell c)
        {
            foreach (var b in sim.Bombs) if (b.Cell.Equals(c)) return true;
            return false;
        }

        private static bool HasVisiblePickupAt(Rule1BattleSimulation sim, GridCell c)
        {
            foreach (var p in sim.Pickups) if (!p.Hidden && !p.Consumed && p.Cell.Equals(c)) return true;
            return false;
        }

        private static bool IsWalkableCell(Rule1BattleSimulation sim, GridCell c)
        {
            if (!sim.Map.InBounds(c.Row, c.Col)) return false;
            if (sim.Map.GetCell(c.Row, c.Col).Collision != CellCollision.Open) return false;
            return !HasBombAt(sim, c);
        }

        private static bool InBounds(LoadedMapData map, GridCell c) => map.InBounds(c.Row, c.Col);

        private static GridCell StepCell(GridCell c, BattleDirection d)
        {
            switch (d)
            {
                case BattleDirection.Up: return new GridCell(c.Row - 1, c.Col);
                case BattleDirection.Down: return new GridCell(c.Row + 1, c.Col);
                case BattleDirection.Left: return new GridCell(c.Row, c.Col - 1);
                case BattleDirection.Right: return new GridCell(c.Row, c.Col + 1);
                default: return c;
            }
        }
    }
}
