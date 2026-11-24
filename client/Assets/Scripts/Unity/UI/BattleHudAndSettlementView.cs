using System;
using Qqtang.Core.Battle;
using Qqtang.Core.Protocol;
using Qqtang.Core.State;
using UnityEngine;
using UnityEngine.UI;

namespace Qqtang.UnityClient.UI
{
    /// <summary>
    /// In-battle HUD (timer, map/art badge, participant strip, combat stats + item/emote hotbar), Esc pause menu, and Match Settlement overlay.
    /// </summary>
    public sealed class BattleHudAndSettlementView
    {
        private readonly UiContext _ctx;
        public readonly RectTransform HudRoot;
        public readonly RectTransform PauseModal;
        public readonly RectTransform SettlementRoot;
        public readonly RectTransform TreasureModal;

        private readonly Text _timerText;
        private readonly Text _mapAndArtText;
        private readonly Text _participantsText;
        private readonly Text _localStatsText;
        private readonly Text _hotbarText;
        private readonly Text _readyBannerText;
        private readonly Text _bossTalkBanner;
        private readonly Button _nextStageBtn;

        // In-battle mini chat
        private readonly RectTransform _miniChatFeed;
        private readonly RectTransform _miniChatInputRow;
        private readonly InputField _miniChatInput;
        private int _lastMiniChatCount = -1;

        // Hidden Treasure Room overlay
        private readonly Text _treasureTitle;
        private readonly RectTransform _treasureContent;

        private readonly Text _settlementTitle;
        private readonly Image _settlementCrestImage;
        private readonly Text _settlementReturnLabel;
        private readonly RectTransform _scoreboardContent;

        public bool IsPauseOpen => PauseModal.gameObject.activeSelf;
        public bool IsChatInputFocused => _miniChatInputRow.gameObject.activeSelf && _miniChatInput != null && _miniChatInput.isFocused;

