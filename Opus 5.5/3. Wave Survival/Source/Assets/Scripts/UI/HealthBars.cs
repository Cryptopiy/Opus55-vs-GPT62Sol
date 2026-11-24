using UnityEngine;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>
    /// Floating enemy health bars (section 4: "shown only after being damaged", pooled). Screen-space bars follow the
    /// enemy's head; the pool is fixed, so nothing is created during play.
    /// </summary>
    public sealed class HealthBars
    {
        struct Bar
        {
            public RectTransform Root;
            public RectTransform Fill;
            public Image FillImage;
        }

        readonly Bar[] bars = new Bar[GameConfig.Pools.HealthBars];
        public int ActiveCount { get; private set; }

        public HealthBars(RectTransform parent)
        {
            float w = GameConfig.Hud.HealthBarWidth, h = GameConfig.Hud.HealthBarHeight;
            for (int i = 0; i < bars.Length; i++)
            {
                RectTransform r = UIFactory.Rect(parent, "EnemyBar" + i, Vector2.zero, new Vector2(0.5f, 0f), Vector2.zero, new Vector2(w + 4f, h + 4f));
                Image bg = r.gameObject.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.65f);
                bg.raycastTarget = false;
                Image fill = UIFactory.Image(r, "Fill", new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(w, h), new Color(0.9f, 0.2f, 0.15f));
                r.gameObject.SetActive(false);
                bars[i] = new Bar { Root = r, Fill = fill.rectTransform, FillImage = fill };
            }
        }

        public void Update(Camera cam, RectTransform canvas)
        {
            var list = Game.Enemies.Active;
            int used = 0;
            float scale = canvas.rect.height / Screen.height;
            for (int i = 0; i < list.Count && used < bars.Length; i++)
            {
                EnemyBase e = list[i];
                if (!e.HealthBarVisible || e.Type == EnemyType.Warden) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.HealthBarAnchor);
                if (sp.z <= 0.1f || sp.z > GameConfig.Hud.HealthBarMaxDistance) continue;
                ref Bar b = ref bars[used++];
                if (!b.Root.gameObject.activeSelf) b.Root.gameObject.SetActive(true);
                b.Root.anchoredPosition = new Vector2(sp.x * scale, sp.y * scale);
                float f = Mathf.Clamp01(e.Health / e.MaxHealth);
                b.Fill.sizeDelta = new Vector2(GameConfig.Hud.HealthBarWidth * f, GameConfig.Hud.HealthBarHeight);
                b.FillImage.color = Color.Lerp(new Color(1f, 0.15f, 0.1f), new Color(1f, 0.75f, 0.2f), f);
            }
            for (int i = used; i < bars.Length; i++)
                if (bars[i].Root.gameObject.activeSelf) bars[i].Root.gameObject.SetActive(false);
            ActiveCount = used;
        }
    }
}
