using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LivingIsland
{
    /// <summary>
    /// The one place that decides whether the interface is drawn (F8 "hide all UI" for clean gameplay footage).
    /// Interface canvases register here; while hidden, every registered canvas and its raycaster are disabled, so
    /// nothing on them is drawn (also not in the F12 window screenshot) and nothing on them can be clicked. UI
    /// created later under a registered canvas, or a canvas registered while hidden, is hidden as well.
    /// The state is static: it survives Restart (scene reload), seed regeneration and respawns, and only F8
    /// changes it. The pause menu and the start fade-in sit on a separate canvas that is never registered, so the
    /// game can never get stuck behind hidden UI. The switch changes no game state: input, time, physics and the
    /// panels' own open/closed state stay exactly as they are.
    /// </summary>
    public static class UIVisibility
    {
        public static bool Hidden { get; private set; }

        /// <summary>Raised after every change of <see cref="Hidden"/> (the UI root updates the mouse cursor).</summary>
        public static event System.Action Changed;

        static readonly List<Canvas> canvases = new List<Canvas>();

        // Every game launch starts with the interface visible (Play Mode runs without a domain reload in this project).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnLaunch()
        {
            Hidden = false;
            canvases.Clear();
            Changed = null;
        }

        public static void Toggle() => SetHidden(!Hidden);

        public static void SetHidden(bool hidden)
        {
            if (hidden == Hidden) return;
            Hidden = hidden;
            for (int i = canvases.Count - 1; i >= 0; i--)
            {
                if (canvases[i] == null) canvases.RemoveAt(i);
                else Apply(canvases[i]);
            }
            // A focused widget would keep receiving keys while invisible (the seed field blocks the hotkeys while
            // it has focus), so hiding drops the UI selection.
            if (hidden && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Changed?.Invoke();
        }

        /// <summary>Puts an interface canvas under the switch; it takes the current state immediately.</summary>
        public static void Register(Canvas canvas)
        {
            if (canvas == null) return;
            if (!canvases.Contains(canvas)) canvases.Add(canvas);
            Apply(canvas);
        }

        public static void Unregister(Canvas canvas) => canvases.Remove(canvas);

        static void Apply(Canvas canvas)
        {
            canvas.enabled = !Hidden;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = !Hidden;
        }
    }
}
