using UnityEngine;
using UnityEngine.UI;
using S = DestructionSandbox.GameConfig.Score;

namespace DestructionSandbox
{
    /// <summary>
    /// D-22 score pop-ups: a fixed pool of 20 UI texts that follow a rising world point and fade out.
    /// When all 20 are visible, the oldest one is reused.
    /// </summary>
    public sealed class FloatingTextPool : MonoBehaviour
    {
        private struct Popup
        {
            public Text Text;
            public RectTransform Rect;
            public Vector3 World;
            public Vector2 Jitter;
            public float Age;
            public bool Active;
            public Color Color;
        }

        private Popup[] popups;
        private RectTransform canvasRect;
        private int next;
        private static readonly string[] pointCache = new string[256];

        public int ActiveCount { get; private set; }

        public void Build(Transform parent)
        {
            Canvas canvas = UIFactory.CreateCanvas(parent, "ScorePopups", 1);
            canvasRect = (RectTransform)canvas.transform;
            popups = new Popup[S.MaxPopups];
            for (int i = 0; i < popups.Length; i++)
            {
                Text t = UIFactory.Label(canvas.transform, "Popup" + i, "", GameConfig.UI.FontSizeMedium, TextAnchor.MiddleCenter, Color.white,
                    Vector2.zero, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 40f));
                t.enabled = false;
                popups[i] = new Popup { Text = t, Rect = t.rectTransform };
            }
        }

        private static string PointsText(int points)
        {
            if (points >= 0 && points < pointCache.Length) return pointCache[points] ?? (pointCache[points] = "+" + points);
            return "+" + points;
        }

        public void Show(Vector3 world, int points, int combo)
        {
            ref Popup p = ref popups[next];
            next = (next + 1) % popups.Length;
            if (!p.Active) ActiveCount++;
            p.Active = true;
            p.World = world;
            p.Jitter = new Vector2(Random.Range(-60f, 60f), Random.Range(-25f, 25f));   // spreads simultaneous pop-ups
            p.Age = 0f;
            p.Color = combo >= 4 ? new Color(1f, 0.45f, 0.25f) : combo >= 2 ? new Color(1f, 0.8f, 0.3f) : new Color(1f, 1f, 1f);
            p.Text.text = PointsText(points);
            p.Text.fontSize = GameConfig.UI.FontSizeSmall + combo * 2;
            p.Text.enabled = true;
        }

        public void ClearAll()
        {
            for (int i = 0; i < popups.Length; i++) { popups[i].Active = false; popups[i].Text.enabled = false; }
            ActiveCount = 0;
        }

        private void LateUpdate()
        {
            Camera cam = Game.MainCamera;
            if (cam == null) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < popups.Length; i++)
            {
                ref Popup p = ref popups[i];
                if (!p.Active) continue;
                p.Age += dt;
                if (p.Age >= S.PopupLifetime)
                {
                    p.Active = false;
                    p.Text.enabled = false;
                    ActiveCount--;
                    continue;
                }
                Vector3 world = p.World + Vector3.up * (S.PopupRiseSpeed * p.Age);
                Vector3 screen = cam.WorldToScreenPoint(world);
                if (screen.z <= 0f) { p.Text.enabled = false; continue; }
                p.Text.enabled = true;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, null, out Vector2 local);
                p.Rect.anchoredPosition = local + canvasRect.rect.size * 0.5f + p.Jitter;
                Color c = p.Color;
                c.a = 1f - Mathf.Clamp01((p.Age / S.PopupLifetime - 0.5f) * 2f);
                p.Text.color = c;
            }
        }
    }
}
