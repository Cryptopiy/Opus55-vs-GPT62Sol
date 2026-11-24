using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace LivingIsland.EditorTools
{
    /// <summary>Windows player builds used for the automated test runs (batch mode).</summary>
    public static class BuildTools
    {
        public static void BuildPlayer() => Build(BuildOptions.None);
        public static void BuildPlayerDevelopment() => Build(BuildOptions.Development);

        /// <summary>Re-runs the idempotent project setup (links freshly exported models), then builds.</summary>
        public static void SetupAndBuild()
        {
            ProjectSetup.Run();
            Build(BuildOptions.None);
        }

        static void Build(BuildOptions options)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Island.unity" },
                locationPathName = OutputPath(),
                target = BuildTarget.StandaloneWindows64,
                options = options
            });
            var s = report.summary;
            Debug.Log("[LI] Build " + s.result + ": " + s.totalErrors + " errors, " + s.totalWarnings + " warnings, " +
                      (s.totalSize / (1024 * 1024)) + " MB, " + s.totalTime.TotalSeconds.ToString("F0") + " s");
            foreach (var step in report.steps)
                foreach (var msg in step.messages)
                    if (msg.type == LogType.Error || msg.type == LogType.Warning)
                        Debug.Log("[LI] build " + msg.type + ": " + msg.content);
            if (Application.isBatchMode) EditorApplication.Exit(s.result == BuildResult.Succeeded ? 0 : 1);
        }

        /// <summary>"Build/LivingIsland.exe", or the path given after "-buildOutput" (temporary test builds).</summary>
        static string OutputPath()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-buildOutput") return args[i + 1];
            return "Build/LivingIsland.exe";
        }
    }
}
