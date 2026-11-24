using System.Collections.Generic;
using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// U-01..U-06: the single place that decides whether interface is drawn. Every canvas is created through
    /// <see cref="UIFactory.CreateCanvas"/>, which registers it here: HUD, crosshair, score pop-ups and the F1
    /// overlay are "hideable"; the pause menu is "always visible" so the player can never get stuck.
    /// Hiding only disables the Canvas components (never GameObjects, time or input), so game state is untouched
    /// and anything spawned on a hidden canvas later (new pop-ups, overlay toggled with F1) stays hidden too.
    /// </summary>
    public sealed class UiVisibility : MonoBehaviour
    {
        private readonly List<Canvas> hideable = new List<Canvas>(8);
        private readonly List<Canvas> alwaysVisible = new List<Canvas>(4);
        private int restoreMenusFrame = -1;

        public bool Visible { get; private set; } = true;
        public int ToggleCount { get; private set; }
        public IReadOnlyList<Canvas> HideableCanvases => hideable;
        public IReadOnlyList<Canvas> AlwaysVisibleCanvases => alwaysVisible;

        public void Register(Canvas canvas, bool keepVisible)
        {
            if (keepVisible) alwaysVisible.Add(canvas);
            else
            {
                hideable.Add(canvas);
                canvas.enabled = Visible;   // a canvas created while hidden starts hidden (U-04)
            }
        }

        /// <summary>F8.</summary>
        public void Toggle()
        {
            SetVisible(!Visible);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            ToggleCount++;
            for (int i = 0; i < hideable.Count; i++) hideable[i].enabled = visible;
        }

        /// <summary>
        /// U-05: called right before a screenshot is requested. With the UI hidden, even the always-visible menus are
        /// left out of the captured frame (ScreenCapture grabs this frame after rendering); they come back next frame.
        /// </summary>
        public void PrepareScreenshot()
        {
            if (Visible) return;
            for (int i = 0; i < alwaysVisible.Count; i++) alwaysVisible[i].enabled = false;
            restoreMenusFrame = Time.frameCount + 1;
        }

        private void LateUpdate()
        {
            if (restoreMenusFrame < 0 || Time.frameCount < restoreMenusFrame) return;
            restoreMenusFrame = -1;
            for (int i = 0; i < alwaysVisible.Count; i++) alwaysVisible[i].enabled = true;
        }
    }
}
