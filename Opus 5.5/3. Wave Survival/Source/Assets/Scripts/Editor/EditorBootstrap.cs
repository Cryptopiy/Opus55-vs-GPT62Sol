using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace WaveSurvival.EditorTools
{
    /// <summary>
    /// Makes "press Play" always start the game: the main scene is the Play Mode start scene, and it is opened
    /// automatically when the editor starts on an empty/untitled scene.
    /// </summary>
    [InitializeOnLoad]
    public static class EditorBootstrap
    {
        static EditorBootstrap()
        {
            EditorApplication.delayCall += Apply;
        }

        static void Apply()
        {
            var main = AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectSetup.ScenePath);
            if (main == null) return;
            EditorSceneManager.playModeStartScene = main;
            if (EditorApplication.isPlayingOrWillChangePlaymode || UnityEngine.Application.isBatchMode) return;
            Scene active = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(active.path) && !active.isDirty) EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
        }
    }
}
