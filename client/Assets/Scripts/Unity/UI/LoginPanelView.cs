using System;
using Qqtang.Core.Battle;
using Qqtang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace Qqtang.UnityClient.UI
{
    public sealed class LoginPanelView
    {
        private readonly UiContext _ctx;
        public readonly RectTransform Root;

        private readonly InputField _hostInput;
        private readonly InputField _portInput;
        private readonly InputField _uinInput;
        private readonly InputField _pwdInput;
        private readonly Text _statusText;
        private readonly Button _loginBtn;
        private readonly Image[] _roleBorders = new Image[4];
        private byte _selectedRoleId = 1;
        private ushort _selectedOfflineMapId = 201;
        private readonly Text _offlineMapLabel;
        private readonly RectTransform _offlineMapModal;
        private readonly InputField _offlineMapSearch;
        private readonly RectTransform _offlineMapListContent;

        public LoginPanelView(Transform parent, UiContext ctx)
        {
            _ctx = ctx;
            Root = UiFactory.CreatePanel(parent, "LoginPanel", UiFactory.BgInkDeep);
            UiFactory.Stretch(Root);

            // Centered Daoist card
            var card = UiFactory.CreatePanel(Root, "LoginCard", UiFactory.BgSlateCard, UiFactory.AccentGold);
            card.anchorMin = new Vector2(0.5f, 0.5f);
            card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(740f, 690f);
            UiFactory.AddVLayout(card.gameObject, 8f, 24, 14);

            // Header banner (Daoist logo sprite if cached, plus subtitle)
            var logoSprite = _ctx.Art.GetLogoSprite();
            if (logoSprite != null)
            {
                var logoGo = new GameObject("DaoistLogo", typeof(RectTransform), typeof(Image));
                logoGo.transform.SetParent(card, false);
                var lImg = logoGo.GetComponent<Image>();
                lImg.sprite = logoSprite;
                lImg.preserveAspect = true;
                UiFactory.SetLayout(logoGo, prefH: 64f);
            }
            else
            {
                var title = UiFactory.CreateText(card, "灵葫大作战 · QQTang Unity 3D", 28, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiFactory.SetLayout(title.gameObject, prefH: 40f);
            }
            var sub = UiFactory.CreateText(card, "原生 QQTAUTH1 + QQ-TEA + QQTPPP UDP 直连 Go 服务端 | 也支持零服务端单机离线演武", 13, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
            UiFactory.SetLayout(sub.gameObject, prefH: 22f);

            // Server row
            var srvRow = UiFactory.CreateEmpty(card, "ServerRow");
            UiFactory.AddHLayout(srvRow.gameObject, 10f, 0, 0);
            UiFactory.SetLayout(srvRow.gameObject, prefH: 36f);
            var hLbl = UiFactory.CreateText(srvRow, "服务器", 14, UiFactory.TextLight);
            UiFactory.SetLayout(hLbl.gameObject, prefW: 60f, prefH: 34f);
            _hostInput = UiFactory.CreateInputField(srvRow, "127.0.0.1", "127.0.0.1");
            var pLbl = UiFactory.CreateText(srvRow, "端口", 14, UiFactory.TextLight, TextAnchor.MiddleRight);
            UiFactory.SetLayout(pLbl.gameObject, prefW: 44f, prefH: 34f);
            _portInput = UiFactory.CreateInputField(srvRow, "18000", "18000", width: 110f);

            // Account row
            var accRow = UiFactory.CreateEmpty(card, "AccountRow");
            UiFactory.AddHLayout(accRow.gameObject, 10f, 0, 0);
            UiFactory.SetLayout(accRow.gameObject, prefH: 36f);
            var uLbl = UiFactory.CreateText(accRow, "账号UIN", 14, UiFactory.TextLight);
            UiFactory.SetLayout(uLbl.gameObject, prefW: 60f, prefH: 34f);
            _uinInput = UiFactory.CreateInputField(accRow, "1000001", "1000001");
            var pwLbl = UiFactory.CreateText(accRow, "密码", 14, UiFactory.TextLight, TextAnchor.MiddleRight);
            UiFactory.SetLayout(pwLbl.gameObject, prefW: 44f, prefH: 34f);
            _pwdInput = UiFactory.CreateInputField(accRow, "123456", "123456", width: 160f, password: true);

            // Quick account fill buttons
            var quickRow = UiFactory.CreateEmpty(card, "QuickAccounts");
            UiFactory.AddHLayout(quickRow.gameObject, 10f, 0, 0);
            UiFactory.SetLayout(quickRow.gameObject, prefH: 30f);
            var qLbl = UiFactory.CreateText(quickRow, "快捷测试号:", 13, UiFactory.TextSubtle);
            UiFactory.SetLayout(qLbl.gameObject, prefW: 82f, prefH: 28f);
            UiFactory.CreateButton(quickRow, "道长一号 (1000001)", () => { _uinInput.text = "1000001"; _pwdInput.text = "123456"; SelectRole(1); }, UiFactory.AccentMuted, fontSize: 13, width: 165f, height: 28f);
            UiFactory.CreateButton(quickRow, "仙姬二号 (1000002)", () => { _uinInput.text = "1000002"; _pwdInput.text = "123456"; SelectRole(2); }, UiFactory.AccentMuted, fontSize: 13, width: 165f, height: 28f);

            // Role selector title
            var rHeader = UiFactory.CreateText(card, "选择初始出战角色（进入房间后可切换全部 16 角色）", 14, UiFactory.AccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(rHeader.gameObject, prefH: 24f);

            // 4 Daoist starter role cards
            var roleRow = UiFactory.CreateEmpty(card, "RoleCards");
            UiFactory.AddHLayout(roleRow.gameObject, 12f, 0, 0);
            UiFactory.SetLayout(roleRow.gameObject, prefH: 156f);

            for (int i = 0; i < 4; i++)
            {
                byte roleId = (byte)(i + 1);
                var info = StaticCatalogs.GetRole(roleId);
                var rCard = UiFactory.CreatePanel(roleRow, $"RoleCard_{roleId}", UiFactory.BgSlateLight, UiFactory.BorderSubtle);
                UiFactory.SetLayout(rCard.gameObject, flexW: 1f, prefH: 152f);
                UiFactory.AddVLayout(rCard.gameObject, 4f, 8, 8);
                _roleBorders[i] = rCard.GetComponent<Image>();

                var btn = rCard.gameObject.AddComponent<Button>();
                btn.onClick.AddListener(() => SelectRole(roleId));

                // Portrait preview from ArtAssetResolver (dedicated 256px Daoist portrait)
                var imgGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                imgGo.transform.SetParent(rCard, false);
                var pImg = imgGo.GetComponent<Image>();
                pImg.preserveAspect = true;
                pImg.sprite = _ctx.Art.GetRolePortraitSprite(roleId, UiFactory.TeamBadgeColor((byte)(i + 1)));
                UiFactory.SetLayout(imgGo, prefH: 64f);

                var nameTxt = UiFactory.CreateText(rCard, info.DaoistName, 15, UiFactory.TextLight, TextAnchor.MiddleCenter, FontStyle.Bold);
                UiFactory.SetLayout(nameTxt.gameObject, prefH: 22f);
                var classicTxt = UiFactory.CreateText(rCard, $"({info.ClassicName})", 12, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
                UiFactory.SetLayout(classicTxt.gameObject, prefH: 18f);
                var statsTxt = UiFactory.CreateText(rCard, $"速{info.BaseSpeed} 泡{info.BaseBombs} 威{info.BasePower}", 12, UiFactory.AccentGold, TextAnchor.MiddleCenter);
                UiFactory.SetLayout(statsTxt.gameObject, prefH: 18f);
            }
            SelectRole(1);

            // Login button
            _loginBtn = UiFactory.CreateButton(card, "联机登录灵葫堂", TriggerLogin, UiFactory.AccentVermilion, fontSize: 17, height: 42f);
            UiFactory.SetLayout(_loginBtn.gameObject, flexW: 1f, prefH: 42f);

            // Offline Solo Practice mode & map selector bar
            var offlineMapRow = UiFactory.CreateEmpty(card, "OfflineMapRow");
            UiFactory.AddHLayout(offlineMapRow.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(offlineMapRow.gameObject, prefH: 28f);
            _offlineMapLabel = UiFactory.CreateText(offlineMapRow, "离线地图: 包子01(201)", 12, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(_offlineMapLabel.gameObject, flexW: 1f, prefH: 26f);
            UiFactory.CreateButton(offlineMapRow, "包子01", () => SelectOfflineMap(201, "包子01"), UiFactory.AccentMuted, fontSize: 11, width: 58f, height: 24f);
            UiFactory.CreateButton(offlineMapRow, "水面01", () => SelectOfflineMap(1, "水面01"), UiFactory.AccentMuted, fontSize: 11, width: 58f, height: 24f);
            UiFactory.CreateButton(offlineMapRow, "英雄01", () => SelectOfflineMap(101, "英雄01"), UiFactory.AccentMuted, fontSize: 11, width: 58f, height: 24f);
            UiFactory.CreateButton(offlineMapRow, "抢包山", () => SelectOfflineMap(301, "抢包山01"), UiFactory.AccentMuted, fontSize: 11, width: 58f, height: 24f);
            UiFactory.CreateButton(offlineMapRow, "全部地图...", OpenOfflineMapModal, UiFactory.AccentGold, UiFactory.TextInk, fontSize: 11, width: 78f, height: 24f);

            var actionRow = UiFactory.CreateEmpty(card, "OfflineActions");
            UiFactory.AddHLayout(actionRow.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(actionRow.gameObject, prefH: 34f);
            UiFactory.CreateButton(actionRow, "离线 1v1", () => _ctx.App.StartOfflineSoloMatch(_selectedRoleId, 1, OfflinePracticeMode.Classic, _selectedOfflineMapId), UiFactory.AccentCeladon, fontSize: 12, width: 86f, height: 32f);
            UiFactory.CreateButton(actionRow, "离线 1v3", () => _ctx.App.StartOfflineSoloMatch(_selectedRoleId, 3, OfflinePracticeMode.Classic, _selectedOfflineMapId), UiFactory.AccentGold, UiFactory.TextInk, fontSize: 12, width: 86f, height: 32f);
            UiFactory.CreateButton(actionRow, "单人BOSS", () => _ctx.App.StartOfflineSoloMatch(_selectedRoleId, 0, OfflinePracticeMode.BossRaid, _selectedOfflineMapId), UiFactory.AccentVermilion, fontSize: 12, width: 88f, height: 32f);
            UiFactory.CreateButton(actionRow, "抢包山", () => _ctx.App.StartOfflineSoloMatch(_selectedRoleId, 1, OfflinePracticeMode.BunCapture, _selectedOfflineMapId), UiFactory.AccentCeladon, fontSize: 12, width: 72f, height: 32f);
            UiFactory.CreateButton(actionRow, "雕塑攻防", () => _ctx.App.StartOfflineSoloMatch(_selectedRoleId, 1, OfflinePracticeMode.SculptureDefense, _selectedOfflineMapId), UiFactory.AccentMuted, fontSize: 12, width: 80f, height: 32f);
            UiFactory.CreateButton(actionRow, "踢泡大战", () => _ctx.App.StartOfflineSoloMatch(_selectedRoleId, 1, OfflinePracticeMode.KickBomb, _selectedOfflineMapId), UiFactory.AccentCeladon, fontSize: 12, width: 80f, height: 32f);
            UiFactory.CreateButton(actionRow, "机械世界", () => _ctx.App.StartOfflineSoloMatch(_selectedRoleId, 1, OfflinePracticeMode.MachineDodge, _selectedOfflineMapId), UiFactory.AccentVermilion, fontSize: 12, width: 80f, height: 32f);
            UiFactory.CreateButton(actionRow, "推箱大战", () => _ctx.App.StartOfflineSoloMatch(_selectedRoleId, 1, OfflinePracticeMode.BoxPush, _selectedOfflineMapId), UiFactory.AccentMuted, fontSize: 12, width: 80f, height: 32f);

            // Status bar
            _statusText = UiFactory.CreateText(card, "请先启动本地 Go 服务端 (tools/start-local-server.ps1)，再点击登录。", 13, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
            UiFactory.SetLayout(_statusText.gameObject, prefH: 30f);

            // Offline Full Map Picker Modal (all 330+ maps with decoded thumbnails)
            _offlineMapModal = UiFactory.CreatePanel(Root, "OfflineMapModal", UiFactory.BgModalBackdrop);
            UiFactory.Stretch(_offlineMapModal);
            var mCard = UiFactory.CreatePanel(_offlineMapModal, "Card", UiFactory.BgSlateCard, UiFactory.AccentGold);
            mCard.anchorMin = mCard.anchorMax = mCard.pivot = new Vector2(0.5f, 0.5f);
            mCard.sizeDelta = new Vector2(540f, 520f);
            UiFactory.AddVLayout(mCard.gameObject, 8f, 16, 14);
            var mTop = UiFactory.CreateEmpty(mCard, "Top");
            UiFactory.AddHLayout(mTop.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(mTop.gameObject, prefH: 34f);
            var mTitle = UiFactory.CreateText(mTop, "选择离线演武地图（全 330+ 原版地图）", 16, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(mTitle.gameObject, flexW: 1f, prefH: 32f);
            UiFactory.CreateButton(mTop, "关闭", () => _offlineMapModal.gameObject.SetActive(false), UiFactory.AccentMuted, width: 68f, height: 30f);
            _offlineMapSearch = UiFactory.CreateInputField(mCard, "按名称/分类/ID搜索（如：包子、水、机械、箱、201）...", "");
            _offlineMapSearch.onValueChanged.AddListener(_ => PopulateOfflineMaps());
            var mScroll = UiFactory.CreateScrollView(mCard, "Maps", out _offlineMapListContent, spacing: 4f);
            UiFactory.SetLayout(mScroll.gameObject, flexW: 1f, flexH: 1f);
            _offlineMapModal.gameObject.SetActive(false);
        }

        public void ApplySavedDefaults(string host, int port, uint uin, byte roleId)
        {
            if (!string.IsNullOrEmpty(host)) _hostInput.text = host;
            if (port > 0) _portInput.text = port.ToString();
            if (uin > 0) _uinInput.text = uin.ToString();
            if (roleId >= 1 && roleId <= 4) SelectRole(roleId);
        }

        private void OpenOfflineMapModal()
        {
            _offlineMapModal.gameObject.SetActive(true);
            PopulateOfflineMaps();
        }

        private void PopulateOfflineMaps()
        {
            UiFactory.ClearChildren(_offlineMapListContent);
            string q = (_offlineMapSearch.text ?? string.Empty).Trim();
            int shown = 0;
            foreach (var m in _ctx.Catalogs.Maps)
            {
                if (!string.IsNullOrEmpty(q) &&
                    m.Name.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0 &&
                    (string.IsNullOrEmpty(m.Family) || m.Family.IndexOf(q, StringComparison.OrdinalIgnoreCase) < 0) &&
                    !m.MapId.ToString().Contains(q))
                    continue;
                ushort id = m.MapId;
                string mName = m.Name;
                var row = UiFactory.CreatePanel(_offlineMapListContent, $"OMap_{id}", UiFactory.BgSlateLight);
                UiFactory.SetLayout(row.gameObject, prefH: 40f);
                UiFactory.AddHLayout(row.gameObject, 8f, 10, 4);
                Sprite thumb = _ctx.Art.GetMapThumbSprite(id);
                if (thumb != null)
                {
                    var tGo = new GameObject("Thumb", typeof(RectTransform), typeof(Image));
                    tGo.transform.SetParent(row, false);
                    var tImg = tGo.GetComponent<Image>();
                    tImg.sprite = thumb;
                    tImg.preserveAspect = true;
                    UiFactory.SetLayout(tGo, prefW: 42f, prefH: 32f);
                }
                string fam = !string.IsNullOrEmpty(m.Family) ? $" [{m.Family}]" : string.Empty;
                var lbl = UiFactory.CreateText(row, $"#{id} {mName}{fam}", 13, id == _selectedOfflineMapId ? UiFactory.AccentGold : UiFactory.TextLight);
                UiFactory.SetLayout(lbl.gameObject, flexW: 1f, prefH: 28f);
                UiFactory.CreateButton(row, "选用", () =>
                {
                    SelectOfflineMap(id, mName);
                    _offlineMapModal.gameObject.SetActive(false);
                }, UiFactory.AccentCeladon, fontSize: 12, width: 60f, height: 26f);
                if (++shown >= 90) break;
            }
        }

        private void SelectOfflineMap(ushort mapId, string name)
        {
            _selectedOfflineMapId = mapId;
            if (_offlineMapLabel != null)
                _offlineMapLabel.text = $"离线地图: {name}({mapId})";
        }

        private void SelectRole(byte roleId)
        {
            _selectedRoleId = roleId;
            for (int i = 0; i < 4; i++)
            {
                if (_roleBorders[i] != null)
                    _roleBorders[i].color = (i + 1 == roleId)
                        ? new Color(0.24f, 0.44f, 0.38f, 1f)
                        : UiFactory.BgSlateLight;
            }
        }

        private void TriggerLogin()
        {
            if (!int.TryParse(_portInput.text.Trim(), out int port)) port = 18000;
            if (!uint.TryParse(_uinInput.text.Trim(), out uint uin))
            {
                SetStatus("请输入有效的数字账号 UIN。");
                return;
            }
            _ctx.App.RequestLogin(_hostInput.text.Trim(), port, uin, _pwdInput.text, _selectedRoleId);
        }

        public void SetStatus(string msg, bool busy = false)
        {
            _statusText.text = msg ?? string.Empty;
            _loginBtn.interactable = !busy;
        }

        public void SetVisible(bool visible) => Root.gameObject.SetActive(visible);
    }
}
