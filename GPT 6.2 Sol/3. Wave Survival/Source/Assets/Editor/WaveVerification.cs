using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Build.Reporting;
using UnityEngine;

internal static class WaveVerification
{
    const string Scene = "Assets/Scenes/SampleScene.unity";

    public static void Play()
    {
        EditorSceneManager.OpenScene(Scene);
        if (!Application.isBatchMode) PrepareGameView();
        double started = EditorApplication.timeSinceStartup;
        double duration = System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-waveCheck") >= 0
            ? Application.isBatchMode ? 95 : 112
            : 10;
        EditorApplication.update += () =>
        {
            if (EditorApplication.timeSinceStartup - started < duration) return;
            if (!EditorApplication.isPlaying && EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.Exit(0);
        };
        EditorApplication.EnterPlaymode();
    }

    static void PrepareGameView()
    {
        var gameViewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView");
        var gameView = EditorWindow.GetWindow(gameViewType);
        gameView.Close();
        gameView = EditorWindow.GetWindow(gameViewType, true, "Verification Game", true);
        gameView.position = new Rect(32, 32, 1060, 620);
        var settings = new SerializedObject(gameView);
        settings.FindProperty("m_VSyncEnabled").boolValue = true;
        settings.ApplyModifiedPropertiesWithoutUndo();
        gameView.Focus();
    }

    public static void Build()
    {
        var result = BuildPipeline.BuildPlayer(new[] { Scene }, "../.verification/Bastion.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.Development);
        if (result.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
    }
}

