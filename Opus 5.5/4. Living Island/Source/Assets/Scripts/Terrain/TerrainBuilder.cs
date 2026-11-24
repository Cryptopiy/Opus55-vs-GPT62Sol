using UnityEngine;
using UnityEngine.Rendering;

namespace LivingIsland
{
    /// <summary>
    /// Turns <see cref="IslandData"/> into the single 512x512-sample terrain mesh (render + MeshCollider)
    /// and uploads the heightmap to the GPU (grass placement, water shore damping, caustics).
    /// Buffers are reused between regenerations to keep re-seeding cheap.
    /// </summary>
    public sealed class TerrainBuilder
    {
        public GameObject Root { get; private set; }
        public Texture2D HeightTexture { get; private set; }

        readonly Mesh mesh;
        readonly MeshFilter filter;
        readonly MeshCollider meshCollider;
        readonly Vector3[] vertices;
        readonly int[] indices;
        readonly float[] heightPixels;

        public TerrainBuilder(Material material, int layer)
        {
            int n = GameConfig.TerrainSamples;
            Root = new GameObject("Island Terrain") { layer = layer };
            filter = Root.AddComponent<MeshFilter>();
            var renderer = Root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            meshCollider = Root.AddComponent<MeshCollider>();

            mesh = new Mesh { name = "IslandTerrainMesh", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            vertices = new Vector3[n * n];
            indices = new int[(n - 1) * (n - 1) * 6];
            heightPixels = new float[n * n];

            // Topology never changes: two clockwise (seen from above) triangles per cell, split along
            // the (i,j)-(i+1,j+1) diagonal, which IslandData.HeightAt() reproduces exactly.
            int t = 0;
            for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int v00 = j * n + i, v10 = v00 + 1, v01 = v00 + n, v11 = v01 + 1;
                indices[t++] = v00; indices[t++] = v01; indices[t++] = v11;
                indices[t++] = v00; indices[t++] = v11; indices[t++] = v10;
            }

            HeightTexture = new Texture2D(n, n, TextureFormat.RFloat, false, true)
            {
                name = "IslandHeightTex",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };
        }

        public void Build(IslandData data)
        {
            int n = data.N;
            for (int j = 0; j < n; j++)
            {
                float z = data.Origin + j * data.Spacing;
                for (int i = 0; i < n; i++)
                {
                    int k = j * n + i;
                    vertices[k] = new Vector3(data.Origin + i * data.Spacing, data.Heights[k], z);
                    heightPixels[k] = data.Heights[k];
                }
            }

            mesh.Clear();
            mesh.SetVertices(vertices);
            mesh.SetNormals(data.Normals);
            mesh.SetIndices(indices, MeshTopology.Triangles, 0, false);
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;

            // Re-assigning forces PhysX to cook the new triangles.
            meshCollider.sharedMesh = null;
            meshCollider.sharedMesh = mesh;

            HeightTexture.SetPixelData(heightPixels, 0);
            HeightTexture.Apply(false, false);
            Shader.SetGlobalTexture(ShaderIds.IslandHeightTex, HeightTexture);
            Shader.SetGlobalVector(ShaderIds.IslandHeightParams, new Vector4(data.Origin, data.Spacing, n, 1f / data.Spacing));
        }
    }
}
