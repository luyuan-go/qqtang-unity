using System;
using Qqtang.Core.Network;
using Qqtang.Core.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Qqtang.UnityClient.UI
{
    public sealed class UiContext
    {
        public QqtangGameApp App;
        public QqtangSessionClient Session;
        public ClientStateStore Store;
        public StaticCatalogs Catalogs;
        public ArtAssetResolver Art;
    }

    /// <summary>
    /// Daoist Ink-Jade & Xuan-Paper programmatic UGUI factory.
    /// Uses OS Chinese font fallback (Microsoft YaHei / SimHei) so all CJK labels render crisply with zero binary font assets in Git.
    /// </summary>
    public static class UiFactory
    {
        // Daoist palette
        public static readonly Color BgInkDeep = new Color(0.08f, 0.13f, 0.17f, 0.96f);
        public static readonly Color BgSlateCard = new Color(0.12f, 0.19f, 0.24f, 0.96f);
        public static readonly Color BgSlateLight = new Color(0.16f, 0.25f, 0.31f, 0.96f);
        public static readonly Color BgParchment = new Color(0.95f, 0.92f, 0.84f, 0.98f);
        public static readonly Color BgModalBackdrop = new Color(0.03f, 0.06f, 0.08f, 0.76f);

        public static readonly Color AccentGold = new Color(0.85f, 0.65f, 0.26f, 1f);
        public static readonly Color AccentCeladon = new Color(0.22f, 0.56f, 0.48f, 1f);
        public static readonly Color AccentVermilion = new Color(0.78f, 0.29f, 0.19f, 1f);
        public static readonly Color AccentDanger = new Color(0.68f, 0.18f, 0.18f, 1f);
        public static readonly Color AccentMuted = new Color(0.26f, 0.34f, 0.40f, 1f);

        public static readonly Color TextLight = new Color(0.95f, 0.94f, 0.90f, 1f);
        public static readonly Color TextSubtle = new Color(0.72f, 0.77f, 0.80f, 1f);
        public static readonly Color TextInk = new Color(0.13f, 0.17f, 0.20f, 1f);
        public static readonly Color BorderGold = new Color(0.85f, 0.65f, 0.26f, 0.45f);
        public static readonly Color BorderSubtle = new Color(1f, 1f, 1f, 0.12f);

        private static Font _cachedFont;

        public static Font DefaultFont
        {
            get
            {
                if (_cachedFont != null) return _cachedFont;
                try
                {
                    _cachedFont = Font.CreateDynamicFontFromOSFont(
                        new[] { "Microsoft YaHei", "SimHei", "PingFang SC", "Noto Sans CJK SC", "Arial" }, 16);
                }
                catch { }
                if (_cachedFont == null)
                    _cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (_cachedFont == null)
                    _cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
                return _cachedFont;
            }
        }

        public static Canvas CreateRootCanvas(string name)
        {
            EnsureEventSystem();
            var go = new GameObject(name);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;

            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
        }

        public static RectTransform CreatePanel(Transform parent, string name, Color bg, Color? border = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = bg;
            if (border.HasValue)
            {
                var outline = go.AddComponent<Outline>();
                outline.effectColor = border.Value;
                outline.effectDistance = new Vector2(1.2f, -1.2f);
            }
            return go.GetComponent<RectTransform>();
        }

        public static RectTransform CreateEmpty(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Stretch(RectTransform rt, float left = 0f, float bottom = 0f, float right = 0f, float top = 0f)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        public static Text CreateText(Transform parent, string content, int fontSize, Color? color, FontStyle style) =>
            CreateText(parent, content, fontSize, color, TextAnchor.MiddleLeft, style);

        public static Text CreateText(
            Transform parent, string content, int fontSize = 15, Color? color = null,
            TextAnchor align = TextAnchor.MiddleLeft, FontStyle style = FontStyle.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<Text>();
            txt.font = DefaultFont;
            txt.text = content ?? string.Empty;
            txt.fontSize = fontSize;
            txt.color = color ?? TextLight;
            txt.alignment = align;
            txt.fontStyle = style;
            txt.horizontalOverflow = HorizontalWrapMode.Wrap;
            txt.verticalOverflow = VerticalWrapMode.Truncate;
            txt.raycastTarget = false;
            return txt;
        }

        public static Button CreateButton(
            Transform parent, string label, Action onClick, Color? bg = null,
            Color? textColor = null, int fontSize = 14, float width = -1f, float height = 34f)
        {
            Color baseColor = bg ?? AccentCeladon;
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = baseColor;

            var outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(1f, 1f, 1f, 0.16f);
            outline.effectDistance = new Vector2(1f, -1f);

            var btn = go.GetComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.12f, 1.12f, 1.12f, 1f);
            colors.pressedColor = new Color(0.84f, 0.84f, 0.84f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.65f);
            btn.colors = colors;
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            var txt = CreateText(go.transform, label, fontSize, textColor ?? TextLight, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(txt.rectTransform, 6f, 2f, 6f, 2f);

            var le = go.AddComponent<LayoutElement>();
            if (width > 0f) { le.preferredWidth = width; le.minWidth = width; }
            if (height > 0f) { le.preferredHeight = height; le.minHeight = height; }
            return btn;
        }

        public static InputField CreateInputField(
            Transform parent, string placeholder, string initialValue = "",
            float width = -1f, float height = 34f, bool password = false)
        {
            var go = new GameObject("Input", typeof(RectTransform), typeof(Image), typeof(InputField));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = new Color(0.06f, 0.10f, 0.14f, 0.96f);

            var outline = go.AddComponent<Outline>();
            outline.effectColor = BorderGold;
            outline.effectDistance = new Vector2(1f, -1f);

            var textComp = CreateText(go.transform, initialValue, 14, TextLight, TextAnchor.MiddleLeft);
            textComp.supportRichText = false;
            Stretch(textComp.rectTransform, 8f, 3f, 8f, 3f);

            var phComp = CreateText(go.transform, placeholder, 14, new Color(0.55f, 0.62f, 0.66f, 0.8f), TextAnchor.MiddleLeft, FontStyle.Italic);
            Stretch(phComp.rectTransform, 8f, 3f, 8f, 3f);

            var input = go.GetComponent<InputField>();
            input.textComponent = textComp;
            input.placeholder = phComp;
            input.text = initialValue ?? string.Empty;
            input.caretColor = AccentGold;
            input.selectionColor = new Color(0.85f, 0.65f, 0.26f, 0.35f);
            if (password) input.contentType = InputField.ContentType.Password;

            var le = go.AddComponent<LayoutElement>();
            if (width > 0f) { le.preferredWidth = width; le.minWidth = width; }
            else le.flexibleWidth = 1f;
            if (height > 0f) { le.preferredHeight = height; le.minHeight = height; }
            return input;
        }

        public static Toggle CreateToggle(Transform parent, string label, bool initialValue, Action<bool> onChanged = null, float height = 28f)
        {
            var go = new GameObject("Toggle_" + label, typeof(RectTransform), typeof(Toggle), typeof(HorizontalLayoutGroup));
            go.transform.SetParent(parent, false);
            var h = go.GetComponent<HorizontalLayoutGroup>();
            h.spacing = 6f;
            h.childAlignment = TextAnchor.MiddleLeft;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;

            var box = CreatePanel(go.transform, "Box", new Color(0.06f, 0.10f, 0.14f, 1f), BorderGold);
            var boxLe = box.gameObject.AddComponent<LayoutElement>();
            boxLe.preferredWidth = 20f;
            boxLe.preferredHeight = 20f;

            var check = CreatePanel(box, "Check", AccentGold);
            Stretch(check, 4f, 4f, 4f, 4f);

            var txt = CreateText(go.transform, label, 14, TextLight, TextAnchor.MiddleLeft);
            var txtLe = txt.gameObject.AddComponent<LayoutElement>();
            txtLe.flexibleWidth = 1f;
            txtLe.preferredHeight = height;

            var toggle = go.GetComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = initialValue;
            if (onChanged != null) toggle.onValueChanged.AddListener(v => onChanged(v));

            var le = go.AddComponent<LayoutElement>();
            le.preferredHeight = height;
            return toggle;
        }

        public static RectTransform CreateScrollView(Transform parent, string name, out RectTransform content, Color? bg = null, float spacing = 6f)
        {
            var root = CreatePanel(parent, name, bg ?? new Color(0.07f, 0.11f, 0.15f, 0.85f), BorderSubtle);
            var scroll = root.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.scrollSensitivity = 28f;
            scroll.movementType = ScrollRect.MovementType.Clamped;

            var viewport = CreatePanel(root, "Viewport", new Color(1f, 1f, 1f, 0.01f));
            Stretch(viewport, 4f, 4f, 4f, 4f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGo.transform.SetParent(viewport, false);
            content = contentGo.GetComponent<RectTransform>();
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;

            var vlg = contentGo.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(4, 4, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var fitter = contentGo.GetComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewport;
            scroll.content = content;
            return root;
        }

        public static HorizontalLayoutGroup AddHLayout(GameObject go, float spacing = 8f, int padH = 8, int padV = 6, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var h = go.AddComponent<HorizontalLayoutGroup>();
            h.spacing = spacing;
            h.padding = new RectOffset(padH, padH, padV, padV);
            h.childAlignment = align;
            h.childControlWidth = true;
            h.childControlHeight = true;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
            return h;
        }

        public static VerticalLayoutGroup AddVLayout(GameObject go, float spacing = 8f, int padH = 10, int padV = 10)
        {
            var v = go.AddComponent<VerticalLayoutGroup>();
            v.spacing = spacing;
            v.padding = new RectOffset(padH, padH, padV, padV);
            v.childControlWidth = true;
            v.childControlHeight = true;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
            return v;
        }

        public static LayoutElement SetLayout(GameObject go, float prefW = -1f, float prefH = -1f, float flexW = -1f, float flexH = -1f)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            if (prefW >= 0f) { le.preferredWidth = prefW; le.minWidth = prefW; }
            if (prefH >= 0f) { le.preferredHeight = prefH; le.minHeight = prefH; }
            if (flexW >= 0f) le.flexibleWidth = flexW;
            if (flexH >= 0f) le.flexibleHeight = flexH;
            return le;
        }

        public static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.Destroy(parent.GetChild(i).gameObject);
        }

        public static Color TeamBadgeColor(byte teamId)
        {
            switch (teamId)
            {
                case 1: return new Color(0.82f, 0.26f, 0.24f, 1f);
                case 2: return new Color(0.22f, 0.52f, 0.85f, 1f);
                case 3: return new Color(0.22f, 0.68f, 0.42f, 1f);
                case 4: return new Color(0.86f, 0.68f, 0.18f, 1f);
                case 5: return new Color(0.65f, 0.34f, 0.78f, 1f);
                case 6: return new Color(0.18f, 0.68f, 0.72f, 1f);
                default: return new Color(0.55f, 0.58f, 0.62f, 1f);
            }
        }
    }
}
