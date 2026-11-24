using UnityEngine;
using UnityEngine.UI;

namespace WaveSurvival
{
    internal static class HudWidgets
    {
        public static readonly Color Paper = Geometry.Hex(0xe5f0ed), Muted = Geometry.Hex(0x92a9a9);
        public static readonly Color Teal = Geometry.Hex(0x65e4d0), Gold = Geometry.Hex(0xffba6d), Red = Geometry.Hex(0xff695d);
        public static readonly Color Panel = new(.031f, .069f, .077f, .91f);
        public static readonly Color Glass = new(.084f, .141f, .15f, .91f);
        public static readonly Vector2 TL = new(0, 1), TR = new(1, 1), BL = new(0, 0), BR = new(1, 0), Center = new(.5f, .5f);
        static Font font;

        public static RectTransform Box(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size)
        {
            var obj = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)obj.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            return rect;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var rect = Box(parent, name, Center, Vector2.zero, Vector2.zero);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static Image ColorBlock(RectTransform rect, Color color)
        {
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        public static Image Block(Transform parent, string name, Vector2 anchor, Vector2 offset, Vector2 size, Color color)
            => ColorBlock(Box(parent, name, anchor, offset, size), color);

        public static Text Label(Transform parent, string value, int size, Vector2 anchor, Vector2 position, Vector2 box,
            Color color, TextAnchor alignment = TextAnchor.MiddleLeft, bool bold = false)
        {
            if (font == null) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var text = Box(parent, string.IsNullOrEmpty(value) ? "Readout" : value, anchor, position, box).gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            text.color = color;
            text.alignment = alignment;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        public static Button Action(Transform parent, string title, Vector2 offset, Vector2 size, Color foreground, Color background,
            UnityEngine.Events.UnityAction clicked)
        {
            var fill = Block(parent, title, TL, offset, size, background);
            fill.raycastTarget = true;
            var button = fill.gameObject.AddComponent<Button>();
            button.targetGraphic = fill;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1.17f, 1.17f, 1.17f);
            colors.pressedColor = new Color(.71f, .79f, .83f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;
            button.onClick.AddListener(clicked);
            Label(fill.transform, title, 19, Center, Vector2.zero, size, foreground, TextAnchor.MiddleCenter, true);
            return button;
        }

        public static Sprite WeaponIcon(int weapon)
        {
            var texture = Geometry.Own(new Texture2D(64, 28, TextureFormat.RGBA32, false)
            {
                name = "Procedural weapon silhouette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
            });
            var pixels = new Color32[64 * 28];
            var white = new Color32(255, 255, 255, 255);
            void Fill(int x, int y, int w, int h, int drift = 0)
            {
                for (int j = 0; j < h; j++)
                    for (int i = 0; i < w; i++)
                        pixels[(y + j) * 64 + x + i + j * drift] = white;
            }

            if (weapon == 0)
            {
                Fill(14, 15, 38, 7); Fill(15, 13, 23, 3); Fill(12, 5, 9, 11, 1);
                Fill(25, 9, 11, 2); Fill(33, 9, 3, 6); Fill(47, 21, 3, 3); Fill(15, 22, 3, 2);
            }
            else if (weapon == 1)
            {
                Fill(5, 12, 10, 6); Fill(13, 13, 34, 8); Fill(46, 16, 13, 3);
                Fill(54, 15, 7, 5); Fill(16, 5, 7, 9, 1); Fill(28, 5, 7, 9, -1);
                Fill(22, 21, 17, 2); Fill(45, 21, 3, 3);
            }
            else
            {
                Fill(3, 11, 11, 7, 1); Fill(13, 13, 17, 8); Fill(25, 16, 35, 4);
                Fill(34, 12, 15, 4); Fill(16, 6, 6, 8, 1); Fill(53, 19, 4, 3);
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Geometry.Own(Sprite.Create(texture, new(0, 0, 64, 28), Center, 64));
        }
    }
}


