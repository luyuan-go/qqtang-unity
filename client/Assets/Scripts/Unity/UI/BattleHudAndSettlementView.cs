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

        private readonly Text _timerText;
        private readonly Text _mapAndArtText;
        private readonly Text _participantsText;
        private readonly Text _localStatsText;
        private readonly Text _hotbarText;
        private readonly Text _readyBannerText;

        private readonly Text _settlementTitle;
        private readonly RectTransform _scoreboardContent;

        public bool IsPauseOpen => PauseModal.gameObject.activeSelf;

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

            // Center Ready/Go banner
            _readyBannerText = UiFactory.CreateText(HudRoot, "", 42, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _readyBannerText.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            _readyBannerText.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            _readyBannerText.rectTransform.sizeDelta = new Vector2(420f, 70f);

            // Bottom HUD bar
            var botBar = UiFactory.CreatePanel(HudRoot, "BottomHudBar", new Color(0.08f, 0.13f, 0.17f, 0.86f), UiFactory.BorderGold);
            botBar.anchorMin = new Vector2(0f, 0f);
            botBar.anchorMax = new Vector2(1f, 0f);
            botBar.pivot = new Vector2(0.5f, 0f);
            botBar.sizeDelta = new Vector2(0f, 54f);
            UiFactory.AddHLayout(botBar.gameObject, 10f, 16, 6);

            _localStatsText = UiFactory.CreateText(botBar, "泡泡 2/6 | 威力 2 | 速度 4", 15, UiFactory.AccentGold, FontStyle.Bold);
            UiFactory.SetLayout(_localStatsText.gameObject, prefW: 320f, prefH: 38f);

            _hotbarText = UiFactory.CreateText(botBar, "[WASD] 移动  [Space] 放泡  [1] 扎针  [2] 护盾  [3] 遥控  [E] 表情", 13, UiFactory.TextLight);
            UiFactory.SetLayout(_hotbarText.gameObject, flexW: 1f, prefH: 38f);

            UiFactory.CreateButton(botBar, "扎针(1)", () => _ctx.App.TriggerBattleWorldItem(63), UiFactory.AccentCeladon, fontSize: 12, width: 70f, height: 32f);
            UiFactory.CreateButton(botBar, "护盾(2)", () => _ctx.App.TriggerBattleWorldItem(41), UiFactory.AccentCeladon, fontSize: 12, width: 70f, height: 32f);
            UiFactory.CreateButton(botBar, "表情(E)", () => _ctx.App.TriggerBattleEmotion(1), UiFactory.AccentMuted, fontSize: 12, width: 70f, height: 32f);

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
            UiFactory.CreateButton(pCard, "放弃本局并返回大厅", () =>
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

            _settlementTitle = UiFactory.CreateText(sCard, "对 局 结 算", 30, UiFactory.AccentGold, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.SetLayout(_settlementTitle.gameObject, prefH: 44f);

            var sbScroll = UiFactory.CreateScrollView(sCard, "Scoreboard", out _scoreboardContent, spacing: 6f);
            UiFactory.SetLayout(sbScroll.gameObject, flexW: 1f, flexH: 1f);

            UiFactory.CreateButton(sCard, "返回等待房间", () => _ctx.App.ReturnToRoomAfterSettlement(), UiFactory.AccentVermilion, fontSize: 17, height: 46f);

            HudRoot.gameObject.SetActive(false);
            SettlementRoot.gameObject.SetActive(false);
        }

        public void SetHudVisible(bool visible)
        {
            HudRoot.gameObject.SetActive(visible);
            if (!visible) PauseModal.gameObject.SetActive(false);
        }

        public void TogglePause()
        {
            if (!HudRoot.gameObject.activeSelf) return;
            PauseModal.gameObject.SetActive(!PauseModal.gameObject.activeSelf);
        }

        public void UpdateHud(Rule1BattleSimulation sim, ushort mapId)
        {
            if (sim == null) return;
            uint remainMs = sim.RoundDurationMs > sim.ElapsedMs ? sim.RoundDurationMs - sim.ElapsedMs : 0;
            uint totalSec = remainMs / 1000;
            _timerText.text = $"{totalSec / 60:00}:{totalSec % 60:00}";

            string artTag = _ctx.Art.GetResolvedSource("characters/role_0_S");
            string artLabel = artTag == "daoist" ? "道风重制" : (artTag == "original" ? "原版兜底" : "程序渲染");
            _mapAndArtText.text = $"地图: {_ctx.Catalogs.GetMapName(mapId)} ({mapId})  [{artLabel}]";

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
            _participantsText.text = summary;

            var local = sim.LocalActor;
            if (local != null)
            {
                string shield = local.IsInvulnerable(sim.ElapsedMs) ? " [护盾中]" : "";
                string kick = local.CanKickBomb ? " [可踢泡]" : "";
                _localStatsText.text = $"泡:{local.BombCapacity}/{local.MaxBombCapacity}  威:{local.BombPower}/{local.MaxBombPower}  速:{local.SpeedRate}/{local.MaxSpeedRate}{shield}{kick}";
                _hotbarText.text = $"针×{local.NeedleCount}  护盾×{local.ShieldCount}  遥控×{local.RemoteDetonatorCount}  击杀:{local.KillCount}  救援:{local.RescueCount}";
            }
        }

        public void ShowSettlement(GameOverData over)
        {
            SetHudVisible(false);
            SettlementRoot.gameObject.SetActive(true);
            UiFactory.ClearChildren(_scoreboardContent);

            var myRes = over?.Results.Find(r => r.PlayerId == _ctx.Store.PlayerId);
            if (myRes != null)
            {
                _settlementTitle.text = myRes.Result == GameResultCode.Win ? "大 获 全 胜"
                    : (myRes.Result == GameResultCode.Draw ? "平 分 秋 色" : "再 接 再 厉");
                _settlementTitle.color = myRes.Result == GameResultCode.Win ? UiFactory.AccentGold
                    : (myRes.Result == GameResultCode.Draw ? UiFactory.TextLight : UiFactory.AccentVermilion);
            }
            else
            {
                _settlementTitle.text = "对 局 结 算";
            }

            if (over != null)
            {
                foreach (var r in over.Results)
                {
                    var member = _ctx.Store.RoomMembers.Find(m => m.PlayerId == r.PlayerId);
                    string name = member != null ? member.Nickname : $"玩家 #{r.PlayerId}";
                    string outcome = r.Result == GameResultCode.Win ? "胜利" : (r.Result == GameResultCode.Draw ? "平局" : "失败");

                    var row = UiFactory.CreatePanel(_scoreboardContent, $"Res_{r.PlayerId}", UiFactory.BgSlateLight,
                        r.PlayerId == _ctx.Store.PlayerId ? UiFactory.AccentGold : UiFactory.BorderSubtle);
                    UiFactory.SetLayout(row.gameObject, prefH: 44f);
                    UiFactory.AddHLayout(row.gameObject, 10f, 14, 6);

                    var nTxt = UiFactory.CreateText(row, $"{name} (P{r.PlayerId})", 15, UiFactory.TextLight, FontStyle.Bold);
                    UiFactory.SetLayout(nTxt.gameObject, flexW: 1f, prefH: 32f);
                    var oTxt = UiFactory.CreateText(row, outcome, 15,
                        r.Result == GameResultCode.Win ? UiFactory.AccentGold : UiFactory.TextSubtle, TextAnchor.MiddleCenter, FontStyle.Bold);
                    UiFactory.SetLayout(oTxt.gameObject, prefW: 80f, prefH: 32f);
                    var pTxt = UiFactory.CreateText(row, $"积分 +{r.Point}  战绩:{r.Remark}", 13, UiFactory.AccentCeladon, TextAnchor.MiddleRight);
                    UiFactory.SetLayout(pTxt.gameObject, prefW: 180f, prefH: 32f);
                }
            }
        }

        public void HideSettlement() => SettlementRoot.gameObject.SetActive(false);
    }
}
