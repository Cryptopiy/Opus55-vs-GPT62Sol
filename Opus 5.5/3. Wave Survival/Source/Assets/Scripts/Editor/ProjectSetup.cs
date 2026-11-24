using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace WaveSurvival.EditorTools
{
    /// <summary>
    /// One-time project setup (run from the menu or with -executeMethod WaveSurvival.EditorTools.ProjectSetup.Setup):
    /// writes the main scene (a single GameBootstrap object + fog/ambient render settings), registers it as the only
    /// build scene, and creates "shader keeper" materials in Resources so player builds keep every URP shader variant
    /// that the runtime-created materials use.
    /// </summary>
    public static class ProjectSetup
    {
        public const string ScenePath = "Assets/Scenes/Main.unity";
        const string KeeperFolder = "Assets/Resources/ShaderKeepers";

        [MenuItem("Wave Survival/Setup Project (scene + shader keepers)")]
        public static void Setup()
        {
            CreateScene();
            CreateShaderKeepers();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[SETUP] Wave Survival project setup complete");
        }

        static void CreateScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
            // Fog/ambient are also set at runtime; saving them in the scene keeps the fog shader variants in builds.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = GameConfig.Lighting.FogDensity;
            RenderSettings.fogColor = GameConfig.Lighting.FogColor;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = GameConfig.Lighting.AmbientSky;
            RenderSettings.ambientEquatorColor = GameConfig.Lighting.AmbientEquator;
            RenderSettings.ambientGroundColor = GameConfig.Lighting.AmbientGround;
            RenderSettings.skybox = null;
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            if (File.Exists("Assets/Scenes/SampleScene.unity")) AssetDatabase.DeleteAsset("Assets/Scenes/SampleScene.unity");
        }

        static void Save(Material m, string name)
        {
            m.hideFlags = HideFlags.None;
            AssetDatabase.CreateAsset(m, KeeperFolder + "/" + name + ".mat");
        }

        static void CreateShaderKeepers()
        {
            Directory.CreateDirectory(KeeperFolder);
            foreach (string f in Directory.GetFiles(KeeperFolder, "*.mat")) AssetDatabase.DeleteAsset(f.Replace('\\', '/'));
            // URP's importer keeps _NORMALMAP only when a bump texture is assigned: use a tiny generated flat normal map.
            string normalPath = KeeperFolder + "/KeeperNormal.asset";
            AssetDatabase.DeleteAsset(normalPath);
            var flat = new Texture2D(4, 4, TextureFormat.RGBA32, false, true) { name = "KeeperNormal" };
            var px = new Color[16];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(1f, 0.5f, 1f, 0.5f);
            flat.SetPixels(px);
            flat.Apply();
            AssetDatabase.CreateAsset(flat, normalPath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            Save(MaterialFactory.Lit("Lit", Color.white, 0f, 0.5f), "Lit");
            Save(MaterialFactory.Lit("LitNormal", Color.white, 0f, 0.5f, null, normal), "LitNormal");
            Save(MaterialFactory.LitEmissive("LitEmissive", Color.white, Color.red, 0f, 0.5f), "LitEmissive");
            Save(MaterialFactory.LitEmissive("LitNormalEmissive", Color.white, Color.red, 0f, 0.5f, null, normal), "LitNormalEmissive");
            Material t1 = MaterialFactory.Lit("LitTransparent", Color.white, 0f, 0.5f);
            MaterialFactory.MakeTransparent(t1);
            Save(t1, "LitTransparent");
            Material t2 = MaterialFactory.LitEmissive("LitTransparentEmissive", Color.white, Color.red, 0f, 0.5f, null, normal);
            MaterialFactory.MakeTransparent(t2);
            Save(t2, "LitTransparentNormalEmissive");
            Material t3 = MaterialFactory.LitEmissive("LitTransparentEmissiveNoNormal", Color.white, Color.red, 0f, 0.5f);
            MaterialFactory.MakeTransparent(t3);
            Save(t3, "LitTransparentEmissive");
            Save(MaterialFactory.Particle("ParticleAdditive", null, Color.white, true), "ParticleAdditive");
            Save(MaterialFactory.Particle("ParticleAlpha", null, Color.white, false), "ParticleAlpha");
        }
    }
}
