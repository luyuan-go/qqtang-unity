using System;
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
            card.sizeDelta = new Vector2(680f, 560f);
            UiFactory.AddVLayout(card.gameObject, 12f, 28, 22);

            // Header banner
            var title = UiFactory.CreateText(card, "灵葫大作战 · QQTang Unity 3D", 28, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetLayout(title.gameObject, prefH: 40f);
            var sub = UiFactory.CreateText(card, "原生 QQTAUTH1 + QQ-TEA + QQTPPP UDP 协议直连 Go 服务端（道风重制 / 原版兜底双轨美术）", 13, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
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

                // Portrait preview from ArtAssetResolver
                var imgGo = new GameObject("Portrait", typeof(RectTransform), typeof(Image));
                imgGo.transform.SetParent(rCard, false);
                var pImg = imgGo.GetComponent<Image>();
                pImg.preserveAspect = true;
                pImg.sprite = _ctx.Art.GetCharacterSprite(roleId, "S", UiFactory.TeamBadgeColor((byte)(i + 1)));
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
            _loginBtn = UiFactory.CreateButton(card, "登录进入灵葫堂", TriggerLogin, UiFactory.AccentVermilion, fontSize: 18, height: 46f);
            UiFactory.SetLayout(_loginBtn.gameObject, flexW: 1f, prefH: 46f);

            // Status bar
            _statusText = UiFactory.CreateText(card, "请先启动本地 Go 服务端 (tools/start-local-server.ps1)，再点击登录。", 13, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
            UiFactory.SetLayout(_statusText.gameObject, prefH: 34f);
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
