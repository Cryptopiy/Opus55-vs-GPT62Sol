using System.Collections.Generic;
using UnityEngine;

namespace LivingIsland
{
    public enum SurfaceType { Sand, Grass, Dirt, Rock, Water }

    /// <summary>Seed-derived layout of the island's big features (all positions in world XZ).</summary>
    public sealed class IslandLayout
    {
        public Vector2 Center;
        public float BayAngle, CliffAngle;      // radians, measured from +X towards +Z
        public Vector2 BayDir, CliffDir;        // unit vectors from the centre
        public Vector2 BayMouth, BayInner;      // capsule segment carving the bay
        public Vector2 MountainCenter;
        public Vector2 PeakPosition;
        public float PeakHeight;
    }

    /// <summary>
    /// The generated heightmap and every CPU-side query on it. HeightAt() reproduces the exact triangle
    /// interpolation of the rendered/collision mesh, so objects placed with it neither float nor sink.
    /// </summary>
    public sealed class IslandData
    {
        public readonly int Seed;
        public readonly int N = GameConfig.TerrainSamples;
        public readonly float Size = GameConfig.TerrainSize;
        public readonly float Spacing = GameConfig.TerrainSize / (GameConfig.TerrainSamples - 1);
        public readonly float Origin = -GameConfig.TerrainSize * 0.5f;
        public readonly float[] Heights;
        public readonly Vector3[] Normals;
        public readonly IslandLayout Layout;
        public readonly List<Vector2> CoastPoints = new List<Vector2>(4096);

        public IslandData(int seed, float[] heights, IslandLayout layout)
        {
            Seed = seed;
            Heights = heights;
            Layout = layout;
            Normals = new Vector3[N * N];
            Recompute();
        }

        /// <summary>Rebuilds normals and coast points after the heights were edited (POI pads).</summary>
        public void Recompute()
        {
            ComputeNormals();
            CollectCoastPoints();
        }

        public float SampleHeight(int i, int j)
        {
            i = Mathf.Clamp(i, 0, N - 1);
            j = Mathf.Clamp(j, 0, N - 1);
            return Heights[j * N + i];
        }

        void ComputeNormals()
        {
            float inv2 = 2f * Spacing;
            for (int j = 0; j < N; j++)
            for (int i = 0; i < N; i++)
            {
                // Central differences: identical formula to the GPU side (TerrainCommon.hlsl).
                float dx = SampleHeight(i - 1, j) - SampleHeight(i + 1, j);
                float dz = SampleHeight(i, j - 1) - SampleHeight(i, j + 1);
                Normals[j * N + i] = new Vector3(dx, inv2, dz).normalized;
            }
        }

        void CollectCoastPoints()
        {
            // Points where the height crosses sea level between neighbouring samples (sub-sample accurate).
            CoastPoints.Clear();
            for (int j = 0; j < N - 1; j += 1)
            for (int i = 0; i < N - 1; i += 1)
            {
                float h = Heights[j * N + i];
                float hx = Heights[j * N + i + 1];
                float hz = Heights[(j + 1) * N + i];
                if ((h >= 0f) != (hx >= 0f))
                {
                    float t = h / (h - hx);
                    CoastPoints.Add(new Vector2(Origin + (i + t) * Spacing, Origin + j * Spacing));
                }
                if ((h >= 0f) != (hz >= 0f))
                {
                    float t = h / (h - hz);
                    CoastPoints.Add(new Vector2(Origin + i * Spacing, Origin + (j + t) * Spacing));
                }
            }
        }

        /// <summary>Exact height of the triangulated terrain mesh at world XZ.</summary>
        public float HeightAt(float x, float z)
        {
            float gx = Mathf.Clamp((x - Origin) / Spacing, 0f, N - 1.0001f);
            float gz = Mathf.Clamp((z - Origin) / Spacing, 0f, N - 1.0001f);
            int i = (int)gx, j = (int)gz;
            float fx = gx - i, fz = gz - j;
            float h00 = Heights[j * N + i], h10 = Heights[j * N + i + 1];
            float h01 = Heights[(j + 1) * N + i], h11 = Heights[(j + 1) * N + i + 1];
            // Mesh cells are split along the (0,0)-(1,1) diagonal (see TerrainBuilder).
            return fx >= fz
                ? h00 + (h10 - h00) * fx + (h11 - h10) * fz
                : h00 + (h11 - h01) * fx + (h01 - h00) * fz;
        }

        public Vector3 NormalAt(float x, float z)
        {
            float gx = Mathf.Clamp((x - Origin) / Spacing, 0f, N - 1.0001f);
            float gz = Mathf.Clamp((z - Origin) / Spacing, 0f, N - 1.0001f);
            int i = (int)gx, j = (int)gz;
            float fx = gx - i, fz = gz - j;
            Vector3 a = Vector3.LerpUnclamped(Normals[j * N + i], Normals[j * N + i + 1], fx);
            Vector3 b = Vector3.LerpUnclamped(Normals[(j + 1) * N + i], Normals[(j + 1) * N + i + 1], fx);
            return Vector3.LerpUnclamped(a, b, fz).normalized;
        }

        public float SlopeDegAt(float x, float z) => Mathf.Acos(Mathf.Clamp01(NormalAt(x, z).y)) * Mathf.Rad2Deg;

        public bool InsideWorld(float x, float z) =>
            x > Origin + 1f && x < -Origin - 1f && z > Origin + 1f && z < -Origin - 1f;

        /// <summary>CPU approximation of the terrain shader's splat rules (footsteps, placement).</summary>
        public SurfaceType SurfaceAt(Vector3 p)
        {
            float h = HeightAt(p.x, p.z);
            if (h < GameConfig.SeaLevel - 0.05f) return SurfaceType.Water;
            float slope = SlopeDegAt(p.x, p.z);
            if (slope > GameConfig.RockMinSlopeDeg || h > GameConfig.SnowlessRockHeight) return SurfaceType.Rock;
            if (h < GameConfig.DrySandMaxHeight) return SurfaceType.Sand;
            if (slope > GameConfig.DirtMinSlopeDeg) return SurfaceType.Dirt;
            return SurfaceType.Grass;
        }

        /// <summary>Horizontal distance to the nearest sea-level contour point (allocation free).</summary>
        public float DistanceToShore(Vector3 p)
        {
            float best = float.MaxValue;
            for (int k = 0; k < CoastPoints.Count; k++)
            {
                Vector2 c = CoastPoints[k];
                float dx = c.x - p.x, dz = c.y - p.z;
                float d = dx * dx + dz * dz;
                if (d < best) best = d;
            }
            return best == float.MaxValue ? 9999f : Mathf.Sqrt(best);
        }
    }
}
