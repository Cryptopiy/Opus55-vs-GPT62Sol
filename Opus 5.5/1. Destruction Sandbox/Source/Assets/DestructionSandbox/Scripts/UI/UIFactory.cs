using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using U = DestructionSandbox.GameConfig.UI;

namespace DestructionSandbox
{
    /// <summary>Small helpers to build the uGUI hierarchy from code (no prefabs, no imported sprites/fonts).</summary>
    public static class UIFactory
    {
        private static Font font;

        public static Font Font
        {
            get
            {
                if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return font;
            }
        }

        public static readonly Color PanelColor = new Color(0.05f, 0.06f, 0.08f, 0.55f);
        public static readonly Color AccentColor = new Color(1f, 0.72f, 0.3f, 1f);
        public static readonly Color TextColor = new Color(0.95f, 0.95f, 0.95f, 1f);
        public static readonly Color DimTextColor = new Color(0.75f, 0.78f, 0.82f, 1f);

        /// <summary>
        /// Screen-space overlay canvas with a Canvas Scaler (D-23: scales with resolution). Every canvas is registered
        /// with <see cref="UiVisibility"/> here, the one place that hides interface (U-06); menus the player must use
        /// pass keepVisible = true.
        /// </summary>
        public static Canvas CreateCanvas(Transform parent, string name, int sortingOrder, bool keepVisible = false)
        {
            var go = new GameObject(name);
            go.layer = 5; // UI
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = U.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = U.MatchWidthOrHeight;
            go.AddComponent<GraphicRaycaster>();
            if (Game.Ui != null) Game.Ui.Register(canvas, keepVisible);
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = position;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.layer = 5;
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(Transform parent, string name, Color color, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            RectTransform rt = Rect(parent, name, anchor, pivot, position, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static Text Label(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Color color,
            Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size, bool shadow = true)
        {
            RectTransform rt = Rect(parent, name, anchor, pivot, position, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            if (shadow)
            {
                var s = rt.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(0f, 0f, 0f, 0.8f);
                s.effectDistance = new Vector2(2f, -2f);
            }
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 position, Vector2 size, UnityAction onClick)
        {
            Image bg = Image(parent, label + "Button", new Color(0.16f, 0.18f, 0.22f, 0.95f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            bg.raycastTarget = true;
            var button = bg.gameObject.AddComponent<Button>();
            button.targetGraphic = bg;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.35f, 1.25f, 1.05f, 1f);
            colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
            colors.selectedColor = Color.white;
            colors.colorMultiplier = 1.4f;
            button.colors = colors;
            button.onClick.AddListener(onClick);
            Label(bg.transform, "Text", label, U.FontSizeMedium, TextAnchor.MiddleCenter, TextColor,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, false);
            return button;
        }

        public static Slider Slider(Transform parent, string name, Vector2 position, Vector2 size, float min, float max, float value, UnityAction<float> onChange)
        {
            RectTransform root = Rect(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);
            var bg = Image(root, "Background", new Color(0.12f, 0.13f, 0.16f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.x, size.y * 0.35f));
            bg.raycastTarget = true;

            RectTransform fillArea = Stretch(root, "FillArea");
            fillArea.offsetMin = new Vector2(0f, size.y * 0.325f);
            fillArea.offsetMax = new Vector2(0f, -size.y * 0.325f);
            RectTransform fill = Stretch(fillArea, "Fill");
            var fillImg = fill.gameObject.AddComponent<Image>();
            fillImg.color = AccentColor;
            fillImg.raycastTarget = false;

            RectTransform handleArea = Stretch(root, "HandleArea");
            handleArea.offsetMin = new Vector2(size.y * 0.3f, 0f);
            handleArea.offsetMax = new Vector2(-size.y * 0.3f, 0f);
            RectTransform handle = Rect(handleArea, "Handle", new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size.y * 0.55f, size.y * 0.8f));
            var handleImg = handle.gameObject.AddComponent<Image>();
            handleImg.color = new Color(0.95f, 0.95f, 0.95f, 1f);

            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handleImg;
            slider.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.onValueChanged.AddListener(onChange);
            return slider;
        }
    }
}
