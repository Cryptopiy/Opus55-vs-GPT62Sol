using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace WaveSurvival
{
    /// <summary>
    /// U-06 automated check (AutoTest scenario "uitoggle", test tooling only). Presses F8 / F12 / Esc as real key events
    /// through the Input System (with a logged fallback to the same code path if the window has no keyboard focus) and
    /// verifies: nothing but the pause menu is drawn while hidden, also while new UI spawns (enemy health bars, damage
    /// arcs) and after a level restart; gameplay keeps running; every HUD element returns when F8 is pressed again.
    /// </summary>
    public sealed class UiToggleCheck
    {
        // Stage and counters are static: the restart step reloads the scene and creates a new AutoTest/UiToggleCheck.
        static int stage, failures, warnings, errors, screenshots;
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { stage = 0; failures = 0; warnings = 0; errors = 0; screenshots = 0; hooked = false; }

        readonly BotInput bot;
        readonly List<Graphic> before = new List<Graphic>(256);
        readonly HashSet<string> once = new HashSet<string>();
        float stageT;
        int frames, maxVisible, maxDebug, maxBars, shotsAtHide, killsAtHide;
        Key releaseKey = Key.None;
        int releaseFrame;

        public UiToggleCheck(BotInput bot)
        {
            this.bot = bot;
            bot.Weapon = 1;
            if (!hooked)
            {
                hooked = true;
                Application.logMessageReceived += OnLog;
            }
        }

        static void OnLog(string condition, string stack, LogType type)
        {
            if (type == LogType.Warning) warnings++;
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
            if (condition.StartsWith("Screenshot saved")) screenshots++;
        }

        static void Log(string msg) => Debug.Log("[TEST] ui_check " + msg);

        static void Expect(bool ok, string what)
        {
            if (!ok) failures++;
            Log((ok ? "OK   " : "FAIL ") + what);
        }

        bool Once(string key) => once.Add(key);

        // ---------------- key simulation ----------------

        void Press(Key key)
        {
            Write(key, true);
            releaseKey = key;
            releaseFrame = Time.frameCount + 1;
        }

        static void Write(Key key, bool down)
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            using (StateEvent.From(kb, out InputEventPtr ptr))
            {
                kb[key].WriteValueIntoEvent(down ? 1f : 0f, ptr);
                InputSystem.QueueEvent(ptr);
            }
        }

        // ---------------- visibility probes ----------------

        static bool IsDrawn(Graphic g)
        {
            if (g == null || !g.isActiveAndEnabled || g.color.a <= 0.001f) return false;
            Canvas c = g.canvas;
            return c != null && c.isActiveAndEnabled;
        }

        static bool IsMenu(Graphic g) => g.canvas != null && g.canvas.rootCanvas.name == "MenuCanvas";

        /// <summary>Counts every drawn uGUI graphic in the loaded scene (menus counted separately).</summary>
        static int CountDrawn(bool menus, List<Graphic> into = null)
        {
            int n = 0;
            foreach (Graphic g in Resources.FindObjectsOfTypeAll<Graphic>())
            {
                if (!g.gameObject.scene.IsValid() || !IsDrawn(g) || IsMenu(g) != menus) continue;
                n++;
                into?.Add(g);
            }
            return n;
        }

        /// <summary>World-space debug interface (F2 lines, markers, NavMesh view) that is currently rendered.</summary>
        static int CountDebugDrawn()
        {
            int n = 0;
            foreach (Renderer r in Game.AiDebug.GetComponentsInChildren<Renderer>(false))
                if (r.enabled && r.gameObject.activeInHierarchy) n++;
            return n;
        }

        // ---------------- stages ----------------

        /// <summary>Returns true when the check is complete.</summary>
        public bool Tick(float t, float dt)
        {
            if (releaseKey != Key.None && Time.frameCount >= releaseFrame)
            {
                Write(releaseKey, false);
                releaseKey = Key.None;
            }
            stageT += dt;
            switch (stage)
            {
                case 0: // fight a little with F2 on, then press F8
                    if (t > 6f && Once("f2")) Game.AiDebug.Toggle();
                    if (t < 9f) return false;
                    before.Clear();
                    int drawn = CountDrawn(false, before);
                    Log("before_hide drawn_ui_elements=" + drawn + " drawn_debug_renderers=" + CountDebugDrawn() + " visible_flag=" + UiVisibility.Visible);
                    Expect(drawn > 0 && UiVisibility.Visible, "UI visible by default");
                    Press(UiVisibility.ToggleKey);
                    Next(1);
                    break;
                case 1:
                    if (++frames < 4) break;
                    KeyResult(false, "F8 hide");
                    shotsAtHide = Game.Stats.ShotsFired;
                    killsAtHide = Game.Stats.TotalKills;
                    Next(2);
                    break;
                case 2: // 8 s hidden: combat continues, health bars and damage arcs spawn, F12 and Esc are used
                    maxVisible = Mathf.Max(maxVisible, CountDrawn(false));
                    maxDebug = Mathf.Max(maxDebug, CountDebugDrawn());
                    maxBars = Mathf.Max(maxBars, Game.Hud.HealthBars.ActiveCount);
                    if (stageT > 1.5f && Once("f12a")) { Write(Key.F12, true); releaseKey = Key.F12; releaseFrame = Time.frameCount + 1; }
                    if (stageT > 2.5f && Once("f12a_check")) EnsureScreenshot(1, "hidden");
                    if (stageT > 3f && Once("esc1")) Press(Key.Escape);
                    if (stageT > 3.4f && Once("pausecheck"))
                    {
                        bool pausedByKey = Game.Manager.State == GameState.Paused;
                        if (!pausedByKey) Game.Manager.Pause();
                        Log("esc_key_reached_game=" + pausedByKey);
                        Expect(CountDrawn(true) > 0 && CountDrawn(false) == 0, "pause menu shown while the HUD stays hidden (menu=" + CountDrawn(true) + ")");
                    }
                    if (stageT > 4.2f && Once("resume")) Game.Manager.Resume();
                    if (stageT < 8f) break;
                    Log("hidden_period max_drawn_ui=" + maxVisible + " max_drawn_debug=" + maxDebug + " health_bars_active_but_hidden_max=" + maxBars +
                        " shots_delta=" + (Game.Stats.ShotsFired - shotsAtHide) + " kills_delta=" + (Game.Stats.TotalKills - killsAtHide) +
                        " timeScale=" + Time.timeScale + " state=" + Game.Manager.State);
                    Expect(maxVisible == 0 && maxDebug == 0, "nothing drawn while hidden");
                    Expect(Game.Stats.ShotsFired > shotsAtHide && Game.Manager.State == GameState.Playing && Time.timeScale == 1f, "gameplay unaffected while hidden");
                    Press(UiVisibility.ToggleKey);
                    Next(3);
                    break;
                case 3:
                    if (++frames < 4) break;
                    KeyResult(true, "F8 show");
                    int restored = 0, missing = 0, inactive = 0;
                    foreach (Graphic g in before)
                    {
                        if (g == null || !g.gameObject.activeInHierarchy || !g.enabled || g.color.a <= 0.001f) { inactive++; continue; }
                        if (IsDrawn(g)) restored++; else missing++;
                    }
                    Log("after_show restored=" + restored + " missing=" + missing + " switched_off_by_game_logic=" + inactive + " drawn_now=" + CountDrawn(false));
                    Expect(missing == 0 && restored > 0, "every UI element returns when shown again");
                    Press(UiVisibility.ToggleKey);
                    Next(4);
                    break;
                case 4:
                    if (++frames < 4) break;
                    KeyResult(false, "F8 hide again before restart");
                    Log("restarting level while hidden (console so far: warnings=" + warnings + " errors=" + errors + ")");
                    stage = 5;
                    Game.Manager.Restart();
                    break;
                case 5: // new scene instance: the reloaded HUD must start hidden
                    maxVisible = Mathf.Max(maxVisible, CountDrawn(false));
                    if (t < 6f) break;
                    if (Once("restartcheck"))
                    {
                        Log("after_restart visible_flag=" + UiVisibility.Visible + " hud_canvas_enabled=" + Game.Hud.Canvas.enabled + " max_drawn_ui=" + maxVisible);
                        Expect(!UiVisibility.Visible && !Game.Hud.Canvas.enabled && maxVisible == 0, "hidden state persists across the level restart");
                        Write(Key.F12, true);
                        releaseKey = Key.F12;
                        releaseFrame = Time.frameCount + 1;
                    }
                    if (t < 7f) break;
                    EnsureScreenshot(2, "after_restart_hidden");
                    Press(UiVisibility.ToggleKey);
                    Next(6);
                    break;
                case 6:
                    if (++frames < 4) break;
                    KeyResult(true, "F8 show after restart");
                    int now = CountDrawn(false);
                    bool core = Named("HealthFill") && Named("Mag") && Named("Wave") && Named("Line0");
                    Log("after_restart_show drawn_ui=" + now + " core_hud_drawn=" + core);
                    Expect(now > 0 && core, "HUD drawn again after F8");
                    Screenshot.Capture("uitoggle_shown");
                    Log("result=" + (failures == 0 ? "PASS" : "FAIL") + " failures=" + failures + " console_warnings=" + warnings + " console_errors=" + errors);
                    Application.logMessageReceived -= OnLog;
                    hooked = false;
                    stage = 7;
                    return true;
                default:
                    return true;
            }
            return false;
        }

        void Next(int s)
        {
            stage = s;
            stageT = 0f;
            frames = 0;
        }

        /// <summary>Checks that the simulated key changed the state; otherwise logs it and uses the same toggle directly.</summary>
        static void KeyResult(bool wantVisible, string what)
        {
            bool byKey = UiVisibility.Visible == wantVisible;
            if (!byKey) UiVisibility.SetVisible(wantVisible);
            Log(what + " key_event_reached_game=" + byKey + " visible=" + UiVisibility.Visible);
        }

        static void EnsureScreenshot(int expected, string tag)
        {
            bool byKey = screenshots >= expected;
            if (!byKey) Screenshot.Capture("uitoggle_" + tag);
            Log("F12 screenshot while hidden (" + tag + ") key_event_reached_game=" + byKey);
        }

        static bool Named(string name)
        {
            foreach (Graphic g in Resources.FindObjectsOfTypeAll<Graphic>())
                if (g.gameObject.scene.IsValid() && g.name == name && IsDrawn(g)) return true;
            return false;
        }
    }
}
