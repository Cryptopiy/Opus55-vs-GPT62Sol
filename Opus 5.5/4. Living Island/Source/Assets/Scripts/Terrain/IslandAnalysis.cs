using System.Collections.Generic;
using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>Measured facts about a generated island, logged as evidence for I-01/I-02.</summary>
    public struct IslandReport
    {
        public int Seed;
        public float GenerationSeconds;
        public float PeakHeight;
        public float IslandDiameter;
        public float EdgeFloorHeight;
        public float BayWidth;
        public float CliffStretch;
        public float CliffMaxSlope;
        public float BeachShareBelowMax;
        public float BeachSlopeP95;

        public override string ToString()
        {
            return string.Format(System.Globalization.CultureInfo.InvariantCulture,
                "seed {0}: generated in {1:F2} s | peak {2:F2} m | island diameter {3:F0} m | ocean floor at edge {4:F2} m | " +
                "bay width {5:F1} m | cliff stretch with slope > {6:F0} deg: {7:F0} m (max {8:F0} deg) | beach band 0-2 m: {9:F1}% below {10:F0} deg (p95 {11:F1} deg)",
                Seed, GenerationSeconds, PeakHeight, IslandDiameter, EdgeFloorHeight, BayWidth, C.CliffMinSlopeDeg,
                CliffStretch, CliffMaxSlope, BeachShareBelowMax * 100f, C.BeachMaxSlopeDeg, BeachSlopeP95);
        }
    }

    public static class IslandAnalysis
    {
        public static IslandReport Analyze(IslandData d, float generationSeconds)
        {
            var L = d.Layout;
            var r = new IslandReport { Seed = d.Seed, GenerationSeconds = generationSeconds, PeakHeight = L.PeakHeight };

            // Ocean floor along the map border.
            float edgeSum = 0f; int edgeCount = 0;
            for (int k = 0; k < d.N; k++)
            {
                edgeSum += d.SampleHeight(k, 0) + d.SampleHeight(k, d.N - 1) + d.SampleHeight(0, k) + d.SampleHeight(d.N - 1, k);
                edgeCount += 4;
            }
            r.EdgeFloorHeight = edgeSum / edgeCount;

            // Diameter: outer coastline radius per direction (bay sector excluded), summed over opposite pairs.
            float diamSum = 0f; int diamCount = 0;
            for (int a = 0; a < 90; a++)
            {
                float ang = a * Mathf.PI / 90f;
                if (Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, L.BayAngle * Mathf.Rad2Deg)) < 20f) continue;
                if (Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg + 180f, L.BayAngle * Mathf.Rad2Deg)) < 20f) continue;
                diamSum += OuterCoastRadius(d, ang) + OuterCoastRadius(d, ang + Mathf.PI);
                diamCount++;
            }
            r.IslandDiameter = diamCount > 0 ? diamSum / diamCount : 0f;

            // Bay width measured across the inlet, half way between its inner end and the original coast.
            Vector2 axisPoint = L.Center + L.BayDir * (C.IslandDiameter * 0.5f - C.BayInlandDepth * 0.5f);
            Vector2 perp = new Vector2(-L.BayDir.y, L.BayDir.x);
            r.BayWidth = WaterRun(d, axisPoint, perp) + WaterRun(d, axisPoint, -perp);

            // Cliff: max slope along radial lines through the cliff arc; longest continuous arc above 60 deg.
            float best = 0f, run = 0f, maxSlope = 0f;
            float arcRadius = C.IslandDiameter * 0.5f;
            const float stepDeg = 0.25f;
            for (float delta = -(C.CliffHalfArcDeg + C.CliffBlendDeg); delta <= C.CliffHalfArcDeg + C.CliffBlendDeg; delta += stepDeg)
            {
                float ang = L.CliffAngle + delta * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float lineMax = 0f;
                for (float rr = arcRadius - 25f; rr <= arcRadius + 15f; rr += 0.5f)
                {
                    Vector2 p = L.Center + dir * rr;
                    lineMax = Mathf.Max(lineMax, d.SlopeDegAt(p.x, p.y));
                }
                maxSlope = Mathf.Max(maxSlope, lineMax);
                if (lineMax > C.CliffMinSlopeDeg) { run += arcRadius * stepDeg * Mathf.Deg2Rad; best = Mathf.Max(best, run); }
                else run = 0f;
            }
            r.CliffStretch = best;
            r.CliffMaxSlope = maxSlope;

            // Beach band: every sample between 0 and 2 m outside the cliff arc.
            var slopes = new List<float>(20000);
            for (int j = 1; j < d.N - 1; j++)
            for (int i = 1; i < d.N - 1; i++)
            {
                float h = d.Heights[j * d.N + i];
                if (h < C.BeachMinHeight || h > C.BeachMaxHeight) continue;
                float x = d.Origin + i * d.Spacing, z = d.Origin + j * d.Spacing;
                if (IslandGenerator.CliffWeightAt(L, x, z) > 0f) continue;
                slopes.Add(Mathf.Acos(Mathf.Clamp01(d.Normals[j * d.N + i].y)) * Mathf.Rad2Deg);
            }
            if (slopes.Count > 0)
            {
                slopes.Sort();
                int below = 0;
                for (int k = 0; k < slopes.Count; k++) if (slopes[k] < C.BeachMaxSlopeDeg) below++;
                r.BeachShareBelowMax = below / (float)slopes.Count;
                r.BeachSlopeP95 = slopes[(int)(slopes.Count * 0.95f)];
            }
            return r;
        }

        static float OuterCoastRadius(IslandData d, float ang)
        {
            var L = d.Layout;
            Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
            for (float rr = 240f; rr > 0f; rr -= 0.5f)
            {
                Vector2 p = L.Center + dir * rr;
                if (d.HeightAt(p.x, p.y) >= C.SeaLevel) return rr;
            }
            return 0f;
        }

        static float WaterRun(IslandData d, Vector2 start, Vector2 dir)
        {
            for (float t = 0f; t < 120f; t += 0.25f)
            {
                Vector2 p = start + dir * t;
                if (d.HeightAt(p.x, p.y) >= C.SeaLevel) return t;
            }
            return 120f;
        }
    }
}
