using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Tileable hash-based gradient noise. Pure functions (no state), so texture generation can run on worker threads.
    /// Coordinates are in "texture space" [0,1); the lattice wraps every <c>period</c> cells which makes every
    /// generated texture repeat seamlessly.
    /// </summary>
    public static class Noise
    {
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float GradDot(int ix, int iy, float dx, float dy, int seed)
        {
            float a = Hash(ix, iy, seed) * 6.2831853f;
            return Mathf.Cos(a) * dx + Mathf.Sin(a) * dy;
        }

        static int Wrap(int v, int p) { int r = v % p; return r < 0 ? r + p : r; }

        /// <summary>Gradient noise in roughly [0,1], tiling every <paramref name="period"/> cells.</summary>
        public static float Gradient(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            int xa = Wrap(x0, period), xb = Wrap(x0 + 1, period);
            int ya = Wrap(y0, period), yb = Wrap(y0 + 1, period);
            float n00 = GradDot(xa, ya, fx, fy, seed);
            float n10 = GradDot(xb, ya, fx - 1f, fy, seed);
            float n01 = GradDot(xa, yb, fx, fy - 1f, seed);
            float n11 = GradDot(xb, yb, fx - 1f, fy - 1f, seed);
            float sx = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f); // quintic fade: C2-continuous
            float sy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float n = Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
            return Mathf.Clamp01(n * 0.75f + 0.5f);
        }

        /// <summary>Anisotropic tileable gradient noise (different periods along u and v, e.g. for streaks).</summary>
        public static float Gradient2(float u, float v, int periodU, int periodV, int seed)
        {
            float x = u * periodU, y = v * periodV;
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float fx = x - x0, fy = y - y0;
            int xa = Wrap(x0, periodU), xb = Wrap(x0 + 1, periodU);
            int ya = Wrap(y0, periodV), yb = Wrap(y0 + 1, periodV);
            float n00 = GradDot(xa, ya, fx, fy, seed);
            float n10 = GradDot(xb, ya, fx - 1f, fy, seed);
            float n01 = GradDot(xa, yb, fx, fy - 1f, seed);
            float n11 = GradDot(xb, yb, fx - 1f, fy - 1f, seed);
            float sx = fx * fx * fx * (fx * (fx * 6f - 15f) + 10f);
            float sy = fy * fy * fy * (fy * (fy * 6f - 15f) + 10f);
            float n = Mathf.Lerp(Mathf.Lerp(n00, n10, sx), Mathf.Lerp(n01, n11, sx), sy);
            return Mathf.Clamp01(n * 0.75f + 0.5f);
        }

        /// <summary>Fractal sum of tileable gradient noise; each octave doubles the period.</summary>
        public static float Fbm(float u, float v, int basePeriod, int octaves, int seed, float persistence = 0.5f)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int period = basePeriod;
            for (int i = 0; i < octaves; i++)
            {
                sum += Gradient(u, v, period, seed + i * 131) * amp;
                norm += amp;
                amp *= persistence;
                period *= 2;
            }
            return sum / norm;
        }

        /// <summary>Tileable cellular value: distance to the nearest random feature point (0 = on a point).</summary>
        public static float Cellular(float u, float v, int period, int seed)
        {
            float x = u * period, y = v * period;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float best = 9f;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int gx = cx + ox, gy = cy + oy;
                int hx = Wrap(gx, period), hy = Wrap(gy, period);
                float px = gx + Hash(hx, hy, seed), py = gy + Hash(hx, hy, seed + 7);
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }
    }
}
