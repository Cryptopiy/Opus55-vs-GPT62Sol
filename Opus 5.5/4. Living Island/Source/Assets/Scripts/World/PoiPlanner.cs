using System.Collections.Generic;
using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>Positions of the points of interest, chosen deterministically from the generated heights.</summary>
    public sealed class PoiPlan
    {
        public Vector3 Lighthouse; public float LighthouseYaw;
        public Vector3 Campfire;
        public Vector3 Boat; public float BoatYaw;
        public Vector3 Spawn; public float SpawnYaw;
        /// <summary>Circles (xz centre, radius in w) that vegetation and rocks must avoid.</summary>
        public readonly List<Vector3> Exclusions = new List<Vector3>();
    }

    /// <summary>
    /// Picks the lighthouse spot on the cliff plateau, the campfire on the bay beach, the rowboat in the
    /// bay's shallow water and the player spawn between fire and water; then flattens small pads under
    /// the lighthouse and the campfire so nothing floats or sinks there (I-13, I-22, I-23, I-24).
    /// </summary>
    public static class PoiPlanner
    {
        const float LighthousePadRadius = 4.4f, CampfirePadRadius = 3.8f, PadBlend = 4f;

        public static PoiPlan Plan(IslandData d)
        {
            var L = d.Layout;
            var plan = new PoiPlan();
            float r0 = C.IslandDiameter * 0.5f;

            // ---------------------------------------------------------------- lighthouse on the cliff top
            float bestScore = float.MaxValue;
            for (float a = -10f; a <= 10.01f; a += 2f)
            {
                float ang = L.CliffAngle + a * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                float edge = FindCliffEdge(d, L.Center, dir, r0);
                Vector2 p = L.Center + dir * (edge - 10f);
                float range = HeightRange(d, p, LighthousePadRadius);
                float score = range + Mathf.Abs(a) * 0.01f;
                if (score < bestScore)
                {
                    bestScore = score;
                    plan.Lighthouse = new Vector3(p.x, AverageHeight(d, p, LighthousePadRadius), p.y);
                    plan.LighthouseYaw = Mathf.Atan2(-dir.x, -dir.y) * Mathf.Rad2Deg; // door faces inland
                }
            }

            // ---------------------------------------------------------------- campfire on the bay beach
            bestScore = float.MaxValue;
            Vector2 campfire = L.Center + L.BayDir * (r0 - C.BayInlandDepth);
            for (int side = -1; side <= 1; side += 2)
            for (float a = 9f; a <= 40f; a += 1.5f)
            {
                float ang = L.BayAngle + side * a * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                // March inward from the sea until the dry-sand height is reached.
                for (float rr = 230f; rr > 20f; rr -= 0.5f)
                {
                    Vector2 p = L.Center + dir * rr;
                    float h = d.HeightAt(p.x, p.y);
                    if (h < 1.4f) continue;
                    float slope = d.SlopeDegAt(p.x, p.y);
                    float score = Mathf.Abs(a - 18f) * 0.2f + slope + HeightRange(d, p, CampfirePadRadius) * 6f;
                    if (h < 2.2f && slope < 7f && score < bestScore) { bestScore = score; campfire = p; }
                    break;
                }
            }
            plan.Campfire = new Vector3(campfire.x, d.HeightAt(campfire.x, campfire.y), campfire.y);

            // ---------------------------------------------------------------- rowboat in the shallows
            Vector3 downhill = Downhill(d, campfire);
            Vector2 boat = campfire + new Vector2(downhill.x, downhill.z) * 16f;
            float bestBoat = float.MaxValue;
            for (float rr = 6f; rr <= 34f; rr += 0.75f)
            for (float a = 0f; a < 360f; a += 7.5f)
            {
                Vector2 p = campfire + new Vector2(Mathf.Cos(a * Mathf.Deg2Rad), Mathf.Sin(a * Mathf.Deg2Rad)) * rr;
                float depth = C.SeaLevel - d.HeightAt(p.x, p.y);
                if (depth < 0.5f || depth > 0.85f) continue;
                float score = rr + Mathf.Abs(depth - 0.68f) * 10f;
                if (score < bestBoat) { bestBoat = score; boat = p; }
            }
            Vector3 boatGrad = Downhill(d, boat);
            plan.Boat = new Vector3(boat.x, C.SeaLevel, boat.y);
            plan.BoatYaw = Mathf.Atan2(-boatGrad.x, -boatGrad.z) * Mathf.Rad2Deg + 20f; // bow towards the beach

            // ---------------------------------------------------------------- spawn between fire and water
            Vector2 spawn = campfire + new Vector2(downhill.x, downhill.z) * 4.5f + new Vector2(-downhill.z, downhill.x) * 2.5f;
            float sh = d.HeightAt(spawn.x, spawn.y);
            if (sh < 0.6f) { spawn = campfire + new Vector2(-downhill.z, downhill.x) * 4.5f; sh = d.HeightAt(spawn.x, spawn.y); }
            plan.Spawn = new Vector3(spawn.x, sh + 0.05f, spawn.y);
            Vector2 toBay = (L.Center + L.BayDir * (r0 - C.BayInlandDepth * 0.4f)) - spawn;
            plan.SpawnYaw = Mathf.Atan2(toBay.x, toBay.y) * Mathf.Rad2Deg;

            plan.Exclusions.Add(new Vector3(plan.Lighthouse.x, plan.Lighthouse.z, LighthousePadRadius + 3f));
            plan.Exclusions.Add(new Vector3(plan.Campfire.x, plan.Campfire.z, CampfirePadRadius + 1.5f));
            plan.Exclusions.Add(new Vector3(plan.Boat.x, plan.Boat.z, 3.5f));
            plan.Exclusions.Add(new Vector3(plan.Spawn.x, plan.Spawn.z, 2.5f));
            return plan;
        }

        /// <summary>Flattens the lighthouse and campfire pads in the height array (then call Recompute).</summary>
        public static void FlattenPads(IslandData d, PoiPlan plan)
        {
            Flatten(d, plan.Lighthouse, LighthousePadRadius);
            Flatten(d, plan.Campfire, CampfirePadRadius);
            plan.Lighthouse.y = d.HeightAt(plan.Lighthouse.x, plan.Lighthouse.z);
            plan.Campfire.y = d.HeightAt(plan.Campfire.x, plan.Campfire.z);
        }

        static void Flatten(IslandData d, Vector3 centre, float radius)
        {
            float outer = radius + PadBlend;
            int i0 = Mathf.Max(0, (int)((centre.x - outer - d.Origin) / d.Spacing));
            int i1 = Mathf.Min(d.N - 1, (int)((centre.x + outer - d.Origin) / d.Spacing) + 1);
            int j0 = Mathf.Max(0, (int)((centre.z - outer - d.Origin) / d.Spacing));
            int j1 = Mathf.Min(d.N - 1, (int)((centre.z + outer - d.Origin) / d.Spacing) + 1);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                float x = d.Origin + i * d.Spacing, z = d.Origin + j * d.Spacing;
                float dist = Mathf.Sqrt((x - centre.x) * (x - centre.x) + (z - centre.z) * (z - centre.z));
                float w = 1f - IslandGenerator.SmoothStep(radius, outer, dist);
                int k = j * d.N + i;
                d.Heights[k] = Mathf.Lerp(d.Heights[k], centre.y, w);
            }
        }

        static float FindCliffEdge(IslandData d, Vector2 centre, Vector2 dir, float r0)
        {
            for (float rr = r0 - 30f; rr < r0 + 20f; rr += 0.25f)
            {
                Vector2 p = centre + dir * rr;
                if (d.HeightAt(p.x, p.y) < C.CliffTopHeight - 3f) return rr;
            }
            return r0;
        }

        static float HeightRange(IslandData d, Vector2 p, float radius)
        {
            float min = float.MaxValue, max = float.MinValue;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                for (float r = 0f; r <= radius; r += radius * 0.5f)
                {
                    float h = d.HeightAt(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r);
                    min = Mathf.Min(min, h); max = Mathf.Max(max, h);
                }
            }
            return max - min;
        }

        static float AverageHeight(IslandData d, Vector2 p, float radius)
        {
            float sum = 0f; int n = 0;
            for (int k = 0; k < 16; k++)
            {
                float a = k * Mathf.PI / 8f;
                for (float r = 0f; r <= radius; r += radius * 0.5f) { sum += d.HeightAt(p.x + Mathf.Cos(a) * r, p.y + Mathf.Sin(a) * r); n++; }
            }
            return sum / n;
        }

        /// <summary>Unit horizontal vector pointing downhill (towards the sea on a beach).</summary>
        public static Vector3 Downhill(IslandData d, Vector2 p)
        {
            float e = 1.5f;
            float gx = d.HeightAt(p.x + e, p.y) - d.HeightAt(p.x - e, p.y);
            float gz = d.HeightAt(p.x, p.y + e) - d.HeightAt(p.x, p.y - e);
            Vector3 g = new Vector3(-gx, 0f, -gz);
            return g.sqrMagnitude > 1e-6f ? g.normalized : Vector3.forward;
        }
    }
}
