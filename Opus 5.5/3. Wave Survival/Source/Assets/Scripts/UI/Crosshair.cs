using UnityEngine;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>
    /// S-25 dynamic crosshair: four lines whose gap equals the current spread cone projected on screen (so it grows
    /// with rifle bloom and shrinks in ADS), hidden while aiming down sights. S-12 hit marker: white on hit, red on kill.
    /// </summary>
    public sealed class Crosshair
    {
        readonly RectTransform root;
        readonly RectTransform[] lines = new RectTransform[4];
        readonly Image[] lineImages = new Image[4];
        readonly Image dot;
        readonly RectTransform markerRoot;
        readonly Image[] marker = new Image[4];
        float markerTimer, markerDuration;
        bool markerKill;
        float gap;

        public Crosshair(RectTransform parent)
        {
            root = UIFactory.Rect(parent, "Crosshair", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            float len = GameConfig.Hud.CrosshairLength, th = GameConfig.Hud.CrosshairThickness;
            for (int i = 0; i < 4; i++)
            {
                bool vertical = i < 2;
                Image img = UIFactory.Image(root, "Line" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                    vertical ? new Vector2(th, len) : new Vector2(len, th), new Color(1f, 1f, 1f, 0.9f));
                img.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
                lines[i] = img.rectTransform;
                lineImages[i] = img;
            }
            dot = UIFactory.Image(root, "Dot", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(4f, 4f), new Color(1f, 1f, 1f, 0.9f), GameAssets.CircleSprite);

            markerRoot = UIFactory.Rect(parent, "HitMarker", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                Vector2 dir = new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad));
                Image img = UIFactory.Image(markerRoot, "M" + i, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), dir * 15f, new Vector2(3f, 13f), Color.white);
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, a - 90f);
                marker[i] = img;
            }
            markerRoot.gameObject.SetActive(false);
        }

        public void HitMarker(bool kill)
        {
            markerKill = kill || (markerKill && markerTimer > 0f);
            markerDuration = markerKill ? GameConfig.Weapons.KillMarkerTime : GameConfig.Weapons.HitMarkerTime;
            markerTimer = markerDuration;
            markerRoot.gameObject.SetActive(true);
        }

        public void Update(PlayerRig p, float dt)
        {
            WeaponController w = p.Weapons;
            float ads = p.View.AdsBlend;
            // Gap = tan(spread) / tan(vFov/2) * half the canvas height: the lines frame the real spread cone.
            float halfH = ((RectTransform)root.parent).rect.height * 0.5f;
            float target = Mathf.Tan(w.CurrentSpread * Mathf.Deg2Rad) / Mathf.Tan(p.Camera.fieldOfView * 0.5f * Mathf.Deg2Rad) * halfH;
            target = Mathf.Max(GameConfig.Hud.CrosshairMinGap, target);
            gap = Mathf.Lerp(gap, target, 1f - Mathf.Exp(-25f * dt));
            float half = GameConfig.Hud.CrosshairLength * 0.5f;
            lines[0].anchoredPosition = new Vector2(0f, gap + half);
            lines[1].anchoredPosition = new Vector2(0f, -gap - half);
            lines[2].anchoredPosition = new Vector2(gap + half, 0f);
            lines[3].anchoredPosition = new Vector2(-gap - half, 0f);
            float alpha = (1f - Mathf.Clamp01(ads * 1.6f)) * (p.IsAlive && Game.IsPlaying ? 0.9f : 0f); // S-25: hides in ADS
            Color c = w.IsReloading || w.IsSwitching ? new Color(1f, 1f, 1f, alpha * 0.4f) : new Color(1f, 1f, 1f, alpha);
            for (int i = 0; i < 4; i++) lineImages[i].color = c;
            dot.color = c;

            if (markerTimer > 0f)
            {
                markerTimer -= dt;
                float k = Mathf.Clamp01(markerTimer / markerDuration);
                Color mc = markerKill ? new Color(1f, 0.15f, 0.1f, k) : new Color(1f, 1f, 1f, k);
                float scale = markerKill ? 1.25f + (1f - k) * 0.35f : 1f + (1f - k) * 0.2f;
                markerRoot.localScale = new Vector3(scale, scale, 1f);
                for (int i = 0; i < 4; i++) marker[i].color = mc;
                if (markerTimer <= 0f)
                {
                    markerKill = false;
                    markerRoot.gameObject.SetActive(false);
                }
            }
        }
    }
}
