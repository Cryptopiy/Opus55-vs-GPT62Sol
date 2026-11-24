using System;
using System.Threading.Tasks;
using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// CPU canvas used to generate every texture of the game at runtime (no image files exist in the project).
    /// Holds an albedo/alpha buffer plus a height buffer that is converted into a tangent-space normal map.
    /// </summary>
    public sealed class TexturePainter
    {
        public readonly int W, H;
        public readonly Color[] Px;
        public readonly float[] Height;

        public TexturePainter(int w, int h)
        {
            W = w; H = h;
            Px = new Color[w * h];
            Height = new float[w * h];
        }

        /// <summary>Runs <paramref name="pixel"/> for every pixel in parallel rows. u,v are pixel centres in [0,1).</summary>
        public void Shade(Func<float, float, int, int, Color> pixel)
        {
            Parallel.For(0, H, y =>
            {
                float v = (y + 0.5f) / H;
                int row = y * W;
                for (int x = 0; x < W; x++) Px[row + x] = pixel((x + 0.5f) / W, v, x, y);
            });
        }

        /// <summary>Like Shade but also writes the height buffer (out parameter of the shader delegate).</summary>
        public void ShadeWithHeight(PixelWithHeight pixel)
        {
            Parallel.For(0, H, y =>
            {
                float v = (y + 0.5f) / H;
                int row = y * W;
                for (int x = 0; x < W; x++)
                {
                    Px[row + x] = pixel((x + 0.5f) / W, v, x, y, out float h);
                    Height[row + x] = h;
                }
            });
        }

        public delegate Color PixelWithHeight(float u, float v, int x, int y, out float height);

        public Texture2D ToTexture(string name, bool linear = false, bool mips = true, TextureWrapMode wrap = TextureWrapMode.Repeat)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, mips, linear)
            {
                name = name,
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            tex.SetPixels(Px);
            tex.Apply(mips, true); // non-readable after upload: frees the CPU copy
            return AssetTracker.Track(tex);
        }

        /// <summary>
        /// Builds a normal map from the height buffer (wrapping at the borders so it tiles). Encoded for Unity's
        /// UnpackNormalmapRGorAG: R = 1, G = y, A = x, which decodes correctly without texture import settings.
        /// </summary>
        public Texture2D NormalMap(string name, float strength)
        {
            var data = new Color[W * H];
            Parallel.For(0, H, y =>
            {
                int yu = (y + 1) % H, yd = (y - 1 + H) % H;
                for (int x = 0; x < W; x++)
                {
                    int xr = (x + 1) % W, xl = (x - 1 + W) % W;
                    float dx = (Height[y * W + xl] - Height[y * W + xr]) * strength;
                    float dy = (Height[yd * W + x] - Height[yu * W + x]) * strength;
                    Vector3 n = new Vector3(dx, dy, 1f).normalized;
                    data[y * W + x] = new Color(1f, n.y * 0.5f + 0.5f, n.z, n.x * 0.5f + 0.5f);
                }
            });
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true, true)
            {
                name = name,
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            tex.SetPixels(data);
            tex.Apply(true, true);
            return AssetTracker.Track(tex);
        }

        // ---------- anti-aliased shape coverage helpers (return 0..1) ----------

        public static float Circle(float px, float py, float cx, float cy, float r, float soft = 1f)
        {
            float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01((r - d) / soft + 0.5f);
        }

        public static float Ring(float px, float py, float cx, float cy, float r, float thickness, float soft = 1f)
        {
            float d = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
            return Mathf.Clamp01((thickness * 0.5f - Mathf.Abs(d - r)) / soft + 0.5f);
        }

        public static float Rect(float px, float py, float x0, float y0, float x1, float y1, float soft = 1f)
        {
            float dx = Mathf.Min(px - x0, x1 - px);
            float dy = Mathf.Min(py - y0, y1 - py);
            return Mathf.Clamp01(Mathf.Min(dx, dy) / soft + 0.5f);
        }

        /// <summary>Coverage of a convex polygon (either winding order).</summary>
        public static float Convex(float px, float py, Vector2[] poly, float soft = 1f)
        {
            // Winding sign so that "inside" is always the positive side of every edge.
            float area = 0f;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                area += a.x * b.y - b.x * a.y;
            }
            float sign = area >= 0f ? 1f : -1f;
            float minDist = float.MaxValue;
            for (int i = 0; i < poly.Length; i++)
            {
                Vector2 a = poly[i], b = poly[(i + 1) % poly.Length];
                Vector2 e = (b - a).normalized;
                float dist = ((px - a.x) * -e.y + (py - a.y) * e.x) * sign;
                if (dist < minDist) minDist = dist;
            }
            return Mathf.Clamp01(minDist / soft + 0.5f);
        }

        /// <summary>Coverage of a thick line segment with round caps.</summary>
        public static float Segment(float px, float py, float ax, float ay, float bx, float by, float thickness, float soft = 1f)
        {
            float vx = bx - ax, vy = by - ay;
            float t = Mathf.Clamp01(((px - ax) * vx + (py - ay) * vy) / Mathf.Max(1e-5f, vx * vx + vy * vy));
            float cx = ax + vx * t - px, cy = ay + vy * t - py;
            float d = Mathf.Sqrt(cx * cx + cy * cy);
            return Mathf.Clamp01((thickness * 0.5f - d) / soft + 0.5f);
        }
    }
}
