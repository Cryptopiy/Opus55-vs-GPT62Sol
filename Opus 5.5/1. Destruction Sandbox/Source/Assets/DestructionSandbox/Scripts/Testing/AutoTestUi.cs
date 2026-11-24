using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace DestructionSandbox
{
    /// <summary>
    /// U-06: presses the real F8 key (Input System events, the same path as a physical keyboard) and checks every
    /// UI graphic that would actually be drawn: none while hidden, all of it back when shown. While hidden it also
    /// drives the other hotkeys and spawns new pop-ups to prove the toggle changes nothing else (U-03, U-04, U-05).
    /// </summary>
    public sealed partial class AutoTest
    {
        private Keyboard virtualKeyboard;

        private IEnumerator UiToggle()
        {
            // In batch mode there is no focused game view; route simulated keys to the game like a focused window.
            InputSettings settings = InputSystem.settings;
            InputSettings.BackgroundBehavior oldBackground = settings.backgroundBehavior;
            InputSettings.EditorInputBehaviorInPlayMode oldEditor = settings.editorInputBehaviorInPlayMode;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;

            Game.Controller.SelectStructure(0);
            yield return WaitGame(0.5f);
            bool overlayWasOn = Game.Overlay.Visible;
            if (!overlayWasOn) yield return PressKey(Key.F1);               // the F1 overlay is part of the check
            yield return FireAt(WeaponType.Cannonball, new Vector3(0f, 10.5f, -2f));
            yield return WaitGame(0.9f);                                    // 28 m at 40 m/s, glass breaks: pop-ups alive
            int hudBefore = CountDrawn(Game.Hud.Canvas);
            int allBefore = CountDrawnGraphics(false);
            int popupsBefore = Game.Popups.ActiveCount;
            yield return Shot("ui_visible");

            // --- F8: hide. The toggle itself must not change any game state.
            int buildsBefore = Game.Structures.BuildCount;
            float scaleBefore = Time.timeScale;
            int scoreBefore = Game.Score.Score;
            CursorLockMode lockBefore = Cursor.lockState;
            yield return PressKey(Key.F8);
            bool hidden = !Game.Ui.Visible && CountDrawnGraphics(false) == 0;
            bool untouched = Game.Structures.BuildCount == buildsBefore && Mathf.Approximately(Time.timeScale, scaleBefore) &&
                             !Game.IsPaused && Game.Overlay.Visible && Cursor.lockState == lockBefore && Game.Score.Score >= scoreBefore;
            Check("U-01 F8 hides", hidden && Game.Ui.ToggleCount == 1, "F8 press: UI visible -> " + (Game.Ui.Visible ? "visible" : "hidden") +
                  ", drawn UI graphics " + allBefore + " -> " + CountDrawnGraphics(false) + " (HUD " + hudBefore + ", pop-ups alive " + popupsBefore + ")");
            yield return Shot("ui_hidden");

            // --- While hidden: every hotkey still works, new UI (pop-ups, F1, rebuilt structures) stays hidden.
            float fixedTimeBefore = Time.fixedTime;
            yield return PressKey(Key.R);
            bool resetOk = Game.Structures.BuildCount == buildsBefore + 1 && Game.Weapons.ShellAmmo == GameConfig.Weapons.ShellAmmo;
            yield return PressKey(Key.F6);
            bool structureOk = Game.Structures.CurrentIndex == 1;
            yield return PressKey(Key.Digit2);
            bool weaponOk = Game.Weapons.Current == WeaponType.Shell;
            yield return PressKey(Key.Digit1);
            weaponOk &= Game.Weapons.Current == WeaponType.Cannonball;
            yield return PressKey(Key.T);
            bool slowOk = Game.SlowMo.Active && Mathf.Approximately(Time.timeScale, GameConfig.Timing.SlowMotionTimeScale);
            yield return PressKey(Key.T);
            slowOk &= !Game.SlowMo.Active && Mathf.Approximately(Time.timeScale, 1f);
            yield return PressKey(Key.F1);
            bool overlayOk = !Game.Overlay.Visible;
            yield return PressKey(Key.F1);
            overlayOk &= Game.Overlay.Visible;
            int scoreHidden = Game.Score.Score;
            yield return FireAt(WeaponType.Cannonball, new Vector3(-6f, 4f, -0.25f));   // glass windows of the wall
            yield return WaitGame(0.95f);                                   // flight time to the wall + breaking glass
            int popupsHidden = Game.Popups.ActiveCount;
            bool scoreOk = Game.Score.Score > scoreHidden;
            int drawnAfterSpawns = CountDrawnGraphics(false);
            yield return PressKey(Key.X);
            bool clearOk = Game.Shards.ActiveCount == 0;
            bool physicsOk = Time.fixedTime > fixedTimeBefore;
            Check("U-03 game unaffected while hidden", untouched && resetOk && structureOk && weaponOk && slowOk && overlayOk && scoreOk && clearOk && physicsOk,
                "toggle kept build count / time scale / pause / cursor / F1 state (" + untouched + "); keys while hidden: R " + resetOk + ", F6 " + structureOk +
                ", 1/2 " + weaponOk + ", T " + slowOk + ", F1 " + overlayOk + ", X " + clearOk + "; firing scored " + (Game.Score.Score - scoreHidden) +
                " points; physics time advanced " + (Time.fixedTime - fixedTimeBefore).ToString("0.00") + " s");
            Check("U-04 hidden state persists", !Game.Ui.Visible && drawnAfterSpawns == 0 && popupsHidden > 0,
                "after R, F6, F1 x2 and " + popupsHidden + " new score pop-ups spawned while hidden: drawn UI graphics " + drawnAfterSpawns);

            // --- Menus the player needs stay usable; a screenshot while hidden leaves even the menu out (U-05).
            yield return PressKey(Key.Escape);
            bool menuShown = Game.IsPaused && CountDrawnGraphics(true) > 0 && CountDrawnGraphics(false) == 0;
            // Batch mode renders nothing to capture, so there the same manager step is exercised directly.
            string pausedShot = null;
            if (headless) Game.Ui.PrepareScreenshot();
            else pausedShot = ScreenshotCapture.Capture(scenario + "_ui_hidden_paused");
            bool menuLeftOut = AlwaysVisibleDrawn() == 0;                  // this frame is the captured one
            yield return null;
            yield return null;
            bool menuBack = AlwaysVisibleDrawn() > 0 && Game.IsPaused;
            if (pausedShot != null) Line("  screenshot " + System.IO.Path.GetFileName(pausedShot));
            // F8 works at any moment, also with the pause menu open (U-03): show, then hide again; both checks
            // also require that the game is still paused.
            yield return PressKey(Key.F8);
            bool pausedShow = Game.IsPaused && Game.Ui.Visible && CountDrawn(Game.Hud.Canvas) > 0;
            yield return PressKey(Key.F8);
            bool pausedHide = Game.IsPaused && !Game.Ui.Visible && CountDrawnGraphics(false) == 0 && CountDrawnGraphics(true) > 0;
            yield return PressKey(Key.Escape);
            bool resumed = !Game.IsPaused && Mathf.Approximately(Time.timeScale, 1f);
            Check("U-02 menus stay usable", menuShown && menuLeftOut && menuBack && resumed,
                "Esc while hidden: pause menu drawn " + menuShown + ", left out of the F12 frame " + menuLeftOut + ", back next frame " + menuBack +
                ", Esc closed it " + resumed);
            Check("U-03 F8 in the pause menu", pausedShow && pausedHide,
                "F8 with the pause menu open: UI shown " + pausedShow + ", hidden again " + pausedHide + " (both require the game still paused with the menu drawn)");

            // --- F8 again: everything returns.
            yield return PressKey(Key.F8);
            int hudAfter = CountDrawn(Game.Hud.Canvas);
            bool canvasesBack = true;
            foreach (Canvas c in Game.Ui.HideableCanvases) canvasesBack &= c.enabled;
            bool overlayBack = CountDrawn(OverlayCanvas()) > 0;
            Check("U-06 all UI returns", Game.Ui.Visible && canvasesBack && hudAfter == hudBefore && overlayBack && CountDrawnGraphics(false) > 0,
                "second F8: every registered canvas enabled " + canvasesBack + ", HUD graphics " + hudBefore + " -> " + hudAfter +
                ", F1 overlay drawn again " + overlayBack + ", drawn UI graphics " + CountDrawnGraphics(false) + " (" + Game.Ui.HideableCanvases.Count +
                " hideable canvases, " + Game.Ui.AlwaysVisibleCanvases.Count + " always-visible)");
            Check("U-05 screenshot follows state", headless || (pausedShot != null && menuLeftOut),
                headless ? "batch mode has no rendered frame to capture; checked in the rendered run" :
                "F12 captures the rendered frame; ui_hidden / ui_hidden_paused were taken with 0 UI graphics drawn");
            yield return Shot("ui_shown_again");

            if (!overlayWasOn) yield return PressKey(Key.F1);               // leave the overlay as it was before the check
            settings.backgroundBehavior = oldBackground;
            settings.editorInputBehaviorInPlayMode = oldEditor;
            if (virtualKeyboard != null) { InputSystem.RemoveDevice(virtualKeyboard); virtualKeyboard = null; }
        }

        /// <summary>Queues a real key press + release through the Input System (read by GameInput next frame).</summary>
        private IEnumerator PressKey(Key key)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                if (virtualKeyboard == null) virtualKeyboard = InputSystem.AddDevice<Keyboard>("AutoTestKeyboard");
                kb = virtualKeyboard;
            }
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        private static Canvas OverlayCanvas()
        {
            foreach (Canvas c in Game.Ui.HideableCanvases) if (c.name == "DebugOverlay") return c;
            return null;
        }

        private static int AlwaysVisibleDrawn()
        {
            int n = 0;
            foreach (Canvas c in Game.Ui.AlwaysVisibleCanvases) n += CountDrawn(c);
            return n;
        }

        /// <summary>Graphics under 'canvas' that are actually drawn (component, objects and canvases all enabled).</summary>
        private static int CountDrawn(Canvas canvas)
        {
            if (canvas == null) return 0;
            int n = 0;
            foreach (Graphic g in canvas.GetComponentsInChildren<Graphic>(false))
                if (IsDrawn(g)) n++;
            return n;
        }

        /// <summary>Every uGUI graphic in the scene that is drawn; the always-visible menus only when asked for.</summary>
        private static int CountDrawnGraphics(bool includeMenus)
        {
            int n = 0;
            foreach (Graphic g in FindObjectsByType<Graphic>(FindObjectsInactive.Exclude))
            {
                if (!IsDrawn(g)) continue;
                if (!includeMenus && IsMenu(g.canvas.rootCanvas)) continue;
                n++;
            }
            return n;
        }

        private static bool IsMenu(Canvas root)
        {
            foreach (Canvas c in Game.Ui.AlwaysVisibleCanvases) if (c == root) return true;
            return false;
        }

        private static bool IsDrawn(Graphic g)
        {
            if (!g.enabled || !g.gameObject.activeInHierarchy || g.color.a <= 0.001f) return false;
            Canvas c = g.canvas;
            return c != null && c.isActiveAndEnabled && c.rootCanvas.isActiveAndEnabled;
        }
    }
}
