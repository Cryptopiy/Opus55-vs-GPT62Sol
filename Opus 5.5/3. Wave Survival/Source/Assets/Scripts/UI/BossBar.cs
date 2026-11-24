using UnityEngine;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>S-23 boss bar: large bar at the top of the screen with the name and three phase markers.</summary>
    public sealed class BossBar
    {
        const float Width = 900f;
        readonly RectTransform root;
        readonly Image fill, lag;
        readonly Text phaseText;
        readonly Image[] phaseLabels = new Image[3];
        readonly Text[] phaseNumerals = new Text[3];
        float lagValue = 1f;
        int lastPhase = -1;

        public BossBar(RectTransform parent)
        {
            Vector2 top = new Vector2(0.5f, 1f);
            root = UIFactory.Rect(parent, "BossBar", top, top, new Vector2(0f, -40f), new Vector2(Width + 20f, 96f));
            UIFactory.Image(root, "Bg", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(Width + 20f, 96f), UIFactory.Panel);
            Text title = UIFactory.Text(root, "Name", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(Width, 34f), 28, TextAnchor.MiddleCenter, new Color(1f, 0.35f, 0.25f));
            title.text = "THE WARDEN";
            phaseText = UIFactory.Text(root, "Phase", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-14f, -8f), new Vector2(200f, 26f), 18, TextAnchor.MiddleRight, Color.white);
            Vector2 l = new Vector2(0f, 1f);
            UIFactory.Image(root, "BarBg", l, l, new Vector2(10f, -40f), new Vector2(Width, 28f), new Color(0.08f, 0.02f, 0.02f, 0.95f));
            lag = UIFactory.Image(root, "Lag", l, l, new Vector2(10f, -40f), new Vector2(Width, 28f), new Color(1f, 0.8f, 0.5f, 0.7f));
            fill = UIFactory.Image(root, "Fill", l, l, new Vector2(10f, -40f), new Vector2(Width, 28f), new Color(0.8f, 0.12f, 0.08f));
            // Phase markers at 66% and 33% plus the phase numerals under each segment.
            float[] marks = { GameConfig.Boss.Phase2, GameConfig.Boss.Phase3 };
            foreach (float m in marks)
                UIFactory.Image(root, "Marker", l, new Vector2(0.5f, 1f), new Vector2(10f + Width * m, -36f), new Vector2(4f, 36f), new Color(1f, 0.85f, 0.4f));
            float[] centers = { (1f + GameConfig.Boss.Phase2) * 0.5f, (GameConfig.Boss.Phase2 + GameConfig.Boss.Phase3) * 0.5f, GameConfig.Boss.Phase3 * 0.5f };
            string[] numerals = { "I", "II", "III" };
            for (int i = 0; i < 3; i++)
            {
                phaseLabels[i] = UIFactory.Image(root, "PhaseBg" + i, l, new Vector2(0.5f, 1f), new Vector2(10f + Width * centers[i], -70f), new Vector2(54f, 22f), new Color(0f, 0f, 0f, 0.5f));
                phaseNumerals[i] = UIFactory.Text(phaseLabels[i].rectTransform, "N", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 22f), 18, TextAnchor.MiddleCenter, Color.white);
                phaseNumerals[i].text = numerals[i];
            }
            root.gameObject.SetActive(false);
        }

        public void Update(WardenBoss boss)
        {
            bool show = boss != null;
            if (root.gameObject.activeSelf != show) root.gameObject.SetActive(show);
            if (!show) return;
            float f = Mathf.Clamp01(boss.HealthFraction);
            fill.rectTransform.sizeDelta = new Vector2(Width * f, 28f);
            lagValue = Mathf.Max(f, Mathf.MoveTowards(lagValue, f, Time.unscaledDeltaTime * 0.25f));
            lag.rectTransform.sizeDelta = new Vector2(Width * lagValue, 28f);
            if (boss.Phase != lastPhase)
            {
                lastPhase = boss.Phase;
                phaseText.text = boss.Phase == 3 ? "PHASE III – ENRAGED" : boss.Phase == 2 ? "PHASE II" : "PHASE I";
                for (int i = 0; i < 3; i++)
                {
                    bool cur = i == boss.Phase - 1;
                    phaseLabels[i].color = cur ? new Color(0.9f, 0.3f, 0.1f, 0.9f) : new Color(0f, 0f, 0f, 0.5f);
                    phaseNumerals[i].color = cur ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                }
                fill.color = boss.Phase == 3 ? new Color(1f, 0.05f, 0.02f) : new Color(0.8f, 0.12f, 0.08f);
            }
        }
    }
}
