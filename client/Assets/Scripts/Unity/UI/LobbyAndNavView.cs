using System;
using System.Collections.Generic;
using Qqtang.Core.Protocol;
using Qqtang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace Qqtang.UnityClient.UI
{
    public enum MainTab
    {
        Lobby,
        Inventory,
        Shop,
        ForgeEgg,
        Pets,
        Social
    }

    /// <summary>
    /// Top persistent Daoist navigation bar + Lobby screen (rooms, filters, create/password modals, online players, lobby chat).
    /// </summary>
    public sealed class LobbyAndNavView
    {
        private readonly UiContext _ctx;
        public readonly RectTransform ShellRoot;
        public readonly RectTransform ContentArea;
        private readonly RectTransform _lobbyRoot;

        // Top nav controls
        private readonly Text _profileText;
        private readonly Text _moneyText;
        private readonly Dictionary<MainTab, Image> _tabImages = new Dictionary<MainTab, Image>();

        // Lobby controls
        private readonly RectTransform _roomListContent;
        private readonly RectTransform _playerListContent;
        private readonly RectTransform _chatContent;
        private readonly InputField _chatInput;
        private readonly Toggle _bugleToggle;
        private readonly Text _roomCountText;
        private readonly Text _playerCountText;
        private int _modeFilter = -1; // -1 = all

        // Modals
        private readonly RectTransform _createModal;
        private readonly InputField _createNameInput;
        private readonly InputField _createPwdInput;
        private readonly Toggle _createFreeToggle;
        private RoomGameType _createGameType = RoomGameType.CompetitiveNoItem;
        private readonly Text _createTypeLabel;

        private readonly RectTransform _pwdModal;
        private readonly InputField _enterPwdInput;
        private ushort _pendingPasswordRoomId;
        private string _pendingPasswordRoomName;

        public Action<MainTab> OnTabSelected;

        public LobbyAndNavView(Transform parent, UiContext ctx)
        {
            _ctx = ctx;
            ShellRoot = UiFactory.CreatePanel(parent, "MainShell", UiFactory.BgInkDeep);
            UiFactory.Stretch(ShellRoot);

            // Top bar (60px height)
            var topBar = UiFactory.CreatePanel(ShellRoot, "TopNavBar", UiFactory.BgSlateCard, UiFactory.BorderGold);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 60f);
            UiFactory.AddHLayout(topBar.gameObject, 8f, 14, 8);

            var brand = UiFactory.CreateText(topBar, "灵葫堂", 20, UiFactory.AccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(brand.gameObject, prefW: 78f, prefH: 42f);

            AddTabButton(topBar, MainTab.Lobby, "大厅");
            AddTabButton(topBar, MainTab.Inventory, "背包");
            AddTabButton(topBar, MainTab.Shop, "商城");
            AddTabButton(topBar, MainTab.ForgeEgg, "锻造/砸蛋");
            AddTabButton(topBar, MainTab.Pets, "灵宠");
            AddTabButton(topBar, MainTab.Social, "社交");

            var spacer = UiFactory.CreateEmpty(topBar, "Spacer");
            UiFactory.SetLayout(spacer.gameObject, flexW: 1f);

            _profileText = UiFactory.CreateText(topBar, "未登录", 13, UiFactory.TextLight, TextAnchor.MiddleRight);
            UiFactory.SetLayout(_profileText.gameObject, prefW: 230f, prefH: 42f);

            _moneyText = UiFactory.CreateText(topBar, "糖币: 0", 14, UiFactory.AccentGold, TextAnchor.MiddleRight, FontStyle.Bold);
            UiFactory.SetLayout(_moneyText.gameObject, prefW: 120f, prefH: 42f);

            UiFactory.CreateButton(topBar, "注销", () => _ctx.App.RequestLogout(), UiFactory.AccentDanger, fontSize: 13, width: 64f, height: 34f);

            // Content area below top bar
            ContentArea = UiFactory.CreateEmpty(ShellRoot, "ContentArea");
            UiFactory.Stretch(ContentArea, 10f, 10f, 10f, 68f);

            // Lobby root inside ContentArea
            _lobbyRoot = UiFactory.CreateEmpty(ContentArea, "LobbyRoot");
            UiFactory.Stretch(_lobbyRoot);
            UiFactory.AddHLayout(_lobbyRoot.gameObject, 10f, 0, 0);

            // Left column (Rooms)
            var leftCol = UiFactory.CreatePanel(_lobbyRoot, "RoomsPanel", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.SetLayout(leftCol.gameObject, flexW: 2f, flexH: 1f);
            UiFactory.AddVLayout(leftCol.gameObject, 8f, 12, 10);

            var roomHeader = UiFactory.CreateEmpty(leftCol, "RoomHeader");
            UiFactory.AddHLayout(roomHeader.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(roomHeader.gameObject, prefH: 36f);
            _roomCountText = UiFactory.CreateText(roomHeader, "房间列表 (0)", 16, UiFactory.AccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(_roomCountText.gameObject, flexW: 1f, prefH: 34f);
            UiFactory.CreateButton(roomHeader, "+ 创建房间", OpenCreateModal, UiFactory.AccentVermilion, fontSize: 13, width: 102f, height: 32f);
            UiFactory.CreateButton(roomHeader, "快速加入", () => _ctx.App.RequestQuickJoin(RoomGameType.CompetitiveNoItem), UiFactory.AccentCeladon, fontSize: 13, width: 86f, height: 32f);
            UiFactory.CreateButton(roomHeader, "刷新", () => _ctx.App.RequestRefreshLobby(), UiFactory.AccentMuted, fontSize: 13, width: 64f, height: 32f);

            // Filter bar
            var filterBar = UiFactory.CreateEmpty(leftCol, "FilterBar");
            UiFactory.AddHLayout(filterBar.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(filterBar.gameObject, prefH: 28f);
            UiFactory.CreateButton(filterBar, "全部", () => { _modeFilter = -1; Refresh(); }, UiFactory.AccentMuted, fontSize: 12, width: 58f, height: 26f);
            UiFactory.CreateButton(filterBar, "竞技", () => { _modeFilter = 0; Refresh(); }, UiFactory.AccentMuted, fontSize: 12, width: 58f, height: 26f);
            UiFactory.CreateButton(filterBar, "道具", () => { _modeFilter = 1; Refresh(); }, UiFactory.AccentMuted, fontSize: 12, width: 58f, height: 26f);
            UiFactory.CreateButton(filterBar, "探险", () => { _modeFilter = 2; Refresh(); }, UiFactory.AccentMuted, fontSize: 12, width: 58f, height: 26f);
            UiFactory.CreateButton(filterBar, "聊天", () => { _modeFilter = 4; Refresh(); }, UiFactory.AccentMuted, fontSize: 12, width: 58f, height: 26f);

            var roomScroll = UiFactory.CreateScrollView(leftCol, "RoomScroll", out _roomListContent);
            UiFactory.SetLayout(roomScroll.gameObject, flexW: 1f, flexH: 1f);

            // Right column (Online Players + Lobby Chat)
            var rightCol = UiFactory.CreateEmpty(_lobbyRoot, "RightColumn");
            UiFactory.SetLayout(rightCol.gameObject, flexW: 1f, flexH: 1f);
            UiFactory.AddVLayout(rightCol.gameObject, 10f, 0, 0);

            // Online players
            var playerBox = UiFactory.CreatePanel(rightCol, "PlayersPanel", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.SetLayout(playerBox.gameObject, flexW: 1f, flexH: 1f);
            UiFactory.AddVLayout(playerBox.gameObject, 6f, 10, 8);
            _playerCountText = UiFactory.CreateText(playerBox, "大厅在线玩家 (0)", 15, UiFactory.AccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(_playerCountText.gameObject, prefH: 24f);
            var playerScroll = UiFactory.CreateScrollView(playerBox, "PlayerScroll", out _playerListContent, spacing: 4f);
            UiFactory.SetLayout(playerScroll.gameObject, flexW: 1f, flexH: 1f);

            // Lobby Chat
            var chatBox = UiFactory.CreatePanel(rightCol, "LobbyChatPanel", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.SetLayout(chatBox.gameObject, flexW: 1f, flexH: 1.15f);
            UiFactory.AddVLayout(chatBox.gameObject, 6f, 10, 8);
            var chatHdr = UiFactory.CreateEmpty(chatBox, "ChatHdr");
            UiFactory.AddHLayout(chatHdr.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(chatHdr.gameObject, prefH: 24f);
            var cTitle = UiFactory.CreateText(chatHdr, "大厅聊天", 15, UiFactory.AccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(cTitle.gameObject, flexW: 1f, prefH: 24f);
            _bugleToggle = UiFactory.CreateToggle(chatHdr, "小喇叭", false, height: 22f);
            UiFactory.SetLayout(_bugleToggle.gameObject, prefW: 82f, prefH: 22f);

            var chatScroll = UiFactory.CreateScrollView(chatBox, "ChatScroll", out _chatContent, spacing: 3f);
            UiFactory.SetLayout(chatScroll.gameObject, flexW: 1f, flexH: 1f);

            var chatInputRow = UiFactory.CreateEmpty(chatBox, "ChatInputRow");
            UiFactory.AddHLayout(chatInputRow.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(chatInputRow.gameObject, prefH: 34f);
            _chatInput = UiFactory.CreateInputField(chatInputRow, "输入大厅消息...", height: 32f);
            UiFactory.CreateButton(chatInputRow, "发送", SendLobbyChat, UiFactory.AccentCeladon, fontSize: 13, width: 64f, height: 32f);

            // Create Room Modal
            _createModal = UiFactory.CreatePanel(ShellRoot, "CreateRoomModal", UiFactory.BgModalBackdrop);
            UiFactory.Stretch(_createModal);
            var cCard = UiFactory.CreatePanel(_createModal, "Card", UiFactory.BgSlateCard, UiFactory.AccentGold);
            cCard.anchorMin = cCard.anchorMax = cCard.pivot = new Vector2(0.5f, 0.5f);
            cCard.sizeDelta = new Vector2(440f, 310f);
            UiFactory.AddVLayout(cCard.gameObject, 10f, 20, 18);

            var cmTitle = UiFactory.CreateText(cCard, "创建新房间", 18, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetLayout(cmTitle.gameObject, prefH: 28f);
            _createNameInput = UiFactory.CreateInputField(cCard, "房间名称", "灵葫演武场");
            _createPwdInput = UiFactory.CreateInputField(cCard, "房间密码（留空则无密码）", "");
            _createFreeToggle = UiFactory.CreateToggle(cCard, "开启自由组队模式 (Free Rule)", false);

            var typeRow = UiFactory.CreateEmpty(cCard, "TypeRow");
            UiFactory.AddHLayout(typeRow.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(typeRow.gameObject, prefH: 32f);
            _createTypeLabel = UiFactory.CreateText(typeRow, "模式: 竞技场", 14, UiFactory.TextLight);
            UiFactory.SetLayout(_createTypeLabel.gameObject, flexW: 1f, prefH: 30f);
            UiFactory.CreateButton(typeRow, "竞技", () => SetCreateType(RoomGameType.CompetitiveNoItem, "竞技场"), UiFactory.AccentMuted, fontSize: 12, width: 52f, height: 28f);
            UiFactory.CreateButton(typeRow, "道具", () => SetCreateType(RoomGameType.CompetitiveItem, "道具场"), UiFactory.AccentMuted, fontSize: 12, width: 52f, height: 28f);
            UiFactory.CreateButton(typeRow, "探险", () => SetCreateType(RoomGameType.Adventure, "探险场"), UiFactory.AccentMuted, fontSize: 12, width: 52f, height: 28f);
            UiFactory.CreateButton(typeRow, "聊天", () => SetCreateType(RoomGameType.Chat, "聊天室"), UiFactory.AccentMuted, fontSize: 12, width: 52f, height: 28f);

            var cmBtns = UiFactory.CreateEmpty(cCard, "Btns");
            UiFactory.AddHLayout(cmBtns.gameObject, 10f, 0, 0, TextAnchor.MiddleCenter);
            UiFactory.SetLayout(cmBtns.gameObject, prefH: 38f);
            UiFactory.CreateButton(cmBtns, "取消", () => _createModal.gameObject.SetActive(false), UiFactory.AccentMuted, width: 110f, height: 36f);
            UiFactory.CreateButton(cmBtns, "创建并进入", ConfirmCreateRoom, UiFactory.AccentVermilion, width: 140f, height: 36f);
            _createModal.gameObject.SetActive(false);

            // Password Prompt Modal
            _pwdModal = UiFactory.CreatePanel(ShellRoot, "PasswordModal", UiFactory.BgModalBackdrop);
            UiFactory.Stretch(_pwdModal);
            var pCard = UiFactory.CreatePanel(_pwdModal, "Card", UiFactory.BgSlateCard, UiFactory.AccentGold);
            pCard.anchorMin = pCard.anchorMax = pCard.pivot = new Vector2(0.5f, 0.5f);
            pCard.sizeDelta = new Vector2(360f, 180f);
            UiFactory.AddVLayout(pCard.gameObject, 12f, 20, 18);
            var pmTitle = UiFactory.CreateText(pCard, "请输入房间密码", 17, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetLayout(pmTitle.gameObject, prefH: 26f);
            _enterPwdInput = UiFactory.CreateInputField(pCard, "密码", "", password: true);
            var pmBtns = UiFactory.CreateEmpty(pCard, "Btns");
            UiFactory.AddHLayout(pmBtns.gameObject, 10f, 0, 0, TextAnchor.MiddleCenter);
            UiFactory.SetLayout(pmBtns.gameObject, prefH: 36f);
            UiFactory.CreateButton(pmBtns, "取消", () => _pwdModal.gameObject.SetActive(false), UiFactory.AccentMuted, width: 100f, height: 34f);
            UiFactory.CreateButton(pmBtns, "进入", ConfirmPasswordEnter, UiFactory.AccentCeladon, width: 120f, height: 34f);
            _pwdModal.gameObject.SetActive(false);
        }

        private void AddTabButton(Transform parent, MainTab tab, string label)
        {
            var btn = UiFactory.CreateButton(parent, label, () => OnTabSelected?.Invoke(tab), UiFactory.AccentMuted, fontSize: 14, width: 86f, height: 36f);
            _tabImages[tab] = btn.GetComponent<Image>();
        }

        public void SetActiveTabHighlight(MainTab active)
        {
            foreach (var kv in _tabImages)
                kv.Value.color = kv.Key == active ? UiFactory.AccentVermilion : UiFactory.AccentMuted;
            _lobbyRoot.gameObject.SetActive(active == MainTab.Lobby);
        }

        public void SetShellVisible(bool visible) => ShellRoot.gameObject.SetActive(visible);

        public void RefreshTopBar()
        {
            var s = _ctx.Store;
            string rank = StaticCatalogs.GetRankTitle(s.GameInfo?.Degree ?? 1);
            _profileText.text = $"{s.Nickname} ({s.Uin}) | {rank}";
            _moneyText.text = $"糖币: {s.GameInfo?.Money ?? 0}";
        }

        public void Refresh()
        {
            RefreshTopBar();
            var s = _ctx.Store;

            // Rooms
            UiFactory.ClearChildren(_roomListContent);
            int shownRooms = 0;
            foreach (var room in s.Rooms)
            {
                if (_modeFilter >= 0 && (int)room.GameType != _modeFilter) continue;
                shownRooms++;
                var row = UiFactory.CreatePanel(_roomListContent, $"Room_{room.RoomId}", UiFactory.BgSlateLight, UiFactory.BorderSubtle);
                UiFactory.SetLayout(row.gameObject, prefH: 52f);
                UiFactory.AddHLayout(row.gameObject, 8f, 10, 6);

                bool preparing = (room.RoomFlag & RoomListFlag.Preparing) != 0;
                bool hasPwd = (room.RoomFlag & RoomListFlag.Password) != 0;
                bool freeRule = (room.RoomFlag & RoomListFlag.FreeRule) != 0;
                string tags = $"{ModeLabel(room.GameType)}" + (freeRule ? " · 自由" : "") + (hasPwd ? " · 加密" : "");
                string mapName = _ctx.Catalogs.GetMapName(room.MapId);

                var idTxt = UiFactory.CreateText(row, $"#{room.RoomId}", 15, UiFactory.AccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiFactory.SetLayout(idTxt.gameObject, prefW: 46f, prefH: 38f);

                var infoCol = UiFactory.CreateEmpty(row, "Info");
                UiFactory.SetLayout(infoCol.gameObject, flexW: 1f, prefH: 38f);
                UiFactory.AddVLayout(infoCol.gameObject, 2f, 0, 0);
                var nTxt = UiFactory.CreateText(infoCol, room.Name, 14, UiFactory.TextLight, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiFactory.SetLayout(nTxt.gameObject, prefH: 20f);
                var subTxt = UiFactory.CreateText(infoCol, $"{tags} | 地图: {mapName}", 12, UiFactory.TextSubtle);
                UiFactory.SetLayout(subTxt.gameObject, prefH: 16f);

                var popTxt = UiFactory.CreateText(row, $"{room.CurrentPlayers}/{room.Capacity} {(preparing ? "等待中" : "对局中")}", 13,
                    preparing ? UiFactory.AccentGold : UiFactory.TextSubtle, TextAnchor.MiddleRight);
                UiFactory.SetLayout(popTxt.gameObject, prefW: 92f, prefH: 38f);

                var capturedRoom = room;
                var enterBtn = UiFactory.CreateButton(row, "进入", () => OnClickRoom(capturedRoom),
                    preparing ? UiFactory.AccentCeladon : UiFactory.AccentMuted, fontSize: 13, width: 66f, height: 32f);
                enterBtn.interactable = preparing;
            }
            if (shownRooms == 0)
            {
                var empty = UiFactory.CreateText(_roomListContent, "暂无符合条件的房间，点击右上角「+ 创建房间」立刻开局！", 14, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
                UiFactory.SetLayout(empty.gameObject, prefH: 48f);
            }
            _roomCountText.text = $"房间列表 ({shownRooms})";

            // Online players
            UiFactory.ClearChildren(_playerListContent);
            _playerCountText.text = $"大厅在线玩家 ({s.OnlinePlayers.Count})";
            foreach (var p in s.OnlinePlayers)
            {
                var row = UiFactory.CreatePanel(_playerListContent, $"Pl_{p.Uin}", UiFactory.BgSlateLight);
                UiFactory.SetLayout(row.gameObject, prefH: 34f);
                UiFactory.AddHLayout(row.gameObject, 6f, 8, 4);

                string title = StaticCatalogs.GetRankTitle(p.GameInfo?.Degree ?? 1);
                var txt = UiFactory.CreateText(row, $"{p.Nickname} ({p.Uin}) · {title}", 13, p.Uin == s.Uin ? UiFactory.AccentGold : UiFactory.TextLight);
                UiFactory.SetLayout(txt.gameObject, flexW: 1f, prefH: 26f);

                if (p.Uin != s.Uin)
                {
                    uint target = p.Uin;
                    UiFactory.CreateButton(row, "+好友", () => _ctx.App.RequestAddFriend(target, "大厅结交道友"), UiFactory.AccentCeladon, fontSize: 11, width: 54f, height: 24f);
                }
            }

            // Lobby chat
            UiFactory.ClearChildren(_chatContent);
            foreach (var msg in s.LobbyChat)
            {
                var line = UiFactory.CreateText(_chatContent,
                    $"[{msg.Timestamp}] {msg.Sender}: {msg.Content}",
                    13, msg.Highlight ? UiFactory.AccentGold : UiFactory.TextLight);
                UiFactory.SetLayout(line.gameObject, prefH: 20f);
            }
        }

        private void OnClickRoom(RoomListEntryData room)
        {
            if ((room.RoomFlag & RoomListFlag.Password) != 0)
            {
                _pendingPasswordRoomId = room.RoomId;
                _pendingPasswordRoomName = room.Name;
                _enterPwdInput.text = string.Empty;
                _pwdModal.gameObject.SetActive(true);
            }
            else
            {
                _ctx.App.RequestEnterRoom(room.RoomId, room.Name, string.Empty);
            }
        }

        private void ConfirmPasswordEnter()
        {
            _pwdModal.gameObject.SetActive(false);
            _ctx.App.RequestEnterRoom(_pendingPasswordRoomId, _pendingPasswordRoomName, _enterPwdInput.text);
        }

        private void OpenCreateModal()
        {
            _createModal.gameObject.SetActive(true);
        }

        private void SetCreateType(RoomGameType type, string label)
        {
            _createGameType = type;
            _createTypeLabel.text = "模式: " + label;
        }

        private void ConfirmCreateRoom()
        {
            string name = string.IsNullOrWhiteSpace(_createNameInput.text) ? "灵葫演武场" : _createNameInput.text.Trim();
            string pwd = _createPwdInput.text.Trim();
            RoomPropertyFlag flag = RoomPropertyFlag.Standard;
            if (!string.IsNullOrEmpty(pwd)) flag |= RoomPropertyFlag.Password;
            if (_createFreeToggle.isOn) flag |= RoomPropertyFlag.FreeRule;
            _createModal.gameObject.SetActive(false);
            _ctx.App.RequestCreateRoom(name, flag, _createGameType, pwd);
        }

        private void SendLobbyChat()
        {
            string text = _chatInput.text.Trim();
            if (string.IsNullOrEmpty(text)) return;
            _chatInput.text = string.Empty;
            _ctx.App.RequestSendLobbyChat(text, _bugleToggle.isOn);
        }

        public static string ModeLabel(RoomGameType t)
        {
            switch (t)
            {
                case RoomGameType.CompetitiveNoItem: return "竞技";
                case RoomGameType.CompetitiveItem: return "道具";
                case RoomGameType.Adventure: return "探险";
                case RoomGameType.CompetitiveLoot: return "夺宝";
                case RoomGameType.Chat: return "聊天";
                default: return "竞技";
            }
        }
    }
}
