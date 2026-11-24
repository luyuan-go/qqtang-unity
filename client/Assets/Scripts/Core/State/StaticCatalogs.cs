using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Qqtang.Core.State
{
    public sealed class MapCatalogEntry
    {
        public ushort MapId;
        public string Name = string.Empty;
        public string Family = string.Empty;
        public bool IsAdventure;
        public bool TransformationItemsEnabled;
    }

    public sealed class ItemCatalogEntry
    {
        public ushort Id;
        public string Name = string.Empty;
        public string Kind = string.Empty;
        public string Category = string.Empty;
    }

    public sealed class ShopCommodityEntry
    {
        public uint CommodityId;
        public ushort ItemId;
        public string Name = string.Empty;
        public ushort CommodityType;
        public uint Quantity = 1;
        public uint SugarPrice = 1000;
        public uint QPrice = 10;
    }

    public sealed class CombineRecipeEntry
    {
        public string Category = string.Empty;
        public string Name = string.Empty;
        public uint BookItemId;
        public uint ProductItemId;
        public string Description = string.Empty;
        public List<(uint itemId, string name, int qty)> Materials = new List<(uint, string, int)>();
    }

    public sealed class PetCatalogEntry
    {
        public uint PetTypeId;
        public string Name = string.Empty;
        public string Description = string.Empty;
    }

    public sealed class RoleInfoEntry
    {
        public byte RoleId;
        public string DaoistName;
        public string ClassicName;
        public int BaseSpeed;
        public int BaseBombs;
        public int BasePower;
        public bool PurpleDiamondOnly;
    }

    /// <summary>
    /// Pure-C# loader for server JSON catalogs, optional local Commodity.ini, and exact progression rank titles.
    /// Zero Unity or NuGet dependencies so it works identically in headless CoreTests and Unity builds.
    /// </summary>
    public sealed class StaticCatalogs
    {
        public readonly List<MapCatalogEntry> Maps = new List<MapCatalogEntry>();
        public readonly List<MapCatalogEntry> AdventureMaps = new List<MapCatalogEntry>();
        public readonly Dictionary<ushort, MapCatalogEntry> MapsById = new Dictionary<ushort, MapCatalogEntry>();
        public readonly Dictionary<ushort, ItemCatalogEntry> ItemsById = new Dictionary<ushort, ItemCatalogEntry>();
        public readonly List<ShopCommodityEntry> ShopCommodities = new List<ShopCommodityEntry>();
        public readonly List<CombineRecipeEntry> CombineRecipes = new List<CombineRecipeEntry>();
        public readonly Dictionary<uint, PetCatalogEntry> PetsByTypeId = new Dictionary<uint, PetCatalogEntry>();

        private static readonly string[] RankTitles =
        {
            "QQ堂平民", "糖果爱好者", "采购学徒", "熬糖工人", "拌糖熟练工", "甜味技术员",
            "捏形师傅", "品糖高手", "造型专家", "QQ糖大师", "糖果志愿者", "奶酪预备兵",
            "QQ糖战士", "刨冰骑兵", "棒棒糖游侠", "饼干队长", "果冻骑士", "棒棒糖将军",
            "冰琪淋勇者", "QQ堂英雄", "糖果镇长", "中华城主", "梦幻岛主", "星星公爵",
            "月亮王子", "太阳国王", "紫钻皇帝", "酷比大天使", "创世之神", "无上真人"
        };

        // Exact 180-step competitivePointThresholds from server/internal/protocol/game/progression.go
        private static readonly uint[] CompetitivePointThresholds =
        {
            0, 1000, 2000, 3000, 4000, 5000, 10000, 12000, 14000, 16000, 18000, 20000,
            30000, 33000, 36000, 39000, 42000, 45000, 60000, 64000, 68000, 72000, 76000,
            80000, 100000, 105000, 110000, 115000, 120000, 125000, 150000, 156000, 162000,
            168000, 174000, 180000, 210000, 217000, 224000, 231000, 238000, 245000, 280000,
            288000, 296000, 304000, 312000, 320000, 360000, 369000, 378000, 387000, 396000,
            405000, 450000, 460000, 470000, 480000, 490000, 500000, 550000, 565000, 580000,
            595000, 610000, 625000, 700000, 720000, 740000, 760000, 780000, 800000, 900000,
            925000, 950000, 975000, 1000000, 1025000, 1150000, 1180000, 1210000, 1240000,
            1270000, 1300000, 1450000, 1485000, 1520000, 1555000, 1590000, 1625000, 1800000,
            1840000, 1880000, 1920000, 1960000, 2000000, 2200000, 2245000, 2290000, 2335000,
            2380000, 2425000, 2650000, 2700000, 2750000, 2800000, 2850000, 2900000, 3150000,
            3205000, 3260000, 3315000, 3370000, 3425000, 3700000, 3760000, 3820000, 3880000,
            3940000, 4000000, 4300000, 4370000, 4440000, 4510000, 4580000, 4650000, 5000000,
            5090000, 5180000, 5270000, 5360000, 5450000, 5900000, 6020000, 6140000, 6260000,
            6380000, 6500000, 7100000, 7260000, 7420000, 7580000, 7740000, 7900000, 8700000,
            8910000, 9120000, 9330000, 9540000, 9750000, 10800000, 11070000, 11340000,
            11610000, 11880000, 12150000, 13500000, 14685000, 15870000, 17055000, 18240000,
            19425000, 25350000, 29145000, 32940000, 36735000, 40530000, 44325000, 63300000,
            71380000, 79460000, 87540000, 95620000, 103700000, 144100000, 292805000, 441510000,
            590515000, 738920000, 887625000, 1631150000
        };

        // Exact 31-level adventurePointThresholds (Lv.0..30) from server/internal/protocol/game/progression.go
        private static readonly uint[] AdventurePointThresholds =
        {
            0, 573, 1289, 2447, 4481, 7952, 20000, 36100, 60000, 96000, 154000, 231000,
            323401, 431858, 565350, 790000, 1040000, 1430000, 1880000, 2420000, 3010000,
            5322310, 9070163, 15066729, 24661235, 40012444, 64574378, 103873473,
            166752024, 267357707, 428326799
        };

        public static (ushort degree, string title, uint minPoints, uint nextPoints, float progress) GetCompetitiveRankInfo(uint points)
        {
            int idx = CompetitivePointThresholds.Length - 2;
            for (int d = 1; d < CompetitivePointThresholds.Length; d++)
            {
                if (points < CompetitivePointThresholds[d])
                {
                    idx = d - 1;
                    break;
                }
            }
            ushort degree = (ushort)(idx + 1);
            uint minP = CompetitivePointThresholds[idx];
            uint nextP = idx + 1 < CompetitivePointThresholds.Length ? CompetitivePointThresholds[idx + 1] : minP;
            float prog = nextP > minP ? Math.Min(1f, Math.Max(0f, (float)(points - minP) / (nextP - minP))) : 1f;
            return (degree, GetRankTitle(degree), minP, nextP, prog);
        }

        public static (byte level, uint minPoints, uint nextPoints, float progress) GetAdventureRankInfo(uint extPoint)
        {
            int idx = AdventurePointThresholds.Length - 1;
            for (int lv = 1; lv < AdventurePointThresholds.Length; lv++)
            {
                if (extPoint < AdventurePointThresholds[lv])
                {
                    idx = lv - 1;
                    break;
                }
            }
            uint minP = AdventurePointThresholds[idx];
            uint nextP = idx + 1 < AdventurePointThresholds.Length ? AdventurePointThresholds[idx + 1] : minP;
            float prog = nextP > minP ? Math.Min(1f, Math.Max(0f, (float)(extPoint - minP) / (nextP - minP))) : 1f;
            return ((byte)idx, minP, nextP, prog);
        }

        public static readonly RoleInfoEntry[] Roles =
        {
            new RoleInfoEntry { RoleId = 1,  DaoistName = "清微童子", ClassicName = "小乖", BaseSpeed = 5, BaseBombs = 1, BasePower = 2 },
            new RoleInfoEntry { RoleId = 2,  DaoistName = "玄素仙姬", ClassicName = "糖果", BaseSpeed = 6, BaseBombs = 1, BasePower = 1 },
            new RoleInfoEntry { RoleId = 3,  DaoistName = "火葫芦道长", ClassicName = "大嘴", BaseSpeed = 4, BaseBombs = 2, BasePower = 2 },
            new RoleInfoEntry { RoleId = 4,  DaoistName = "碧波灵女", ClassicName = "贝贝", BaseSpeed = 5, BaseBombs = 2, BasePower = 1 },
            new RoleInfoEntry { RoleId = 5,  DaoistName = "云游小道", ClassicName = "小齐", BaseSpeed = 6, BaseBombs = 1, BasePower = 2 },
            new RoleInfoEntry { RoleId = 6,  DaoistName = "金丹炼士", ClassicName = "波波", BaseSpeed = 4, BaseBombs = 1, BasePower = 3 },
            new RoleInfoEntry { RoleId = 7,  DaoistName = "剑心真人", ClassicName = "影",   BaseSpeed = 6, BaseBombs = 2, BasePower = 2 },
            new RoleInfoEntry { RoleId = 8,  DaoistName = "玉清仙子", ClassicName = "嘟嘟", BaseSpeed = 5, BaseBombs = 2, BasePower = 2 },
            new RoleInfoEntry { RoleId = 9,  DaoistName = "太极童子", ClassicName = "包子", BaseSpeed = 5, BaseBombs = 1, BasePower = 2 },
            new RoleInfoEntry { RoleId = 13, DaoistName = "紫霞尊者", ClassicName = "紫钻1", BaseSpeed = 6, BaseBombs = 2, BasePower = 2, PurpleDiamondOnly = true },
            new RoleInfoEntry { RoleId = 14, DaoistName = "玄雷天师", ClassicName = "紫钻2", BaseSpeed = 6, BaseBombs = 2, BasePower = 3, PurpleDiamondOnly = true },
            new RoleInfoEntry { RoleId = 18, DaoistName = "青莲道童", ClassicName = "小美", BaseSpeed = 5, BaseBombs = 2, BasePower = 1 },
            new RoleInfoEntry { RoleId = 19, DaoistName = "御风散人", ClassicName = "阿酷", BaseSpeed = 6, BaseBombs = 1, BasePower = 2 },
            new RoleInfoEntry { RoleId = 20, DaoistName = "灵符师妹", ClassicName = "琳琳", BaseSpeed = 5, BaseBombs = 2, BasePower = 2 },
            new RoleInfoEntry { RoleId = 21, DaoistName = "墨竹书生", ClassicName = "墨客", BaseSpeed = 5, BaseBombs = 1, BasePower = 3 },
            new RoleInfoEntry { RoleId = 22, DaoistName = "丹青小仙", ClassicName = "糖宝", BaseSpeed = 6, BaseBombs = 2, BasePower = 1 }
        };

        public static string GetRankTitle(ushort degree)
        {
            int idx = Math.Max(0, Math.Min(RankTitles.Length - 1, (degree > 0 ? degree - 1 : 0) / 6));
            int sub = (degree > 0 ? (degree - 1) % 6 : 0) + 1;
            return $"{RankTitles[idx]} {sub}阶";
        }

        public static RoleInfoEntry GetRole(byte roleId)
        {
            foreach (var r in Roles)
                if (r.RoleId == roleId) return r;
            return Roles[0];
        }

        public string GetMapName(ushort mapId) =>
            MapsById.TryGetValue(mapId, out var m) ? m.Name : $"地图#{mapId}";

        public string GetItemName(ushort itemId) =>
            ItemsById.TryGetValue(itemId, out var it) ? it.Name : $"道具#{itemId}";

        public string GetItemCategory(ushort itemId) =>
            ItemsById.TryGetValue(itemId, out var it) ? it.Category : "item";

        public string GetPetTypeName(uint petTypeId) =>
            PetsByTypeId.TryGetValue(petTypeId, out var p) ? p.Name : $"灵宠#{petTypeId}";

        public StaticCatalogs()
        {
            SeedBuiltInDefaults();
        }

        public void LoadFromDirectories(params string[] searchRoots)
        {
            foreach (string root in searchRoots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                TryLoadMaps(Path.Combine(root, "qqt_competitive_map_rules.json"));
                TryLoadMaps(Path.Combine(root, "data", "qqt_competitive_map_rules.json"));
                TryLoadMapDescPy(Path.Combine(root, "mapDesc.py"));
                TryLoadMapDescPy(Path.Combine(root, "map", "mapDesc.py"));
                TryLoadItems(Path.Combine(root, "item-id-catalog.json"));
                TryLoadItems(Path.Combine(root, "docs", "item-id-catalog.json"));
                TryLoadCombineRecipes(Path.Combine(root, "qqt_combine_recipes.json"));
                TryLoadCombineRecipes(Path.Combine(root, "data", "qqt_combine_recipes.json"));
                TryLoadPets(Path.Combine(root, "qqt_pet_catalog.json"));
                TryLoadPets(Path.Combine(root, "data", "qqt_pet_catalog.json"));
                TryLoadCommodityIni(Path.Combine(root, "Commodity.ini"));
                TryLoadCommodityIni(Path.Combine(root, "config", "Commodity.ini"));
            }
        }

        private void SeedBuiltInDefaults()
        {
            AddMap(201, "包子01", true, "town");
            AddMap(202, "包子02", true, "town");
            AddMap(1, "水面01", true, "water");
            AddMap(101, "英雄01", true, "field");
            AddMap(301, "抢包山01", false, "bun");
            AddMap(401, "比武01", true, "match");
            AddAdventureMap(1601, "魔法王国1 · 第一关");
            AddAdventureMap(1607, "魔法王国2 · 第一关");
            AddAdventureMap(1612, "机械王国 · 第一关");
            AddAdventureMap(1616, "黑龙王国 · 第一关");

            // Core functional & egg/craft items (guaranteed valid in server Commodity.ini)
            var coreShop = new (uint cid, ushort iid, string name, ushort type, uint qty)[]
            {
                (294, 294, "小喇叭[4个]", 513, 4),
                (99, 99, "单人探险卡[1日]", 515, 1),
                (30098, 30098, "单人BOSS卡", 515, 1),
                (30099, 30099, "AI对战卡", 515, 1),
                (9001, 9001, "铁蛋", 513, 1),
                (9002, 9002, "银蛋", 513, 1),
                (9003, 9003, "金蛋", 513, 1),
                (9004, 9004, "彩蛋", 513, 1),
                (9011, 9011, "铁锤", 513, 1),
                (9012, 9012, "银锤", 513, 1),
                (9013, 9013, "金锤", 513, 1),
                (20043, 20043, "大体力药水[100个]", 515, 100),
                (20044, 20044, "小体力药水[16个]", 515, 16),
                (20050, 20050, "医疗包[12个]", 515, 12),
                (20100, 20100, "小复活卡[12个]", 515, 12),
                (119, 119, "星光糖泡", 769, 1),
                (244, 244, "紫晶糖泡", 769, 1),
                (1005, 1005, "火焰糖泡", 769, 1),
                (1189, 1189, "雷电糖泡", 769, 1),
                (1445, 1445, "龙之糖泡", 769, 1),
                (9021, 9021, "水晶戒指", 514, 1),
                (9024, 9024, "钻石戒指", 514, 1)
            };
            foreach (var c in coreShop)
            {
                ShopCommodities.Add(new ShopCommodityEntry
                {
                    CommodityId = c.cid, ItemId = c.iid, Name = c.name,
                    CommodityType = c.type, Quantity = c.qty, SugarPrice = 1000, QPrice = 10
                });
                if (!ItemsById.ContainsKey(c.iid))
                    ItemsById[c.iid] = new ItemCatalogEntry { Id = c.iid, Name = c.name, Category = c.type == 769 ? "bubble" : "item" };
            }
        }

        private void AddMap(ushort id, string name, bool transform, string family = "")
        {
            if (MapsById.TryGetValue(id, out var existing))
            {
                if (string.IsNullOrEmpty(existing.Family) && !string.IsNullOrEmpty(family)) existing.Family = family;
                return;
            }
            var entry = new MapCatalogEntry { MapId = id, Name = name, Family = family, IsAdventure = false, TransformationItemsEnabled = transform };
            Maps.Add(entry);
            MapsById[id] = entry;
        }

        private void AddAdventureMap(ushort id, string name)
        {
            if (MapsById.ContainsKey(id)) return;
            var entry = new MapCatalogEntry { MapId = id, Name = name, Family = "pve", IsAdventure = true, TransformationItemsEnabled = false };
            AdventureMaps.Add(entry);
            MapsById[id] = entry;
        }

        public List<MapCatalogEntry> GetMapsForRoomType(Qqtang.Core.Protocol.RoomGameType gameType) =>
            gameType == Qqtang.Core.Protocol.RoomGameType.Adventure ? AdventureMaps : Maps;

        private void TryLoadMapDescPy(string path)
        {
            if (!File.Exists(path) || AdventureMaps.Count > 10) return;
            try
            {
                byte[] raw = File.ReadAllBytes(path);
                string text = Qqtang.Core.Protocol.LegacyTextEncoding.DecodeNullTerminated(raw);
                var rx = new System.Text.RegularExpressions.Regex(
                    @"^\(\s*([0-9]+),\s*'([^']+)',\s*'([^']*)',\s*[0-9]+,\s*[0-9]+,\s*[0-9]+,\s*'([^']+\.map)'",
                    System.Text.RegularExpressions.RegexOptions.Multiline);
                var parsedPve = new List<(ushort id, string name)>();
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(text))
                {
                    if (!ushort.TryParse(m.Groups[1].Value, out ushort id) || id == 0) continue;
                    string fam = m.Groups[2].Value.Trim().ToLowerInvariant();
                    string name = m.Groups[3].Value.Trim();
                    if (fam == "pve")
                    {
                        parsedPve.Add((id, string.IsNullOrEmpty(name) ? $"探险关卡 #{id}" : $"{name} (#{id})"));
                    }
                    else if (MapsById.TryGetValue(id, out var existing))
                    {
                        existing.Family = fam;
                    }
                    else
                    {
                        AddMap(id, string.IsNullOrEmpty(name) ? $"地图#{id}" : name, true, fam);
                    }
                }
                if (parsedPve.Count > 0)
                {
                    AdventureMaps.Clear();
                    foreach (var p in parsedPve)
                    {
                        var entry = new MapCatalogEntry { MapId = p.id, Name = p.name, Family = "pve", IsAdventure = true };
                        AdventureMaps.Add(entry);
                        MapsById[p.id] = entry;
                    }
                }
            }
            catch { }
        }

        private void TryLoadMaps(string path)
        {
            if (!File.Exists(path) || Maps.Count > 20) return;
            try
            {
                var root = MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null || !root.TryGetValue("maps", out var mapsObj) || !(mapsObj is List<object> arr)) return;
                Maps.Clear();
                MapsById.Clear();
                foreach (var adv in AdventureMaps) MapsById[adv.MapId] = adv;
                foreach (var item in arr)
                {
                    if (!(item is Dictionary<string, object> obj)) continue;
                    ushort id = (ushort)GetInt(obj, "map_id");
                    string name = GetString(obj, "map_name");
                    bool tf = GetBool(obj, "transformation_items_enabled");
                    if (id > 0) AddMap(id, string.IsNullOrEmpty(name) ? $"地图#{id}" : name, tf);
                }
            }
            catch { }
        }

        private void TryLoadItems(string path)
        {
            if (!File.Exists(path) || ItemsById.Count > 200) return;
            try
            {
                var arr = MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8)) as List<object>;
                if (arr == null) return;
                foreach (var item in arr)
                {
                    if (!(item is Dictionary<string, object> obj)) continue;
                    int rawId = GetInt(obj, "id");
                    if (rawId <= 0 || rawId > ushort.MaxValue) continue;
                    string cat = "item";
                    if (obj.TryGetValue("categories", out var catsObj) && catsObj is List<object> cats && cats.Count > 0)
                        cat = cats[0]?.ToString() ?? "item";
                    ItemsById[(ushort)rawId] = new ItemCatalogEntry
                    {
                        Id = (ushort)rawId,
                        Name = GetString(obj, "name"),
                        Kind = GetString(obj, "kind"),
                        Category = cat
                    };
                }
            }
            catch { }
        }

        private void TryLoadCombineRecipes(string path)
        {
            if (!File.Exists(path) || CombineRecipes.Count > 0) return;
            try
            {
                var root = MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null || !root.TryGetValue("recipes", out var listObj) || !(listObj is List<object> arr)) return;
                foreach (var item in arr)
                {
                    if (!(item is Dictionary<string, object> obj)) continue;
                    var rec = new CombineRecipeEntry
                    {
                        Category = GetString(obj, "category"),
                        Name = GetString(obj, "name"),
                        BookItemId = (uint)GetInt(obj, "book_item_id"),
                        ProductItemId = (uint)GetInt(obj, "product_item_id"),
                        Description = GetString(obj, "description")
                    };
                    if (obj.TryGetValue("materials", out var matsObj) && matsObj is List<object> mats)
                    {
                        foreach (var mObj in mats)
                        {
                            if (!(mObj is Dictionary<string, object> m)) continue;
                            rec.Materials.Add(((uint)GetInt(m, "item_id"), GetString(m, "name"), Math.Max(1, GetInt(m, "quantity"))));
                        }
                    }
                    if (rec.ProductItemId > 0) CombineRecipes.Add(rec);
                }
            }
            catch { }
        }

        private void TryLoadPets(string path)
        {
            if (!File.Exists(path) || PetsByTypeId.Count > 0) return;
            try
            {
                var root = MiniJson.Parse(File.ReadAllText(path, Encoding.UTF8)) as Dictionary<string, object>;
                if (root == null || !root.TryGetValue("definitions", out var defsObj) || !(defsObj is List<object> arr)) return;
                foreach (var item in arr)
                {
                    if (!(item is Dictionary<string, object> obj)) continue;
                    uint typeId = (uint)GetInt(obj, "pet_type_id");
                    if (typeId == 0) continue;
                    PetsByTypeId[typeId] = new PetCatalogEntry
                    {
                        PetTypeId = typeId,
                        Name = GetString(obj, "name"),
                        Description = GetString(obj, "description")
                    };
                }
            }
            catch { }
        }

        private void TryLoadCommodityIni(string path)
        {
            if (!File.Exists(path) || ShopCommodities.Count > 100) return;
            try
            {
                byte[] raw = File.ReadAllBytes(path);
                string text = Qqtang.Core.Protocol.LegacyTextEncoding.DecodeNullTerminated(raw);
                var parsed = new List<ShopCommodityEntry>();
                ShopCommodityEntry current = null;
                foreach (string rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();
                    if (line.Length == 0) continue;
                    if (line[0] == '[')
                    {
                        if (current != null && current.CommodityId > 0 && current.ItemId > 0)
                            parsed.Add(current);
                        current = line.StartsWith("[COMMODITY.", StringComparison.OrdinalIgnoreCase)
                            ? new ShopCommodityEntry()
                            : null;
                        continue;
                    }
                    if (current == null) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = line.Substring(0, eq).Trim().ToUpperInvariant();
                    string val = line.Substring(eq + 1).Trim();
                    switch (key)
                    {
                        case "COMMODITYID": uint.TryParse(val, out current.CommodityId); break;
                        case "COMMODITYNAME": current.Name = val; break;
                        case "COMMODITYTYPE": ushort.TryParse(val, out current.CommodityType); break;
                        case "QPRICE": uint.TryParse(val, out current.QPrice); break;
                        case "QQTANGPRICE": uint.TryParse(val, out current.SugarPrice); break;
                        case "ITEM.1.ID": ushort.TryParse(val, out current.ItemId); break;
                        case "ITEM.1.NUM": uint.TryParse(val, out current.Quantity); break;
                    }
                }
                if (current != null && current.CommodityId > 0 && current.ItemId > 0)
                    parsed.Add(current);
                if (parsed.Count > 0)
                {
                    ShopCommodities.Clear();
                    ShopCommodities.AddRange(parsed);
                }
            }
            catch { }
        }

        private static string GetString(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) && v != null ? v.ToString() : string.Empty;

        private static int GetInt(Dictionary<string, object> d, string k)
        {
            if (!d.TryGetValue(k, out var v) || v == null) return 0;
            if (v is long l) return (int)l;
            if (v is double dbl) return (int)dbl;
            int.TryParse(v.ToString(), out int r);
            return r;
        }

        private static bool GetBool(Dictionary<string, object> d, string k) =>
            d.TryGetValue(k, out var v) && v is bool b && b;

        // Minimal self-contained JSON parser so Qqtang.Core stays dependency-free.
        private sealed class MiniJson
        {
            private readonly string _s;
            private int _i;

            private MiniJson(string s) { _s = s; }

            public static object Parse(string s)
            {
                if (string.IsNullOrEmpty(s)) return null;
                var p = new MiniJson(s);
                return p.ReadValue();
            }

            private void SkipWs()
            {
                while (_i < _s.Length && char.IsWhiteSpace(_s[_i])) _i++;
            }

            private object ReadValue()
            {
                SkipWs();
                if (_i >= _s.Length) return null;
                char c = _s[_i];
                if (c == '{') return ReadObject();
                if (c == '[') return ReadArray();
                if (c == '"') return ReadString();
                if (c == 't') { _i += 4; return true; }
                if (c == 'f') { _i += 5; return false; }
                if (c == 'n') { _i += 4; return null; }
                return ReadNumber();
            }

            private Dictionary<string, object> ReadObject()
            {
                var d = new Dictionary<string, object>();
                _i++; // '{'
                while (true)
                {
                    SkipWs();
                    if (_i >= _s.Length) break;
                    if (_s[_i] == '}') { _i++; break; }
                    string key = ReadString();
                    SkipWs();
                    if (_i < _s.Length && _s[_i] == ':') _i++;
                    d[key] = ReadValue();
                    SkipWs();
                    if (_i < _s.Length && _s[_i] == ',') _i++;
                }
                return d;
            }

            private List<object> ReadArray()
            {
                var list = new List<object>();
                _i++; // '['
                while (true)
                {
                    SkipWs();
                    if (_i >= _s.Length) break;
                    if (_s[_i] == ']') { _i++; break; }
                    list.Add(ReadValue());
                    SkipWs();
                    if (_i < _s.Length && _s[_i] == ',') _i++;
                }
                return list;
            }

            private string ReadString()
            {
                var sb = new StringBuilder();
                _i++; // '"'
                while (_i < _s.Length)
                {
                    char c = _s[_i++];
                    if (c == '"') break;
                    if (c == '\\' && _i < _s.Length)
                    {
                        char e = _s[_i++];
                        switch (e)
                        {
                            case '"': sb.Append('"'); break;
                            case '\\': sb.Append('\\'); break;
                            case '/': sb.Append('/'); break;
                            case 'n': sb.Append('\n'); break;
                            case 'r': sb.Append('\r'); break;
                            case 't': sb.Append('\t'); break;
                            case 'u':
                                if (_i + 4 <= _s.Length &&
                                    int.TryParse(_s.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                {
                                    sb.Append((char)code);
                                    _i += 4;
                                }
                                break;
                            default: sb.Append(e); break;
                        }
                    }
                    else sb.Append(c);
                }
                return sb.ToString();
            }

            private object ReadNumber()
            {
                int start = _i;
                bool isFloat = false;
                while (_i < _s.Length)
                {
                    char c = _s[_i];
                    if (c == '.' || c == 'e' || c == 'E') isFloat = true;
                    if ((c >= '0' && c <= '9') || c == '-' || c == '+' || c == '.' || c == 'e' || c == 'E')
                        _i++;
                    else break;
                }
                string token = _s.Substring(start, _i - start);
                if (!isFloat && long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out long l))
                    return l;
                double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double d);
                return d;
            }
        }
    }
}
