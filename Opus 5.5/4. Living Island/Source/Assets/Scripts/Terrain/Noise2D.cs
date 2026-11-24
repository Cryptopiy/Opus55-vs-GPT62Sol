using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// Seeded 2D gradient (Perlin-style) noise plus fBm and ridged multifractal helpers.
    /// Immutable after construction, so it is safe to sample from Parallel.For workers.
    /// </summary>
    public sealed class Noise2D
    {
        readonly int[] perm = new int[512];
        readonly Vector2[] octaveOffsets = new Vector2[12];

        // 8 unit gradients; the 1.4142 output scale below maps the result to roughly [-1, 1].
        static readonly float[] GradX = { 1f, -1f, 0f, 0f, 0.70710678f, -0.70710678f, 0.70710678f, -0.70710678f };
        static readonly float[] GradY = { 0f, 0f, 1f, -1f, 0.70710678f, 0.70710678f, -0.70710678f, -0.70710678f };

        public Noise2D(int seed)
        {
            var rng = new DetRandom(seed, 77);
            for (int i = 0; i < 256; i++) perm[i] = i;
            for (int i = 255; i > 0; i--)
            {
                int j = rng.Range(0, i + 1);
                int t = perm[i]; perm[i] = perm[j]; perm[j] = t;
            }
            for (int i = 0; i < 256; i++) perm[256 + i] = perm[i];
            // Per-octave offsets decorrelate the octaves (otherwise all octaves share the origin artefact).
            for (int i = 0; i < octaveOffsets.Length; i++)
                octaveOffsets[i] = new Vector2(rng.Range(-1000f, 1000f), rng.Range(-1000f, 1000f));
        }

        static int FastFloor(float v) { int i = (int)v; return v < i ? i - 1 : i; }
        static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        float Grad(int hash, float x, float y)
        {
            int h = hash & 7;
            return GradX[h] * x + GradY[h] * y;
        }

        /// <summary>Gradient noise in roughly [-1, 1].</summary>
        public float Sample(float x, float y)
        {
            int xi = FastFloor(x), yi = FastFloor(y);
            float xf = x - xi, yf = y - yi;
            int X = xi & 255, Y = yi & 255;
            float u = Fade(xf), v = Fade(yf);
            int aa = perm[perm[X] + Y], ab = perm[perm[X] + Y + 1];
            int ba = perm[perm[X + 1] + Y], bb = perm[perm[X + 1] + Y + 1];
            float x1 = Mathf.LerpUnclamped(Grad(aa, xf, yf), Grad(ba, xf - 1f, yf), u);
            float x2 = Mathf.LerpUnclamped(Grad(ab, xf, yf - 1f), Grad(bb, xf - 1f, yf - 1f), u);
            return Mathf.LerpUnclamped(x1, x2, v) * 1.41421356f;
        }

        /// <summary>Fractional Brownian motion, normalised to roughly [-1, 1].</summary>
        public float Fbm(float x, float y, int octaves, float lacunarity = 2f, float gain = 0.5f)
        {
            float sum = 0f, amp = 1f, freq = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                Vector2 off = octaveOffsets[o % octaveOffsets.Length];
                sum += amp * Sample(x * freq + off.x, y * freq + off.y);
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return sum / norm;
        }

        /// <summary>Ridged multifractal (Musgrave), normalised to [0, 1]; sharp crests where noise crosses 0.</summary>
        public float Ridged(float x, float y, int octaves, float lacunarity = 2.1f, float gain = 0.5f)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, weight = 1f, norm = 0f;
            for (int o = 0; o < octaves; o++)
            {
                Vector2 off = octaveOffsets[(o + 5) % octaveOffsets.Length];
                float n = 1f - Mathf.Abs(Sample(x * freq + off.x, y * freq + off.y));
                n *= n;
                n *= weight;
                // Successive octaves are weighted by the previous ridge so detail gathers on the crests.
                weight = Mathf.Clamp01(n * 2f);
                sum += n * amp;
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
            }
            return Mathf.Clamp01(sum / norm);
        }
    }
}
