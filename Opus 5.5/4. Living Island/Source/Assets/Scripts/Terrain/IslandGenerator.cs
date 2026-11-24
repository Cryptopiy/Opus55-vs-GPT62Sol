using System.Threading.Tasks;
using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Deterministic island height function (I-01, I-02):
    /// radial falloff mask with a noisy coastline -> beach/seabed profile (ocean floor -12 m),
    /// 6-octave fBm hills, ridged-noise mountain normalised to a 45 m peak, a capsule-shaped bay
    /// (60 m wide, smooth concave inlet) and a vertical cliff arc on the opposite side.
    /// </summary>
    public static class IslandGenerator
    {
        sealed class HeightFunction
        {
            readonly Noise2D hillNoise, ridgeNoise, warpNoise, coastNoise;
            readonly IslandLayout L;
            readonly Vector2 coastNoiseOffset;

            public HeightFunction(int seed, IslandLayout layout)
            {
                hillNoise = new Noise2D(seed * 31 + 1);
                ridgeNoise = new Noise2D(seed * 31 + 2);
                warpNoise = new Noise2D(seed * 31 + 3);
                coastNoise = new Noise2D(seed * 31 + 4);
                L = layout;
                var rng = new DetRandom(seed, 5);
                coastNoiseOffset = new Vector2(rng.Range(-500f, 500f), rng.Range(-500f, 500f));
            }

            /// <summary>0..1 weight of the cliff arc for a polar angle around the island centre.</summary>
            public float CliffWeight(float angle)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, L.CliffAngle * Mathf.Rad2Deg));
                return 1f - SmoothStep(C.CliffHalfArcDeg, C.CliffHalfArcDeg + C.CliffBlendDeg, delta);
            }

            public float CoastRadius(float angle, float cliffW)
            {
                // Noise sampled on a circle -> periodic, seam-free coastline wobble.
                float n = coastNoise.Fbm(Mathf.Cos(angle) * 2.2f + coastNoiseOffset.x, Mathf.Sin(angle) * 2.2f + coastNoiseOffset.y, 4);
                return C.IslandDiameter * 0.5f + C.CoastNoiseAmplitude * n * (1f - 0.75f * cliffW);
            }

            /// <summary>Signed horizontal distance inland from the coastline (bay included).</summary>
            public float InlandDistance(float x, float z, out float cliffW)
            {
                Vector2 p = new Vector2(x - L.Center.x, z - L.Center.y);
                float r = p.magnitude;
                float angle = Mathf.Atan2(p.y, p.x);
                cliffW = CliffWeight(angle);
                float dCoast = CoastRadius(angle, cliffW) - r;
                // The bay is the outside of a capsule: positive outside the capsule = land.
                float dBay = DistanceToSegment(new Vector2(x, z), L.BayMouth, L.BayInner) - C.BayWidth * 0.5f;
                return SmoothMin(dCoast, dBay, C.BaySmoothing);
            }

            static float ShoreProfile(float d)
            {
                if (d >= C.SeabedShelfDepth / C.BeachSlope)
                    return Mathf.Min(d * C.BeachSlope, C.BeachMaxHeight); // 0..2 m beach band at 5.7 deg
                float shelfD = C.SeabedShelfDepth / C.BeachSlope;
                float t = Mathf.Clamp01((shelfD - d) / (C.SeabedDropDistance + shelfD));
                return Mathf.Lerp(C.SeabedShelfDepth, C.OceanFloorHeight, SmoothStep(0f, 1f, t));
            }

            static float CliffProfile(float d)
            {
                if (d >= C.CliffFaceTop) return C.CliffTopHeight + 0.12f * (d - C.CliffFaceTop);
                if (d >= C.CliffFaceBottom)
                {
                    float t = (d - C.CliffFaceBottom) / (C.CliffFaceTop - C.CliffFaceBottom);
                    return Mathf.Lerp(C.CliffFootHeight, C.CliffTopHeight, SmootherStep(t)); // > 60 deg face
                }
                float s = Mathf.Clamp01((C.CliffFaceBottom - d) / 22f);
                return Mathf.Lerp(C.CliffFootHeight, C.OceanFloorHeight, SmoothStep(0f, 1f, s));
            }

            public float Evaluate(float x, float z)
            {
                float d = InlandDistance(x, z, out float cliffW);

                // Domain warp makes hills and ridges organic instead of grid-aligned blobs.
                float wx = warpNoise.Fbm(x * 0.012f, z * 0.012f, 2) * C.WarpAmplitude;
                float wz = warpNoise.Fbm(x * 0.012f + 41.3f, z * 0.012f - 17.9f, 2) * C.WarpAmplitude;
                float qx = x + wx, qz = z + wz;

                float hShore = ShoreProfile(d);
                float rise = d > C.BeachBandWidth
                    ? C.InlandRiseHeight * (1f - Mathf.Exp(-(d - C.BeachBandWidth) / C.InlandRiseDistance))
                    : 0f;
                float wInland = SmoothStep(C.HillsStart, C.HillsFull, d);

                float hillN = hillNoise.Fbm(qx * C.HillFrequency, qz * C.HillFrequency, C.HillFbmOctaves) * 0.5f + 0.5f;
                float hills = C.HillAmplitude * Mathf.Pow(Mathf.Clamp01(hillN), 1.4f);

                float t = Vector2.Distance(new Vector2(qx, qz), L.MountainCenter) / C.MountainRadius;
                float bell = t < 1f ? (1f - t * t) * (1f - t * t) : 0f;
                float ridged = ridgeNoise.Ridged(qx * 0.018f, qz * 0.018f, C.RidgedOctaves);
                float mountain = C.MountainBaseHeight * Mathf.Pow(bell, 1.1f) * (0.45f + 0.55f * ridged);

                float hLand = hShore + rise + wInland * (hills + mountain);
                if (cliffW <= 0f) return hLand;

                // Cliff arc: vertical face down to the sea, plateau smoothly merging into the hills.
                float hCliff = CliffProfile(d);
                if (d >= C.CliffFaceTop) hCliff = SmoothMax(hCliff, hLand, 5f);
                return Mathf.Lerp(hLand, hCliff, cliffW);
            }
        }

        public static IslandData Generate(int seed)
        {
            IslandLayout layout = BuildLayout(seed);
            var fn = new HeightFunction(seed, layout);
            int n = C.TerrainSamples;
            float spacing = C.TerrainSize / (n - 1);
            float origin = -C.TerrainSize * 0.5f;
            float[] heights = new float[n * n];

            Parallel.For(0, n, j =>
            {
                float z = origin + j * spacing;
                for (int i = 0; i < n; i++)
                    heights[j * n + i] = fn.Evaluate(origin + i * spacing, z);
            });

            // Normalise the mountain: everything above the knee is rescaled so the peak is exactly 45 m,
            // leaving beaches, hills and the cliff untouched.
            float max = float.MinValue; int maxIdx = 0;
            for (int k = 0; k < heights.Length; k++) if (heights[k] > max) { max = heights[k]; maxIdx = k; }
            if (max > C.MountainKnee + 0.01f)
            {
                float scale = (C.MountainPeakHeight - C.MountainKnee) / (max - C.MountainKnee);
                for (int k = 0; k < heights.Length; k++)
                    if (heights[k] > C.MountainKnee) heights[k] = C.MountainKnee + (heights[k] - C.MountainKnee) * scale;
            }
            layout.PeakHeight = heights[maxIdx];
            layout.PeakPosition = new Vector2(origin + (maxIdx % n) * spacing, origin + (maxIdx / n) * spacing);
            return new IslandData(seed, heights, layout);
        }

        static IslandLayout BuildLayout(int seed)
        {
            var rng = new DetRandom(seed, 1);
            var L = new IslandLayout();
            L.Center = new Vector2(rng.Range(-6f, 6f), rng.Range(-6f, 6f));
            L.BayAngle = rng.Range(0f, Mathf.PI * 2f);
            L.CliffAngle = L.BayAngle + Mathf.PI;
            L.BayDir = new Vector2(Mathf.Cos(L.BayAngle), Mathf.Sin(L.BayAngle));
            L.CliffDir = -L.BayDir;
            Vector2 side = new Vector2(-L.CliffDir.y, L.CliffDir.x);
            L.MountainCenter = L.Center + L.CliffDir * C.MountainOffset + side * rng.Range(-12f, 12f);
            float coastR = C.IslandDiameter * 0.5f;
            L.BayMouth = L.Center + L.BayDir * (coastR + C.BayMouthOffshore);
            L.BayInner = L.Center + L.BayDir * (coastR - C.BayInlandDepth);
            return L;
        }

        /// <summary>Cliff-arc weight at a world position (used by the analysis and POI placement).</summary>
        public static float CliffWeightAt(IslandLayout L, float x, float z)
        {
            float angle = Mathf.Atan2(z - L.Center.y, x - L.Center.x);
            float delta = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg, L.CliffAngle * Mathf.Rad2Deg));
            return 1f - SmoothStep(C.CliffHalfArcDeg, C.CliffHalfArcDeg + C.CliffBlendDeg, delta);
        }

        public static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float SmootherStep(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * t * (t * (t * 6f - 15f) + 10f);
        }

        static float SmoothMin(float a, float b, float k)
        {
            float h = Mathf.Max(k - Mathf.Abs(a - b), 0f) / k;
            return Mathf.Min(a, b) - h * h * k * 0.25f;
        }

        static float SmoothMax(float a, float b, float k) => -SmoothMin(-a, -b, k);

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
    }
}
