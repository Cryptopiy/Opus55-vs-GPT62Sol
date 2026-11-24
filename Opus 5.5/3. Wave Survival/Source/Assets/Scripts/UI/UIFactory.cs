using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>uGUI construction helpers (everything is built in code; the font is Unity's built-in runtime font).</summary>
    public static class UIFactory
    {
        public static readonly Color Panel = new Color(0.02f, 0.025f, 0.03f, 0.62f);
        public static readonly Color Accent = new Color(1f, 0.62f, 0.18f, 1f);

        public static Canvas Canvas(Transform parent, string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            Canvas c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            CanvasScaler s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(GameConfig.Hud.ReferenceWidth, GameConfig.Hud.ReferenceHeight);
            s.matchWidthOrHeight = 0.5f; // stays proportionate at 1080p and 1440p
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static void EnsureEventSystem(Transform parent)
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.transform.SetParent(parent, false);
            go.AddComponent<EventSystem>();
            InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
            module.AssignDefaultActions();
        }

        public static RectTransform Rect(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchor;
            rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return rt;
        }

        public static Image Image(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, Color color, Sprite sprite = null)
        {
            RectTransform rt = Rect(parent, name, anchor, pivot, pos, size);
            Image img = rt.gameObject.AddComponent<Image>();
            img.color = color;
            img.sprite = sprite;
            img.raycastTarget = false;
            return img;
        }

        public static Text Text(Transform parent, string name, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size, int fontSize,
            TextAnchor align, Color color, FontStyle style = FontStyle.Bold)
        {
            RectTransform rt = Rect(parent, name, anchor, pivot, pos, size);
            Text t = rt.gameObject.AddComponent<Text>();
            t.font = GameAssets.Font;
            t.fontSize = fontSize;
            t.fontStyle = style;
            t.alignment = align;
            t.color = color;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            Shadow sh = rt.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0f, 0f, 0f, 0.85f);
            sh.effectDistance = new Vector2(2f, -2f);
            return t;
        }

        public static Button Button(Transform parent, string label, Vector2 pos, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            RectTransform rt = Rect(parent, label, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            Image bg = rt.gameObject.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.13f, 0.15f, 0.92f);
            Button b = rt.gameObject.AddComponent<Button>();
            ColorBlock cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(1f, 0.75f, 0.45f);
            cb.pressedColor = new Color(0.8f, 0.5f, 0.2f);
            cb.selectedColor = Color.white;
            b.colors = cb;
            b.targetGraphic = bg;
            b.onClick.AddListener(() => Game.Audio?.Play2D(SoundId.UiClick, 0.8f));
            b.onClick.AddListener(onClick);
            Outline o = rt.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(1f, 0.62f, 0.18f, 0.6f);
            o.effectDistance = new Vector2(2f, 2f);
            Text t = Text(rt, "Label", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size, 30, TextAnchor.MiddleCenter, Color.white);
            t.text = label;
            return b;
        }
    }
}
