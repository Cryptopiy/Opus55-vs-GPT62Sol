using UnityEngine;

namespace LivingIsland
{
    /// <summary>Procedurally painted weather icons (signed-distance shapes, anti-aliased) for the HUD.</summary>
    public static class IconFactory
    {
        const int Size = 64;

        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;

        static float Cloud(Vector2 p)
        {
            float d = Circle(p, new Vector2(24, 30), 11f);
            d = Mathf.Min(d, Circle(p, new Vector2(36, 35), 14f));
            d = Mathf.Min(d, Circle(p, new Vector2(47, 29), 9f));
            // Flat base.
            float baseBox = Mathf.Max(Mathf.Abs(p.x - 35f) - 17f, Mathf.Abs(p.y - 24f) - 6f);
            return Mathf.Min(d, baseBox);
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / ba.sqrMagnitude);
            return (pa - ba * h).magnitude - r;
        }

        static Color Over(Color under, Color over, float dist)
        {
            float a = Mathf.Clamp01(0.5f - dist) * over.a;
            return new Color(Mathf.Lerp(under.r, over.r, a), Mathf.Lerp(under.g, over.g, a), Mathf.Lerp(under.b, over.b, a), Mathf.Max(under.a, a));
        }

        public static Texture2D Build(WeatherState state)
        {
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Icon_" + state, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[Size * Size];
            Color sun = new Color(1f, 0.82f, 0.25f, 1f), cloudC = new Color(0.92f, 0.95f, 0.97f, 1f);
            Color dark = new Color(0.45f, 0.5f, 0.58f, 1f), rain = new Color(0.35f, 0.75f, 1f, 1f), bolt = new Color(1f, 0.88f, 0.2f, 1f);
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                Color c = new Color(0, 0, 0, 0);
                switch (state)
                {
                    case WeatherState.Clear:
                    {
                        c = Over(c, sun, Circle(p, new Vector2(32, 32), 12f));
                        float ang = Mathf.Atan2(p.y - 32f, p.x - 32f);
                        float rays = Mathf.Abs(Mathf.Repeat(ang / (Mathf.PI / 4f) + 0.5f, 1f) - 0.5f) * 8f;
                        float rr = (p - new Vector2(32, 32)).magnitude;
                        if (rr > 17f && rr < 27f) c = Over(c, sun, rays - 1.2f);
                        break;
                    }
                    case WeatherState.Cloudy:
                        c = Over(c, sun, Circle(p, new Vector2(42, 44), 10f));
                        c = Over(c, cloudC, Cloud(p - new Vector2(-2, -4)));
                        break;
                    case WeatherState.Rain:
                        c = Over(c, cloudC, Cloud(p - new Vector2(0, 6)));
                        for (int i = 0; i < 3; i++)
                            c = Over(c, rain, Segment(p, new Vector2(24 + i * 9, 20), new Vector2(20 + i * 9, 9), 1.8f));
                        break;
                    case WeatherState.Fog:
                        for (int i = 0; i < 4; i++)
                            c = Over(c, cloudC, Segment(p, new Vector2(12 + (i % 2) * 6, 16 + i * 10), new Vector2(50 - (i % 2) * 4, 16 + i * 10), 2.4f));
                        break;
                    default:
                        c = Over(c, dark, Cloud(p - new Vector2(0, 6)));
                        float b = Mathf.Min(Segment(p, new Vector2(36, 28), new Vector2(27, 16), 2.2f), Segment(p, new Vector2(27, 16), new Vector2(36, 16), 2.2f));
                        b = Mathf.Min(b, Segment(p, new Vector2(36, 16), new Vector2(28, 3), 2.2f));
                        c = Over(c, bolt, b);
                        break;
                }
                px[y * Size + x] = c;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }
    }
}
