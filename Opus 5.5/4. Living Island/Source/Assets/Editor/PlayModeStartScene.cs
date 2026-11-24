using UnityEditor;
using UnityEditor.SceneManagement;

namespace LivingIsland.EditorTools
{
    /// <summary>
    /// Pressing Play in the editor always starts the game scene (Assets/Scenes/Island.unity), whatever
    /// scene happens to be open, so the game starts directly into a playable state.
    /// </summary>
    [InitializeOnLoad]
    public static class PlayModeStartScene
    {
        static PlayModeStartScene()
        {
            // Batch-mode test runs enter play mode with their own init scene; only redirect interactive Play.
            if (UnityEngine.Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                var scene = AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/Island.unity");
                if (scene != null) EditorSceneManager.playModeStartScene = scene;
            };
        }
    }
}
