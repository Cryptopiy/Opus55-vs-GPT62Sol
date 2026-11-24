using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DestructionSandbox.EditorTools
{
    /// <summary>
    /// Editor-side setup: creates the main scene (one GameBootstrap object with its shader references),
    /// registers it as the only build scene, makes Play mode always start from it, and offers batch-mode
    /// entry points for building the Windows player and running the self-test in Play mode.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectSetup
    {
        public const string MainScenePath = "Assets/Scenes/DestructionSandbox.unity";
        private const string ShaderDir = "Assets/DestructionSandbox/Shaders/";

        static ProjectSetup()
        {
            EditorApplication.delayCall += ApplyPlayModeStartScene;
        }

        private static void ApplyPlayModeStartScene()
        {
            var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>(MainScenePath);
            if (scene != null && EditorSceneManager.playModeStartScene != scene) EditorSceneManager.playModeStartScene = scene;
        }

        [MenuItem("Destruction Sandbox/Rebuild Main Scene")]
        public static void CreateMainScene()
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var go = new GameObject("GameBootstrap");
            var boot = go.AddComponent<GameBootstrap>();
            boot.BlockShader = LoadShader("DS_Block.shader");
            boot.GlassShader = LoadShader("DS_Glass.shader");
            boot.GroundShader = LoadShader("DS_Ground.shader");
            boot.SkyShader = LoadShader("DS_Sky.shader");
            boot.ParticleShader = LoadShader("DS_Particle.shader");
            boot.ShockwaveShader = LoadShader("DS_Shockwave.shader");
            boot.OrbShader = LoadShader("DS_Orb.shader");
            boot.ScorchShader = LoadShader("DS_Scorch.shader");
            Directory.CreateDirectory(Path.GetDirectoryName(MainScenePath));
            EditorSceneManager.SaveScene(scene, MainScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MainScenePath, true) };
            AssetDatabase.SaveAssets();
            ApplyPlayModeStartScene();
            Debug.Log("[ProjectSetup] Main scene written to " + MainScenePath);
        }

        private static Shader LoadShader(string file)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderDir + file);
            if (shader == null) throw new FileNotFoundException("Shader missing: " + ShaderDir + file);
            return shader;
        }

        /// <summary>Batch: create the scene, make the editor open it next time, quit.</summary>
        public static void BatchSetup()
        {
            CreateMainScene();
            string library = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "LastSceneManagerSetup.txt");
            File.WriteAllText(library, "sceneSetups:\n- path: " + MainScenePath + "\n  isLoaded: 1\n  isActive: 1\n  isSubScene: 0\n");
            EditorApplication.Exit(0);
        }

        /// <summary>Batch: open the main scene and enter Play mode (the game reads "-autotest" from the command line).</summary>
        public static void BatchPlay()
        {
            EditorSceneManager.OpenScene(MainScenePath, OpenSceneMode.Single);
            ApplyPlayModeStartScene();
            EditorApplication.EnterPlaymode();
        }

        /// <summary>Batch: build the Windows 64-bit player into &lt;working folder&gt;/Builds/Windows.</summary>
        public static void BatchBuildPlayer()
        {
            string working = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string output = Path.Combine(working, "Builds", "Windows", "DestructionSandbox.exe");
            bool development = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-devbuild") >= 0;
            var options = new BuildPlayerOptions
            {
                scenes = new[] { MainScenePath },
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                options = development ? BuildOptions.Development : BuildOptions.None
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[ProjectSetup] Build " + report.summary.result + " -> " + output + " (" + report.summary.totalErrors + " errors, " +
                      report.summary.totalWarnings + " warnings, " + report.summary.totalTime + ")");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
