using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using W = DestructionSandbox.GameConfig.World;
using L = DestructionSandbox.GameConfig.Lighting;
using Layers = DestructionSandbox.GameConfig.Layers;

namespace DestructionSandbox
{
    /// <summary>Builds the static world: ground, border, invisible walls, sun, sky, ambient, fog, post-processing.</summary>
    public static class WorldBuilder
    {
        public static Light Sun { get; private set; }

        public static void Build(Transform parent, MaterialLibrary materials, MeshLibrary meshes)
        {
            var world = new GameObject("World").transform;
            world.SetParent(parent, false);

            BuildGround(world, materials, meshes);
            BuildBorder(world, materials, meshes);
            BuildInvisibleWalls(world, materials);
            BuildLighting(world, materials);
            BuildPostProcessing(world);
        }

        private static void BuildGround(Transform world, MaterialLibrary materials, MeshLibrary meshes)
        {
            var ground = new GameObject("Ground");
            ground.layer = Layers.Environment;
            ground.transform.SetParent(world, false);
            ground.AddComponent<MeshFilter>().sharedMesh = meshes.GroundPlane;
            var renderer = ground.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials.Ground;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            var box = ground.AddComponent<BoxCollider>();
            box.size = new Vector3(W.GroundSize, W.GroundColliderThickness, W.GroundSize);
            box.center = new Vector3(0f, -W.GroundColliderThickness * 0.5f, 0f);
            box.sharedMaterial = materials.GroundPhysics;
            Game.GroundTransform = ground.transform;
        }

        private static void BuildBorder(Transform world, MaterialLibrary materials, MeshLibrary meshes)
        {
            float half = W.GroundSize * 0.5f;
            float t = W.BorderThickness;
            float y = W.BorderHeight * 0.5f;
            float inner = half - t * 0.5f;
            CreateBorderPiece(world, materials, meshes, new Vector3(0f, y, inner), new Vector3(W.GroundSize, W.BorderHeight, t));
            CreateBorderPiece(world, materials, meshes, new Vector3(0f, y, -inner), new Vector3(W.GroundSize, W.BorderHeight, t));
            CreateBorderPiece(world, materials, meshes, new Vector3(inner, y, 0f), new Vector3(t, W.BorderHeight, W.GroundSize - 2f * t));
            CreateBorderPiece(world, materials, meshes, new Vector3(-inner, y, 0f), new Vector3(t, W.BorderHeight, W.GroundSize - 2f * t));
        }

        private static void CreateBorderPiece(Transform world, MaterialLibrary materials, MeshLibrary meshes, Vector3 position, Vector3 size)
        {
            var go = new GameObject("Border");
            go.layer = Layers.Environment;
            go.transform.SetParent(world, false);
            go.transform.localPosition = position;
            go.transform.localScale = size;
            go.AddComponent<MeshFilter>().sharedMesh = meshes.UnitCube;
            go.AddComponent<MeshRenderer>().sharedMaterial = materials.Concrete;
            go.AddComponent<BoxCollider>().sharedMaterial = materials.GroundPhysics;
        }

        private static void BuildInvisibleWalls(Transform world, MaterialLibrary materials)
        {
            float half = W.GroundSize * 0.5f;
            float t = W.InvisibleWallThickness;
            float h = W.InvisibleWallHeight;
            float y = h * 0.5f - W.GroundColliderThickness;
            float span = W.GroundSize + 2f * t;
            CreateWall(world, materials, new Vector3(0f, y, half + t * 0.5f), new Vector3(span, h, t));
            CreateWall(world, materials, new Vector3(0f, y, -half - t * 0.5f), new Vector3(span, h, t));
            CreateWall(world, materials, new Vector3(half + t * 0.5f, y, 0f), new Vector3(t, h, span));
            CreateWall(world, materials, new Vector3(-half - t * 0.5f, y, 0f), new Vector3(t, h, span));
        }

        private static void CreateWall(Transform world, MaterialLibrary materials, Vector3 position, Vector3 size)
        {
            var go = new GameObject("InvisibleWall");
            go.layer = Layers.Environment;
            go.transform.SetParent(world, false);
            go.transform.localPosition = position;
            var box = go.AddComponent<BoxCollider>();
            box.size = size;
            box.sharedMaterial = materials.GroundPhysics;
        }

        private static void BuildLighting(Transform world, MaterialLibrary materials)
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(world, false);
            sunGo.transform.rotation = Quaternion.Euler(L.SunEuler);
            Sun = sunGo.AddComponent<Light>();
            Sun.type = LightType.Directional;
            Sun.color = L.SunColor;
            Sun.intensity = L.SunIntensity;
            Sun.shadows = LightShadows.Soft;
            Sun.shadowStrength = L.ShadowStrength;
            sunGo.AddComponent<UniversalAdditionalLightData>();
            RenderSettings.sun = Sun;

            // Shadow distance 80 m (D-02) on the active URP asset (identical value is also stored in the asset).
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && !Mathf.Approximately(urp.shadowDistance, L.ShadowDistance))
                urp.shadowDistance = L.ShadowDistance;

            materials.SetSunDirection(-sunGo.transform.forward);
            RenderSettings.skybox = materials.Sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.ambientIntensity = L.AmbientIntensity;
            RenderSettings.ambientProbe = SkyAmbient.Project(L.AmbientSampleCount);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = L.FogDensity;
            RenderSettings.fogColor = L.SkyHorizon;

            // Realtime reflection probe that only sees the gradient sky, rendered once (metal/specular reflections).
            var probeGo = new GameObject("SkyReflection");
            probeGo.transform.SetParent(world, false);
            probeGo.transform.position = new Vector3(0f, 5f, 0f);
            var probe = probeGo.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.NoTimeSlicing;
            probe.size = new Vector3(W.GroundSize * 4f, W.InvisibleWallHeight, W.GroundSize * 4f);
            probe.cullingMask = 0;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.resolution = L.ReflectionCubeSize;
            probe.hdr = true;
            probe.importance = 0;
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) probe.RenderProbe();
        }

        private static void BuildPostProcessing(Transform world)
        {
            var go = new GameObject("PostProcessVolume");
            go.transform.SetParent(world, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RuntimePostProfile";

            Bloom bloom = profile.Add<Bloom>(true);
            bloom.intensity.Override(L.BloomIntensity);
            bloom.threshold.Override(L.BloomThreshold);
            bloom.scatter.Override(L.BloomScatter);

            Tonemapping tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            Vignette vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(L.VignetteIntensity);

            volume.sharedProfile = profile;
        }
    }
}
