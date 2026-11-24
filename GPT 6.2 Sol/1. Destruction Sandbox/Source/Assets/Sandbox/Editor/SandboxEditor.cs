using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;

namespace DestructionRange
{
    internal static class SandboxEditor
    {
        const string ScenePath = "Assets/Scenes/Destruction Range.unity";

        public static void Play()
        {
            EditorSceneManager.OpenScene(ScenePath);
            EditorApplication.EnterPlaymode();
        }

        public static void Build()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-sandbox-output");
            if (index < 0 || index + 1 >= args.Length) throw new ArgumentException("Provide -sandbox-output.");
            string path = args[index + 1];
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            BuildReport result = BuildPipeline.BuildPlayer(new[] { ScenePath }, path, BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (result.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Windows build failed: " + result.summary.result);
        }
    }
}