        public BattleHudAndSettlementView(Transform parent, UiContext ctx)
        {
            _ctx = ctx;

            // --- HUD ROOT (non-blocking transparent overlay) ---
            HudRoot = UiFactory.CreateEmpty(parent, "BattleHud");
            UiFactory.Stretch(HudRoot);

            // Top HUD bar
            var topBar = UiFactory.CreatePanel(HudRoot, "TopHudBar", new Color(0.08f, 0.13f, 0.17f, 0.84f), UiFactory.BorderGold);
            topBar.anchorMin = new Vector2(0f, 1f);
            topBar.anchorMax = new Vector2(1f, 1f);
            topBar.pivot = new Vector2(0.5f, 1f);
            topBar.sizeDelta = new Vector2(0f, 52f);
            UiFactory.AddHLayout(topBar.gameObject, 12f, 16, 6);

            _timerText = UiFactory.CreateText(topBar, "04:00", 22, UiFactory.AccentGold, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(_timerText.gameObject, prefW: 86f, prefH: 38f);

            _mapAndArtText = UiFactory.CreateText(topBar, "地图 | 美术源", 14, UiFactory.TextLight);
            UiFactory.SetLayout(_mapAndArtText.gameObject, prefW: 280f, prefH: 38f);

            _participantsText = UiFactory.CreateText(topBar, "", 13, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
            UiFactory.SetLayout(_participantsText.gameObject, flexW: 1f, prefH: 38f);

            UiFactory.CreateButton(topBar, "菜单 (Esc)", TogglePause, UiFactory.AccentMuted, fontSize: 13, width: 98f, height: 32f);

            // Center Ready/Go banner & Boss Speech banner
            _readyBannerText = UiFactory.CreateText(HudRoot, "", 42, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _readyBannerText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _readyBannerText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _readyBannerText.rectTransform.sizeDelta = new Vector2(420f, 70f);

            _bossTalkBanner = UiFactory.CreateText(HudRoot, "", 16, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _bossTalkBanner.rectTransform.anchorMin = new Vector2(0.5f, 1f);
            _bossTalkBanner.rectTransform.anchorMax = new Vector2(0.5f, 1f);
            _bossTalkBanner.rectTransform.anchoredPosition = new Vector2(0f, -72f);
            _bossTalkBanner.rectTransform.sizeDelta = new Vector2(540f, 28f);
            _bossTalkBanner.gameObject.SetActive(false);

            _nextStageBtn = UiFactory.CreateButton(HudRoot, "进入下一关 (探险传送门)", () => _ctx.App.RequestAdventureNextMap(), UiFactory.AccentVermilion, fontSize: 15, width: 210f, height: 40f);
            var nsRt = _nextStageBtn.GetComponent<RectTransform>();
            nsRt.anchorMin = nsRt.anchorMax = new Vector2(0.5f, 1f);
            nsRt.anchoredPosition = new Vector2(0f, -112f);
            _nextStageBtn.gameObject.SetActive(false);

            // Bottom HUD bar
            var botBar = UiFactory.CreatePanel(HudRoot, "BottomHudBar", new Color(0.08f, 0.13f, 0.17f, 0.86f), UiFactory.BorderGold);
            botBar.anchorMin = new Vector2(0f, 0f);
            botBar.anchorMax = new Vector2(1f, 0f);
            botBar.pivot = new Vector2(0.5f, 0f);
            botBar.sizeDelta = new Vector2(0f, 54f);
            UiFactory.AddHLayout(botBar.gameObject, 6f, 12, 6);

            _localStatsText = UiFactory.CreateText(botBar, "泡:2 威:2 速:4", 12, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(_localStatsText.gameObject, prefW: 210f, prefH: 38f);

            _hotbarText = UiFactory.CreateText(botBar, "", 12, UiFactory.TextLight);
            UiFactory.SetLayout(_hotbarText.gameObject, flexW: 1f, prefH: 38f);

            UiFactory.CreateButton(botBar, "针(1)", () => _ctx.App.TriggerBattleWorldItem(63), UiFactory.AccentCeladon, fontSize: 11, width: 44f, height: 30f);
            UiFactory.CreateButton(botBar, "陷(2)", () => _ctx.App.TriggerBattleWorldItem(41), UiFactory.AccentVermilion, fontSize: 11, width: 44f, height: 30f);
            UiFactory.CreateButton(botBar, "蕉(3)", () => _ctx.App.TriggerBattleWorldItem(42), UiFactory.AccentGold, UiFactory.TextInk, fontSize: 11, width: 44f, height: 30f);
            UiFactory.CreateButton(botBar, "胶(4)", () => _ctx.App.TriggerBattleWorldItem(43), UiFactory.AccentCeladon, fontSize: 11, width: 44f, height: 30f);
            UiFactory.CreateButton(botBar, "镖(5)", () => _ctx.App.TriggerBattleProjectileItem(), UiFactory.AccentMuted, fontSize: 11, width: 44f, height: 30f);
            UiFactory.CreateButton(botBar, "遥(6)", () => _ctx.App.TriggerBattleWorldItem(64), UiFactory.AccentMuted, fontSize: 11, width: 44f, height: 30f);
            UiFactory.CreateButton(botBar, "绝技(F)", () => _ctx.App.TriggerBattleModeSkill(), UiFactory.AccentVermilion, fontSize: 11, width: 56f, height: 30f);
            UiFactory.CreateButton(botBar, "表(E)", () => _ctx.App.TriggerBattleEmotion(1), UiFactory.AccentMuted, fontSize: 11, width: 44f, height: 30f);

            // In-battle Mini Chat (bottom-left above bottom bar)
            var miniChatBox = UiFactory.CreateEmpty(HudRoot, "BattleMiniChat");
            miniChatBox.anchorMin = new Vector2(0f, 0f);
            miniChatBox.anchorMax = new Vector2(0f, 0f);
            miniChatBox.pivot = new Vector2(0f, 0f);
            miniChatBox.anchoredPosition = new Vector2(14f, 62f);
            miniChatBox.sizeDelta = new Vector2(360f, 150f);
            UiFactory.AddVLayout(miniChatBox.gameObject, 4f, 0, 0);

            _miniChatFeed = UiFactory.CreatePanel(miniChatBox, "Feed", new Color(0.05f, 0.08f, 0.11f, 0.52f));
            UiFactory.SetLayout(_miniChatFeed.gameObject, flexW: 1f, flexH: 1f);
            UiFactory.AddVLayout(_miniChatFeed.gameObject, 2f, 8, 4);

            _miniChatInputRow = UiFactory.CreatePanel(miniChatBox, "InputRow", new Color(0.08f, 0.13f, 0.17f, 0.92f), UiFactory.BorderGold);
            UiFactory.SetLayout(_miniChatInputRow.gameObject, flexW: 1f, prefH: 34f);
            UiFactory.AddHLayout(_miniChatInputRow.gameObject, 6f, 6, 3);
            _miniChatInput = UiFactory.CreateInputField(_miniChatInputRow, "按 Enter 发送局内聊天...", "", height: 26f);
            UiFactory.CreateButton(_miniChatInputRow, "发", SendMiniChat, UiFactory.AccentCeladon, fontSize: 12, width: 42f, height: 26f);
            _miniChatInputRow.gameObject.SetActive(false);

            // --- ESC PAUSE MODAL ---
            PauseModal = UiFactory.CreatePanel(parent, "BattlePauseModal", UiFactory.BgModalBackdrop);
            UiFactory.Stretch(PauseModal);
            var pCard = UiFactory.CreatePanel(PauseModal, "PauseCard", UiFactory.BgSlateCard, UiFactory.AccentGold);
            pCard.anchorMin = pCard.anchorMax = pCard.pivot = new Vector2(0.5f, 0.5f);
            pCard.sizeDelta = new Vector2(340f, 210f);
            UiFactory.AddVLayout(pCard.gameObject, 14f, 22, 20);
            var pTitle = UiFactory.CreateText(pCard, "对局菜单", 20, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetLayout(pTitle.gameObject, prefH: 30f);
            UiFactory.CreateButton(pCard, "继续对局", () => PauseModal.gameObject.SetActive(false), UiFactory.AccentCeladon, fontSize: 15, height: 42f);
            UiFactory.CreateButton(pCard, "放弃本局并返回", () =>
            {
                PauseModal.gameObject.SetActive(false);
                _ctx.App.RequestLeaveRoom();
            }, UiFactory.AccentDanger, fontSize: 14, height: 42f);
            PauseModal.gameObject.SetActive(false);

            // --- SETTLEMENT OVERLAY ---
            SettlementRoot = UiFactory.CreatePanel(parent, "SettlementOverlay", UiFactory.BgModalBackdrop);
            UiFactory.Stretch(SettlementRoot);
            var sCard = UiFactory.CreatePanel(SettlementRoot, "SettlementCard", UiFactory.BgSlateCard, UiFactory.AccentGold);
            sCard.anchorMin = sCard.anchorMax = sCard.pivot = new Vector2(0.5f, 0.5f);
            sCard.sizeDelta = new Vector2(620f, 430f);
            UiFactory.AddVLayout(sCard.gameObject, 12f, 24, 20);

            var crestGo = new GameObject("ResultCrest", typeof(RectTransform), typeof(Image));
            crestGo.transform.SetParent(sCard, false);
            _settlementCrestImage = crestGo.GetComponent<Image>();
            _settlementCrestImage.preserveAspect = true;
            UiFactory.SetLayout(crestGo, prefH: 76f);
            crestGo.SetActive(false);

            _settlementTitle = UiFactory.CreateText(sCard, "对 局 结 算", 28, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetLayout(_settlementTitle.gameObject, prefH: 38f);

            var sbScroll = UiFactory.CreateScrollView(sCard, "Scoreboard", out _scoreboardContent, spacing: 6f);
            UiFactory.SetLayout(sbScroll.gameObject, flexW: 1f, flexH: 1f);

            var retBtn = UiFactory.CreateButton(sCard, "返回等待房间", () => _ctx.App.ReturnToRoomAfterSettlement(), UiFactory.AccentVermilion, fontSize: 17, height: 46f);
            _settlementReturnLabel = retBtn.GetComponentInChildren<Text>();

            // --- HIDDEN TREASURE ROOM MODAL ---
            TreasureModal = UiFactory.CreatePanel(parent, "TreasureRoomModal", UiFactory.BgModalBackdrop);
            UiFactory.Stretch(TreasureModal);
            var tCard = UiFactory.CreatePanel(TreasureModal, "TreasureCard", UiFactory.BgSlateCard, UiFactory.AccentGold);
            tCard.anchorMin = tCard.anchorMax = tCard.pivot = new Vector2(0.5f, 0.5f);
            tCard.sizeDelta = new Vector2(520f, 420f);
            UiFactory.AddVLayout(tCard.gameObject, 10f, 20, 18);

            _treasureTitle = UiFactory.CreateText(tCard, "隐藏藏宝室", 22, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetLayout(_treasureTitle.gameObject, prefH: 34f);

            var tScroll = UiFactory.CreateScrollView(tCard, "TreasureScroll", out _treasureContent, spacing: 6f);
            UiFactory.SetLayout(tScroll.gameObject, flexW: 1f, flexH: 1f);

            UiFactory.CreateButton(tCard, "离开藏宝室", () => _ctx.App.RequestLeaveTreasureRoom(), UiFactory.AccentMuted, fontSize: 15, height: 40f);
            TreasureModal.gameObject.SetActive(false);

            HudRoot.gameObject.SetActive(false);
            SettlementRoot.gameObject.SetActive(false);
        }

        public void SetHudVisible(bool visible)
        {
            HudRoot.gameObject.SetActive(visible);
            if (!visible)
            {
                PauseModal.gameObject.SetActive(false);
                _miniChatInputRow.gameObject.SetActive(false);
            }
        }

        public void TogglePause()
        {
            if (!HudRoot.gameObject.activeSelf) return;
            PauseModal.gameObject.SetActive(!PauseModal.gameObject.activeSelf);
        }

        public void ToggleBattleChat()
        {
            if (!HudRoot.gameObject.activeSelf || IsPauseOpen) return;
            if (!_miniChatInputRow.gameObject.activeSelf)
            {
                _miniChatInputRow.gameObject.SetActive(true);
                _miniChatInput.text = string.Empty;
                _miniChatInput.ActivateInputField();
                _miniChatInput.Select();
            }
            else
            {
                SendMiniChat();
            }
        }

        private void SendMiniChat()
        {
            string txt = (_miniChatInput.text ?? string.Empty).Trim();
            _miniChatInput.text = string.Empty;
            _miniChatInputRow.gameObject.SetActive(false);
            if (!string.IsNullOrEmpty(txt))
                _ctx.App.RequestSendRoomChat(txt);
        }

        public void ShowTreasureRoom(TreasureRoomResponseData data)
        {
            if (data == null) return;
            TreasureModal.gameObject.SetActive(true);
            _treasureTitle.text = $"隐藏藏宝室 (地图 #{data.MapId} · {data.Items.Count} 件宝物)";
            UiFactory.ClearChildren(_treasureContent);
            if (data.Items.Count == 0)
            {
                var empty = UiFactory.CreateText(_treasureContent, "宝物已全部拾取完毕！", 14, UiFactory.TextSubtle, TextAnchor.MiddleCenter);
                UiFactory.SetLayout(empty.gameObject, prefH: 48f);
                return;
            }
            foreach (var it in data.Items)
            {
                var row = UiFactory.CreatePanel(_treasureContent, $"Tr_{it.ItemId}", UiFactory.BgSlateLight);
                UiFactory.SetLayout(row.gameObject, prefH: 40f);
                UiFactory.AddHLayout(row.gameObject, 8f, 10, 4);
                var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(row, false);
                var img = iconGo.GetComponent<Image>();
                img.sprite = _ctx.Art.GetItemIconSprite(it.ItemId);
                img.preserveAspect = true;
                UiFactory.SetLayout(iconGo, prefW: 26f, prefH: 26f);

                var lbl = UiFactory.CreateText(row, $"#{it.ItemId} {_ctx.Catalogs.GetItemName(it.ItemId)} ×{it.Quantity}", 13, UiFactory.AccentGold);
                UiFactory.SetLayout(lbl.gameObject, flexW: 1f, prefH: 28f);
                uint capturedId = it.ItemId;
                UiFactory.CreateButton(row, "拾取", () => _ctx.App.RequestGetTreasureItem(capturedId), UiFactory.AccentVermilion, fontSize: 12, width: 64f, height: 28f);
            }
        }

        public void HideTreasureRoom() => TreasureModal.gameObject.SetActive(false);

        public void UpdateHud(Rule1BattleSimulation sim, ushort mapId)
        {
            if (sim == null) return;
            uint remainMs = sim.RoundDurationMs > sim.ElapsedMs ? sim.RoundDurationMs - sim.ElapsedMs : 0;
            uint totalSec = remainMs / 1000;
            _timerText.text = $"{totalSec / 60:00}:{totalSec % 60:00}";

            string artTag = _ctx.Art.GetResolvedSource("characters/role_0_S");
            string artLabel = artTag == "daoist" ? "道风重制" : (artTag == "original" ? "原版兜底" : "程序渲染");
            string ruleTag = RuleDisplayName(sim.NativeRule);
            string modePrefix = sim.IsOfflineSoloMode ? "[离线] " : $"[{ruleTag}] ";
            _mapAndArtText.text = $"{modePrefix}{_ctx.Catalogs.GetMapName(mapId)} ({mapId}) [{artLabel}]";

            if (sim.ElapsedMs < Rule1BattleSimulation.RoundStartClockMs)
            {
                uint sec = (Rule1BattleSimulation.RoundStartClockMs - sim.ElapsedMs + 999) / 1000;
                _readyBannerText.text = sec > 0 ? $"准备 {sec}" : "开 战 !";
                _readyBannerText.gameObject.SetActive(true);
            }
            else if (sim.ElapsedMs < Rule1BattleSimulation.RoundStartClockMs + 800)
            {
                _readyBannerText.text = "开 战 !";
                _readyBannerText.gameObject.SetActive(true);
            }
            else
            {
                _readyBannerText.gameObject.SetActive(false);
            }

            string summary = string.Empty;
            foreach (var a in sim.Actors)
            {
                string st = a.State == ActorLifeState.Active ? "战" : (a.State == ActorLifeState.Trapped ? "泡" : "亡");
                summary += $"[队{a.TeamId} P{a.PlayerId}:{st}] ";
            }
            string bossSpeech = string.Empty;
            foreach (var boss in sim.Bosses)
            {
                if (boss.Alive)
                {
                    summary += $"| BOSS:{boss.Hp}/{boss.MaxHp} ";
                    if (!string.IsNullOrEmpty(boss.SpeechText) && sim.ElapsedMs < boss.SpeechExpireMs)
                        bossSpeech = $"[BOSS] {boss.SpeechText}";
                }
            }
            _bossTalkBanner.text = bossSpeech;
            _bossTalkBanner.gameObject.SetActive(!string.IsNullOrEmpty(bossSpeech));
            _nextStageBtn.gameObject.SetActive(!sim.IsOfflineSoloMode && _ctx.Store.GameType == RoomGameType.Adventure && sim.AllBossesDefeated);

            if (sim.BunCarrierPlayerId > 0)
            {
                summary += $"| 包子携带:P{sim.BunCarrierPlayerId} ";
            }
            else if (sim.TeamBunScores.Count > 0)
            {
                summary += "| 包子得分:";
                foreach (var kv in sim.TeamBunScores) summary += $"队{kv.Key}={kv.Value} ";
            }
            if (sim.TankBaseHpByTeam.Count > 0)
            {
                summary += "| 基地HP:";
                foreach (var kv in sim.TankBaseHpByTeam) summary += $"队{kv.Key}={kv.Value} ";
            }
            _participantsText.text = summary;

            var local = sim.LocalActor;
            if (local != null)
            {
                string status = string.Empty;
                if (local.MaxNativeHitPoints > 0) status += $" 命:{local.NativeHitPoints}/{local.MaxNativeHitPoints}";
                if (local.IsInvulnerable(sim.ElapsedMs)) status += " [护盾]";
                if ((local.PassiveEffects & PassiveFunctionEffectMask.Lightning) != 0) status += " [雷电]";
                if ((local.PassiveEffects & PassiveFunctionEffectMask.SwordArc) != 0) status += " [剑气]";
                if ((local.PassiveEffects & PassiveFunctionEffectMask.KickImpact) != 0) status += " [少林踢]";
                if (local.MovementStatus == MovementStatusKind.ForcedSlide) status += " [冰滑]";
                else if (local.MovementStatus == MovementStatusKind.Slow) status += " [胶水减速]";
                else if (local.MovementStatus == MovementStatusKind.Fast) status += " [极速]";
                if (Rule1BattleSimulation.TryGetTransformation(local.TransformationSceneId, out var td))
                {
                    uint remS = local.TransformationExpiresAtMs > sim.ElapsedMs ? (local.TransformationExpiresAtMs - sim.ElapsedMs + 999) / 1000 : 0;
                    status += $" [{td.DisplayName} {remS}s]";
                }
                _localStatsText.text = $"泡:{local.BombCapacity}/{local.MaxBombCapacity} 威:{local.BombPower} 速:{local.SpeedRate}{status}";
                int projCount = local.AxeCount > 0 ? local.AxeCount : local.DartCount;
                string projName = local.AxeCount > 0 ? "斧" : "镖";
                _hotbarText.text = $"针×{local.NeedleCount} 陷×{local.TrapCount} 蕉×{local.BananaCount} 胶×{local.GlueCount} {projName}×{projCount} 遥×{local.RemoteDetonatorCount} | 杀:{local.KillCount} 救:{local.RescueCount} 糖:{local.MatchSugar}";
            }

            // Refresh mini chat feed only when message count changes
            var chat = _ctx.Store.RoomChat;
            if (!sim.IsOfflineSoloMode && chat.Count != _lastMiniChatCount)
            {
                _lastMiniChatCount = chat.Count;
                UiFactory.ClearChildren(_miniChatFeed);
                int start = Math.Max(0, chat.Count - 5);
                for (int i = start; i < chat.Count; i++)
                {
                    var m = chat[i];
                    var t = UiFactory.CreateText(_miniChatFeed, $"{m.Sender}: {m.Content}", 12, m.Highlight ? UiFactory.AccentGold : UiFactory.TextLight);
                    UiFactory.SetLayout(t.gameObject, prefH: 18f);
                }
            }
            _miniChatFeed.gameObject.SetActive(!sim.IsOfflineSoloMode && chat.Count > 0);
        }

        public void ShowSettlement(GameOverData over, bool isOfflineSolo = false)
        {
            SetHudVisible(false);
            SettlementRoot.gameObject.SetActive(true);
            if (_settlementReturnLabel != null)
                _settlementReturnLabel.text = isOfflineSolo ? "返回登录界面" : "返回等待房间";
            UiFactory.ClearChildren(_scoreboardContent);

            ushort myPid = isOfflineSolo ? (ushort)1 : _ctx.Store.PlayerId;
            var myRes = over?.Results.Find(r => r.PlayerId == myPid);
            if (myRes != null)
            {
                _settlementTitle.text = myRes.Result == GameResultCode.Win ? "大 获 全 胜"
                    : (myRes.Result == GameResultCode.Draw ? "平 分 秋 色" : "再 接 再 厉");
                _settlementTitle.color = myRes.Result == GameResultCode.Win ? UiFactory.AccentGold
                    : (myRes.Result == GameResultCode.Draw ? UiFactory.TextLight : UiFactory.AccentVermilion);
                string crestKey = myRes.Result == GameResultCode.Win ? "victory" : (myRes.Result == GameResultCode.Draw ? "draw" : "defeat");
                Sprite crest = _ctx.Art.GetResultCrestSprite(crestKey);
                _settlementCrestImage.sprite = crest;
                _settlementCrestImage.gameObject.SetActive(crest != null);
            }
            else
            {
                _settlementTitle.text = "对 局 结 算";
                _settlementCrestImage.gameObject.SetActive(false);
            }

            if (over != null)
            {
                foreach (var r in over.Results)
                {
                    string name;
                    if (isOfflineSolo)
                    {
                        name = r.PlayerId == 1 ? "道长 (你)" : $"离线道友 AI #{r.PlayerId - 1}";
                    }
                    else
                    {
                        var member = _ctx.Store.RoomMembers.Find(m => m.PlayerId == r.PlayerId);
                        name = member != null ? member.Nickname : $"玩家 #{r.PlayerId}";
                    }
                    string outcome = r.Result == GameResultCode.Win ? "胜利" : (r.Result == GameResultCode.Draw ? "平局" : "失败");

                    var row = UiFactory.CreatePanel(_scoreboardContent, $"Res_{r.PlayerId}", UiFactory.BgSlateLight,
                        r.PlayerId == myPid ? UiFactory.AccentGold : UiFactory.BorderSubtle);
                    UiFactory.SetLayout(row.gameObject, prefH: 44f);
                    UiFactory.AddHLayout(row.gameObject, 10f, 14, 6);

                    var nTxt = UiFactory.CreateText(row, $"{name} (P{r.PlayerId})", 15, UiFactory.TextLight, FontStyle.Bold);
                    UiFactory.SetLayout(nTxt.gameObject, flexW: 1f, prefH: 32f);
                    var oTxt = UiFactory.CreateText(row, outcome, 15,
                        r.Result == GameResultCode.Win ? UiFactory.AccentGold : UiFactory.TextSubtle, TextAnchor.MiddleCenter, FontStyle.Bold);
                    UiFactory.SetLayout(oTxt.gameObject, prefW: 80f, prefH: 32f);
                    string detail = $"积分 +{r.Point}  战绩:{r.Remark}";
                    if (r.Fields.Count >= 3)
                        detail = $"积分+{r.Point} | 杀:{r.Fields[0].count} 救:{r.Fields[1].count} 奖:{r.Fields[2].score}";
                    var pTxt = UiFactory.CreateText(row, detail, 12, UiFactory.AccentCeladon, TextAnchor.MiddleRight);
                    UiFactory.SetLayout(pTxt.gameObject, prefW: 240f, prefH: 32f);
                }
            }
        }

        public void HideSettlement() => SettlementRoot.gameObject.SetActive(false);

        private static string RuleDisplayName(uint rule)
        {
            switch (rule)
            {
                case 2: return "踢泡";
                case 3: return "抢包山";
                case 4: return "比武";
                case 5: return "夺宝";
                case 6: return "雕像";
                case 7: return "机械";
                case 8: return "推箱";
                case 13: return "坦克";
                default: return "竞技";
            }
        }
    }
}
