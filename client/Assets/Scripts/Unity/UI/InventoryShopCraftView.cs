using System;
using System.Collections.Generic;
using Qqtang.Core.Protocol;
using Qqtang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace Qqtang.UnityClient.UI
{
    /// <summary>
    /// Houses the Inventory (背包), Shop (商城), and Forge/Combine/BreakEgg (锻造与砸蛋) sub-views inside the MainShell ContentArea.
    /// </summary>
    public sealed class InventoryShopCraftView
    {
        private readonly UiContext _ctx;
        public readonly RectTransform InventoryRoot;
        public readonly RectTransform ShopRoot;
        public readonly RectTransform CraftRoot;

        // Inventory
        private readonly InputField _invSearch;
        private readonly RectTransform _invContent;
        private readonly Text _invSummary;

        // Shop
        private readonly InputField _shopSearch;
        private readonly RectTransform _shopContent;
        private readonly Text _shopStatus;
        private int _shopCategoryFilter; // 0=All, 1=Items/Cards, 2=Bubbles, 3=Rings/Eggs

        // Craft / Egg / Forge
        private readonly RectTransform _eggPanel;
        private readonly RectTransform _combinePanel;
        private readonly RectTransform _forgePanel;
        private readonly Text _eggResultText;
        private readonly InputField _eggIdInput;
        private readonly InputField _hammerIdInput;
        private readonly RectTransform _combineContent;
        private readonly InputField _forgeItemInput;
        private readonly InputField _forgeMatInput;
        private readonly Text _forgeResultText;

        public InventoryShopCraftView(Transform parent, UiContext ctx)
        {
            _ctx = ctx;

            // --- 1. INVENTORY ---
            InventoryRoot = UiFactory.CreatePanel(parent, "InventoryView", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.Stretch(InventoryRoot);
            UiFactory.AddVLayout(InventoryRoot.gameObject, 8f, 14, 12);

            var invHdr = UiFactory.CreateEmpty(InventoryRoot, "InvHdr");
            UiFactory.AddHLayout(invHdr.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(invHdr.gameObject, prefH: 36f);
            _invSummary = UiFactory.CreateText(invHdr, "我的背包 (0)", 16, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(_invSummary.gameObject, prefW: 200f, prefH: 34f);
            _invSearch = UiFactory.CreateInputField(invHdr, "搜索道具名或ID...", "");
            _invSearch.onValueChanged.AddListener(_ => RefreshInventory());
            UiFactory.CreateButton(invHdr, "刷新糖币", () => _ctx.App.RequestRefreshMoney(), UiFactory.AccentMuted, fontSize: 13, width: 92f, height: 32f);

            var invScroll = UiFactory.CreateScrollView(InventoryRoot, "InvScroll", out _invContent, spacing: 5f);
            UiFactory.SetLayout(invScroll.gameObject, flexW: 1f, flexH: 1f);

            // --- 2. SHOP ---
            ShopRoot = UiFactory.CreatePanel(parent, "ShopView", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.Stretch(ShopRoot);
            UiFactory.AddVLayout(ShopRoot.gameObject, 8f, 14, 12);

            var shopHdr = UiFactory.CreateEmpty(ShopRoot, "ShopHdr");
            UiFactory.AddHLayout(shopHdr.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(shopHdr.gameObject, prefH: 36f);
            _shopStatus = UiFactory.CreateText(shopHdr, "灵葫商城 (糖币直购)", 16, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(_shopStatus.gameObject, prefW: 240f, prefH: 34f);
            _shopSearch = UiFactory.CreateInputField(shopHdr, "搜索商品名称或ID...", "");
            _shopSearch.onValueChanged.AddListener(_ => RefreshShop());
            UiFactory.CreateButton(shopHdr, "同步目录", () => _ctx.App.RequestQueryShopCatalog(), UiFactory.AccentCeladon, fontSize: 13, width: 88f, height: 32f);

            var shopFilters = UiFactory.CreateEmpty(ShopRoot, "ShopFilters");
            UiFactory.AddHLayout(shopFilters.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(shopFilters.gameObject, prefH: 28f);
            UiFactory.CreateButton(shopFilters, "全部商品", () => { _shopCategoryFilter = 0; RefreshShop(); }, UiFactory.AccentMuted, fontSize: 12, width: 82f, height: 26f);
            UiFactory.CreateButton(shopFilters, "功能/道具", () => { _shopCategoryFilter = 1; RefreshShop(); }, UiFactory.AccentMuted, fontSize: 12, width: 82f, height: 26f);
            UiFactory.CreateButton(shopFilters, "糖泡皮肤", () => { _shopCategoryFilter = 2; RefreshShop(); }, UiFactory.AccentMuted, fontSize: 12, width: 82f, height: 26f);
            UiFactory.CreateButton(shopFilters, "彩蛋/戒指", () => { _shopCategoryFilter = 3; RefreshShop(); }, UiFactory.AccentMuted, fontSize: 12, width: 82f, height: 26f);

            var shopScroll = UiFactory.CreateScrollView(ShopRoot, "ShopScroll", out _shopContent, spacing: 5f);
            UiFactory.SetLayout(shopScroll.gameObject, flexW: 1f, flexH: 1f);

            // --- 3. FORGE / COMBINE / BREAK EGG ---
            CraftRoot = UiFactory.CreatePanel(parent, "CraftView", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.Stretch(CraftRoot);
            UiFactory.AddVLayout(CraftRoot.gameObject, 10f, 14, 12);

            var craftTabs = UiFactory.CreateEmpty(CraftRoot, "CraftTabs");
            UiFactory.AddHLayout(craftTabs.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(craftTabs.gameObject, prefH: 36f);
            UiFactory.CreateButton(craftTabs, "砸彩蛋 (0x00FE)", () => SelectCraftSubTab(0), UiFactory.AccentVermilion, fontSize: 13, width: 140f, height: 34f);
            UiFactory.CreateButton(craftTabs, "配方合成 (0x00CF)", () => SelectCraftSubTab(1), UiFactory.AccentCeladon, fontSize: 13, width: 150f, height: 34f);
            UiFactory.CreateButton(craftTabs, "形象锻造 (0x0353)", () => SelectCraftSubTab(2), UiFactory.AccentMuted, fontSize: 13, width: 150f, height: 34f);

            var craftBody = UiFactory.CreateEmpty(CraftRoot, "CraftBody");
            UiFactory.SetLayout(craftBody.gameObject, flexW: 1f, flexH: 1f);

            // Sub 0: Break Egg
            _eggPanel = UiFactory.CreatePanel(craftBody, "EggPanel", UiFactory.BgSlateLight, UiFactory.BorderGold);
            UiFactory.Stretch(_eggPanel);
            UiFactory.AddVLayout(_eggPanel.gameObject, 12f, 20, 18);
            var eggTitle = UiFactory.CreateText(_eggPanel, "砸彩蛋抽奖（背包需持有对应彩蛋与锤子，可在商城用糖币购买）", 15, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(eggTitle.gameObject, prefH: 26f);

            var eggQuick = UiFactory.CreateEmpty(_eggPanel, "EggQuick");
            UiFactory.AddHLayout(eggQuick.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(eggQuick.gameObject, prefH: 34f);
            UiFactory.CreateButton(eggQuick, "铁蛋(9001)+铁锤(9011)", () => { _eggIdInput.text = "9001"; _hammerIdInput.text = "9011"; }, UiFactory.AccentMuted, fontSize: 12, width: 165f, height: 30f);
            UiFactory.CreateButton(eggQuick, "银蛋(9002)+银锤(9012)", () => { _eggIdInput.text = "9002"; _hammerIdInput.text = "9012"; }, UiFactory.AccentMuted, fontSize: 12, width: 165f, height: 30f);
            UiFactory.CreateButton(eggQuick, "金蛋(9003)+金锤(9013)", () => { _eggIdInput.text = "9003"; _hammerIdInput.text = "9013"; }, UiFactory.AccentMuted, fontSize: 12, width: 165f, height: 30f);
            UiFactory.CreateButton(eggQuick, "彩蛋(9004)+金锤(9013)", () => { _eggIdInput.text = "9004"; _hammerIdInput.text = "9013"; }, UiFactory.AccentMuted, fontSize: 12, width: 165f, height: 30f);

            var eggInputs = UiFactory.CreateEmpty(_eggPanel, "EggInputs");
            UiFactory.AddHLayout(eggInputs.gameObject, 10f, 0, 0);
            UiFactory.SetLayout(eggInputs.gameObject, prefH: 38f);
            var eLbl = UiFactory.CreateText(eggInputs, "蛋ID:", 14, UiFactory.TextLight);
            UiFactory.SetLayout(eLbl.gameObject, prefW: 48f, prefH: 34f);
            _eggIdInput = UiFactory.CreateInputField(eggInputs, "9001", "9001", width: 120f);
            var hLbl = UiFactory.CreateText(eggInputs, "锤ID:", 14, UiFactory.TextLight);
            UiFactory.SetLayout(hLbl.gameObject, prefW: 48f, prefH: 34f);
            _hammerIdInput = UiFactory.CreateInputField(eggInputs, "9011", "9011", width: 120f);
            UiFactory.CreateButton(eggInputs, "立即砸蛋！", OnBreakEgg, UiFactory.AccentVermilion, fontSize: 15, width: 140f, height: 36f);

            _eggResultText = UiFactory.CreateText(_eggPanel, "结果将在此显示。", 14, UiFactory.TextSubtle);
            UiFactory.SetLayout(_eggResultText.gameObject, prefH: 50f);

            // Sub 1: Combine Forge
            _combinePanel = UiFactory.CreatePanel(craftBody, "CombinePanel", UiFactory.BgSlateLight, UiFactory.BorderSubtle);
            UiFactory.Stretch(_combinePanel);
            UiFactory.AddVLayout(_combinePanel.gameObject, 8f, 14, 12);
            var cInfo = UiFactory.CreateText(_combinePanel, "探险配方合成列表（数据源自 server/data/qqt_combine_recipes.json）", 14, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(cInfo.gameObject, prefH: 24f);
            var cScroll = UiFactory.CreateScrollView(_combinePanel, "CombineScroll", out _combineContent, spacing: 5f);
            UiFactory.SetLayout(cScroll.gameObject, flexW: 1f, flexH: 1f);

            // Sub 2: Avatar Forge
            _forgePanel = UiFactory.CreatePanel(craftBody, "ForgePanel", UiFactory.BgSlateLight, UiFactory.BorderSubtle);
            UiFactory.Stretch(_forgePanel);
            UiFactory.AddVLayout(_forgePanel.gameObject, 12f, 20, 18);
            var fTitle = UiFactory.CreateText(_forgePanel, "装扮锻造强化（使用水晶 20051~20057 强化装扮颜色与光效）", 15, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(fTitle.gameObject, prefH: 26f);
            var fRow = UiFactory.CreateEmpty(_forgePanel, "ForgeRow");
            UiFactory.AddHLayout(fRow.gameObject, 10f, 0, 0);
            UiFactory.SetLayout(fRow.gameObject, prefH: 38f);
            var fiLbl = UiFactory.CreateText(fRow, "装备ID:", 14, UiFactory.TextLight);
            UiFactory.SetLayout(fiLbl.gameObject, prefW: 64f, prefH: 34f);
            _forgeItemInput = UiFactory.CreateInputField(fRow, "例如 119", "119", width: 130f);
            var fmLbl = UiFactory.CreateText(fRow, "材料ID:", 14, UiFactory.TextLight);
            UiFactory.SetLayout(fmLbl.gameObject, prefW: 64f, prefH: 34f);
            _forgeMatInput = UiFactory.CreateInputField(fRow, "例如 20051", "20051", width: 130f);
            UiFactory.CreateButton(fRow, "开始锻造", OnForgeAvatar, UiFactory.AccentVermilion, fontSize: 14, width: 120f, height: 36f);
            _forgeResultText = UiFactory.CreateText(_forgePanel, "锻造结果将在此显示。", 14, UiFactory.TextSubtle);
            UiFactory.SetLayout(_forgeResultText.gameObject, prefH: 50f);

            SelectCraftSubTab(0);
            SetActiveView(MainTab.Lobby);
        }

        public void SetActiveView(MainTab tab)
        {
            InventoryRoot.gameObject.SetActive(tab == MainTab.Inventory);
            ShopRoot.gameObject.SetActive(tab == MainTab.Shop);
            CraftRoot.gameObject.SetActive(tab == MainTab.ForgeEgg);
            if (tab == MainTab.Inventory) RefreshInventory();
            else if (tab == MainTab.Shop) RefreshShop();
            else if (tab == MainTab.ForgeEgg) RefreshCombineRecipes();
        }

        public void RefreshAllVisible()
        {
            if (InventoryRoot.gameObject.activeSelf) RefreshInventory();
            if (ShopRoot.gameObject.activeSelf) RefreshShop();
            if (CraftRoot.gameObject.activeSelf) RefreshCombineRecipes();
        }

        public void RefreshInventory()
        {
            var inv = _ctx.Store.Inventory;
            _invSummary.text = $"我的背包 ({inv.Count} 件)";
            UiFactory.ClearChildren(_invContent);
            string q = (_invSearch.text ?? string.Empty).Trim();

            foreach (var it in inv)
            {
                string name = _ctx.Catalogs.GetItemName(it.ItemId);
                if (!string.IsNullOrEmpty(q) &&
                    name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    !it.ItemId.ToString().Contains(q))
                    continue;

                var row = UiFactory.CreatePanel(_invContent, $"Inv_{it.ItemId}", UiFactory.BgSlateLight);
                UiFactory.SetLayout(row.gameObject, prefH: 42f);
                UiFactory.AddHLayout(row.gameObject, 8f, 10, 4);

                AddItemIcon(row, it.ItemId);

                bool active = it.Status == 1;
                string statusTag = active ? "[已装备/启用]" : (it.Status == 2 ? "[已收藏]" : "[背包中]");
                var lbl = UiFactory.CreateText(row, $"#{it.ItemId}  {name}  ×{it.Quantity}   {statusTag}", 13,
                    active ? UiFactory.AccentGold : UiFactory.TextLight);
                UiFactory.SetLayout(lbl.gameObject, flexW: 1f, prefH: 32f);

                ushort capturedId = it.ItemId;
                if (_ctx.Store.InRoom)
                {
                    UiFactory.CreateButton(row, "使用", () => _ctx.App.RequestUseItemInRoom(capturedId),
                        UiFactory.AccentVermilion, fontSize: 12, width: 54f, height: 28f);
                }
                byte nextStatus = (byte)(active ? 0 : 1);
                UiFactory.CreateButton(row, active ? "卸下" : "装备/启用",
                    () => _ctx.App.RequestSetItemStatus(capturedId, nextStatus),
                    active ? UiFactory.AccentMuted : UiFactory.AccentCeladon, fontSize: 12, width: 82f, height: 28f);
                bool collected = it.Status == 2;
                byte nextCabinet = (byte)(collected ? 0 : 2);
                UiFactory.CreateButton(row, collected ? "取出" : "收藏柜",
                    () => _ctx.App.RequestSetItemStatus(capturedId, nextCabinet),
                    collected ? UiFactory.AccentGold : UiFactory.AccentMuted,
                    collected ? UiFactory.TextInk : UiFactory.TextLight,
                    fontSize: 11, width: 58f, height: 28f);
            }
        }

        private void AddItemIcon(Transform parent, ushort itemId)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconGo.transform.SetParent(parent, false);
            var img = iconGo.GetComponent<Image>();
            img.sprite = _ctx.Art.GetItemIconSprite(itemId);
            img.preserveAspect = true;
            UiFactory.SetLayout(iconGo, prefW: 28f, prefH: 28f);
        }

        public void RefreshShop()
        {
            UiFactory.ClearChildren(_shopContent);
            string q = (_shopSearch.text ?? string.Empty).Trim();
            int shown = 0;
            foreach (var c in _ctx.Catalogs.ShopCommodities)
            {
                if (_shopCategoryFilter == 1 && c.CommodityType != 513 && c.CommodityType != 515) continue;
                if (_shopCategoryFilter == 2 && c.CommodityType != 769) continue;
                if (_shopCategoryFilter == 3 && c.CommodityType != 514 && (c.ItemId < 9001 || c.ItemId > 9026)) continue;

                if (!string.IsNullOrEmpty(q) &&
                    c.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    !c.CommodityId.ToString().Contains(q) &&
                    !c.ItemId.ToString().Contains(q))
                    continue;

                var row = UiFactory.CreatePanel(_shopContent, $"Shop_{c.CommodityId}", UiFactory.BgSlateLight);
                UiFactory.SetLayout(row.gameObject, prefH: 42f);
                UiFactory.AddHLayout(row.gameObject, 8f, 10, 4);

                AddItemIcon(row, (ushort)c.ItemId);

                var lbl = UiFactory.CreateText(row, $"[{c.CommodityId}] {c.Name}  (道具#{c.ItemId} ×{c.Quantity})", 13, UiFactory.TextLight);
                UiFactory.SetLayout(lbl.gameObject, flexW: 1f, prefH: 32f);

                var price = UiFactory.CreateText(row, $"{c.SugarPrice} 糖币", 13, UiFactory.AccentGold, TextAnchor.MiddleRight, FontStyle.Bold);
                UiFactory.SetLayout(price.gameObject, prefW: 92f, prefH: 32f);

                uint cid = c.CommodityId;
                string cname = c.Name;
                UiFactory.CreateButton(row, "购买", () => _ctx.App.RequestBuyCommodity(cid, cname), UiFactory.AccentVermilion, fontSize: 12, width: 68f, height: 28f);

                if (++shown >= 80) break;
            }
            _shopStatus.text = $"灵葫商城（共 {_ctx.Catalogs.ShopCommodities.Count} 件，显示 {shown}）";
        }

        private void RefreshCombineRecipes()
        {
            UiFactory.ClearChildren(_combineContent);
            foreach (var rec in _ctx.Catalogs.CombineRecipes)
            {
                var row = UiFactory.CreatePanel(_combineContent, $"Rec_{rec.ProductItemId}", UiFactory.BgSlateCard);
                UiFactory.SetLayout(row.gameObject, prefH: 52f);
                UiFactory.AddHLayout(row.gameObject, 8f, 10, 6);

                string mats = string.Empty;
                var matIds = new List<uint>();
                foreach (var m in rec.Materials)
                {
                    mats += $"{m.name}×{m.qty} ";
                    for (int k = 0; k < m.qty; k++) matIds.Add(m.itemId);
                }

                var col = UiFactory.CreateEmpty(row, "Col");
                UiFactory.SetLayout(col.gameObject, flexW: 1f, prefH: 40f);
                UiFactory.AddVLayout(col.gameObject, 2f, 0, 0);
                var t1 = UiFactory.CreateText(col, $"[{rec.Category}] {rec.Name} (产物#{rec.ProductItemId})", 13, UiFactory.AccentGold, FontStyle.Bold);
                UiFactory.SetLayout(t1.gameObject, prefH: 18f);
                var t2 = UiFactory.CreateText(col, $"材料: {mats}", 12, UiFactory.TextSubtle);
                UiFactory.SetLayout(t2.gameObject, prefH: 18f);

                var capturedRec = rec;
                var capturedMats = matIds;
                UiFactory.CreateButton(row, "合成", () => _ctx.App.RequestCombineForge(capturedRec.ProductItemId, capturedRec.BookItemId, capturedMats),
                    UiFactory.AccentCeladon, fontSize: 12, width: 68f, height: 30f);
            }
        }

        private void SelectCraftSubTab(int idx)
        {
            _eggPanel.gameObject.SetActive(idx == 0);
            _combinePanel.gameObject.SetActive(idx == 1);
            _forgePanel.gameObject.SetActive(idx == 2);
            if (idx == 1) RefreshCombineRecipes();
        }

        private void OnBreakEgg()
        {
            if (!uint.TryParse(_eggIdInput.text.Trim(), out uint eggId) || !uint.TryParse(_hammerIdInput.text.Trim(), out uint hammerId))
            {
                _eggResultText.text = "请输入有效的彩蛋ID与锤子ID。";
                return;
            }
            _ctx.App.RequestBreakEgg(hammerId, eggId, res =>
            {
                _eggResultText.text = res.ResultId == 0
                    ? $"砸蛋成功！获得道具 #{res.ShowItemId} ({_ctx.Catalogs.GetItemName((ushort)res.ShowItemId)}) {res.AttachInfo}"
                    : $"砸蛋失败 (code={res.ResultId}): {res.AttachInfo}";
            });
        }

        private void OnForgeAvatar()
        {
            if (!uint.TryParse(_forgeItemInput.text.Trim(), out uint itemId) || !uint.TryParse(_forgeMatInput.text.Trim(), out uint matId))
            {
                _forgeResultText.text = "请输入有效的装扮ID与材料ID。";
                return;
            }
            _ctx.App.RequestForgeAvatar(itemId, matId, res =>
            {
                _forgeResultText.text = res.ResultId == 0
                    ? $"锻造成功！道具#{res.ItemId} 光效={res.Effect} 颜色={res.Color} {res.Reason}"
                    : $"锻造未成功 (code={res.ResultId}): {res.Reason}";
            });
        }
    }
}
