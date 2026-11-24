using System.Collections.Generic;
using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// Procedural meshes for bushes (leaf cards on an ellipsoid shell, spherical normals for soft
    /// shading, wind weights in vertex colour) and flower patches (billboarded coloured dots on stems).
    /// </summary>
    public static class FoliageFactory
    {
        public static Mesh BuildBush(int variant)
        {
            var rng = new DetRandom(1234 + variant, 17);
            var verts = new List<Vector3>(); var normals = new List<Vector3>();
            var uvs = new List<Vector2>(); var colors = new List<Color>(); var tris = new List<int>();
            float radius = 0.75f + variant * 0.12f;
            float height = radius * (0.95f + 0.1f * variant);
            Vector3 centre = new Vector3(0f, height * 0.42f, 0f);
            int cards = 46 + variant * 6;
            for (int c = 0; c < cards; c++)
            {
                // Fibonacci spiral over the upper ~70% of a sphere -> even coverage without clumps.
                float t = (c + 0.5f) / cards;
                float y = 1f - t * 1.45f;
                float r = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
                float phi = c * 2.39996323f + rng.Range(-0.3f, 0.3f);
                Vector3 dir = new Vector3(Mathf.Cos(phi) * r, y, Mathf.Sin(phi) * r).normalized;
                float shell = rng.Range(0.55f, 1f);
                Vector3 p = centre + Vector3.Scale(dir, new Vector3(radius, height * 0.55f, radius)) * shell;
                p.y = Mathf.Max(p.y, 0.12f);
                float size = rng.Range(0.42f, 0.62f) * (0.8f + 0.25f * variant);
                Quaternion rot = Quaternion.LookRotation(dir) * Quaternion.Euler(0f, 0f, rng.Range(0f, 360f));
                Vector3 right = rot * Vector3.right * size * 0.5f, up = rot * Vector3.up * size * 0.5f;
                float phase = rng.Value;
                int b = verts.Count;
                Vector3[] corners = { p - right - up, p - right + up, p + right + up, p + right - up };
                Vector2[] cuv = { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
                for (int k = 0; k < 4; k++)
                {
                    verts.Add(corners[k]);
                    // Spherical normals around the bush centre give the soft "volume" look of a canopy.
                    normals.Add((corners[k] - centre * 0.6f).normalized);
                    uvs.Add(cuv[k]);
                    float sway = Mathf.Clamp01(corners[k].y / (height * 1.2f));
                    colors.Add(new Color(sway, sway, phase, shell));
                }
                tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
            }
            var mesh = new Mesh { name = "Bush_" + variant };
            mesh.SetVertices(verts); mesh.SetNormals(normals); mesh.SetUVs(0, uvs); mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        static readonly Color[] FlowerPalette =
        {
            new Color(0.95f, 0.18f, 0.22f), new Color(1f, 0.82f, 0.15f), new Color(0.98f, 0.97f, 0.92f),
            new Color(0.62f, 0.32f, 0.95f), new Color(1f, 0.45f, 0.72f), new Color(1f, 0.55f, 0.12f)
        };

        /// <summary>
        /// One cluster of flowers as a single mesh in world space (positions sit exactly on the terrain).
        /// Per vertex: position = flower base, uv0 = billboard corner, uv1 = (part, height, size, phase).
        /// </summary>
        public static Mesh BuildFlowerCluster(IslandData data, Vector2 centre, int count, float clusterRadius, int seed)
        {
            var rng = new DetRandom(seed, 61);
            Color baseColor = FlowerPalette[rng.Range(0, FlowerPalette.Length)];
            var verts = new List<Vector3>(count * 8); var uv0 = new List<Vector2>(count * 8);
            var uv1 = new List<Vector4>(count * 8); var colors = new List<Color>(count * 8); var tris = new List<int>(count * 12);
            Vector2[] corners = { new Vector2(-1, -1), new Vector2(-1, 1), new Vector2(1, 1), new Vector2(1, -1) };
            for (int f = 0; f < count; f++)
            {
                // Denser in the middle of the patch (sqrt would be uniform; the power clusters them).
                float a = rng.Range(0f, Mathf.PI * 2f), r = Mathf.Pow(rng.Value, 0.75f) * clusterRadius;
                float x = centre.x + Mathf.Cos(a) * r, z = centre.y + Mathf.Sin(a) * r;
                Vector3 basePos = new Vector3(x, data.HeightAt(x, z), z);
                float h = rng.Range(0.16f, 0.36f), size = rng.Range(0.035f, 0.06f), phase = rng.Value;
                Color petal = Color.Lerp(baseColor, FlowerPalette[rng.Range(0, FlowerPalette.Length)], rng.Value < 0.15f ? 1f : 0.1f);
                for (int part = 0; part < 2; part++)
                {
                    int b = verts.Count;
                    for (int k = 0; k < 4; k++)
                    {
                        verts.Add(basePos);
                        uv0.Add(corners[k]);
                        uv1.Add(new Vector4(part, h, size, phase));
                        colors.Add(part == 0 ? new Color(0.22f, 0.42f, 0.12f) : petal);
                    }
                    tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
                    tris.Add(b); tris.Add(b + 2); tris.Add(b + 3);
                }
            }
            var mesh = new Mesh { name = "FlowerCluster" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uv0); mesh.SetUVs(1, uv1); mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            Bounds bounds = new Bounds(new Vector3(centre.x, data.HeightAt(centre.x, centre.y), centre.y), Vector3.one * (clusterRadius * 2f + 1f));
            mesh.bounds = bounds; // vertices are collapsed bases; the shader expands them
            return mesh;
        }
    }
}
