using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace LivingIsland
{
    /// <summary>
    /// "-autotest ui": the F8 "hide all UI" switch, driven by real Input System key events.
    /// A UI element counts as drawn when its Graphic is active and enabled, sits on an enabled root canvas and is
    /// not fully transparent; every Graphic in the scene is inspected (all canvases, world-space ones included).
    /// Checks: nothing is drawn while hidden, whatever panel is open; the F12 screenshot taken while hidden has no
    /// UI pixels; every hotkey and the gameplay keep working while hidden and F8 changes no game state; F8 also
    /// works while paused and while typing in a text field; the pause menu stays visible and clickable; the state
    /// survives respawn, a new island and Restart, and applies to UI created while hidden; F8 again brings all of
    /// the UI back.
    /// </summary>
    public sealed partial class AutoTest
    {
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        static bool IsDrawn(Graphic g)
        {
            if (g == null || !g.isActiveAndEnabled) return false;
            Canvas c = g.canvas;
            if (c == null || !c.isActiveAndEnabled) return false;
            Canvas root = c.rootCanvas;
            if (root == null || !root.isActiveAndEnabled || g.canvasRenderer.cull) return false;
            return g.color.a * g.canvasRenderer.GetInheritedAlpha() > 0.001f;
        }

        /// <summary>UI graphics drawn right now on any canvas (optionally not counting the pause menu).</summary>
        static int DrawnUi(bool withoutPauseMenu = false, HashSet<Graphic> into = null)
        {
            int n = 0;
            foreach (var g in FindObjectsByType<Graphic>(FindObjectsInactive.Exclude))
            {
                if (!IsDrawn(g) || (withoutPauseMenu && g.GetComponentInParent<PauseMenu>() != null)) continue;
                n++;
                into?.Add(g);
            }
            return n;
        }

        static bool AllDrawn(Component root)
        {
            if (root == null) return false;
            foreach (var g in root.GetComponentsInChildren<Graphic>()) if (!IsDrawn(g)) return false;
            return true;
        }

        /// <summary>Screen-pixel rectangle of an element on an overlay canvas (its world corners are screen pixels).</summary>
        static Rect ScreenRect(Component c)
        {
            var corners = new Vector3[4];
            ((RectTransform)c.transform).GetWorldCorners(corners);
            return Rect.MinMaxRect(corners[0].x, corners[0].y, corners[2].x, corners[2].y);
        }

        /// <summary>UI elements a mouse click at this screen position would reach.</summary>
        static int UiHits(Vector2 screenPos)
        {
            uiHits.Clear();
            if (EventSystem.current == null) return -1;
            EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screenPos }, uiHits);
            return uiHits.Count;
        }

        static Texture2D LoadPng(string path)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (File.Exists(path)) tex.LoadImage(File.ReadAllBytes(path));
            return tex;
        }

        /// <summary>Mean absolute RGB difference (0..1) of two same-size images inside a screen rectangle.</summary>
        static float PixelDiff(Texture2D a, Texture2D b, Rect r)
        {
            if (a.width != b.width || a.height != b.height || a.width < 4) return -1f;
            Color32[] pa = a.GetPixels32(), pb = b.GetPixels32();
            int x0 = Mathf.Clamp(Mathf.FloorToInt(r.xMin), 0, a.width), x1 = Mathf.Clamp(Mathf.CeilToInt(r.xMax), 0, a.width);
            int y0 = Mathf.Clamp(Mathf.FloorToInt(r.yMin), 0, a.height), y1 = Mathf.Clamp(Mathf.CeilToInt(r.yMax), 0, a.height);
            long sum = 0;
            int n = 0;
            for (int y = y0; y < y1; y++)
                for (int x = x0; x < x1; x++)
                {
                    int i = y * a.width + x;
                    sum += Mathf.Abs(pa[i].r - pb[i].r) + Mathf.Abs(pa[i].g - pb[i].g) + Mathf.Abs(pa[i].b - pb[i].b);
                    n++;
                }
            return n > 0 ? sum / (3f * 255f * n) : -1f;
        }

        static string StateSnapshot() => string.Format(Inv,
            "paused {0}, time scale {1}, settings {2}, F1 {3}, photo {4}, weather {5}{6}, quality {7}, seed {8}, day cycle paused {9}, player frozen {10}",
            Game.Paused, Time.timeScale, Game.UI.SettingsOpen, Game.UI.DebugVisible, Game.Photo.Active, Game.Weather.State,
            Game.Weather.AutoMode ? " (auto)" : "", Game.Quality.Current, Game.World.Data.Seed, Game.TimeOfDay.Paused, Game.Player.Frozen);

        /// <summary>Presses F8 and checks that only the drawing changed: same state, clock still running, player in place.</summary>
        IEnumerator PressF8KeepingState(string where)
        {
            string s0 = StateSnapshot();
            float h0 = Game.TimeOfDay.Hour;
            Vector3 p0 = Game.Player.transform.position;
            bool hidden0 = UIVisibility.Hidden;
            yield return Press(Key.F8);
            string s1 = StateSnapshot();
            float h1 = Game.TimeOfDay.Hour;
            float moved = Vector3.Distance(p0, Game.Player.transform.position);
            bool clockOk = Game.Paused || Game.TimeOfDay.Paused ? Mathf.Approximately(h0, h1) : h1 >= h0 || h0 - h1 > 23f;
            Check("F8 " + where + ": UI " + (UIVisibility.Hidden ? "hidden" : "shown") + ", game state unchanged (" + s1 + "), clock " +
                  F(h0, "F4") + " -> " + F(h1, "F4") + " h, player moved " + F(moved, "F3") + " m",
                  UIVisibility.Hidden != hidden0 && s0 == s1 && clockOk && moved < 0.05f);
        }

        IEnumerator UiToggle()
        {
            // The test window is usually not focused: let the Input System keep processing keyboard events.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            Game.TimeOfDay.SetHour(12f);
            Game.Weather.SnapTo(WeatherState.Clear);
            yield return new WaitForSeconds(2.5f);
            var boot = FindAnyObjectByType<GameBootstrap>();
            var hud = FindAnyObjectByType<Hud>();
            var overlay = FindAnyObjectByType<DebugOverlay>(FindObjectsInactive.Include);
            Check("UI visible by default (" + DrawnUi() + " graphics drawn, HUD complete)", !UIVisibility.Hidden && AllDrawn(hud));

            // Everything at once: HUD + F1 overlay + Tab settings panel, then F8 twice.
            yield return Press(Key.F1);
            yield return Press(Key.Tab);
            var shown = new HashSet<Graphic>();
            int shownCount = DrawnUi(false, shown);
            Vector2 hudCenter = ScreenRect(hud).center;
            int hitsShown = UiHits(hudCenter);
            yield return PressF8KeepingState("with HUD, F1 overlay and settings open");
            int hitsHidden = UiHits(hudCenter);
            Check("hidden: no UI element drawn (" + DrawnUi() + " of " + shownCount + "), pointer not drawn, hidden UI not clickable (raycast hits on the HUD " +
                  hitsHidden + ", " + hitsShown + " when shown)", UIVisibility.Hidden && DrawnUi() == 0 && !Cursor.visible && hitsHidden == 0 && hitsShown > 0);
            yield return PressF8KeepingState("again");
            var back = new HashSet<Graphic>();
            DrawnUi(false, back);
            Check("F8 again brings all of it back: " + back.Count + " of " + shownCount + " graphics (the same ones), pointer visible over the open settings panel",
                  !UIVisibility.Hidden && back.SetEquals(shown) && Cursor.visible);
            yield return Press(Key.Tab);

            // F12 with the UI (HUD + F1 overlay), then F8 and F12 again a few frames later.
            yield return Press(Key.F12);
            string withUi = ScreenshotService.LastPath;
            yield return Press(Key.F8);
            Check("F8 hides the UI before the second screenshot", UIVisibility.Hidden && DrawnUi() == 0);
            yield return Press(Key.F12);
            string noUi = ScreenshotService.LastPath;
            yield return new WaitForSecondsRealtime(0.6f);
            Texture2D a = LoadPng(withUi), b = LoadPng(noUi);
            float dHud = PixelDiff(a, b, ScreenRect(hud)), dOverlay = PixelDiff(a, b, ScreenRect(overlay));
            float dControl = PixelDiff(a, b, new Rect(Screen.width * 0.42f, Screen.height * 0.8f, Screen.width * 0.16f, Screen.height * 0.12f));
            Destroy(a);
            Destroy(b);
            Log("ui screenshot with UI: " + withUi + " " + PngSize(withUi) + " | while hidden: " + noUi + " " + PngSize(noUi));
            Check(string.Format(Inv, "F12 while hidden has no UI: pixel difference to the shot with UI {0:F3} in the HUD area, {1:F3} in the F1 overlay area, {2:F3} in a UI-free control area",
                  dHud, dOverlay, dControl), withUi != noUi && dHud > 0.05f && dOverlay > 0.05f && dControl >= 0f && dControl < 0.03f && Mathf.Min(dHud, dOverlay) > 3f * dControl);

            // Every hotkey and the gameplay keep working while hidden, and nothing becomes visible.
            bool dbg = Game.UI.DebugVisible;
            yield return Press(Key.F1);
            Check("hidden: F1 still toggles the debug overlay, nothing drawn", Game.UI.DebugVisible != dbg && DrawnUi() == 0);
            yield return Press(Key.F1);
            yield return Press(Key.Tab);
            Check("hidden: Tab opens settings (mouse look released as before, pointer not drawn), nothing drawn",
                  Game.UI.SettingsOpen && !Game.CameraRig.InputEnabled && !Cursor.visible && DrawnUi() == 0);
            yield return Press(Key.Tab);
            Check("hidden: Tab closes settings, mouse look back", !Game.UI.SettingsOpen && Game.CameraRig.InputEnabled && DrawnUi() == 0);

            bool tPaused = Game.TimeOfDay.Paused;
            yield return Press(Key.T);
            float h0 = Game.TimeOfDay.Hour;
            yield return new WaitForSeconds(1f);
            Check("hidden: T pauses the day cycle (" + F(h0, "F3") + " -> " + F(Game.TimeOfDay.Hour, "F3") + " h)",
                  Game.TimeOfDay.Paused != tPaused && Mathf.Approximately(h0, Game.TimeOfDay.Hour));
            yield return Press(Key.T);
            h0 = Game.TimeOfDay.Hour;
            yield return new WaitForSeconds(1f);
            Check("hidden: T resumes, the clock keeps running (+" + F((Game.TimeOfDay.Hour - h0) * 60f, "F1") + " game minutes in 1 s)",
                  Game.TimeOfDay.Paused == tPaused && Game.TimeOfDay.Hour > h0);

            Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
            bool weatherOk = true;
            for (int i = 0; i < 5; i++)
            {
                yield return Press(digits[i]);
                weatherOk &= Game.Weather.State == (WeatherState)i;
            }
            yield return Press(Key.Digit0);
            bool autoOn = Game.Weather.AutoMode;
            yield return Press(Key.Digit0);
            Check("hidden: 1-5 set the five weather states, 0 toggles auto weather on and off, nothing drawn",
                  weatherOk && autoOn && !Game.Weather.AutoMode && DrawnUi() == 0);
            yield return Press(Key.Digit1);
            Game.Weather.SnapTo(WeatherState.Clear);

            yield return Hold(1.2f, Key.W);
            float walk = Game.Player.HorizontalSpeed;
            yield return Hold(1.2f, Key.W, Key.LeftShift);
            float run = Game.Player.HorizontalSpeed;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return new WaitForSeconds(0.6f);
            yield return Press(Key.Space);
            yield return new WaitForSeconds(0.15f);
            float vy = Game.Player.Velocity.y;
            Check(string.Format(Inv, "hidden: W walks at {0:F2} m/s, Shift runs at {1:F2} m/s, Space jumps ({2:F2} m/s up), nothing drawn", walk, run, vy),
                  Mathf.Abs(walk - GameConfig.WalkSpeed) < 0.15f && Mathf.Abs(run - GameConfig.RunSpeed) < 0.25f && vy > 1f && DrawnUi() == 0);
            yield return new WaitForSeconds(1.2f);
            boot.RespawnPlayer();
            yield return null;
            Check("hidden: respawn keeps the UI hidden", UIVisibility.Hidden && DrawnUi() == 0);

            yield return Press(Key.P);
            Check("hidden: P enters photo mode (player frozen), photo panel and pointer not drawn",
                  Game.Photo.Active && Game.Player.Frozen && DrawnUi() == 0 && !Cursor.visible);
            Vector3 c0 = Game.MainCamera.transform.position;
            yield return Hold(0.8f, Key.W);
            yield return Hold(0.6f, Key.E);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            float flown = Vector3.Distance(c0, Game.MainCamera.transform.position);
            string cap = Game.Photo.LastCapture;
            yield return Press(Key.F12);
            Check("hidden: the photo camera flies (" + F(flown) + " m), F12 writes the 4K photo " + PngSize(Game.Photo.LastCapture) + ", nothing drawn",
                  flown > 3f && Game.Photo.LastCapture != cap && PngSize(Game.Photo.LastCapture).StartsWith("(3840x2160") && DrawnUi() == 0);
            yield return Press(Key.P);
            Check("hidden: P leaves photo mode", !Game.Photo.Active && !Game.Player.Frozen && DrawnUi() == 0);

            // The pause menu is a screen needed to continue: never hidden; F8 works while paused and keeps the pause.
            yield return Press(Key.Escape);
            int pauseDrawn = DrawnUi();
            Check("hidden: Esc opens the pause menu, which is drawn (" + pauseDrawn + " graphics, all of the pause menu) with a visible pointer",
                  Game.Paused && Time.timeScale == 0f && pauseDrawn > 0 && DrawnUi(true) == 0 && Cursor.visible);
            yield return PressF8KeepingState("while paused");
            Check("paused and shown: HUD back behind the pause menu", !UIVisibility.Hidden && Game.Paused && AllDrawn(hud) && DrawnUi() > pauseDrawn);
            yield return PressF8KeepingState("while paused, again");
            Check("paused and hidden: only the pause menu is drawn", Game.Paused && DrawnUi(true) == 0 && DrawnUi() == pauseDrawn);
            yield return Press(Key.Escape);
            Check("hidden: Esc resumes, nothing drawn, pointer not drawn", !Game.Paused && Time.timeScale == 1f && DrawnUi() == 0 && !Cursor.visible);

            // F8 works while typing in the seed field (other hotkeys are blocked while typing) and hiding ends the typing.
            yield return Press(Key.F8);
            yield return Press(Key.Tab);
            var field = FindAnyObjectByType<InputField>();
            if (field != null)
            {
                field.Select();
                field.ActivateInputField();
            }
            for (int i = 0; i < 3; i++) yield return null;
            bool typing = GameInput.TextEntryActive;
            bool tp = Game.TimeOfDay.Paused;
            yield return Press(Key.T);
            bool blocked = Game.TimeOfDay.Paused == tp;
            yield return PressF8KeepingState("while typing in the seed field");
            yield return null;
            Check("F8 works while typing (typing " + typing + ", T blocked " + blocked + ") and hiding ends the typing (typing now " + GameInput.TextEntryActive + ")",
                  field != null && typing && blocked && UIVisibility.Hidden && !GameInput.TextEntryActive && !field.isFocused);
            yield return Press(Key.T);
            Check("hidden: hotkeys work again after the field lost focus (T)", Game.TimeOfDay.Paused != tp);
            yield return Press(Key.T);
            yield return Press(Key.Tab);

            // The state survives a new island; UI created while hidden is hidden too.
            float regen = boot.Regenerate(778);
            yield return new WaitForSeconds(0.5f);
            Check("hidden: a new island (seed 778, " + F(regen) + " s) keeps the UI hidden", UIVisibility.Hidden && DrawnUi() == 0 && Game.World.Data.Seed == 778);
            Text popup = UIFactory.Label(hud.transform.parent, "Test popup", 32, TextAnchor.MiddleCenter, UIFactory.TextColor, 760, 480, 400, 60);
            var worldGo = new GameObject("Test floating text", typeof(RectTransform));
            var worldCanvas = worldGo.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldGo.transform.SetPositionAndRotation(Game.Player.transform.position + Vector3.up * 2.4f, Game.MainCamera.transform.rotation);
            worldGo.transform.localScale = Vector3.one * 0.01f;
            Text floating = UIFactory.Label(worldGo.transform, "Floating text", 40, TextAnchor.MiddleCenter, UIFactory.TextColor, 0, 0, 300, 60);
            UIVisibility.Register(worldCanvas);
            yield return null;
            Check("hidden: UI created while hidden stays hidden (a popup on the interface canvas, world-space floating text), drawn " + DrawnUi(),
                  !IsDrawn(popup) && !IsDrawn(floating) && DrawnUi() == 0);
            yield return PressF8KeepingState("after respawn, new island and new UI");
            Check("F8 brings all of it back: HUD, F1 overlay, the new popup and the floating text (" + DrawnUi() + " graphics)",
                  !UIVisibility.Hidden && AllDrawn(hud) && AllDrawn(overlay) && IsDrawn(popup) && IsDrawn(floating));
            Destroy(popup.gameObject);
            Destroy(worldGo);
            yield return PressF8KeepingState("before the restart");

            // Restart while hidden through the pause menu's own Restart button; the next AutoTest instance continues.
            Log("ui checks failed: " + errorsFromChecks);
            yield return Press(Key.Escape);
            Button restart = null;
            foreach (var btn in FindAnyObjectByType<PauseMenu>().GetComponentsInChildren<Button>())
                if (btn.name == "Button Restart") restart = btn;
            Vector2 at = ScreenRect(restart).center;
            int hits = UiHits(at);
            bool reaches = hits > 0 && uiHits[0].gameObject == restart.gameObject;
            Check("hidden: the pause menu's Restart button is drawn and a click reaches it (first raycast hit: " +
                  (hits > 0 ? uiHits[0].gameObject.name : "none") + ")", reaches && IsDrawn(restart.targetGraphic));
            Log("RESULT warnings=" + warnings + " errors=" + errors + " (before restart)");
            restartPhase = true;
            ExecuteEvents.Execute(restart.gameObject, new PointerEventData(EventSystem.current) { position = at }, ExecuteEvents.pointerClickHandler);
            yield return new WaitForSeconds(30f);
        }

        /// <summary>Second life of the "ui" scenario, after the scene was reloaded while the UI was hidden.</summary>
        IEnumerator UiAfterRestart()
        {
            var hud = FindAnyObjectByType<Hud>();
            Check("after Restart the rebuilt UI is still hidden (" + DrawnUi() + " graphics drawn)", UIVisibility.Hidden && hud != null && DrawnUi() == 0);
            yield return Press(Key.F8);
            Check("F8 shows the rebuilt UI (HUD complete, " + DrawnUi() + " graphics)", !UIVisibility.Hidden && AllDrawn(hud));
            Log("ui checks failed after restart: " + errorsFromChecks);
        }
    }
}
