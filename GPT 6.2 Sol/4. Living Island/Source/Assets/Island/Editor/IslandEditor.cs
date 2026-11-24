using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class IslandEditor
{
    const string ScenePath = "Assets/Scenes/SampleScene.unity";

    public static void Compile()
    {
        IslandGlbImporter.BakeLibrary();
        Debug.Log("[Island] C# compilation and asset import complete.");
    }

    public static void Build()
    {
        Compile();
        var report = BuildPipeline.BuildPlayer(new[]{ScenePath}, "BuildValidation/IslandValidation.exe",
            BuildTarget.StandaloneWindows64, BuildOptions.None);
        Debug.Log($"[Island] Validation player build: {report.summary.result} ({report.summary.totalErrors} errors).");
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorApplication.Exit(1);
    }

    public static void Enter()
    {
        EditorSceneManager.OpenScene(ScenePath);
        string test = TestName();
        if (!Application.isBatchMode)
        {
            var view = EditorWindow.GetWindow(Type.GetType("UnityEditor.GameView,UnityEditor"));
            view.Show();
            view.Focus();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var vSync = view.GetType().GetProperty("vSyncEnabled", flags);
            if (vSync != null && vSync.CanWrite) vSync.SetValue(view, true);
            else view.GetType().GetField("m_VSyncEnabled", flags)?.SetValue(view, true);
            SelectResolution(view, test == "Day" || test == "Storm");
            Debug.Log($"[Island] Game View VSync: {vSync?.GetValue(view)}.");
        }
        EditorApplication.isPlaying = true;
    }

    static void SelectResolution(EditorWindow view, bool benchmark)
    {
        var asm = typeof(EditorWindow).Assembly;
        var sizesType = asm.GetType("UnityEditor.GameViewSizes");
        var instance = typeof(ScriptableSingleton<>).MakeGenericType(sizesType).GetProperty("instance").GetValue(null);
        var group = sizesType.GetMethod("GetGroup").Invoke(instance, new object[] { Enum.ToObject(asm.GetType("UnityEditor.GameViewSizeGroupType"),0) });
        var groupType = group.GetType();
        int width = benchmark ? IslandConfig.BenchmarkWidth : IslandConfig.NormalWidth;
        int height = benchmark ? IslandConfig.BenchmarkHeight : IslandConfig.NormalHeight;
        int total = (int)groupType.GetMethod("GetTotalCount").Invoke(group, null);
        int selected = -1;
        for (int i = 0; i < total; i++)
        {
            var size = groupType.GetMethod("GetGameViewSize").Invoke(group, new object[]{ i });
            if ((int)size.GetType().GetProperty("width").GetValue(size) == width &&
                (int)size.GetType().GetProperty("height").GetValue(size) == height) { selected = i; break; }
        }
        if (selected < 0)
        {
            var sizeType = asm.GetType("UnityEditor.GameViewSize");
            var kind = Enum.ToObject(asm.GetType("UnityEditor.GameViewSizeType"), 1);
            var size = Activator.CreateInstance(sizeType, kind, width, height, "Living Island");
            groupType.GetMethod("AddCustomSize").Invoke(group, new[]{ size });
            selected = total;
        }
        view.GetType().GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view,selected);
    }

    public static string TestName()
    {
        string[] args = Environment.GetCommandLineArgs();
        int arg = Array.IndexOf(args,"-islandTest");
        return arg >= 0 && arg + 1 < args.Length ? args[arg + 1] : "";
    }
}

