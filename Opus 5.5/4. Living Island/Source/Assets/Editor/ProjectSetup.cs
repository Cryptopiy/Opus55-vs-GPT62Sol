using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LivingIsland.EditorTools
{
    /// <summary>
    /// One-shot, idempotent project configuration (run in batch mode:
    /// -executeMethod LivingIsland.EditorTools.ProjectSetup.Run). Everything the game needs is created here,
    /// so opening the project and pressing Play requires no manual editor setup.
    /// </summary>
    public static class ProjectSetup
    {
        const string ScenePath = "Assets/Scenes/Island.unity";
        const string AssetsPath = "Assets/Resources/IslandAssets.asset";

        [MenuItem("Living Island/Run Project Setup")]
        public static void Run()
        {
            AssetDatabase.Refresh();
            EnsureLayers();
            ConfigurePipeline();
            ConfigureQuality();
            ConfigurePlayer();
            CreateAssetRegistry();
            CreateScene();
            AssetDatabase.SaveAssets();
            Debug.Log("[LI] Project setup complete");
        }

        static void EnsureLayers()
        {
            var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tagManager.FindProperty("layers");
            SetLayer(layers, 8, "Terrain");
            SetLayer(layers, 9, "Player");
            SetLayer(layers, 10, "Props");
            tagManager.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SetLayer(SerializedProperty layers, int index, string name)
        {
            var p = layers.GetArrayElementAtIndex(index);
            if (string.IsNullOrEmpty(p.stringValue)) p.stringValue = name;
        }

        static void ConfigurePipeline()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (asset == null) { Debug.LogError("[LI] PC_RPAsset not found"); return; }
            // High preset values are the committed defaults (QualityManager changes them live at runtime).
            asset.shadowDistance = GameConfig.ShadowDistanceHigh;
            asset.shadowCascadeCount = GameConfig.ShadowCascades;
            asset.cascade4Split = new Vector3(8f / GameConfig.ShadowDistanceHigh, 24f / GameConfig.ShadowDistanceHigh, 56f / GameConfig.ShadowDistanceHigh);
            asset.mainLightShadowmapResolution = 4096;
            asset.additionalLightsShadowmapResolution = 2048;
            asset.msaaSampleCount = 4;
            asset.renderScale = 1f;
            asset.gpuResidentDrawerMode = GPUResidentDrawerMode.Disabled;
            var so = new SerializedObject(asset);
            Set(so, "m_RequireDepthTexture", true);
            Set(so, "m_RequireOpaqueTexture", true);
            Set(so, "m_OpaqueDownsampling", 0);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_SoftShadowQuality", 3);
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_AdditionalLightShadowsSupported", true);
            Set(so, "m_UseAdaptivePerformance", false);
            Set(so, "m_EnableLODCrossFade", true);
            Set(so, "m_ShadowDepthBias", 0.6f);
            Set(so, "m_ShadowNormalBias", 0.6f);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            // SSAO renderer feature (I-16, I-28): stronger, larger radius than the template default.
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (rendererData != null)
            {
                foreach (var feature in rendererData.rendererFeatures)
                {
                    if (feature == null || feature.GetType().Name != "ScreenSpaceAmbientOcclusion") continue;
                    var fso = new SerializedObject(feature);
                    Set(fso, "m_Settings.Intensity", 0.7f);
                    Set(fso, "m_Settings.Radius", 0.45f);
                    Set(fso, "m_Settings.DirectLightingStrength", 0.3f);
                    Set(fso, "m_Settings.Downsample", false);
                    fso.ApplyModifiedPropertiesWithoutUndo();
                    feature.SetActive(true);
                    EditorUtility.SetDirty(feature);
                }
                EditorUtility.SetDirty(rendererData);
            }
        }

        static void Set(SerializedObject so, string path, object value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning("[LI] setup: property not found " + path); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: if (p.propertyType == SerializedPropertyType.Enum) p.enumValueIndex = i; else p.intValue = i; break;
                case float f: p.floatValue = f; break;
            }
        }

        static void ConfigureQuality()
        {
            var qso = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/QualitySettings.asset")[0]);
            var levels = qso.FindProperty("m_QualitySettings");
            for (int i = 0; i < levels.arraySize; i++)
            {
                var level = levels.GetArrayElementAtIndex(i);
                level.FindPropertyRelative("vSyncCount").intValue = 1;          // VSync on by default (GPU safety)
                if (level.FindPropertyRelative("name").stringValue == "PC")
                {
                    level.FindPropertyRelative("lodBias").floatValue = GameConfig.LodBiasHigh;
                    level.FindPropertyRelative("antiAliasing").intValue = 0;
                    qso.FindProperty("m_CurrentQuality").intValue = i;
                }
            }
            qso.ApplyModifiedPropertiesWithoutUndo();
        }

        static void ConfigurePlayer()
        {
            PlayerSettings.productName = "Living Island";
            PlayerSettings.runInBackground = true;
            PlayerSettings.visibleInBackground = true;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11 });
        }

        static T Load<T>(string path) where T : Object
        {
            var a = AssetDatabase.LoadAssetAtPath<T>(path);
            if (a == null) Debug.LogError("[LI] setup: missing asset " + path);
            return a;
        }

        static void CreateAssetRegistry()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
            var reg = AssetDatabase.LoadAssetAtPath<IslandAssets>(AssetsPath);
            if (reg == null)
            {
                reg = ScriptableObject.CreateInstance<IslandAssets>();
                AssetDatabase.CreateAsset(reg, AssetsPath);
            }
            reg.terrainShader = Load<Shader>("Assets/Shaders/Terrain.shader");
            reg.waterShader = Load<Shader>("Assets/Shaders/Water.shader");
            reg.skyShader = Load<Shader>("Assets/Shaders/Sky.shader");
            reg.skyCubeShader = Load<Shader>("Assets/Shaders/SkyCube.shader");
            reg.grassShader = Load<Shader>("Assets/Shaders/Grass.shader");
            reg.barkShader = Load<Shader>("Assets/Shaders/Bark.shader");
            reg.foliageShader = Load<Shader>("Assets/Shaders/Foliage.shader");
            reg.rockShader = Load<Shader>("Assets/Shaders/Rock.shader");
            reg.propShader = Load<Shader>("Assets/Shaders/Prop.shader");
            reg.glassShader = Load<Shader>("Assets/Shaders/Glass.shader");
            reg.flowerShader = Load<Shader>("Assets/Shaders/Flower.shader");
            reg.particleAdditiveShader = Load<Shader>("Assets/Shaders/ParticleAdditive.shader");
            reg.particleAlphaShader = Load<Shader>("Assets/Shaders/ParticleAlpha.shader");
            reg.rainShader = Load<Shader>("Assets/Shaders/Rain.shader");
            reg.beamShader = Load<Shader>("Assets/Shaders/Beam.shader");
            reg.lightningShader = Load<Shader>("Assets/Shaders/Lightning.shader");
            reg.grassCompute = Load<ComputeShader>("Assets/Shaders/GrassPlacement.compute");
            reg.palms = new GameObject[3];
            for (int i = 0; i < 3; i++) reg.palms[i] = Load<GameObject>("Assets/Models/SM_Palm_0" + (i + 1) + ".glb");
            reg.rocks = new GameObject[5];
            for (int i = 0; i < 5; i++) reg.rocks[i] = Load<GameObject>("Assets/Models/SM_Rock_0" + (i + 1) + ".glb");
            reg.lighthouse = Load<GameObject>("Assets/Models/SM_Lighthouse.glb");
            reg.rowboat = Load<GameObject>("Assets/Models/SM_Rowboat.glb");
            reg.driftwood = Load<GameObject>("Assets/Models/SM_Driftwood.glb");
            reg.campfireLogs = Load<GameObject>("Assets/Models/SM_CampfireLogs.glb");
            EditorUtility.SetDirty(reg);
        }

        static void CreateScene()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scenes")) AssetDatabase.CreateFolder("Assets", "Scenes");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("Living Island (built by code)");
            go.AddComponent<GameBootstrap>();
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }
    }
}
