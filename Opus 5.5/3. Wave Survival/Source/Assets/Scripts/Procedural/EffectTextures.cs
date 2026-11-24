using UnityEngine;

namespace WaveSurvival
{
    /// <summary>Procedural textures with alpha: decals, particle sprites and HUD icons.</summary>
    public static class EffectTextures
    {
        static Texture2D Clamp(TexturePainter p, string name, bool mips = true) =>
            p.ToTexture(name, false, mips, TextureWrapMode.Clamp);

        // ---------------- decals (S-12: one per surface type) ----------------

        public static Texture2D BulletHoleConcrete(int s = 128)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float ang = Mathf.Atan2(dy, dx);
                float jag = Noise.Gradient2(ang / 6.2832f + 0.5f, r, 24, 4, 701);
                float crater = Mathf.Clamp01((0.55f + jag * 0.25f - r) * 8f);
                float hole = Mathf.Clamp01((0.16f - r) * 30f);
                float crack = 0f;
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 1.047f + Noise.Hash(i, 0, 709) * 0.8f;
                    float len = 0.55f + Noise.Hash(i, 1, 709) * 0.4f;
                    crack = Mathf.Max(crack, TexturePainter.Segment(u * s, v * s, s * 0.5f, s * 0.5f,
                        s * (0.5f + Mathf.Cos(a) * len * 0.5f), s * (0.5f + Mathf.Sin(a) * len * 0.5f), 1.6f, 1f) * (1f - r * 0.9f));
                }
                float dust = Noise.Fbm(u, v, 6, 3, 719);
                float alpha = Mathf.Max(crater * (0.55f + dust * 0.3f), Mathf.Max(hole, crack * 0.8f));
                float g = Mathf.Lerp(0.32f, 0.03f, Mathf.Max(hole, crack));
                return new Color(g, g * 0.98f, g * 0.95f, alpha);
            });
            return Clamp(p, "DecalConcrete");
        }

        public static Texture2D BulletHoleMetal(int s = 128)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float hole = Mathf.Clamp01((0.18f - r) * 28f);
                float ring = Mathf.Clamp01(1f - Mathf.Abs(r - 0.32f) * 9f);
                float scorch = Mathf.Clamp01((0.75f - r) * 2.2f);
                float alpha = Mathf.Max(hole, Mathf.Max(ring * 0.9f, scorch * 0.55f));
                Color c = Color.Lerp(new Color(0.08f, 0.07f, 0.06f), new Color(0.78f, 0.76f, 0.72f), ring * (1f - hole));
                return new Color(c.r, c.g, c.b, alpha);
            });
            return Clamp(p, "DecalMetal");
        }

        public static Texture2D BloodSplat(int s = 128)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float ang = Mathf.Atan2(dy, dx) / 6.2832f + 0.5f;
                float edge = 0.42f + Noise.Gradient(ang, 0.3f, 9, 803) * 0.3f;
                float blob = Mathf.Clamp01((edge - r) * 14f);
                float drops = 0f;
                for (int i = 0; i < 7; i++)
                {
                    float a = Noise.Hash(i, 3, 811) * 6.2832f, d = 0.55f + Noise.Hash(i, 4, 811) * 0.35f;
                    drops = Mathf.Max(drops, TexturePainter.Circle(u, v, 0.5f + Mathf.Cos(a) * d * 0.5f, 0.5f + Mathf.Sin(a) * d * 0.5f,
                        0.025f + Noise.Hash(i, 5, 811) * 0.03f, 0.01f));
                }
                float n = Noise.Fbm(u, v, 5, 3, 821);
                float a2 = Mathf.Max(blob, drops) * (0.75f + n * 0.25f);
                return new Color(0.28f + n * 0.12f, 0.015f, 0.02f, a2);
            });
            return Clamp(p, "DecalBlood");
        }

        // ---------------- particle sprites ----------------

        public static Texture2D SoftDot(int s = 64)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r2 = (dx * dx + dy * dy) * 4f;
                float a = Mathf.Exp(-r2 * 4.5f) * Mathf.Clamp01((1f - r2) * 4f);
                return new Color(1f, 1f, 1f, a);
            });
            return Clamp(p, "SoftDot");
        }

        public static Texture2D Smoke(int s = 128)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float n = Noise.Fbm(u, v, 4, 4, 901);
                float a = Mathf.Clamp01((1f - r) * 1.6f) * Mathf.Clamp01(n * 1.6f - 0.25f);
                return new Color(1f, 1f, 1f, a * a);
            });
            return Clamp(p, "Smoke");
        }

        /// <summary>Muzzle flash sprite: hot core plus sharp radial spikes (S-12).</summary>
        public static Texture2D MuzzleStar(int s = 128)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float ang = Mathf.Atan2(dy, dx);
                float spikes = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 3.5f)), 18f) * Mathf.Clamp01(1f - r);
                float spikes2 = Mathf.Pow(Mathf.Abs(Mathf.Cos(ang * 2f + 0.4f)), 30f) * Mathf.Clamp01(1f - r * 0.8f);
                float core = Mathf.Exp(-r * r * 22f);
                float a = Mathf.Clamp01(core + spikes * 0.9f + spikes2 * 0.7f);
                float heat = Mathf.Clamp01(core * 1.5f);
                return new Color(1f, 0.75f + heat * 0.25f, 0.45f + heat * 0.55f, a);
            });
            return Clamp(p, "MuzzleStar");
        }

        public static Texture2D RingSprite(int s = 256)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float dx = u - 0.5f, dy = v - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.92f) * 16f) + Mathf.Clamp01((0.92f - r) * 1.2f) * 0.18f;
                return new Color(1f, 1f, 1f, Mathf.Clamp01(a) * Mathf.Clamp01((1f - r) * 30f));
            });
            return Clamp(p, "Ring");
        }

        // ---------------- HUD ----------------

        public static Texture2D Vignette(int s = 256)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float dx = (u - 0.5f) * 2f, dy = (v - 0.5f) * 2f;
                float r = Mathf.Sqrt(dx * dx * 0.8f + dy * dy * 1.1f);
                float a = Mathf.SmoothStep(0f, 1f, (r - 0.55f) / 0.75f);
                return new Color(1f, 1f, 1f, a);
            });
            return Clamp(p, "Vignette", false);
        }

        /// <summary>Arc segment used by the directional damage indicators (S-06). Points "up" in texture space.</summary>
        public static Texture2D DamageArc(int w = 128, int h = 40)
        {
            var p = new TexturePainter(w, h);
            p.Shade((u, v, x, y) =>
            {
                float cx = w * 0.5f, cy = -150f;
                float dx = x - cx, dy = y - cy;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Abs(Mathf.Atan2(dx, dy)) * Mathf.Rad2Deg;
                float band = Mathf.Clamp01(1f - Mathf.Abs(r - 168f) / 9f);
                float fade = Mathf.Clamp01((22f - ang) / 6f);
                return new Color(1f, 1f, 1f, Mathf.Clamp01(band * 1.6f) * fade);
            });
            return Clamp(p, "DamageArc", false);
        }

        public static Texture2D Circle(int s = 64)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) => new Color(1f, 1f, 1f, TexturePainter.Circle(x, y, s * 0.5f, s * 0.5f, s * 0.5f - 1.5f, 1.2f)));
            return Clamp(p, "Circle", false);
        }

        public static Texture2D HealthCross(int s = 64)
        {
            var p = new TexturePainter(s, s);
            p.Shade((u, v, x, y) =>
            {
                float a = Mathf.Max(TexturePainter.Rect(x, y, s * 0.38f, s * 0.1f, s * 0.62f, s * 0.9f, 1.2f),
                                    TexturePainter.Rect(x, y, s * 0.1f, s * 0.38f, s * 0.9f, s * 0.62f, 1.2f));
                return new Color(1f, 1f, 1f, a);
            });
            return Clamp(p, "HealthCross", false);
        }

        static readonly Vector2[] PistolGrip = { new Vector2(68, 14), new Vector2(98, 14), new Vector2(110, 60), new Vector2(78, 60) };
        static readonly Vector2[] RifleStock = { new Vector2(8, 40), new Vector2(60, 50), new Vector2(60, 68), new Vector2(8, 64) };
        static readonly Vector2[] RifleMag = { new Vector2(110, 16), new Vector2(128, 14), new Vector2(136, 50), new Vector2(118, 50) };
        static readonly Vector2[] RifleGrip = { new Vector2(74, 24), new Vector2(88, 24), new Vector2(96, 50), new Vector2(80, 50) };
        static readonly Vector2[] ShotgunStock = { new Vector2(6, 34), new Vector2(74, 52), new Vector2(74, 68), new Vector2(6, 58) };

        /// <summary>White weapon silhouettes for the HUD (tinted by the Image colour). 0 pistol, 1 rifle, 2 shotgun.</summary>
        public static Texture2D WeaponIcon(int weapon, int w = 256, int h = 96)
        {
            var p = new TexturePainter(w, h);
            p.Shade((u, v, x, y) =>
            {
                float a = 0f;
                if (weapon == 0)
                {
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 70, 58, 190, 78, 1.2f));               // slide
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 186, 63, 196, 72, 1.2f));              // muzzle
                    a = Mathf.Max(a, TexturePainter.Convex(x, y, PistolGrip, 1.2f));
                    a = Mathf.Max(a, TexturePainter.Ring(x, y, 122, 52, 10, 4, 1.2f) * (y < 56 ? 1f : 0f)); // trigger guard
                }
                else if (weapon == 1)
                {
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 56, 50, 176, 70, 1.2f));               // receiver
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 176, 56, 246, 63, 1.2f));              // barrel
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 150, 50, 200, 68, 1.2f));              // handguard
                    a = Mathf.Max(a, TexturePainter.Convex(x, y, RifleStock, 1.2f));
                    a = Mathf.Max(a, TexturePainter.Convex(x, y, RifleMag, 1.2f));
                    a = Mathf.Max(a, TexturePainter.Convex(x, y, RifleGrip, 1.2f));
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 100, 70, 140, 80, 1.2f));              // sight
                }
                else
                {
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 70, 52, 150, 70, 1.2f));               // receiver
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 150, 60, 250, 68, 1.2f));              // barrel
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 150, 50, 240, 58, 1.2f));              // magazine tube
                    a = Mathf.Max(a, TexturePainter.Rect(x, y, 170, 46, 214, 60, 1.2f));              // pump
                    a = Mathf.Max(a, TexturePainter.Convex(x, y, ShotgunStock, 1.2f));
                }
                return new Color(1f, 1f, 1f, a);
            });
            return Clamp(p, "WeaponIcon" + weapon, false);
        }
    }
}
