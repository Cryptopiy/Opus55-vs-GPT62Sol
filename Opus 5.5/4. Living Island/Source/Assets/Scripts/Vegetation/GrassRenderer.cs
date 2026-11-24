using UnityEngine;
using UnityEngine.Rendering;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// GPU-instanced grass (I-10, I-11, I-30): GrassPlacement.compute fills an instance buffer around the
    /// camera each frame (density falls with distance, nothing beyond 80 m, count steered to the quality
    /// budget 20k / 50k / 100k) and a single Graphics.RenderMeshIndirect call draws every blade.
    /// The drawn count is read back asynchronously for the F1 overlay (no CPU stall, no GC).
    /// </summary>
    public sealed class GrassRenderer : MonoBehaviour
    {
        public int Budget { get; private set; }
        public int Capacity { get; private set; }
        public int DrawnBlades { get; private set; }
        public bool Visible { get; set; } = true;

        const int InstanceStride = 32;
        ComputeShader compute;
        Material material;
        Camera cam;
        Mesh blade;
        GraphicsBuffer instances, counter, state, args;
        int kMeasure, kFinalize, kPlace, kArgs;
        int gridDim;
        RenderParams rp;
        float readbackTimer;
        System.Action<AsyncGPUReadbackRequest> onReadback;
        readonly Vector4[] exclusions = new Vector4[4];
        static readonly int ExclusionsId = Shader.PropertyToID("_GrassExclusions");
        static readonly int SeedId = Shader.PropertyToID("_GrassSeed");

        public void Init(ComputeShader cs, Material mat, Camera camera)
        {
            compute = cs;
            material = mat;
            cam = camera;
            blade = BuildBlade();
            kMeasure = compute.FindKernel("CSMeasure");
            kFinalize = compute.FindKernel("CSFinalize");
            kPlace = compute.FindKernel("CSPlace");
            kArgs = compute.FindKernel("CSArgs");
            gridDim = Mathf.CeilToInt(2f * C.GrassCullDistance / C.GrassCellSize / 16f) * 16;

            counter = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 4, sizeof(uint));
            counter.SetData(new uint[4]);
            state = new GraphicsBuffer(GraphicsBuffer.Target.Structured, 1, sizeof(float));
            state.SetData(new float[1]);
            args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 5, sizeof(uint));
            args.SetData(new uint[] { blade.GetIndexCount(0), 0, 0, 0, 0 });
            onReadback = OnReadback;
            SetBudget(C.GrassBudgetHigh);

            rp = new RenderParams(material)
            {
                worldBounds = new Bounds(Vector3.zero, Vector3.one * 10000f),
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = true,
                layer = 0
            };
        }

        /// <summary>Quality presets change the budget live; the buffer is re-created at the new size.</summary>
        public void SetBudget(int budget)
        {
            Budget = budget;
            Capacity = Mathf.CeilToInt(budget * 1.08f);
            instances?.Release();
            instances = new GraphicsBuffer(GraphicsBuffer.Target.Append, Capacity, InstanceStride);
            material.SetBuffer(ShaderIds.GrassInstances, instances);
            state.SetData(new float[1]);    // restart the density estimate for the new budget
        }

        /// <summary>One curved, tapering blade: 3 quads + tip (7 verts). uv.x = side, uv.y = height.</summary>
        static Mesh BuildBlade()
        {
            var verts = new Vector3[7];
            var uvs = new Vector2[7];
            float[] h = { 0f, 0.33f, 0.66f };
            for (int i = 0; i < 3; i++)
            {
                float w = 0.5f * (1f - h[i] * 0.75f);
                verts[i * 2] = new Vector3(-w, h[i], 0f); uvs[i * 2] = new Vector2(0f, h[i]);
                verts[i * 2 + 1] = new Vector3(w, h[i], 0f); uvs[i * 2 + 1] = new Vector2(1f, h[i]);
            }
            verts[6] = new Vector3(0f, 1f, 0f); uvs[6] = new Vector2(0.5f, 1f);
            int[] tris = { 0, 2, 1, 1, 2, 3, 2, 4, 3, 3, 4, 5, 4, 6, 5 };
            var m = new Mesh { name = "GrassBlade", vertices = verts, uv = uvs, triangles = tris };
            m.bounds = new Bounds(Vector3.zero, Vector3.one * 10000f);
            return m;
        }

        public void SetExclusions(PoiPlan plan)
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 e = i < plan.Exclusions.Count ? plan.Exclusions[i] : new Vector3(1e6f, 1e6f, 0f);
                exclusions[i] = new Vector4(e.x, e.y, i < 2 ? e.z : 0f, 0f); // only lighthouse + campfire pads
            }
        }

        void Update()
        {
            if (instances == null || cam == null) return;
            if (Game.World != null && Game.World.Plan != null) SetExclusions(Game.World.Plan);
            Vector3 c = cam.transform.position;
            float smoothing = 1f - Mathf.Exp(-Time.unscaledDeltaTime / 0.35f);

            compute.SetVector(ShaderIds.GrassCameraPos, new Vector4(c.x, c.y, c.z, 0f));
            compute.SetVector(ShaderIds.GrassParams, new Vector4(C.GrassCellSize, gridDim, C.GrassCullDistance, Budget));
            compute.SetVector(ShaderIds.GrassParams2, new Vector4(C.GrassFullDensityRadius, C.GrassMinDensity, smoothing, Capacity));
            compute.SetVectorArray(ExclusionsId, exclusions);
            compute.SetInt(SeedId, Game.World != null && Game.World.Data != null ? Game.World.Data.Seed : C.DefaultSeed);

            int groups = gridDim / 16;
            compute.SetBuffer(kMeasure, ShaderIds.GrassCounter, counter);
            compute.Dispatch(kMeasure, groups, groups, 1);
            compute.SetBuffer(kFinalize, ShaderIds.GrassCounter, counter);
            compute.SetBuffer(kFinalize, ShaderIds.GrassState, state);
            compute.Dispatch(kFinalize, 1, 1, 1);
            instances.SetCounterValue(0);
            compute.SetBuffer(kPlace, ShaderIds.GrassCounter, counter);
            compute.SetBuffer(kPlace, ShaderIds.GrassState, state);
            compute.SetBuffer(kPlace, ShaderIds.GrassInstances, instances);
            compute.Dispatch(kPlace, groups, groups, 1);
            compute.SetBuffer(kArgs, ShaderIds.GrassCounter, counter);
            compute.Dispatch(kArgs, 1, 1, 1);
            // Instance count of the indirect draw = number of appended blades (offset 4 = instanceCount).
            GraphicsBuffer.CopyCount(instances, args, sizeof(uint));

            if (Visible) Graphics.RenderMeshIndirect(rp, blade, args);

            readbackTimer -= Time.unscaledDeltaTime;
            if (readbackTimer <= 0f)
            {
                readbackTimer = 0.25f;
                AsyncGPUReadback.Request(counter, onReadback);
            }
        }

        void OnReadback(AsyncGPUReadbackRequest req)
        {
            if (req.hasError || counter == null) return;
            var data = req.GetData<uint>();
            if (data.Length > 2) DrawnBlades = (int)data[2];
        }

        void OnDestroy()
        {
            instances?.Release(); instances = null;
            counter?.Release(); counter = null;
            state?.Release(); state = null;
            args?.Release(); args = null;
        }
    }
}
