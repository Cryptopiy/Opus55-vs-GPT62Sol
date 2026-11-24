using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaveSurvival
{
    /// <summary>
    /// U-01..U-06: the single place that decides whether interface is drawn. <see cref="ToggleKey"/> (F8) switches it;
    /// default is visible.
    /// Interface roots register once when they are created: the HUD canvas (health/stamina/ammo/slots/wave/score,
    /// crosshair + hit markers, damage arcs + damage vignette, banners, notifications, boss bar, floating enemy health
    /// bars, F1 overlay/FPS) and the F2 AI-debug view (labels, lines, NavMesh and cover markers). The current state is
    /// applied to every root at once and to every root registered later, so UI created while hidden starts hidden.
    /// The state is static: it survives level restarts (scene reloads) and resets to visible only when the application
    /// or Play Mode starts. Screens the player needs to continue (pause, death, victory) never register, so they always
    /// show. Hiding only stops rendering (Canvas.enabled / debug root active); input, AI, physics, timers and score run on.
    /// </summary>
    public sealed class UiVisibility : MonoBehaviour
    {
        public const Key ToggleKey = Key.F8;

        static readonly List<Canvas> canvases = new List<Canvas>(4);
        static readonly List<GameObject> overlayRoots = new List<GameObject>(4);

        public static bool Visible { get; private set; } = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Visible = true;
            canvases.Clear();
            overlayRoots.Clear();
        }

        public static UiVisibility Create(Transform parent)
        {
            var go = new GameObject("UiVisibility");
            go.transform.SetParent(parent, false);
            return go.AddComponent<UiVisibility>();
        }

        /// <summary>Screen-space interface canvas: hidden by switching its Canvas component off (its scripts keep running).</summary>
        public static void RegisterCanvas(Canvas canvas)
        {
            if (canvas == null) return;
            canvases.Add(canvas);
            canvas.enabled = Visible;
        }

        /// <summary>Root of interface drawn in the world (debug lines/markers/labels): hidden by deactivating the root.</summary>
        public static void RegisterOverlayRoot(GameObject root)
        {
            if (root == null) return;
            overlayRoots.Add(root);
            root.SetActive(Visible);
        }

        public static void Toggle() => SetVisible(!Visible);

        public static void SetVisible(bool visible)
        {
            Visible = visible;
            // Entries of an unloaded scene are destroyed objects: drop them while applying.
            for (int i = canvases.Count - 1; i >= 0; i--)
            {
                if (canvases[i] == null) { canvases.RemoveAt(i); continue; }
                canvases[i].enabled = visible;
            }
            for (int i = overlayRoots.Count - 1; i >= 0; i--)
            {
                if (overlayRoots[i] == null) { overlayRoots.RemoveAt(i); continue; }
                overlayRoots[i].SetActive(visible);
            }
        }

        void Update()
        {
            // Read every frame (also while paused or on an end screen): the switch works at any moment of play.
            Keyboard kb = Keyboard.current;
            if (kb != null && kb[ToggleKey].wasPressedThisFrame) Toggle();
        }
    }
}
