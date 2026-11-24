using UnityEngine;
using UnityEngine.Rendering;

namespace LivingIsland
{
    /// <summary>
    /// The single large water plane (I-04): a 400x400-vertex grid centred on the camera whose vertex
    /// spacing grows with distance (dense ~0.44 m core, ~40 m at the 2.5 km edge). The grid is moved in
    /// steps of the core spacing so near vertices always sample the same wave positions (no swimming).
    /// </summary>
    public sealed class WaterSurface : MonoBehaviour
    {
        Mesh mesh;
        float snapStep;
        Transform target;

        public void Init(Material material, Transform followTarget)
        {
            target = followTarget;
            int n = GameConfig.WaterGridVertices;
            float r = GameConfig.WaterGridHalfExtent;
            float a = GameConfig.WaterGridLinearFraction;
            float p = GameConfig.WaterGridPower;

            // Distance-based density: x = R * (a*u + (1-a)*u^p) for u in [0,1] (mirrored).
            float[] axis = new float[n];
            for (int i = 0; i < n; i++)
            {
                float u = i / (float)(n - 1) * 2f - 1f;
                float au = Mathf.Abs(u);
                axis[i] = Mathf.Sign(u) * r * (a * au + (1f - a) * Mathf.Pow(au, p));
            }
            snapStep = axis[n / 2 + 1] - axis[n / 2];

            var verts = new Vector3[n * n];
            var uvs = new Vector2[n * n];
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int idx = j * n + i;
                verts[idx] = new Vector3(axis[i], 0f, axis[j]);
                // uv.x = local grid spacing; the shader fades waves shorter than ~4 vertex spacings (anti-aliasing).
                float sx = i < n - 1 ? axis[i + 1] - axis[i] : axis[i] - axis[i - 1];
                float sz = j < n - 1 ? axis[j + 1] - axis[j] : axis[j] - axis[j - 1];
                uvs[idx] = new Vector2(Mathf.Max(sx, sz), 0f);
            }
            var tris = new int[(n - 1) * (n - 1) * 6];
            int t = 0;
            for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int v00 = j * n + i, v10 = v00 + 1, v01 = v00 + n, v11 = v01 + 1;
                tris[t++] = v00; tris[t++] = v01; tris[t++] = v11;
                tris[t++] = v00; tris[t++] = v11; tris[t++] = v10;
            }

            mesh = new Mesh { name = "WaterGrid", indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetIndices(tris, MeshTopology.Triangles, 0, false);
            mesh.SetNormals(FilledNormals(n * n));
            // Never frustum-cull the ocean: waves displace it and it always surrounds the camera.
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(r * 2f, 60f, r * 2f));

            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = gameObject.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false; // the water shader samples the shadow map itself
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            Shader.SetGlobalVector(ShaderIds.WaterGridParams, new Vector4(r, a, p, n));
        }

        static Vector3[] FilledNormals(int count)
        {
            var normals = new Vector3[count];
            for (int i = 0; i < count; i++) normals[i] = Vector3.up;
            return normals;
        }

        void LateUpdate()
        {
            if (target == null) return;
            Vector3 c = target.position;
            transform.position = new Vector3(Mathf.Round(c.x / snapStep) * snapStep, GameConfig.SeaLevel,
                                             Mathf.Round(c.z / snapStep) * snapStep);
        }
    }
}
