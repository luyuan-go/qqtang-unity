using System;
using Qqtang.Core.Protocol;
using Qqtang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace Qqtang.UnityClient.UI
{
    /// <summary>
    /// Houses Pets (灵宠) and Social (好友 / 家族 / 伴侣) panels inside MainShell ContentArea.
    /// </summary>
    public sealed class PetAndSocialView
    {
        private readonly UiContext _ctx;
        public readonly RectTransform PetsRoot;
        public readonly RectTransform SocialRoot;

        // Pets
        private readonly RectTransform _petsContent;
        private readonly InputField _petParamInput;
        private readonly Text _petStatusText;

        // Social Sub-tabs
        private readonly RectTransform _friendsPanel;
        private readonly RectTransform _kinPanel;
        private readonly RectTransform _marriagePanel;

        // Friends
        private readonly InputField _friendUinInput;
        private readonly Text _findFriendResult;
        private readonly RectTransform _friendReqContent;
        private readonly RectTransform _friendListContent;

        // Kin
        private readonly RectTransform _kinNoGroupBox;
        private readonly RectTransform _kinActiveBox;
        private readonly InputField _kinCreateNameInput;
        private readonly InputField _kinCreateDeclInput;
        private readonly RectTransform _kinInvitesContent;
        private readonly Text _kinInfoText;
        private readonly InputField _kinInviteUinInput;
        private readonly InputField _kinNoticeInput;
        private readonly RectTransform _kinMembersContent;
        private readonly RectTransform _kinChatContent;
        private readonly InputField _kinChatInput;

        // Marriage
        private readonly Text _marriageStatusText;
        private readonly InputField _sparkUinInput;
        private readonly InputField _sparkWordInput;
        private readonly InputField _loveWordInput;
        private readonly RectTransform _sparkReqContent;

        public PetAndSocialView(Transform parent, UiContext ctx)
        {
            _ctx = ctx;

            // --- PETS ---
            PetsRoot = UiFactory.CreatePanel(parent, "PetsView", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.Stretch(PetsRoot);
            UiFactory.AddVLayout(PetsRoot.gameObject, 8f, 14, 12);

            var pHdr = UiFactory.CreateEmpty(PetsRoot, "PetHdr");
            UiFactory.AddHLayout(pHdr.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(pHdr.gameObject, prefH: 36f);
            _petStatusText = UiFactory.CreateText(pHdr, "我的灵宠 (0)", 16, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(_petStatusText.gameObject, prefW: 160f, prefH: 34f);
            _petParamInput = UiFactory.CreateInputField(pHdr, "参数：新名字 / 饲料道具ID / 宠物卡ID...", "");
            UiFactory.CreateButton(pHdr, "领养宠物卡", () => _ctx.App.RequestHandlePet(PetEventId.Adopt, 0, _petParamInput.text.Trim()), UiFactory.AccentVermilion, fontSize: 12, width: 96f, height: 32f);
            UiFactory.CreateButton(pHdr, "刷新列表", () => _ctx.App.RequestRefreshPets(), UiFactory.AccentMuted, fontSize: 12, width: 80f, height: 32f);

            var pScroll = UiFactory.CreateScrollView(PetsRoot, "PetsScroll", out _petsContent, spacing: 6f);
            UiFactory.SetLayout(pScroll.gameObject, flexW: 1f, flexH: 1f);

            // --- SOCIAL ---
            SocialRoot = UiFactory.CreatePanel(parent, "SocialView", UiFactory.BgSlateCard, UiFactory.BorderSubtle);
            UiFactory.Stretch(SocialRoot);
            UiFactory.AddVLayout(SocialRoot.gameObject, 8f, 14, 12);

            var sTabs = UiFactory.CreateEmpty(SocialRoot, "SocialTabs");
            UiFactory.AddHLayout(sTabs.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(sTabs.gameObject, prefH: 36f);
            UiFactory.CreateButton(sTabs, "道友 / 好友", () => SelectSocialSubTab(0), UiFactory.AccentCeladon, fontSize: 13, width: 120f, height: 34f);
            UiFactory.CreateButton(sTabs, "仙门 / 家族", () => SelectSocialSubTab(1), UiFactory.AccentGold, UiFactory.TextInk, fontSize: 13, width: 120f, height: 34f);
            UiFactory.CreateButton(sTabs, "道侣 / 结婚", () => SelectSocialSubTab(2), UiFactory.AccentVermilion, fontSize: 13, width: 120f, height: 34f);
            var sSpacer = UiFactory.CreateEmpty(sTabs, "Sp");
            UiFactory.SetLayout(sSpacer.gameObject, flexW: 1f);
            UiFactory.CreateButton(sTabs, "刷新社交数据", () => _ctx.App.RequestRefreshSocial(), UiFactory.AccentMuted, fontSize: 12, width: 108f, height: 32f);

            var sBody = UiFactory.CreateEmpty(SocialRoot, "SocialBody");
            UiFactory.SetLayout(sBody.gameObject, flexW: 1f, flexH: 1f);

            // -- Friends Panel --
            _friendsPanel = UiFactory.CreateEmpty(sBody, "FriendsPanel");
            UiFactory.Stretch(_friendsPanel);
            UiFactory.AddVLayout(_friendsPanel.gameObject, 8f, 0, 0);

            var fAddRow = UiFactory.CreateEmpty(_friendsPanel, "FriendAddRow");
            UiFactory.AddHLayout(fAddRow.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(fAddRow.gameObject, prefH: 36f);
            _friendUinInput = UiFactory.CreateInputField(fAddRow, "输入对方 UIN（例如 1000002）...", "");
            UiFactory.CreateButton(fAddRow, "添加好友", () =>
            {
                if (uint.TryParse(_friendUinInput.text.Trim(), out uint uin))
                    _ctx.App.RequestAddFriend(uin, "道友同行，请多指教");
            }, UiFactory.AccentCeladon, fontSize: 13, width: 92f, height: 32f);
            UiFactory.CreateButton(fAddRow, "查找玩家", () =>
            {
                if (uint.TryParse(_friendUinInput.text.Trim(), out uint uin))
                    _ctx.App.RequestFindFriend(uin);
            }, UiFactory.AccentMuted, fontSize: 13, width: 92f, height: 32f);

            _findFriendResult = UiFactory.CreateText(_friendsPanel, "查找结果将在此显示。", 13, UiFactory.TextSubtle);
            UiFactory.SetLayout(_findFriendResult.gameObject, prefH: 24f);

            var fReqScroll = UiFactory.CreateScrollView(_friendsPanel, "FriendReqScroll", out _friendReqContent, spacing: 4f);
            UiFactory.SetLayout(fReqScroll.gameObject, flexW: 1f, prefH: 96f);

            var fListScroll = UiFactory.CreateScrollView(_friendsPanel, "FriendListScroll", out _friendListContent, spacing: 4f);
            UiFactory.SetLayout(fListScroll.gameObject, flexW: 1f, flexH: 1f);

            // -- Kin Panel --
            _kinPanel = UiFactory.CreateEmpty(sBody, "KinPanel");
            UiFactory.Stretch(_kinPanel);

            // No-Kin Create Box
            _kinNoGroupBox = UiFactory.CreatePanel(_kinPanel, "KinCreateBox", UiFactory.BgSlateLight, UiFactory.BorderGold);
            UiFactory.Stretch(_kinNoGroupBox);
            UiFactory.AddVLayout(_kinNoGroupBox.gameObject, 10f, 18, 16);
            var kcHdr = UiFactory.CreateText(_kinNoGroupBox, "尚未加入家族 — 可创建新家族（需背包持有超级盟约书 2235）或接受家族邀请", 15, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(kcHdr.gameObject, prefH: 26f);
            var kcRow = UiFactory.CreateEmpty(_kinNoGroupBox, "CreateRow");
            UiFactory.AddHLayout(kcRow.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(kcRow.gameObject, prefH: 36f);
            _kinCreateNameInput = UiFactory.CreateInputField(kcRow, "家族名称（如：青云宗）", "青云宗", width: 200f);
            _kinCreateDeclInput = UiFactory.CreateInputField(kcRow, "家族宣言", "以葫会友，逍遥天下");
            UiFactory.CreateButton(kcRow, "创建家族", () => _ctx.App.RequestCreateKin(_kinCreateNameInput.text.Trim(), _kinCreateDeclInput.text.Trim()),
                UiFactory.AccentVermilion, fontSize: 13, width: 100f, height: 34f);

            var kiLbl = UiFactory.CreateText(_kinNoGroupBox, "收到的家族邀请:", 14, UiFactory.TextLight, FontStyle.Bold);
            UiFactory.SetLayout(kiLbl.gameObject, prefH: 22f);
            var kiScroll = UiFactory.CreateScrollView(_kinNoGroupBox, "KinInvites", out _kinInvitesContent, spacing: 4f);
            UiFactory.SetLayout(kiScroll.gameObject, flexW: 1f, flexH: 1f);

            // Active Kin Box
            _kinActiveBox = UiFactory.CreateEmpty(_kinPanel, "KinActiveBox");
            UiFactory.Stretch(_kinActiveBox);
            UiFactory.AddHLayout(_kinActiveBox.gameObject, 10f, 0, 0);

            var kinLeft = UiFactory.CreatePanel(_kinActiveBox, "KinLeft", UiFactory.BgSlateLight, UiFactory.BorderSubtle);
            UiFactory.SetLayout(kinLeft.gameObject, flexW: 1.2f, flexH: 1f);
            UiFactory.AddVLayout(kinLeft.gameObject, 8f, 12, 10);
            _kinInfoText = UiFactory.CreateText(kinLeft, "家族信息", 14, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(_kinInfoText.gameObject, prefH: 52f);

            var kinOps = UiFactory.CreateEmpty(kinLeft, "KinOps");
            UiFactory.AddHLayout(kinOps.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(kinOps.gameObject, prefH: 34f);
            _kinInviteUinInput = UiFactory.CreateInputField(kinOps, "邀请UIN", "", width: 120f, height: 30f);
            UiFactory.CreateButton(kinOps, "邀请入族", () => { if (uint.TryParse(_kinInviteUinInput.text.Trim(), out uint u)) _ctx.App.RequestInviteKin(u); }, UiFactory.AccentCeladon, fontSize: 12, width: 82f, height: 30f);
            UiFactory.CreateButton(kinOps, "退出", () => _ctx.App.RequestExitKin(), UiFactory.AccentMuted, fontSize: 12, width: 58f, height: 30f);
            UiFactory.CreateButton(kinOps, "解散", () => _ctx.App.RequestDismissKin(), UiFactory.AccentDanger, fontSize: 12, width: 58f, height: 30f);

            var kinNotifRow = UiFactory.CreateEmpty(kinLeft, "KinNotifRow");
            UiFactory.AddHLayout(kinNotifRow.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(kinNotifRow.gameObject, prefH: 34f);
            _kinNoticeInput = UiFactory.CreateInputField(kinNotifRow, "新家族公告...", "", height: 30f);
            UiFactory.CreateButton(kinNotifRow, "更新公告", () => _ctx.App.RequestSetKinNotification(_kinNoticeInput.text.Trim()), UiFactory.AccentCeladon, fontSize: 12, width: 84f, height: 30f);

            var kmScroll = UiFactory.CreateScrollView(kinLeft, "KinMembers", out _kinMembersContent, spacing: 4f);
            UiFactory.SetLayout(kmScroll.gameObject, flexW: 1f, flexH: 1f);

            var kinRight = UiFactory.CreatePanel(_kinActiveBox, "KinRight", UiFactory.BgSlateLight, UiFactory.BorderSubtle);
            UiFactory.SetLayout(kinRight.gameObject, flexW: 1f, flexH: 1f);
            UiFactory.AddVLayout(kinRight.gameObject, 6f, 10, 10);
            var kcTitle = UiFactory.CreateText(kinRight, "家族频道聊天", 14, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(kcTitle.gameObject, prefH: 22f);
            var kChatScroll = UiFactory.CreateScrollView(kinRight, "KinChatScroll", out _kinChatContent, spacing: 3f);
            UiFactory.SetLayout(kChatScroll.gameObject, flexW: 1f, flexH: 1f);
            var kChatRow = UiFactory.CreateEmpty(kinRight, "KinChatRow");
            UiFactory.AddHLayout(kChatRow.gameObject, 6f, 0, 0);
            UiFactory.SetLayout(kChatRow.gameObject, prefH: 34f);
            _kinChatInput = UiFactory.CreateInputField(kChatRow, "发送家族消息...", "", height: 30f);
            UiFactory.CreateButton(kChatRow, "发送", () =>
            {
                string m = _kinChatInput.text.Trim();
                if (string.IsNullOrEmpty(m)) return;
                _kinChatInput.text = string.Empty;
                _ctx.App.RequestSendKinChat(m);
            }, UiFactory.AccentCeladon, fontSize: 12, width: 64f, height: 30f);

            // -- Marriage Panel --
            _marriagePanel = UiFactory.CreatePanel(sBody, "MarriagePanel", UiFactory.BgSlateLight, UiFactory.BorderGold);
            UiFactory.Stretch(_marriagePanel);
            UiFactory.AddVLayout(_marriagePanel.gameObject, 10f, 18, 16);

            _marriageStatusText = UiFactory.CreateText(_marriagePanel, "当前状态：单身逍遥", 16, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(_marriageStatusText.gameObject, prefH: 48f);

            var proposeRow = UiFactory.CreateEmpty(_marriagePanel, "ProposeRow");
            UiFactory.AddHLayout(proposeRow.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(proposeRow.gameObject, prefH: 36f);
            _sparkUinInput = UiFactory.CreateInputField(proposeRow, "求婚目标 UIN", "", width: 150f);
            _sparkWordInput = UiFactory.CreateInputField(proposeRow, "求婚誓言", "愿与君结为道侣，共游灵葫堂");
            UiFactory.CreateButton(proposeRow, "发起求婚", () =>
            {
                if (uint.TryParse(_sparkUinInput.text.Trim(), out uint u))
                    _ctx.App.RequestSpark(u, _sparkWordInput.text.Trim());
            }, UiFactory.AccentVermilion, fontSize: 13, width: 100f, height: 34f);

            var loveRow = UiFactory.CreateEmpty(_marriagePanel, "LoveWordRow");
            UiFactory.AddHLayout(loveRow.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(loveRow.gameObject, prefH: 36f);
            _loveWordInput = UiFactory.CreateInputField(loveRow, "修改伴侣爱情宣言...", "");
            UiFactory.CreateButton(loveRow, "保存宣言", () => _ctx.App.RequestModifyLoveWord(_loveWordInput.text.Trim()), UiFactory.AccentCeladon, fontSize: 13, width: 92f, height: 34f);
            UiFactory.CreateButton(loveRow, "解除关系", () => _ctx.App.RequestDivorce(), UiFactory.AccentDanger, fontSize: 13, width: 92f, height: 34f);

            var wedRow = UiFactory.CreateEmpty(_marriagePanel, "WeddingRow");
            UiFactory.AddHLayout(wedRow.gameObject, 8f, 0, 0);
            UiFactory.SetLayout(wedRow.gameObject, prefH: 34f);
            var wLbl = UiFactory.CreateText(wedRow, "婚礼仪式:", 13, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(wLbl.gameObject, prefW: 72f, prefH: 30f);
            UiFactory.CreateButton(wedRow, "发起中式婚礼", () => _ctx.App.RequestStartWedding(1), UiFactory.AccentVermilion, fontSize: 12, width: 108f, height: 30f);
            UiFactory.CreateButton(wedRow, "发起西式婚礼", () => _ctx.App.RequestStartWedding(0), UiFactory.AccentCeladon, fontSize: 12, width: 108f, height: 30f);
            UiFactory.CreateButton(wedRow, "切中式礼堂", () => _ctx.App.RequestChangeWeddingMode(1), UiFactory.AccentMuted, fontSize: 12, width: 96f, height: 30f);
            UiFactory.CreateButton(wedRow, "切西式教堂", () => _ctx.App.RequestChangeWeddingMode(0), UiFactory.AccentMuted, fontSize: 12, width: 96f, height: 30f);

            var spLbl = UiFactory.CreateText(_marriagePanel, "收到的求婚与婚礼邀请:", 14, UiFactory.TextLight, FontStyle.Bold);
            UiFactory.SetLayout(spLbl.gameObject, prefH: 22f);
            var spScroll = UiFactory.CreateScrollView(_marriagePanel, "SparkReqs", out _sparkReqContent, spacing: 4f);
            UiFactory.SetLayout(spScroll.gameObject, flexW: 1f, flexH: 1f);

            SelectSocialSubTab(0);
            SetActiveView(MainTab.Lobby);
        }

        public void SetActiveView(MainTab tab)
        {
            PetsRoot.gameObject.SetActive(tab == MainTab.Pets);
            SocialRoot.gameObject.SetActive(tab == MainTab.Social);
            if (tab == MainTab.Pets) RefreshPets();
            else if (tab == MainTab.Social) RefreshSocial();
        }

        public void RefreshAllVisible()
        {
            if (PetsRoot.gameObject.activeSelf) RefreshPets();
            if (SocialRoot.gameObject.activeSelf) RefreshSocial();
        }

        private void SelectSocialSubTab(int idx)
        {
            _friendsPanel.gameObject.SetActive(idx == 0);
            _kinPanel.gameObject.SetActive(idx == 1);
            _marriagePanel.gameObject.SetActive(idx == 2);
            RefreshSocial();
        }

        public void RefreshPets()
        {
            var pets = _ctx.Store.Pets;
            _petStatusText.text = $"我的灵宠 ({pets.Count})";
            UiFactory.ClearChildren(_petsContent);
            if (pets.Count == 0)
            {
                var empty = UiFactory.CreateText(_petsContent, "暂无灵宠。可在上方输入宠物卡道具ID点击「领养宠物卡」。", 14, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
                UiFactory.SetLayout(empty.gameObject, prefH: 46f);
                return;
            }
            foreach (var p in pets)
            {
                var row = UiFactory.CreatePanel(_petsContent, $"Pet_{p.PetId}", UiFactory.BgSlateLight, p.IsActive ? UiFactory.AccentGold : UiFactory.BorderSubtle);
                UiFactory.SetLayout(row.gameObject, prefH: 52f);
                UiFactory.AddHLayout(row.gameObject, 6f, 10, 6);

                string typeName = _ctx.Catalogs.GetPetTypeName(p.PetTypeId);
                var lbl = UiFactory.CreateText(row, $"{p.Name} [{typeName}] Lv.{p.Level} | 经验:{p.Experience} 忠诚:{p.Loyalty} 心情:{p.Mood} {(p.IsActive ? "★出战中" : "")}", 13,
                    p.IsActive ? UiFactory.AccentGold : UiFactory.TextLight);
                UiFactory.SetLayout(lbl.gameObject, flexW: 1f, prefH: 36f);

                uint pid = p.PetId;
                UiFactory.CreateButton(row, p.IsActive ? "休息" : "出战",
                    () => _ctx.App.RequestHandlePet(p.IsActive ? PetEventId.Deactivate : PetEventId.Activate, pid),
                    UiFactory.AccentCeladon, fontSize: 12, width: 54f, height: 28f);
                UiFactory.CreateButton(row, "喂食", () => _ctx.App.RequestHandlePet(PetEventId.Feed, pid, _petParamInput.text.Trim()), UiFactory.AccentMuted, fontSize: 12, width: 52f, height: 28f);
                UiFactory.CreateButton(row, "改名", () => _ctx.App.RequestHandlePet(PetEventId.Rename, pid, _petParamInput.text.Trim()), UiFactory.AccentMuted, fontSize: 12, width: 52f, height: 28f);
                UiFactory.CreateButton(row, "放生", () => _ctx.App.RequestHandlePet(PetEventId.Release, pid), UiFactory.AccentDanger, fontSize: 12, width: 52f, height: 28f);
            }
        }

        public void RefreshSocial()
        {
            var s = _ctx.Store;

            // Friends
            if (s.LastFindFriend != null)
            {
                var ff = s.LastFindFriend;
                _findFriendResult.text = ff.ResultId == 0
                    ? $"查找到玩家: {ff.PlayerName} ({ff.FriendUin}) | {(ff.Online ? "在线" : "离线")} | 房间:{(ff.RoomId > 0 ? $"#{ff.RoomId} {ff.RoomName}" : "大厅")} | 家族:{ff.KinName}"
                    : $"未找到该玩家 (code={ff.ResultId})";
            }

            UiFactory.ClearChildren(_friendReqContent);
            foreach (var req in s.PendingFriendRequests)
            {
                var row = UiFactory.CreatePanel(_friendReqContent, $"FReq_{req.RequesterUin}", UiFactory.BgSlateLight, UiFactory.AccentGold);
                UiFactory.SetLayout(row.gameObject, prefH: 36f);
                UiFactory.AddHLayout(row.gameObject, 6f, 8, 4);
                var t = UiFactory.CreateText(row, $"[好友申请] UIN {req.RequesterUin}: {req.Word}", 13, UiFactory.AccentGold);
                UiFactory.SetLayout(t.gameObject, flexW: 1f, prefH: 26f);
                uint rUin = req.RequesterUin;
                UiFactory.CreateButton(row, "同意", () => _ctx.App.RequestAnswerFriend(rUin, true), UiFactory.AccentCeladon, fontSize: 12, width: 58f, height: 26f);
                UiFactory.CreateButton(row, "拒绝", () => _ctx.App.RequestAnswerFriend(rUin, false), UiFactory.AccentDanger, fontSize: 12, width: 58f, height: 26f);
            }

            UiFactory.ClearChildren(_friendListContent);
            foreach (uint fUin in s.FriendUins)
            {
                s.FriendDetails.TryGetValue(fUin, out var detail);
                var row = UiFactory.CreatePanel(_friendListContent, $"Fr_{fUin}", UiFactory.BgSlateLight);
                UiFactory.SetLayout(row.gameObject, prefH: 36f);
                UiFactory.AddHLayout(row.gameObject, 8f, 10, 4);
                string label = detail != null
                    ? $"{(detail.Online ? "● 在线" : "○ 离线")}  {detail.Nickname} ({fUin}) | 积分: {detail.Point}"
                    : $"○ 好友 ({fUin})";
                var txt = UiFactory.CreateText(row, label, 13, detail != null && detail.Online ? UiFactory.AccentGold : UiFactory.TextLight);
                UiFactory.SetLayout(txt.gameObject, flexW: 1f, prefH: 26f);
                uint captured = fUin;
                UiFactory.CreateButton(row, "删除", () => _ctx.App.RequestRemoveFriend(captured), UiFactory.AccentDanger, fontSize: 12, width: 58f, height: 26f);
            }

            // Kin
            bool hasKin = s.KinIndex > 0 && s.KinBase != null;
            _kinNoGroupBox.gameObject.SetActive(!hasKin);
            _kinActiveBox.gameObject.SetActive(hasKin);

            if (!hasKin)
            {
                UiFactory.ClearChildren(_kinInvitesContent);
                foreach (var inv in s.PendingKinInvites)
                {
                    var row = UiFactory.CreatePanel(_kinInvitesContent, $"KInv_{inv.KinIndex}", UiFactory.BgSlateCard, UiFactory.AccentGold);
                    UiFactory.SetLayout(row.gameObject, prefH: 38f);
                    UiFactory.AddHLayout(row.gameObject, 6f, 8, 4);
                    var t = UiFactory.CreateText(row, $"邀请加入 [{inv.Kin?.Name ?? inv.KinIndex.ToString()}] (邀请人 {inv.InviterUin})", 13, UiFactory.AccentGold);
                    UiFactory.SetLayout(t.gameObject, flexW: 1f, prefH: 28f);
                    uint inviter = inv.InviterUin, kid = inv.KinIndex;
                    UiFactory.CreateButton(row, "加入", () => _ctx.App.RequestAnswerKinInvite(inviter, kid, true), UiFactory.AccentCeladon, fontSize: 12, width: 60f, height: 26f);
                    UiFactory.CreateButton(row, "拒绝", () => _ctx.App.RequestAnswerKinInvite(inviter, kid, false), UiFactory.AccentDanger, fontSize: 12, width: 60f, height: 26f);
                }
            }
            else
            {
                var kb = s.KinBase;
                _kinInfoText.text = $"[{kb.Name}] (ID:{kb.Index}) | 成员:{kb.MemberCount} | 荣誉:{kb.Honor} | 活跃:{kb.ActivePoint}\n宣言: {kb.Declaration}   公告: {kb.Notification}";

                UiFactory.ClearChildren(_kinMembersContent);
                foreach (var m in s.KinMembers)
                {
                    var row = UiFactory.CreatePanel(_kinMembersContent, $"KM_{m.Uin}", UiFactory.BgSlateCard);
                    UiFactory.SetLayout(row.gameObject, prefH: 34f);
                    UiFactory.AddHLayout(row.gameObject, 6f, 8, 4);
                    var t = UiFactory.CreateText(row, $"{(m.IsOnline ? "●" : "○")} {m.Nickname} ({m.Uin}) | 荣誉:{m.Honor}", 13,
                        m.Uin == kb.OwnerUin ? UiFactory.AccentGold : UiFactory.TextLight);
                    UiFactory.SetLayout(t.gameObject, flexW: 1f, prefH: 24f);
                    if (kb.OwnerUin == s.Uin && m.Uin != s.Uin)
                    {
                        uint target = m.Uin;
                        UiFactory.CreateButton(row, "移出", () => _ctx.App.RequestKickKinMember(target), UiFactory.AccentDanger, fontSize: 11, width: 52f, height: 24f);
                    }
                }

                UiFactory.ClearChildren(_kinChatContent);
                foreach (var msg in s.KinChat)
                {
                    var line = UiFactory.CreateText(_kinChatContent, $"[{msg.Timestamp}] {msg.Sender}: {msg.Content}", 13,
                        msg.Highlight ? UiFactory.AccentGold : UiFactory.TextLight);
                    UiFactory.SetLayout(line.gameObject, prefH: 20f);
                }
            }

            // Marriage
            if (s.Marriage != null && s.Marriage.IsMarried)
            {
                var m = s.Marriage;
                _marriageStatusText.text = $"已结良缘！道侣: {m.SpouseNickname} ({m.MarriageUin}) | 甜蜜度:{m.Loyalty} 等级:{m.Level}\n爱情宣言: {m.LoveWord}";
            }
            else
            {
                _marriageStatusText.text = "当前状态：单身逍遥（持有求婚道具/戒指可向在线好友发起求婚）";
            }

            UiFactory.ClearChildren(_sparkReqContent);
            foreach (var sp in s.PendingSparks)
            {
                var row = UiFactory.CreatePanel(_sparkReqContent, $"Spark_{sp.ProposerUin}", UiFactory.BgSlateCard, UiFactory.AccentGold);
                UiFactory.SetLayout(row.gameObject, prefH: 38f);
                UiFactory.AddHLayout(row.gameObject, 6f, 8, 4);
                var t = UiFactory.CreateText(row, $"[求婚] {sp.Nickname} ({sp.ProposerUin}): {sp.Word}", 13, UiFactory.AccentGold);
                UiFactory.SetLayout(t.gameObject, flexW: 1f, prefH: 28f);
                uint proposer = sp.ProposerUin;
                UiFactory.CreateButton(row, "答应", () => _ctx.App.RequestAnswerSpark(proposer, true), UiFactory.AccentVermilion, fontSize: 12, width: 64f, height: 26f);
                UiFactory.CreateButton(row, "婉拒", () => _ctx.App.RequestAnswerSpark(proposer, false), UiFactory.AccentMuted, fontSize: 12, width: 64f, height: 26f);
            }
            foreach (var w in s.PendingWeddings)
            {
                var row = UiFactory.CreatePanel(_sparkReqContent, $"Wed_{w.ProposerUin}", UiFactory.BgSlateCard, UiFactory.AccentVermilion);
                UiFactory.SetLayout(row.gameObject, prefH: 38f);
                UiFactory.AddHLayout(row.gameObject, 6f, 8, 4);
                var t = UiFactory.CreateText(row, $"[婚礼邀请] {w.Nickname} ({w.ProposerUin}): {w.Word}", 13, UiFactory.AccentGold);
                UiFactory.SetLayout(t.gameObject, flexW: 1f, prefH: 28f);
                uint proposer = w.ProposerUin;
                UiFactory.CreateButton(row, "同意举行婚礼", () => _ctx.App.RequestAnswerWedding(proposer, true), UiFactory.AccentVermilion, fontSize: 12, width: 102f, height: 26f);
                UiFactory.CreateButton(row, "婉拒", () => _ctx.App.RequestAnswerWedding(proposer, false), UiFactory.AccentMuted, fontSize: 12, width: 58f, height: 26f);
            }
        }
    }
}
