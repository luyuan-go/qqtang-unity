using System;
using System.Collections.Generic;
using Qqtang.Core.Protocol;
using Qqtang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace Qqtang.UnityClient.UI
{
    /// <summary>
    /// Waiting Room screen: 8 seat cards, role picker, team bar, map picker modal (326 maps), room settings modal, special cards, and room chat.
    /// </summary>
    public sealed class RoomPanelView
    {
        private readonly UiContext _ctx;
        public readonly RectTransform Root;

        private readonly Text _roomTitleText;
        private readonly Text _mapLabelText;
        private readonly Button _selectMapBtn;
        private readonly Button _settingsBtn;
        private readonly Button _modeCycleBtn;
        private readonly Button _bgBtn;
        private readonly Button _primaryActionBtn;
        private readonly Text _primaryActionLabel;

        private readonly RectTransform _seatsGrid;
        private readonly RectTransform _rolesRow;
        private readonly RectTransform _chatContent;
        private readonly InputField _chatInput;
        private readonly Text _cardsStatusText;

        // Map picker modal
        private readonly RectTransform _mapModal;
        private readonly InputField _mapSearchInput;
        private readonly RectTransform _mapListContent;

        // Room settings modal
        private readonly RectTransform _settingsModal;
        private readonly InputField _setNameInput;
        private readonly InputField _setPwdInput;
        private readonly Toggle _setFreeToggle;

        public RoomPanelView(Transform parent, UiContext ctx)
        {
            _ctx = ctx;
            Root = UiFactory.CreatePanel(parent, "RoomPanel", UiFactory.BgInkDeep);
            UiFactory.Stretch(Root);
            UiFactory.AddVLayout(Root.gameObject, 10f, 14, 12);

            // Header bar
            var header = UiFactory.CreatePanel(Root, "RoomHeader", UiFactory.BgSlateCard, UiFactory.BorderGold);
            UiFactory.SetLayout(header.gameObject, prefH: 54f);
            UiFactory.AddHLayout(header.gameObject, 8f, 12, 8);

            _roomTitleText = UiFactory.CreateText(header, "房间 #0", 18, UiFactory.AccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(_roomTitleText.gameObject, flexW: 1f, prefH: 36f);

            _mapLabelText = UiFactory.CreateText(header, "地图: 包子01 (201)", 14, UiFactory.TextLight, TextAnchor.MiddleRight);
            UiFactory.SetLayout(_mapLabelText.gameObject, prefW: 190f, prefH: 36f);

            _selectMapBtn = UiFactory.CreateButton(header, "选地图", OpenMapModal, UiFactory.AccentCeladon, fontSize: 13, width: 76f, height: 34f);
            _modeCycleBtn = UiFactory.CreateButton(header, "切模式", CycleRoomMode, UiFactory.AccentMuted, fontSize: 13, width: 76f, height: 34f);
            _bgBtn = UiFactory.CreateButton(header, "换背景", () => _ctx.App.RequestCycleChatBackground(), UiFactory.AccentMuted, fontSize: 13, width: 76f, height: 34f);
            UiFactory.CreateButton(header, "藏宝室", () => _ctx.App.RequestEnterTreasureRoom(), UiFactory.AccentGold, UiFactory.TextInk, fontSize: 13, width: 72f, height: 34f);
            _settingsBtn = UiFactory.CreateButton(header, "设置", OpenSettingsModal, UiFactory.AccentMuted, fontSize: 13, width: 64f, height: 34f);
            UiFactory.CreateButton(header, "离开房间", () => _ctx.App.RequestLeaveRoom(), UiFactory.AccentDanger, fontSize: 13, width: 86f, height: 34f);

            // Middle content (Left = 8 seats + team/role/cards, Right = Room Chat)
            var body = UiFactory.CreateEmpty(Root, "Body");
            UiFactory.SetLayout(body.gameObject, flexW: 1f, flexH: 1f);
            UiFactory.AddHLayout(body.gameObject, 10f, 0, 0);

            var left = UiFactory.CreateEmpty(body, "Left");
            UiFactory.SetLayout(left.gameObject, flexW: 2.1f, flexH: 1f);
            UiFactory.AddVLayout(left.gameObject, 10f, 0, 0);

            // 8 Seats Grid (4 cols x 2 rows)
            var seatsWrap = UiFactory.CreatePanel(left, "SeatsWrap", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.SetLayout(seatsWrap.gameObject, flexW: 1f, prefH: 300f);
            _seatsGrid = seatsWrap;
            var grid = seatsWrap.gameObject.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(182f, 132f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(12, 12, 12, 12);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;

            // Team + Special Cards bar
            var controlsCard = UiFactory.CreatePanel(left, "ControlsCard", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.SetLayout(controlsCard.gameObject, flexW: 1f, flexH: 1f);
            UiFactory.AddVLayout(controlsCard.gameObject, 8f, 12, 10);

            var teamRow = UiFactory.CreateEmpty(controlsCard, "TeamRow");
            UiFactory.AddHLayout(teamRow.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(teamRow.gameObject, prefH: 32f);
            var tLbl = UiFactory.CreateText(teamRow, "队伍:", 14, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(tLbl.gameObject, prefW: 48f, prefH: 30f);
            for (byte t = 1; t <= 8; t++)
            {
                byte team = t;
                UiFactory.CreateButton(teamRow, $"队{t}", () => _ctx.App.RequestChangeTeam(team), UiFactory.TeamBadgeColor(team), fontSize: 12, width: 46f, height: 28f);
            }

            // Role selector scroll
            var roleLbl = UiFactory.CreateText(controlsCard, "切换角色:", 13, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(roleLbl.gameObject, prefH: 20f);
            var roleScrollRoot = UiFactory.CreatePanel(controlsCard, "RoleScroll", new Color(0.07f, 0.11f, 0.15f, 0.85f));
            UiFactory.SetLayout(roleScrollRoot.gameObject, flexW: 1f, prefH: 52f);
            var sr = roleScrollRoot.gameObject.AddComponent<ScrollRect>();
            sr.horizontal = true;
            sr.vertical = false;
            var rViewport = UiFactory.CreatePanel(roleScrollRoot, "VP", new Color(1, 1, 1, 0.01f));
            UiFactory.Stretch(rViewport, 4, 4, 4, 4);
            rViewport.gameObject.AddComponent<RectMask2D>();
            var rContent = UiFactory.CreateEmpty(rViewport, "Roles");
            rContent.anchorMin = new Vector2(0, 0);
            rContent.anchorMax = new Vector2(0, 1);
            rContent.pivot = new Vector2(0, 0.5f);
            UiFactory.AddHLayout(rContent.gameObject, 6f, 4, 4);
            var csf = rContent.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.viewport = rViewport;
            sr.content = rContent;
            _rolesRow = rContent;
            BuildRoleButtons();

            // Special cards row
            var cardRow = UiFactory.CreateEmpty(controlsCard, "SpecialCards");
            UiFactory.AddHLayout(cardRow.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(cardRow.gameObject, prefH: 34f);
            _cardsStatusText = UiFactory.CreateText(cardRow, "功能卡:", 13, UiFactory.TextSubtle);
            UiFactory.SetLayout(_cardsStatusText.gameObject, prefW: 110f, prefH: 30f);
            UiFactory.CreateButton(cardRow, "AI对战卡", () => _ctx.App.RequestToggleSpecialCard(SpecialItemIds.CompetitiveAiCard), UiFactory.AccentCeladon, fontSize: 12, width: 94f, height: 30f);
            UiFactory.CreateButton(cardRow, "单人BOSS卡", () => _ctx.App.RequestToggleSpecialCard(SpecialItemIds.SinglePlayerBossCard), UiFactory.AccentCeladon, fontSize: 12, width: 100f, height: 30f);
            UiFactory.CreateButton(cardRow, "单人探险卡", () => _ctx.App.RequestToggleSpecialCard(SpecialItemIds.SinglePlayerAdventureCard), UiFactory.AccentCeladon, fontSize: 12, width: 100f, height: 30f);

            // Right column: Room Chat + Primary Start/Ready button
            var right = UiFactory.CreatePanel(body, "RoomChatCard", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.SetLayout(right.gameObject, flexW: 1f, flexH: 1f);
            UiFactory.AddVLayout(right.gameObject, 8f, 10, 10);

            var cHdr = UiFactory.CreateText(right, "房间聊天", 15, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(cHdr.gameObject, prefH: 24f);
            var cScroll = UiFactory.CreateScrollView(right, "RoomChatScroll", out _chatContent, spacing: 3f);
            UiFactory.SetLayout(cScroll.gameObject, flexW: 1f, flexH: 1f);

            var cInputRow = UiFactory.CreateEmpty(right, "ChatInput");
            UiFactory.AddHLayout(cInputRow.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(cInputRow.gameObject, prefH: 34f);
            _chatInput = UiFactory.CreateInputField(cInputRow, "输入房间聊天...", height: 32f);
            UiFactory.CreateButton(cInputRow, "发送", SendRoomChat, UiFactory.AccentCeladon, fontSize: 13, width: 64f, height: 32f);

            _primaryActionBtn = UiFactory.CreateButton(right, "开始对局", OnPrimaryAction, UiFactory.AccentVermilion, fontSize: 18, height: 48f);
            UiFactory.SetLayout(_primaryActionBtn.gameObject, flexW: 1f, prefH: 48f);
            _primaryActionLabel = _primaryActionBtn.GetComponentInChildren<Text>();

            // Map Picker Modal
            _mapModal = UiFactory.CreatePanel(Root, "MapPickerModal", UiFactory.BgModalBackdrop);
            UiFactory.Stretch(_mapModal);
            var mCard = UiFactory.CreatePanel(_mapModal, "Card", UiFactory.BgSlateCard, UiFactory.AccentGold);
            mCard.anchorMin = mCard.anchorMax = mCard.pivot = new Vector2(0.5f, 0.5f);
            mCard.sizeDelta = new Vector2(540f, 520f);
            UiFactory.AddVLayout(mCard.gameObject, 8f, 16, 14);
            var mTop = UiFactory.CreateEmpty(mCard, "Top");
            UiFactory.AddHLayout(mTop.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(mTop.gameObject, prefH: 34f);
            var mTitle = UiFactory.CreateText(mTop, "选择竞技地图", 17, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(mTitle.gameObject, flexW: 1f, prefH: 32f);
            UiFactory.CreateButton(mTop, "关闭", () => _mapModal.gameObject.SetActive(false), UiFactory.AccentMuted, width: 68f, height: 30f);

            _mapSearchInput = UiFactory.CreateInputField(mCard, "按地图名称或ID搜索（如：包子、水面、201）...", "");
            _mapSearchInput.onValueChanged.AddListener(_ => PopulateMapList());
            var mScroll = UiFactory.CreateScrollView(mCard, "Maps", out _mapListContent, spacing: 4f);
            UiFactory.SetLayout(mScroll.gameObject, flexW: 1f, flexH: 1f);
            _mapModal.gameObject.SetActive(false);

            // Settings Modal
            _settingsModal = UiFactory.CreatePanel(Root, "SettingsModal", UiFactory.BgModalBackdrop);
            UiFactory.Stretch(_settingsModal);
            var sCard = UiFactory.CreatePanel(_settingsModal, "Card", UiFactory.BgSlateCard, UiFactory.AccentGold);
            sCard.anchorMin = sCard.anchorMax = sCard.pivot = new Vector2(0.5f, 0.5f);
            sCard.sizeDelta = new Vector2(400f, 240f);
            UiFactory.AddVLayout(sCard.gameObject, 10f, 18, 16);
            var sTitle = UiFactory.CreateText(sCard, "修改房间设置", 17, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetLayout(sTitle.gameObject, prefH: 26f);
            _setNameInput = UiFactory.CreateInputField(sCard, "房间名", "");
            _setPwdInput = UiFactory.CreateInputField(sCard, "密码（留空则移除密码）", "");
            _setFreeToggle = UiFactory.CreateToggle(sCard, "自由组队 (Free Rule)", false);
            var sBtns = UiFactory.CreateEmpty(sCard, "Btns");
            UiFactory.AddHLayout(sBtns.gameObject, 10f, 0, 0, TextAnchor.MiddleCenter);
            UiFactory.SetLayout(sBtns.gameObject, prefH: 36f);
            UiFactory.CreateButton(sBtns, "取消", () => _settingsModal.gameObject.SetActive(false), UiFactory.AccentMuted, width: 100f, height: 34f);
            UiFactory.CreateButton(sBtns, "保存设置", SaveSettings, UiFactory.AccentCeladon, width: 120f, height: 34f);
            _settingsModal.gameObject.SetActive(false);
        }

        private void BuildRoleButtons()
        {
            UiFactory.ClearChildren(_rolesRow);
            foreach (var role in StaticCatalogs.Roles)
            {
                byte rid = role.RoleId;
                UiFactory.CreateButton(_rolesRow, $"{role.DaoistName}({rid})", () => _ctx.App.RequestChangeRole(rid),
                    role.PurpleDiamondOnly ? new Color(0.42f, 0.24f, 0.52f, 1f) : UiFactory.AccentMuted,
                    fontSize: 12, width: 104f, height: 38f);
            }
        }

        public void SetVisible(bool visible) => Root.gameObject.SetActive(visible);

        public void Refresh()
        {
            var s = _ctx.Store;
            bool isOwner = s.IsRoomOwner;
            string modeName = LobbyAndNavView.ModeLabel(s.GameType);
            _roomTitleText.text = $"房间 #{s.RoomId} · {s.RoomName}  [{modeName}]" + (isOwner ? " (房主)" : "");
            _mapLabelText.text = $"地图: {_ctx.Catalogs.GetMapName(s.MapId)} ({s.MapId})";
            _selectMapBtn.interactable = isOwner;
            _settingsBtn.interactable = isOwner;
            _modeCycleBtn.interactable = isOwner;
            _bgBtn.gameObject.SetActive(s.GameType == RoomGameType.Chat && isOwner);

            // Cards summary
            string activeCards = string.Empty;
            if (s.HasActiveItem(SpecialItemIds.CompetitiveAiCard)) activeCards += "AI ";
            if (s.HasActiveItem(SpecialItemIds.SinglePlayerBossCard)) activeCards += "BOSS ";
            if (s.HasActiveItem(SpecialItemIds.SinglePlayerAdventureCard)) activeCards += "单人探险 ";
            _cardsStatusText.text = string.IsNullOrEmpty(activeCards) ? "功能卡: 未启用" : $"已启: {activeCards.Trim()}";

            // Primary action button
            if (isOwner)
            {
                _primaryActionLabel.text = "开始对局";
                _primaryActionBtn.GetComponent<Image>().color = UiFactory.AccentVermilion;
            }
            else
            {
                var me = s.RoomMembers.Find(m => m.PlayerId == s.PlayerId);
                bool ready = me != null && me.Status == 1;
                _primaryActionLabel.text = ready ? "取消准备" : "准备就绪";
                _primaryActionBtn.GetComponent<Image>().color = ready ? UiFactory.AccentMuted : UiFactory.AccentCeladon;
            }

            // 8 Seat Cards
            UiFactory.ClearChildren(_seatsGrid);
            for (byte seat = 1; seat <= 8; seat++)
            {
                var member = s.RoomMembers.Find(m => m.SeatId == seat);
                var status = s.SeatStatus[seat - 1];
                var card = UiFactory.CreatePanel(_seatsGrid, $"Seat_{seat}",
                    member != null ? UiFactory.BgSlateLight : new Color(0.09f, 0.14f, 0.18f, 0.85f),
                    member != null && member.PlayerId == s.PlayerId ? UiFactory.AccentGold : UiFactory.BorderSubtle);
                UiFactory.AddVLayout(card.gameObject, 4f, 8, 6);

                if (member != null)
                {
                    var top = UiFactory.CreateEmpty(card, "Top");
                    UiFactory.AddHLayout(top.gameObject, 4f, 0, 0);
                    UiFactory.SetLayout(top.gameObject, prefH: 20f);
                    var teamTag = UiFactory.CreateText(top, $"队{member.TeamId}", 11, UiFactory.TeamBadgeColor(member.TeamId), FontStyle.Bold);
                    UiFactory.SetLayout(teamTag.gameObject, prefW: 34f, prefH: 18f);
                    string roleState = member.PlayerId == s.RoomOwnerId ? "房主" : (member.Status == 1 ? "已准备" : "未准备");
                    var stTxt = UiFactory.CreateText(top, roleState, 11, member.Status == 1 || member.PlayerId == s.RoomOwnerId ? UiFactory.AccentGold : UiFactory.TextSubtle, TextAnchor.MiddleRight);
                    UiFactory.SetLayout(stTxt.gameObject, flexW: 1f, prefH: 18f);

                    var mid = UiFactory.CreateEmpty(card, "Mid");
                    UiFactory.AddHLayout(mid.gameObject, 6f, 0, 0);
                    UiFactory.SetLayout(mid.gameObject, prefH: 52f);
                    var pGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                    pGo.transform.SetParent(mid, false);
                    var pImg = pGo.GetComponent<Image>();
                    pImg.preserveAspect = true;
                    byte rId = member.GameInfo != null ? member.GameInfo.RoleId : (byte)1;
                    pImg.sprite = _ctx.Art.GetCharacterSprite(rId, "S", UiFactory.TeamBadgeColor(member.TeamId));
                    UiFactory.SetLayout(pGo, prefW: 48f, prefH: 48f);

                    var nameCol = UiFactory.CreateEmpty(mid, "Names");
                    UiFactory.SetLayout(nameCol.gameObject, flexW: 1f, prefH: 48f);
                    UiFactory.AddVLayout(nameCol.gameObject, 2f, 0, 0);
                    var nTxt = UiFactory.CreateText(nameCol, member.Nickname, 13, UiFactory.TextLight, FontStyle.Bold);
                    UiFactory.SetLayout(nTxt.gameObject, prefH: 20f);
                    var rTxt = UiFactory.CreateText(nameCol, StaticCatalogs.GetRole(rId).DaoistName, 11, UiFactory.TextSubtle);
                    UiFactory.SetLayout(rTxt.gameObject, prefH: 18f);

                    if (isOwner && member.PlayerId != s.PlayerId)
                    {
                        ushort tPid = member.PlayerId;
                        uint tUin = member.Uin;
                        UiFactory.CreateButton(card, "踢出房间", () => _ctx.App.RequestKickPlayer(tPid, tUin), UiFactory.AccentDanger, fontSize: 11, height: 24f);
                    }
                }
                else
                {
                    bool locked = status == EnterRoomSeatStatus.Locked;
                    var emptyTxt = UiFactory.CreateText(card, locked ? $"座位 {seat}\n[已锁定]" : $"座位 {seat}\n[空位]", 13,
                        locked ? UiFactory.AccentVermilion : UiFactory.TextSubtle, TextAnchor.MiddleCenter);
                    UiFactory.SetLayout(emptyTxt.gameObject, flexH: 1f);
                    if (isOwner)
                    {
                        byte capturedSeat = seat;
                        bool nextLock = !locked;
                        UiFactory.CreateButton(card, locked ? "解锁座位" : "锁定座位", () => _ctx.App.RequestSetSeatStatus(capturedSeat, nextLock),
                            UiFactory.AccentMuted, fontSize: 11, height: 24f);
                    }
                }
            }

            // Room chat
            UiFactory.ClearChildren(_chatContent);
            foreach (var msg in s.RoomChat)
            {
                var line = UiFactory.CreateText(_chatContent,
                    $"[{msg.Timestamp}] {msg.Sender}: {msg.Content}",
                    13, msg.Highlight ? UiFactory.AccentGold : UiFactory.TextLight);
                UiFactory.SetLayout(line.gameObject, prefH: 20f);
            }
        }

        private void OnPrimaryAction()
        {
            if (_ctx.Store.IsRoomOwner)
            {
                _ctx.App.RequestStartGame();
            }
            else
            {
                var me = _ctx.Store.RoomMembers.Find(m => m.PlayerId == _ctx.Store.PlayerId);
                bool ready = me != null && me.Status == 1;
                _ctx.App.RequestSetReady(!ready);
            }
        }

        private void CycleRoomMode()
        {
            var cur = _ctx.Store.GameType;
            RoomGameType next;
            switch (cur)
            {
                case RoomGameType.CompetitiveNoItem: next = RoomGameType.CompetitiveItem; break;
                case RoomGameType.CompetitiveItem: next = RoomGameType.Adventure; break;
                case RoomGameType.Adventure: next = RoomGameType.Chat; break;
                default: next = RoomGameType.CompetitiveNoItem; break;
            }
            _ctx.App.RequestChangeRoomType(next);
        }

        private void OpenMapModal()
        {
            _mapModal.gameObject.SetActive(true);
            PopulateMapList();
        }

        private void PopulateMapList()
        {
            UiFactory.ClearChildren(_mapListContent);
            string q = (_mapSearchInput.text ?? string.Empty).Trim();
            int shown = 0;
            foreach (var m in _ctx.Catalogs.Maps)
            {
                if (!string.IsNullOrEmpty(q) &&
                    m.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    !m.MapId.ToString().Contains(q))
                    continue;

                ushort id = m.MapId;
                var row = UiFactory.CreatePanel(_mapListContent, $"Map_{id}", UiFactory.BgSlateLight);
                UiFactory.SetLayout(row.gameObject, prefH: 34f);
                UiFactory.AddHLayout(row.gameObject, 8f, 10, 4);
                var lbl = UiFactory.CreateText(row, $"#{id}  {m.Name}", 13, id == _ctx.Store.MapId ? UiFactory.AccentGold : UiFactory.TextLight);
                UiFactory.SetLayout(lbl.gameObject, flexW: 1f, prefH: 26f);
                UiFactory.CreateButton(row, "选用", () =>
                {
                    _mapModal.gameObject.SetActive(false);
                    _ctx.App.RequestSelectMap(id);
                }, UiFactory.AccentCeladon, fontSize: 12, width: 64f, height: 26f);

                if (++shown >= 80) break; // Keep scroll smooth; users narrow with search box
            }
        }

        private void OpenSettingsModal()
        {
            _setNameInput.text = _ctx.Store.RoomName;
            _setPwdInput.text = string.Empty;
            _setFreeToggle.isOn = (_ctx.Store.RoomFlag & (byte)RoomPropertyFlag.FreeRule) != 0;
            _settingsModal.gameObject.SetActive(true);
        }

        private void SaveSettings()
        {
            string name = string.IsNullOrWhiteSpace(_setNameInput.text) ? _ctx.Store.RoomName : _setNameInput.text.Trim();
            string pwd = _setPwdInput.text.Trim();
            RoomPropertyFlag flag = RoomPropertyFlag.Standard;
            if (!string.IsNullOrEmpty(pwd)) flag |= RoomPropertyFlag.Password;
            if (_setFreeToggle.isOn) flag |= RoomPropertyFlag.FreeRule;
            _settingsModal.gameObject.SetActive(false);
            _ctx.App.RequestModifyRoom(name, flag, pwd);
        }

        private void SendRoomChat()
        {
            string text = _chatInput.text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            _chatInput.text = string.Empty;
            _ctx.App.RequestSendRoomChat(text);
        }
    }
}
