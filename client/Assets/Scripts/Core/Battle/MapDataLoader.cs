using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Qqtang.Core.Battle
{
    public enum CellCollision : byte
    {
        Open = 0,
        Breakable = 1,
        Solid = 2
    }

    public struct GridCell : IEquatable<GridCell>
    {
        public short Row;
        public short Col;

        public GridCell(int row, int col)
        {
            Row = (short)row;
            Col = (short)col;
        }

        public bool Equals(GridCell other) => Row == other.Row && Col == other.Col;
        public override bool Equals(object obj) => obj is GridCell other && Equals(other);
        public override int GetHashCode() => (Row << 16) ^ (ushort)Col;
        public override string ToString() => $"({Row},{Col})";
    }

    public sealed class MapElementDef
    {
        public uint Id;
        public int LifeTime = -1;
        public uint CanMove;
        public int Width = 1;
        public int Height = 1;
        public uint[] GridAttrs = { 0 };
    }

    public struct BattleCell
    {
        public CellCollision Collision;
        public bool FlamePassable;
        public uint NativeGridAttr;
        public bool NativeGridAttrSet;
        public uint NativePlayerExitAttr;
        public bool MapElementOccupied;
        public byte Durability;
        public uint MapElementId;
        public int VisualGroundElementId;
        public int VisualSceneElementId;
        public int VisualOverlayElementId;
    }

    public sealed class LoadedMapData
    {
        public uint MapId;
        public uint NativeRule;
        public ushort Width;
        public ushort Height;
        public BattleCell[] Cells;
        public List<GridCell> HiddenItemCells = new List<GridCell>();
        public List<GridCell> SpawnGroupA = new List<GridCell>();
        public List<GridCell> SpawnGroupB = new List<GridCell>();
        public List<GridCell> ObjectiveCells = new List<GridCell>();

        public BattleCell GetCell(int row, int col)
        {
            if (row < 0 || col < 0 || row >= Height || col >= Width)
                return new BattleCell { Collision = CellCollision.Solid };
            return Cells[row * Width + col];
        }

        public bool InBounds(int row, int col) => row >= 0 && col >= 0 && row < Height && col < Width;

        public LoadedMapData Clone()
        {
            var copy = new LoadedMapData
            {
                MapId = MapId,
                NativeRule = NativeRule,
                Width = Width,
                Height = Height,
                Cells = (BattleCell[])Cells.Clone(),
                HiddenItemCells = new List<GridCell>(HiddenItemCells),
                SpawnGroupA = new List<GridCell>(SpawnGroupA),
                SpawnGroupB = new List<GridCell>(SpawnGroupB),
                ObjectiveCells = new List<GridCell>(ObjectiveCells)
            };
            return copy;
        }
    }

    /// <summary>
    /// Parses QQTang v3/v4 .map binaries and mapElem.py catalogs (mirrors server/internal/game/mapdata).
    /// </summary>
    public static class MapDataLoader
    {
        private static readonly Regex ClassRegex = new Regex(@"^class\s+QQTMapElem([0-9]+)\(QQTMapElem\):", RegexOptions.Compiled);
        private static readonly Regex IntRegex = new Regex(@"^(LifeTime|imageID|canMove)\s*=\s*(-?[0-9]+)\s*$", RegexOptions.Compiled);
        private static readonly Regex SizeRegex = new Regex(@"^size\s*=\s*\(\s*([0-9]+)\s*,\s*([0-9]+)\s*\)\s*$", RegexOptions.Compiled);
        private static readonly Regex AttrsRegex = new Regex(@"^GridAttr\s*=\s*\(([^)]*)\)\s*$", RegexOptions.Compiled);
        private static readonly Regex MapDescEntryRegex = new Regex(@"^\s*([0-9]+)\s*:\s*\[.*?['""]([^'""]+\.map)['""]", RegexOptions.Compiled);

        public static Dictionary<uint, MapElementDef> LoadMapElementsPy(string mapElemPyPath)
        {
            var elements = new Dictionary<uint, MapElementDef>();
            MapElementDef current = null;

            void Commit()
            {
                if (current == null) return;
                int count = Math.Max(1, current.Width * current.Height);
                if (current.GridAttrs.Length == 1 && count > 1)
                {
                    uint val = current.GridAttrs[0];
                    current.GridAttrs = new uint[count];
                    for (int i = 0; i < count; i++) current.GridAttrs[i] = val;
                }
                elements[current.Id] = current;
            }

            foreach (string rawLine in File.ReadLines(mapElemPyPath))
            {
                string line = rawLine.Trim();
                Match m = ClassRegex.Match(line);
                if (m.Success)
                {
                    Commit();
                    current = new MapElementDef { Id = uint.Parse(m.Groups[1].Value) };
                    continue;
                }
                if (current == null) continue;

                m = IntRegex.Match(line);
                if (m.Success)
                {
                    int val = int.Parse(m.Groups[2].Value);
                    switch (m.Groups[1].Value)
                    {
                        case "LifeTime": current.LifeTime = val; break;
                        case "imageID": current.Id = (uint)val; break;
                        case "canMove": if (val >= 0) current.CanMove = (uint)val; break;
                    }
                    continue;
                }
                m = SizeRegex.Match(line);
                if (m.Success)
                {
                    current.Width = int.Parse(m.Groups[1].Value);
                    current.Height = int.Parse(m.Groups[2].Value);
                    continue;
                }
                m = AttrsRegex.Match(line);
                if (m.Success)
                {
                    string[] parts = m.Groups[1].Value.Split(',');
                    var attrs = new List<uint>();
                    foreach (string p in parts)
                    {
                        string t = p.Trim();
                        if (t.Length == 0) continue;
                        attrs.Add(t.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                            ? Convert.ToUInt32(t.Substring(2), 16)
                            : uint.Parse(t));
                    }
                    if (attrs.Count > 0) current.GridAttrs = attrs.ToArray();
                }
            }
            Commit();
            return elements;
        }

        public static Dictionary<uint, string> LoadMapFilenamesFromMapDescPy(string mapDescPyPath)
        {
            var result = new Dictionary<uint, string>();
            if (!File.Exists(mapDescPyPath)) return result;

            // Parse patterns like id = 201 ... mapFile = 'town01.map' or dict literals
            uint? currentId = null;
            var idLine = new Regex(@"(?:^|\b)(?:id|mapID|MapID)\s*=\s*([0-9]+)");
            var fileLine = new Regex(@"['""]([A-Za-z0-9_\-]+\.map)['""]");
            foreach (string line in File.ReadLines(mapDescPyPath))
            {
                Match dm = MapDescEntryRegex.Match(line);
                if (dm.Success)
                {
                    result[uint.Parse(dm.Groups[1].Value)] = dm.Groups[2].Value;
                    continue;
                }
                Match im = idLine.Match(line);
                if (im.Success)
                {
                    currentId = uint.Parse(im.Groups[1].Value);
                }
                Match fm = fileLine.Match(line);
                if (fm.Success && currentId.HasValue)
                {
                    result[currentId.Value] = fm.Groups[1].Value;
                    currentId = null;
                }
            }
            return result;
        }

        public static LoadedMapData ParseMapBytes(uint mapId, byte[] mapBytes, Dictionary<uint, MapElementDef> elements)
        {
            if (mapBytes == null || mapBytes.Length < 12)
                throw new InvalidOperationException("Map binary truncated.");

            uint version = ReadUInt32LE(mapBytes, 0);
            uint nativeRule = ReadUInt32LE(mapBytes, 4);
            int headerSize = 12;
            uint width = 15, height = 13;
            if (version == 4)
            {
                if (mapBytes.Length < 20) throw new InvalidOperationException("Map v4 header truncated.");
                headerSize = 20;
                width = ReadUInt32LE(mapBytes, 12);
                height = ReadUInt32LE(mapBytes, 16);
            }
            else if (version != 3)
            {
                throw new InvalidOperationException($"Unsupported map version {version}.");
            }

            int cellCount = (int)(width * height);
            int layerBytes = cellCount * 4;
            if (headerSize + 3 * layerBytes > mapBytes.Length)
                throw new InvalidOperationException("Map layers truncated.");

            var map = new LoadedMapData
            {
                MapId = mapId,
                NativeRule = nativeRule,
                Width = (ushort)width,
                Height = (ushort)height,
                Cells = new BattleCell[cellCount]
            };

            for (int i = 0; i < cellCount; i++)
            {
                map.Cells[i] = new BattleCell
                {
                    Collision = CellCollision.Open,
                    FlamePassable = true,
                    NativePlayerExitAttr = 15
                };
            }

            // Read all 3 visual layers and project layers 0 & 1 for collision (matches competitive_battlefield.go)
            for (int layer = 0; layer < 3; layer++)
            {
                int layerBase = headerSize + layer * layerBytes;
                for (int row = 0; row < height; row++)
                {
                    for (int col = 0; col < width; col++)
                    {
                        int raw = (int)ReadUInt32LE(mapBytes, layerBase + (row * (int)width + col) * 4);
                        int idx = row * (int)width + col;
                        if (layer == 0) map.Cells[idx].VisualGroundElementId = raw;
                        else if (layer == 1) map.Cells[idx].VisualSceneElementId = raw;
                        else map.Cells[idx].VisualOverlayElementId = raw;

                        if (layer >= 2 || raw <= 0) continue;

                        if (elements == null || !elements.TryGetValue((uint)raw, out var def))
                        {
                            if (layer == 0) map.Cells[idx].NativePlayerExitAttr = 0;
                            map.Cells[idx].Collision = CellCollision.Solid;
                            map.Cells[idx].MapElementOccupied = true;
                            map.Cells[idx].FlamePassable = false;
                            continue;
                        }

                        for (int lr = 0; lr < def.Height; lr++)
                        {
                            for (int lc = 0; lc < def.Width; lc++)
                            {
                                int tr = row + lr, tc = col + lc;
                                if (tr >= height || tc >= width) continue;
                                int tidx = tr * (int)width + tc;
                                uint attr = def.GridAttrs[Math.Min(lr * def.Width + lc, def.GridAttrs.Length - 1)];

                                BattleCell projected = map.Cells[tidx];
                                projected.MapElementOccupied = true;
                                projected.FlamePassable = (attr & 0xF00u) != 0;
                                projected.NativeGridAttr = attr;
                                projected.NativeGridAttrSet = true;
                                if (layer == 0) projected.NativePlayerExitAttr = attr;
                                projected.Durability = def.LifeTime > 0 ? (byte)Math.Min(255, def.LifeTime) : (byte)0;
                                if ((attr & 0xFu) == 0)
                                    projected.Collision = def.LifeTime > 0 ? CellCollision.Breakable : CellCollision.Solid;
                                else
                                    projected.Collision = CellCollision.Open;
                                if (def.LifeTime > 0 || (def.CanMove & 2u) != 0)
                                    projected.MapElementId = def.Id;
                                map.Cells[tidx] = projected;
                            }
                        }
                    }
                }
            }

            // Parse trailing tables: wall-item rules, hidden items, spawn A, spawn B, objective cells
            int offset = headerSize + 3 * layerBytes;
            if (offset + 4 <= mapBytes.Length)
            {
                uint ruleCount = ReadUInt32LE(mapBytes, offset);
                offset += 4 + (int)ruleCount * 16;
                map.HiddenItemCells = ParseCellTable(mapBytes, ref offset, width, height);
                map.SpawnGroupA = ParseCellTable(mapBytes, ref offset, width, height);
                map.SpawnGroupB = ParseCellTable(mapBytes, ref offset, width, height);
                map.ObjectiveCells = ParseCellTable(mapBytes, ref offset, width, height);
            }

            return map;
        }

        private static List<GridCell> ParseCellTable(byte[] data, ref int offset, uint width, uint height)
        {
            var list = new List<GridCell>();
            if (offset + 4 > data.Length) return list;
            uint count = ReadUInt32LE(data, offset);
            offset += 4;
            for (uint i = 0; i < count && offset + 4 <= data.Length; i++)
            {
                ushort row = (ushort)(data[offset] | (data[offset + 1] << 8));
                ushort col = (ushort)(data[offset + 2] | (data[offset + 3] << 8));
                offset += 4;
                if (row < height && col < width)
                    list.Add(new GridCell(row, col));
            }
            return list;
        }

        /// <summary>
        /// Fallback classic 15x13 arena used when local original .map files are not mounted yet.
        /// </summary>
        public static LoadedMapData BuildFallbackArena(uint mapId = 201)
        {
            const ushort w = 15, h = 13;
            var map = new LoadedMapData
            {
                MapId = mapId,
                NativeRule = 1,
                Width = w,
                Height = h,
                Cells = new BattleCell[w * h]
            };
            for (int r = 0; r < h; r++)
            {
                for (int c = 0; c < w; c++)
                {
                    int idx = r * w + c;
                    bool solidPillar = (r % 2 == 1) && (c % 2 == 1);
                    bool cornerSafe = (r <= 1 && c <= 1) || (r <= 1 && c >= w - 2) ||
                                      (r >= h - 2 && c <= 1) || (r >= h - 2 && c >= w - 2);
                    bool breakableBox = !solidPillar && !cornerSafe && ((r + c) % 2 == 0);
                    map.Cells[idx] = new BattleCell
                    {
                        Collision = solidPillar ? CellCollision.Solid : (breakableBox ? CellCollision.Breakable : CellCollision.Open),
                        FlamePassable = !solidPillar,
                        Durability = breakableBox ? (byte)1 : (byte)0,
                        MapElementOccupied = solidPillar || breakableBox,
                        MapElementId = breakableBox ? 2002u : (solidPillar ? 2001u : 0u),
                        NativePlayerExitAttr = 15
                    };
                    if (breakableBox) map.HiddenItemCells.Add(new GridCell(r, c));
                }
            }
            map.SpawnGroupA.Add(new GridCell(0, 0));
            map.SpawnGroupA.Add(new GridCell(h - 1, w - 1));
            map.SpawnGroupB.Add(new GridCell(0, w - 1));
            map.SpawnGroupB.Add(new GridCell(h - 1, 0));
            return map;
        }

        private static uint ReadUInt32LE(byte[] b, int off) =>
            b[off] | ((uint)b[off + 1] << 8) | ((uint)b[off + 2] << 16) | ((uint)b[off + 3] << 24);
    }
}
