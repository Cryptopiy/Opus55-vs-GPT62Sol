using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LivingIsland
{
    /// <summary>Builds uGUI widgets from code with a procedural rounded-rectangle sprite (no image assets).</summary>
    public static class UIFactory
    {
        public static Font Font;
        public static Sprite Rounded;
        public static readonly Color PanelColor = new Color(0.05f, 0.07f, 0.09f, 0.8f);
        public static readonly Color Accent = new Color(0.18f, 0.84f, 0.79f, 1f);   // #2fd6c9, the shallow-water colour
        public static readonly Color TextColor = new Color(0.93f, 0.95f, 0.96f, 1f);
        public static readonly Color MutedText = new Color(0.65f, 0.72f, 0.75f, 1f);
        public static readonly Color ButtonColor = new Color(0.14f, 0.18f, 0.21f, 0.95f);

        public static void Init()
        {
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Rounded = BuildRoundedSprite(32, 10);
        }

        static Sprite BuildRoundedSprite(int size, int radius)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "UIRounded", wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Signed distance to a rounded square, anti-aliased over one pixel.
                float dx = Mathf.Max(Mathf.Abs(x + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float dy = Mathf.Max(Mathf.Abs(y + 0.5f - size * 0.5f) - (size * 0.5f - radius), 0f);
                float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                byte a = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                px[y * size + x] = new Color32(255, 255, 255, a);
            }
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                                 new Vector4(radius, radius, radius, radius));
        }

        public static RectTransform Rect(GameObject go, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            // Explicit null check: Unity objects must not be used with '??' (fake-null objects in the editor).
            var rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        /// <summary>Element anchored to the parent's top-left corner (y grows downwards).</summary>
        public static RectTransform TopLeft(GameObject go, Transform parent, float x, float y, float w, float h)
            => Rect(go, parent, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(x, -y), new Vector2(w, h));

        public static Image Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.sprite = Rounded;
            img.type = Image.Type.Sliced;
            img.color = color;
            return img;
        }

        public static Text Label(Transform parent, string text, int size, TextAnchor align, Color color, float x, float y, float w, float h)
        {
            var go = new GameObject("Label", typeof(RectTransform), typeof(Text));
            TopLeft(go, parent, x, y, w, h);
            var t = go.GetComponent<Text>();
            t.font = Font; t.fontSize = size; t.alignment = align; t.color = color; t.text = text;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string label, float x, float y, float w, float h, UnityAction onClick)
        {
            var img = Panel(parent, "Button " + label, ButtonColor);
            TopLeft(img.gameObject, parent, x, y, w, h);
            var b = img.gameObject.AddComponent<Button>();
            var colors = b.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.25f, 1.35f, 1.35f, 1f);
            colors.pressedColor = new Color(0.8f, 0.9f, 0.9f, 1f);
            colors.colorMultiplier = 1.4f;
            b.colors = colors;
            b.onClick.AddListener(onClick);
            var t = Label(img.transform, label, 18, TextAnchor.MiddleCenter, TextColor, 0, 0, w, h);
            t.name = "Text";
            return b;
        }

        public static Slider Slider(Transform parent, string label, float min, float max, float value, float x, float y, float w,
                                    UnityAction<float> onChange, out Text valueText)
        {
            Label(parent, label, 16, TextAnchor.MiddleLeft, MutedText, x, y, w * 0.6f, 22);
            valueText = Label(parent, "", 16, TextAnchor.MiddleRight, TextColor, x + w * 0.5f, y, w * 0.5f, 22);

            var root = new GameObject("Slider " + label, typeof(RectTransform));
            TopLeft(root, parent, x, y + 26, w, 18);
            var bg = Panel(root.transform, "Background", new Color(1f, 1f, 1f, 0.12f));
            Rect(bg.gameObject, root.transform, new Vector2(0, 0.3f), new Vector2(1, 0.7f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            Rect(fillArea, root.transform, new Vector2(0, 0.3f), new Vector2(1, 0.7f), new Vector2(0.5f, 0.5f), new Vector2(-4, 0), new Vector2(-8, 0));
            var fill = Panel(fillArea.transform, "Fill", Accent);
            Rect(fill.gameObject, fillArea.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(8, 0));

            var handleArea = new GameObject("Handle Area", typeof(RectTransform));
            Rect(handleArea, root.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-16, 0));
            var handle = Panel(handleArea.transform, "Handle", Color.white);
            Rect(handle.gameObject, handleArea.transform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 0));

            var s = root.AddComponent<Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = min; s.maxValue = max; s.value = value;
            s.onValueChanged.AddListener(onChange);
            return s;
        }

        public static InputField Input(Transform parent, string text, float x, float y, float w, float h)
        {
            var img = Panel(parent, "Input", new Color(1f, 1f, 1f, 0.1f));
            TopLeft(img.gameObject, parent, x, y, w, h);
            var t = Label(img.transform, text, 18, TextAnchor.MiddleLeft, TextColor, 10, 0, w - 20, h);
            t.supportRichText = false;
            var field = img.gameObject.AddComponent<InputField>();
            field.textComponent = t;
            field.contentType = InputField.ContentType.IntegerNumber;
            field.characterLimit = 9;
            field.text = text;
            field.targetGraphic = img;
            return field;
        }

        public static Toggle Toggle(Transform parent, string label, bool value, float x, float y, UnityAction<bool> onChange)
        {
            var root = new GameObject("Toggle " + label, typeof(RectTransform));
            TopLeft(root, parent, x, y, 260, 24);
            var box = Panel(root.transform, "Box", new Color(1f, 1f, 1f, 0.15f));
            TopLeft(box.gameObject, root.transform, 0, 0, 24, 24);
            var check = Panel(box.transform, "Check", Accent);
            Rect(check.gameObject, box.transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(-8, -8));
            Label(root.transform, label, 16, TextAnchor.MiddleLeft, MutedText, 34, 0, 230, 24);
            var tg = root.AddComponent<Toggle>();
            tg.targetGraphic = box;
            tg.graphic = check;
            tg.isOn = value;
            tg.onValueChanged.AddListener(onChange);
            return tg;
        }
    }
}
