using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace WaveSurvival.EditorTools
{
    /// <summary>
    /// Test/build tooling for batch mode:
    ///  -executeMethod WaveSurvival.EditorTools.BuildTools.BuildWindows   (player build into &lt;project&gt;/Build/)
    ///  -executeMethod WaveSurvival.EditorTools.BuildTools.PlayModeTest   (enters Play Mode, exits when the AutoTest quits
    ///                                                                     or after -testDuration + 30 s)
    /// </summary>
    public static class BuildTools
    {
        const string PlayTestKey = "WS_PlayModeTest";
        const string PlayTestDeadlineKey = "WS_PlayModeTestDeadline";

        /// <summary>Batch entry point: project setup (scene + shader keepers) followed by the player build.</summary>
        public static void SetupAndBuild()
        {
            ProjectSetup.Setup();
            BuildWindows();
        }

        [MenuItem("Wave Survival/Build Windows Player")]
        public static void BuildWindows()
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Build"));
            Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath },
                locationPathName = Path.Combine(dir, "WaveSurvival.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            BuildReport report = BuildPipeline.BuildPlayer(options);
            Debug.Log("[BUILD] result=" + report.summary.result + " errors=" + report.summary.totalErrors +
                      " warnings=" + report.summary.totalWarnings + " size=" + report.summary.totalSize + " path=" + options.locationPathName);
            if (Application.isBatchMode) EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void PlayModeTest()
        {
            float duration = 90f;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-testDuration") float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out duration);
            SessionState.SetBool(PlayTestKey, true);
            SessionState.SetFloat(PlayTestDeadlineKey, (float)EditorApplication.timeSinceStartup + duration + 60f);
            // Open the main scene and enter Play Mode only after the editor finished starting up.
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            UnityEditor.SceneManagement.EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.ScenePath);
            enterAt = EditorApplication.timeSinceStartup + 3.0;
            Hook();
        }

        static double enterAt = -1.0;

        [InitializeOnLoadMethod]
        static void Hook()
        {
            if (!SessionState.GetBool(PlayTestKey, false)) return;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.update -= Watchdog;
            EditorApplication.update += Watchdog;
        }

        static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode) return;
            SessionState.SetBool(PlayTestKey, false);
            Debug.Log("[PLAYTEST] play mode ended, exiting editor");
            EditorApplication.Exit(0);
        }

        static void Watchdog()
        {
            if (enterAt > 0.0 && EditorApplication.timeSinceStartup >= enterAt && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                enterAt = -1.0;
                Debug.Log("[PLAYTEST] entering play mode");
                EditorApplication.EnterPlaymode();
            }
            if (EditorApplication.timeSinceStartup < SessionState.GetFloat(PlayTestDeadlineKey, float.MaxValue)) return;
            SessionState.SetBool(PlayTestKey, false);
            Debug.Log("[PLAYTEST] watchdog timeout, exiting editor");
            EditorApplication.Exit(2);
        }
    }
}
