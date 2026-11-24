using UnityEngine;
using UnityEngine.Rendering;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Storm lightning (I-19): a strike every 6-14 s while the storm weight is above 0.5. Each strike
    /// spikes the sky/sun light for 0.15 s, shows a procedural branching bolt on the horizon and schedules
    /// thunder delayed by distance / speed of sound (1-4 s). Mesh buffers are preallocated (no GC per strike).
    /// </summary>
    public sealed class LightningSystem : MonoBehaviour
    {
        public float Flash { get; private set; }
        public int StrikeCount { get; private set; }
        public float LastStrikeDistance { get; private set; }
        /// <summary>(delay seconds, distance metres) - consumed by the audio system.</summary>
        public System.Action<float, float> OnThunder;

        const int MainLevels = 7;                  // 129 points on the main channel
        const int BranchLevels = 5;                // 33 points per branch
        const int BranchCount = 3;
        const int MaxPoints = (1 << MainLevels) + 1 + BranchCount * ((1 << BranchLevels) + 1);

        WeatherSystem weather;
        Transform cam;
        Material material;
        Mesh mesh;
        MeshRenderer meshRenderer;
        DetRandom rng = new DetRandom(C.DefaultSeed, 333);
        float nextStrike = -1f;
        float strikeTime = -100f;
        bool wasActive;

        readonly Vector3[] points = new Vector3[MaxPoints];
        readonly float[] widths = new float[MaxPoints];
        readonly Vector3[] verts = new Vector3[MaxPoints * 2];
        readonly Vector2[] uvs = new Vector2[MaxPoints * 2];
        readonly int[] tris = new int[MaxPoints * 6];
        int vertCount, triCount;

        public void Init(WeatherSystem weatherSystem, Transform cameraTransform, Material boltMaterial)
        {
            weather = weatherSystem;
            cam = cameraTransform;
            material = boltMaterial;
            mesh = new Mesh { name = "LightningBolt" };
            mesh.MarkDynamic();
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
            meshRenderer.enabled = false;
        }

        void Update()
        {
            bool active = weather.Current.Lightning > 0.5f;
            if (active && !wasActive) nextStrike = rng.Range(C.LightningMinInterval, C.LightningMaxInterval);
            wasActive = active;
            if (active)
            {
                nextStrike -= Time.deltaTime;
                if (nextStrike <= 0f)
                {
                    Strike();
                    nextStrike = rng.Range(C.LightningMinInterval, C.LightningMaxInterval);
                }
            }

            float t = Time.time - strikeTime;
            // Sky/light spike lasts exactly LightningFlashSeconds with a fast flicker inside it.
            Flash = t < C.LightningFlashSeconds ? (1f - t / C.LightningFlashSeconds) * (0.75f + 0.25f * Mathf.Sin(t * 140f)) : 0f;
            bool boltVisible = t < 0.32f || warmupFrames > 0;
            if (warmupFrames > 0) { warmupFrames--; t = 0.1f; }
            meshRenderer.enabled = boltVisible;
            if (boltVisible)
                material.SetFloat(ShaderIds.Intensity, (1f - t / 0.32f) * (0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 55f))));
            Shader.SetGlobalFloat(ShaderIds.LightningFlash, Flash);
        }

        int warmupFrames;

        /// <summary>
        /// Startup shader warm-up: shows a bolt for a few frames (behind the fade-in) without flash, thunder or
        /// counting a strike, so its shaders are created before play instead of hitching at the first storm.
        /// </summary>
        public void WarmupBolt()
        {
            Vector3 origin = cam != null ? cam.position : Vector3.zero;
            Vector3 fwd = cam != null ? cam.forward : Vector3.forward;
            fwd.y = 0f;
            Vector3 bottom = new Vector3(origin.x, 0f, origin.z) + fwd.normalized * 700f;
            BuildBolt(bottom + Vector3.up * 400f, bottom, origin);
            warmupFrames = 3;
        }

        /// <summary>Forces a strike now (used by the automated acceptance test).</summary>
        public void Strike()
        {
            float delay = rng.Range(C.ThunderMinDelay, C.ThunderMaxDelay);
            float distance = delay * C.SpeedOfSound;
            float yaw = (cam != null ? cam.eulerAngles.y : 0f) + rng.Range(-55f, 55f);
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            Vector3 origin = cam != null ? cam.position : Vector3.zero;
            Vector3 bottom = new Vector3(origin.x, 0f, origin.z) + dir * distance;
            Vector3 top = bottom + Vector3.up * rng.Range(320f, 520f) + Quaternion.Euler(0f, yaw + 90f, 0f) * Vector3.forward * rng.Range(-80f, 80f);
            BuildBolt(top, bottom, origin);
            strikeTime = Time.time;
            StrikeCount++;
            LastStrikeDistance = distance;
            OnThunder?.Invoke(delay, distance);
        }

        void BuildBolt(Vector3 top, Vector3 bottom, Vector3 viewer)
        {
            vertCount = 0; triCount = 0;
            int mainCount = (1 << MainLevels) + 1;
            Displace(0, mainCount, top, bottom, 0.16f);
            for (int i = 0; i < mainCount; i++) widths[i] = Mathf.Lerp(5.5f, 3.5f, i / (float)mainCount);
            EmitRibbon(0, mainCount, viewer);

            int offset = mainCount;
            int branchPoints = (1 << BranchLevels) + 1;
            for (int b = 0; b < BranchCount; b++)
            {
                int start = rng.Range(mainCount / 8, mainCount * 2 / 3);
                Vector3 from = points[start];
                float len = (top - bottom).magnitude * rng.Range(0.2f, 0.4f);
                Vector3 side = Quaternion.Euler(0f, rng.Range(0f, 360f), 0f) * Vector3.forward;
                Vector3 to = from + (Vector3.down * 0.8f + side * 0.7f).normalized * len;
                Displace(offset, branchPoints, from, to, 0.2f);
                for (int i = 0; i < branchPoints; i++) widths[offset + i] = Mathf.Lerp(2.6f, 0.6f, i / (float)branchPoints);
                EmitRibbon(offset, branchPoints, viewer);
                offset += branchPoints;
            }

            mesh.Clear();
            mesh.SetVertices(verts, 0, vertCount);
            mesh.SetUVs(0, uvs, 0, vertCount);
            mesh.SetIndices(tris, 0, triCount, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds((top + bottom) * 0.5f, new Vector3(400f, 800f, 400f));
        }

        /// <summary>Midpoint displacement: each level offsets midpoints sideways by a fraction of the span.</summary>
        void Displace(int offset, int count, Vector3 a, Vector3 b, float roughness)
        {
            points[offset] = a;
            points[offset + count - 1] = b;
            for (int step = count - 1; step > 1; step /= 2)
            {
                for (int i = 0; i + step < count; i += step)
                {
                    Vector3 p0 = points[offset + i], p1 = points[offset + i + step];
                    float span = (p1 - p0).magnitude;
                    Vector3 jitter = new Vector3(rng.Range(-1f, 1f), rng.Range(-0.3f, 0.3f), rng.Range(-1f, 1f)) * span * roughness;
                    points[offset + i + step / 2] = (p0 + p1) * 0.5f + jitter;
                }
            }
        }

        void EmitRibbon(int offset, int count, Vector3 viewer)
        {
            int baseVert = vertCount;
            for (int i = 0; i < count; i++)
            {
                Vector3 p = points[offset + i];
                Vector3 along = (i < count - 1 ? points[offset + i + 1] - p : p - points[offset + i - 1]).normalized;
                Vector3 side = Vector3.Cross(along, (viewer - p).normalized).normalized * (widths[offset + i] * 0.5f);
                verts[vertCount] = p - side; uvs[vertCount] = new Vector2(0f, i / (float)(count - 1)); vertCount++;
                verts[vertCount] = p + side; uvs[vertCount] = new Vector2(1f, i / (float)(count - 1)); vertCount++;
            }
            for (int i = 0; i < count - 1; i++)
            {
                int v = baseVert + i * 2;
                tris[triCount++] = v; tris[triCount++] = v + 2; tris[triCount++] = v + 1;
                tris[triCount++] = v + 1; tris[triCount++] = v + 2; tris[triCount++] = v + 3;
            }
        }
    }
}
