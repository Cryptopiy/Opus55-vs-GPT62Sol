using UnityEngine;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>
    /// S-06 directional damage indicators: pooled red arcs around the crosshair pointing toward the attacker's
    /// position, re-aimed every frame as the player turns, fading out over 1.6 s.
    /// </summary>
    public sealed class DamageIndicators
    {
        struct Indicator
        {
            public RectTransform Pivot;
            public Image Arc;
            public Vector3 Source;
            public float Timer;
        }

        readonly Indicator[] items = new Indicator[GameConfig.Pools.DamageIndicators];
        int next;

        public DamageIndicators(RectTransform parent)
        {
            for (int i = 0; i < items.Length; i++)
            {
                RectTransform pivot = UIFactory.Rect(parent, "DamageDir" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
                Image arc = UIFactory.Image(pivot, "Arc", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, GameConfig.Hud.IndicatorRadius),
                    new Vector2(190f, 60f), new Color(1f, 0.12f, 0.08f, 0f), GameAssets.DamageArcSprite);
                pivot.gameObject.SetActive(false);
                items[i] = new Indicator { Pivot = pivot, Arc = arc };
            }
        }

        public void Show(Vector3 source)
        {
            // Re-use an indicator that already tracks (almost) the same source, else take the next slot.
            int idx = -1;
            for (int i = 0; i < items.Length; i++)
                if (items[i].Timer > 0f && (items[i].Source - source).sqrMagnitude < 1f) { idx = i; break; }
            if (idx < 0)
            {
                idx = next;
                next = (next + 1) % items.Length;
            }
            items[idx].Source = source;
            items[idx].Timer = GameConfig.Hud.IndicatorTime;
            items[idx].Pivot.gameObject.SetActive(true);
        }

        public void Update(PlayerRig p, float dt)
        {
            Transform cam = p.Camera.transform;
            for (int i = 0; i < items.Length; i++)
            {
                ref Indicator it = ref items[i];
                if (it.Timer <= 0f) continue;
                it.Timer -= dt;
                if (it.Timer <= 0f)
                {
                    it.Pivot.gameObject.SetActive(false);
                    continue;
                }
                // Angle of the attacker relative to the view direction on the ground plane (0 = straight ahead = up).
                float angle = MathUtil.SignedAngleY(cam.forward, it.Source - cam.position);
                it.Pivot.localRotation = Quaternion.Euler(0f, 0f, -angle);
                float k = it.Timer / GameConfig.Hud.IndicatorTime;
                it.Arc.color = new Color(1f, 0.12f, 0.08f, Mathf.Clamp01(k * 1.4f) * 0.9f);
            }
        }
    }
}
