using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// Maps the polled input to game actions (structure selection, reset, clear, slow motion, weapons, pause,
    /// debug overlay, UI visibility, screenshots). The self-test drives the very same action methods.
    /// </summary>
    [DefaultExecutionOrder(-800)]
    public sealed class GameController : MonoBehaviour
    {
        public int ResetCount { get; private set; }

        private void Update()
        {
            GameInput input = Game.Input;
            if (input == null) return;
            if (!Game.IsPaused) Game.SessionTime += Time.unscaledDeltaTime;

            if (input.ToggleOverlay) Game.Overlay.Toggle();
            if (input.ToggleUi) Game.Ui.Toggle();          // F8 works at any moment, also while paused (U-03)
            if (input.Screenshot) ScreenshotCapture.Capture();
            if (input.TogglePause)
            {
                if (Game.IsPaused) Game.Pause.Close(); else Game.Pause.Open();
            }
            if (Game.IsPaused) return;

            // Clicking into the game view re-captures the mouse (e.g. after the editor stole focus).
            if (Cursor.lockState != CursorLockMode.Locked && input.FireHeld) { LockCursor(); return; }

            if (input.SelectStructure >= 0) SelectStructure(input.SelectStructure);
            if (input.ResetStructure) ResetStructure();
            if (input.ClearDebris) ClearDebris();
            if (input.ToggleSlowMotion) Game.SlowMo.Toggle();

            if (input.WeaponSelect >= 0) Game.Weapons.Select((WeaponType)input.WeaponSelect);
            if (input.WeaponScroll != 0) Game.Weapons.Cycle(input.WeaponScroll);
            if (input.FireHeld && Cursor.lockState == CursorLockMode.Locked) Game.Weapons.TryFire();
        }

        public static void LockCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public static void UnlockCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        /// <summary>F5 / F6 / F7.</summary>
        public void SelectStructure(int index)
        {
            Game.Structures.Select(index);
        }

        /// <summary>R (and "Restart structure" in the pause menu): rebuild the current structure, refill ammo.</summary>
        public void ResetStructure()
        {
            Game.Structures.Rebuild();
            Game.Weapons.Refill();
            ResetCount++;
        }

        /// <summary>X: clear all shards and debris (spent cannonballs) but keep the structure as it is.</summary>
        public void ClearDebris()
        {
            Game.Shards.ClearAll();
            Game.Projectiles.ClearSpent();
            Game.Audio.PlayUiClick();
        }
    }
}
