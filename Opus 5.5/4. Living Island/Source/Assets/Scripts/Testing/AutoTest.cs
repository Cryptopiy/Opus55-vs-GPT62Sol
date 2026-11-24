using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace LivingIsland
{
    /// <summary>
    /// Automated self-check driver, active only with the command-line flag "-autotest &lt;scenario&gt;":
    ///   acceptance : the six steps of the acceptance script with screenshots and measured values
    ///   perf       : uncapped FPS benchmark (clear noon, then storm at night), under 60 s
    ///   ui         : the F8 "hide all UI" switch (AutoTestUI.cs)
    ///   soak1..3   : ~110 s of scripted normal play each (walking, swimming, weather, time, menus, seeds)
    /// Every Unity warning/error/exception during the run is counted and reported at the end.
    /// </summary>
    public sealed partial class AutoTest : MonoBehaviour
    {
        public static string Scenario { get; private set; }
        public static bool Requested
        {
            get
            {
                if (Scenario != null) return true;
                string[] args = Environment.GetCommandLineArgs();
                for (int i = 0; i < args.Length - 1; i++)
                    if (args[i] == "-autotest") Scenario = args[i + 1];
                return Scenario != null;
            }
        }

        int warnings, errors;
        readonly List<string> problems = new List<string>();
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        float frameAccum; int frameCount; readonly List<float> frameTimes = new List<float>(8192);
        bool sampling;

        void Awake() => Application.logMessageReceived += OnLog;
        void OnDestroy() => Application.logMessageReceived -= OnLog;

        void OnLog(string message, string stack, LogType type)
        {
            if (type == LogType.Warning) warnings++;
            else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
            else return;
            if (problems.Count < 20) problems.Add(type + ": " + message);
        }

        static void Log(string msg) => Debug.Log("[LI] TEST " + msg);
        static string F(float v, string fmt = "F2") => v.ToString(fmt, Inv);

        void Update()
        {
            if (!sampling) return;
            frameTimes.Add(Time.unscaledDeltaTime);
            frameAccum += Time.unscaledDeltaTime; frameCount++;
        }

        static bool restartPhase;

        IEnumerator Start()
        {
            yield return null;
            if (restartPhase)
            {
                // Second life of the "keys" and "ui" scenarios: the pause menu's Restart reloaded the scene.
                yield return new WaitForSeconds(2f);
                bool rebuilt = Game.World != null && Game.World.Placer.Palms == GameConfig.PalmCount && !Game.Paused && Time.timeScale == 1f;
                Log((rebuilt ? "PASS " : "FAIL ") + "Restart reloaded a fresh, playable island (palms " + (Game.World != null ? Game.World.Placer.Palms : 0) + ")");
                if (Scenario == "ui") yield return UiAfterRestart();
                Log("RESULT warnings=" + warnings + " errors=" + errors);
                for (int i = 0; i < problems.Count; i++) Log("problem: " + problems[i]);
                yield return new WaitForSecondsRealtime(0.5f);
                Application.Quit();
                yield break;
            }
            Log("scenario '" + Scenario + "' started, screen " + Screen.width + "x" + Screen.height);
            switch (Scenario)
            {
                case "perf": yield return Perf(); break;
                case "showcase": yield return Showcase(); break;
                case "keys": yield return Keys(); break;
                case "ui": yield return UiToggle(); break;
                case "audio": yield return AudioCheck(); break;
                case "hitch": yield return Hitch(); break;
                case "soak1": yield return Soak(1); break;
                case "soak2": yield return Soak(2); break;
                case "soak3": yield return Soak(3); break;
                default: yield return Acceptance(); break;
            }
            Log("RESULT warnings=" + warnings + " errors=" + errors);
            for (int i = 0; i < problems.Count; i++) Log("problem: " + problems[i]);
            yield return new WaitForSecondsRealtime(0.5f);
            Application.Quit();
        }

        // ------------------------------------------------------------------ helpers
        IEnumerator Shot(string tag)
        {
            yield return new WaitForEndOfFrame();
            ScreenshotService.CaptureWindow(tag);
            yield return null;
        }

        void Aim(Vector3 target)
        {
            Vector3 d = target - Game.Player.transform.position;
            Game.CameraRig.Yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        void StartSampling() { frameTimes.Clear(); frameAccum = 0f; frameCount = 0; sampling = true; }

        string StopSampling()
        {
            sampling = false;
            if (frameTimes.Count == 0) return "no frames";
            frameTimes.Sort();
            float avg = frameAccum / frameCount;
            float p99 = frameTimes[Mathf.Min(frameTimes.Count - 1, (int)(frameTimes.Count * 0.99f))];
            float worst = frameTimes[frameTimes.Count - 1];
            return "avg FPS " + F(1f / avg, "F1") + " (" + F(avg * 1000f) + " ms), 1% low FPS " + F(1f / p99, "F1") +
                   " (" + F(p99 * 1000f) + " ms), worst frame " + F(worst * 1000f, "F1") + " ms, frames " + frameCount;
        }

        Vector3 BayPoint() => new Vector3(Game.World.Data.Layout.Center.x + Game.World.Data.Layout.BayDir.x * 100f, 0f,
                                          Game.World.Data.Layout.Center.y + Game.World.Data.Layout.BayDir.y * 100f);

        /// <summary>A point far out at sea beyond the bay mouth (swim target).</summary>
        Vector3 OpenSeaPoint() => new Vector3(Game.World.Data.Layout.Center.x + Game.World.Data.Layout.BayDir.x * 235f, 0f,
                                              Game.World.Data.Layout.Center.y + Game.World.Data.Layout.BayDir.y * 235f);

        // ------------------------------------------------------------------ acceptance script
        IEnumerator Acceptance()
        {
            Game.TimeOfDay.SetHour(12f);
            Game.Weather.SnapTo(WeatherState.Clear);
            yield return new WaitForSeconds(3f);
            Game.UI.ToggleDebugOverlay();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("A1_noon_beach");
            Game.UI.ToggleDebugOverlay();
            Log(string.Format(Inv, "step1 noon clear: time {0:F2} h, player {1}, grass blades {2}, palms {3}", Game.TimeOfDay.Hour,
                Game.Player.transform.position, Game.Grass.DrawnBlades, Game.World.Placer.Palms));

            // Step 2: walk into the bay, swim 30 m, dive the camera.
            Vector3 bay = OpenSeaPoint();
            float t0 = Time.time;
            Vector3 swimStart = Vector3.zero;
            bool swimming = false;
            while (Time.time - t0 < 45f)
            {
                Aim(bay);
                GameInput.ScriptedMove = Vector2.up;
                if (!swimming && Game.Player.IsSwimming)
                {
                    swimming = true;
                    swimStart = Game.Player.transform.position;
                    Log(string.Format(Inv, "step2 started swimming after {0:F1} s at depth {1:F2} m", Time.time - t0, Game.Player.WaterDepthHere));
                }
                if (swimming)
                {
                    Vector3 d = Game.Player.transform.position - swimStart; d.y = 0f;
                    if (d.magnitude >= 30f) break;
                }
                yield return null;
            }
            Vector3 swum = Game.Player.transform.position - swimStart; swum.y = 0f;
            Log(string.Format(Inv, "step2 swam {0:F1} m, swimming={1}, speed {2:F2} m/s, surface {3:F2} feet {4:F2}", swum.magnitude,
                Game.Player.IsSwimming, Game.Player.HorizontalSpeed, Game.Player.WaterSurfaceHere, Game.Player.transform.position.y));
            yield return Shot("A2_swimming");
            GameInput.ScriptedMove = Vector2.zero;
            yield return new WaitForSeconds(0.5f);
            Game.CameraRig.Pitch = -32f;
            yield return new WaitForSeconds(1.2f);
            Log("step2 camera underwater=" + Game.Atmosphere.CameraUnderwater);
            yield return Shot("A3_underwater");
            Game.CameraRig.Pitch = 12f;

            // Step 3: dusk, then night at the campfire looking towards the lighthouse.
            FindAnyObjectByType<GameBootstrap>().RespawnPlayer();
            Aim(Game.World.Lighthouse != null ? Game.World.Lighthouse.transform.position : BayPoint());
            Game.CameraRig.Pitch = 8f;
            Game.TimeOfDay.SetHour(17.85f);
            yield return new WaitForSeconds(2f);
            yield return Shot("A4_dusk");
            Game.TimeOfDay.SetHour(22.5f);
            yield return new WaitForSeconds(5f);
            yield return Shot("A5_night");
            Log(string.Format(Inv, "step3 night: stars {0}, fireflies {1}, campfire light {2:F2}, lighthouse beam {3:F2}, night factor {4:F2}",
                Game.Sky.StarCount, Game.Fireflies.Count, Game.World.Campfire.FireLight.intensity,
                Game.World.Lighthouse != null ? Game.World.Lighthouse.BeamVisibility : -1f, Game.TimeOfDay.NightFactor));

            // Step 4: Clear -> Rain -> Storm -> Fog.
            Game.TimeOfDay.SetHour(13f);
            Game.CameraRig.Yaw += 140f;
            yield return new WaitForSeconds(1f);
            Game.Weather.SetState(WeatherState.Rain);
            yield return new WaitForSeconds(4f);
            Log(string.Format(Inv, "step4 rain blend at 4 s: {0:F0}% cloud {1:F2}", Game.Weather.TransitionProgress * 100f, Game.Weather.Current.CloudCoverage));
            yield return Shot("A6_rain_transition");
            yield return new WaitForSeconds(4.5f);
            Log(string.Format(Inv, "step4 rain: particles {0}, wetness {1:F2}, fog density {2:F4}", Game.Rain.RainParticles, Game.Weather.Wetness, Game.Weather.Current.FogDensity));
            yield return Shot("A7_rain");
            Game.Weather.SetState(WeatherState.Storm);
            yield return new WaitForSeconds(8.5f);
            Game.Lightning.Strike();
            yield return new WaitForSeconds(0.18f);   // after the 0.15 s flash, while the bolt is still visible
            yield return Shot("A8_storm_lightning");
            Log(string.Format(Inv, "step4 storm: wind x{0:F2}, waves x{1:F2}, rain particles {2}, strikes {3}, last strike {4:F0} m",
                Game.Weather.Current.Wind, Game.Weather.Current.WaveScale, Game.Rain.RainParticles, Game.Lightning.StrikeCount, Game.Lightning.LastStrikeDistance));
            Game.Weather.SetState(WeatherState.Fog);
            yield return new WaitForSeconds(8.5f);
            yield return Shot("A9_fog");
            Log(string.Format(Inv, "step4 fog: visibility {0:F0} m", 3.912f / Game.Weather.Current.FogDensity));

            // Step 5: photo mode with DOF and a 3840x2160 capture.
            Game.Weather.SnapTo(WeatherState.Clear);
            Game.TimeOfDay.SetHour(16.8f);
            FindAnyObjectByType<GameBootstrap>().RespawnPlayer();
            yield return new WaitForSeconds(1.5f);
            Game.Photo.SetActive(true);
            Game.Photo.Fov = 40f;
            Game.Post.DofFocusDistance = 4.5f;
            Game.Post.DofAperture = 2.2f;
            yield return new WaitForSeconds(1f);
            Game.Photo.CaptureHighRes();
            string path = Game.Photo.LastCapture;
            Log("step5 photo capture " + path + " " + PngSize(path));
            Game.Photo.SetActive(false);

            // Step 6: seed 778.
            float seconds = FindAnyObjectByType<GameBootstrap>().Regenerate(778);
            yield return new WaitForSeconds(2f);
            Log(string.Format(Inv, "step6 seed 778 regenerated in {0:F2} s: {1}", seconds, Game.World.ValidationSummary));
            Log("step6 report " + Game.World.Report);
            yield return Shot("A10_seed778");
        }

        // ------------------------------------------------------------------ hotkeys through real Input System events
        IEnumerator Press(Key key, int holdFrames = 1)
        {
            var kb = Keyboard.current;
            InputSystem.QueueStateEvent(kb, new KeyboardState(key));
            for (int i = 0; i < holdFrames; i++) yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
        }

        IEnumerator Hold(float seconds, params Key[] keys)
        {
            var kb = Keyboard.current;
            InputSystem.QueueStateEvent(kb, new KeyboardState(keys));
            yield return new WaitForSeconds(seconds);
        }

        void Check(string what, bool ok)
        {
            Log((ok ? "PASS " : "FAIL ") + what);
            if (!ok) errorsFromChecks++;
        }

        int errorsFromChecks;

        IEnumerator Keys()
        {
            // The test window is usually not focused: let the Input System keep processing keyboard events.
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            if (Keyboard.current == null) InputSystem.AddDevice<Keyboard>();
            Game.TimeOfDay.SetHour(12f);
            yield return new WaitForSeconds(2.5f);

            bool before = Game.UI.DebugVisible;
            yield return Press(Key.F1);
            Check("F1 toggles the debug overlay", Game.UI.DebugVisible != before);
            yield return Press(Key.F1);

            string last = ScreenshotService.LastPath;
            yield return Press(Key.F12);
            yield return new WaitForSeconds(0.5f);
            Check("F12 saves a timestamped PNG to Screenshots (" + ScreenshotService.LastPath + ")",
                  ScreenshotService.LastPath != last && File.Exists(ScreenshotService.LastPath));

            yield return Press(Key.Tab);
            Check("Tab opens the settings panel", Game.UI.SettingsOpen);
            yield return Press(Key.Tab);
            Check("Tab closes the settings panel", !Game.UI.SettingsOpen);

            yield return Press(Key.Escape);
            Check("Esc opens the pause menu (time scale " + Time.timeScale + ")", Game.Paused && Time.timeScale == 0f);
            yield return Press(Key.Escape);
            Check("Esc resumes", !Game.Paused && Time.timeScale == 1f);

            bool tPaused = Game.TimeOfDay.Paused;
            yield return Press(Key.T);
            Check("T pauses the day cycle", Game.TimeOfDay.Paused != tPaused);
            float h0 = Game.TimeOfDay.Hour;
            yield return new WaitForSeconds(1f);
            Check("time frozen while paused", Mathf.Approximately(h0, Game.TimeOfDay.Hour));
            yield return Press(Key.T);

            Key[] digits = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };
            for (int i = 0; i < 5; i++)
            {
                yield return Press(digits[i]);
                Check("key " + (i + 1) + " sets weather " + (WeatherState)i, Game.Weather.State == (WeatherState)i);
            }
            yield return Press(Key.Digit0);
            Check("0 toggles auto weather", Game.Weather.AutoMode);
            yield return Press(Key.Digit0);
            Check("0 toggles auto weather off", !Game.Weather.AutoMode);
            yield return Press(Key.Digit1);
            Game.Weather.SnapTo(WeatherState.Clear);

            Vector3 p0 = Game.Player.transform.position;
            yield return Hold(1.2f, Key.W);
            float walk = Game.Player.HorizontalSpeed;
            yield return Hold(1.2f, Key.W, Key.LeftShift);
            float run = Game.Player.HorizontalSpeed;
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return new WaitForSeconds(0.6f);
            Check(string.Format(Inv, "W walks at {0:F2} m/s, Shift runs at {1:F2} m/s, moved {2:F1} m", walk, run, Vector3.Distance(p0, Game.Player.transform.position)),
                  Mathf.Abs(walk - GameConfig.WalkSpeed) < 0.15f && Mathf.Abs(run - GameConfig.RunSpeed) < 0.25f);
            yield return Press(Key.Space);
            yield return new WaitForSeconds(0.15f);
            Check("Space jumps (vertical velocity " + F(Game.Player.Velocity.y) + ")", Game.Player.Velocity.y > 1f);
            yield return new WaitForSeconds(1.2f);

            yield return Press(Key.P);
            Check("P enters photo mode (player frozen, HUD hidden)", Game.Photo.Active && Game.Player.Frozen);
            Vector3 c0 = Game.MainCamera.transform.position;
            yield return Hold(0.8f, Key.W);
            yield return Hold(0.6f, Key.E);
            InputSystem.QueueStateEvent(Keyboard.current, new KeyboardState());
            yield return null;
            Check("photo camera flies with WASD/E (moved " + F(Vector3.Distance(c0, Game.MainCamera.transform.position)) + " m)",
                  Vector3.Distance(c0, Game.MainCamera.transform.position) > 3f);
            string cap = Game.Photo.LastCapture;
            yield return Press(Key.F12);
            Check("F12 in photo mode writes 3840x2160 " + PngSize(Game.Photo.LastCapture), Game.Photo.LastCapture != cap && PngSize(Game.Photo.LastCapture).StartsWith("(3840x2160"));
            yield return Press(Key.P);
            Check("P leaves photo mode", !Game.Photo.Active && !Game.Player.Frozen);

            foreach (QualityPreset q in new[] { QualityPreset.Low, QualityPreset.Medium, QualityPreset.High })
            {
                Game.Quality.Apply(q);
                yield return new WaitForSeconds(1.5f);
                var asset = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
                Log(string.Format(Inv, "preset {0}: grass budget {1}, drawn {2}, shadow distance {3} m, cascades {4}, render scale {5:F2}, MSAA {6}, LOD bias {7:F1}",
                    QualityManager.Name(q), Game.Grass.Budget, Game.Grass.DrawnBlades, asset.shadowDistance, asset.shadowCascadeCount,
                    asset.renderScale, asset.msaaSampleCount, QualitySettings.lodBias));
            }
            Log("keys checks failed: " + errorsFromChecks);

            // Pause menu -> Restart (same call as the button); the next AutoTest instance checks the result.
            yield return Press(Key.Escape);
            Log("RESULT warnings=" + warnings + " errors=" + errors + " (before restart)");
            restartPhase = true;
            FindAnyObjectByType<GameBootstrap>().Restart();
            yield return new WaitForSeconds(30f);
        }

        // ------------------------------------------------------------------ showcase shots (visual verification)
        static float YawTo(Vector3 from, Vector3 to) => Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        Vector3 FindSurface(SurfaceType wanted, float minH, float maxH, Vector2 near, float radius)
        {
            var d = Game.World.Data;
            var rng = new DetRandom(d.Seed, 4711);
            for (int i = 0; i < 4000; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), r = rng.Range(0f, radius);
                float x = near.x + Mathf.Cos(a) * r, z = near.y + Mathf.Sin(a) * r;
                Vector3 p = new Vector3(x, d.HeightAt(x, z), z);
                if (p.y >= minH && p.y <= maxH && d.SurfaceAt(p) == wanted && d.SlopeDegAt(x, z) < 14f) return p;
            }
            return Game.World.SpawnPoint;
        }

        IEnumerator PhotoShot(string tag, Vector3 pos, Vector3 lookAt)
        {
            if (!Game.Photo.Active) Game.Photo.SetActive(true);
            Game.UI.SetPhotoPanelVisible(false);
            Vector3 d = lookAt - pos;
            float pitch = -Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg;
            Game.Photo.SetView(pos, YawTo(pos, lookAt), pitch);
            Game.Post.DofEnabled = false;
            yield return new WaitForSeconds(1.2f);
            yield return Shot(tag);
        }

        IEnumerator Showcase()
        {
            var d = Game.World.Data;
            Game.TimeOfDay.SetHour(10.5f);
            Game.Weather.SnapTo(WeatherState.Clear);
            yield return new WaitForSeconds(2.5f);

            // Meadow: grass blades, palms, bushes, flowers - with the F1 overlay for the grass count.
            Vector3 meadow = FindSurface(SurfaceType.Grass, 4f, 14f, d.Layout.Center, 90f);
            Game.Player.Teleport(meadow + Vector3.up * 0.1f, YawTo(meadow, new Vector3(d.Layout.Center.x, 0f, d.Layout.Center.y)) + 180f);
            Game.CameraRig.Yaw = Game.Player.transform.eulerAngles.y;
            Game.CameraRig.Pitch = 14f;
            Game.CameraRig.Snap();
            yield return new WaitForSeconds(2.5f);
            Game.UI.ToggleDebugOverlay();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("B1_meadow_overlay");
            Log("showcase meadow at " + meadow + ": grass blades " + Game.Grass.DrawnBlades + " (budget " + Game.Grass.Budget + ")");
            Game.UI.ToggleDebugOverlay();
            yield return Shot("B2_meadow");

            // Underwater: caustics on the sandy bay floor.
            Vector2 inner = d.Layout.Center + d.Layout.BayDir * (GameConfig.IslandDiameter * 0.5f - 20f);
            Vector3 under = new Vector3(inner.x, -1.4f, inner.y);
            yield return PhotoShot("B3_underwater_caustics", under, under + new Vector3(d.Layout.BayDir.x * 6f, -2.5f, d.Layout.BayDir.y * 6f));
            Log("showcase underwater=" + Game.Atmosphere.CameraUnderwater + " floor " + F(d.HeightAt(under.x, under.z)) + " m");

            // Sunset towards the sun from the beach.
            Game.TimeOfDay.SetHour(17.75f);
            Game.TimeOfDay.ComputeDirections();
            Vector3 sun = Game.TimeOfDay.SunDirection;
            Vector3 beach = Game.World.SpawnPoint + Vector3.up * 1.7f;
            yield return PhotoShot("B4_sunset", beach, beach + new Vector3(sun.x, 0.12f, sun.z) * 50f);

            // Night at the campfire.
            Game.TimeOfDay.SetHour(22f);
            Vector3 fire = Game.World.Campfire.transform.position;
            Vector3 toSpawn = (Game.World.SpawnPoint - fire); toSpawn.y = 0f;
            Vector3 camPos = fire + toSpawn.normalized * 5.5f + Vector3.up * 1.6f;
            yield return PhotoShot("B5_campfire_night", camPos, fire + Vector3.up * 0.6f);
            Log("showcase campfire light " + F(Game.World.Campfire.FireLight.intensity) + ", particles " + Game.World.Campfire.ActiveParticles + ", fireflies " + Game.Fireflies.Count);

            // Lighthouse at night, then in fog.
            if (Game.World.Lighthouse != null)
            {
                Vector3 lh = Game.World.Lighthouse.transform.position;
                Vector3 inland = new Vector3(d.Layout.Center.x - lh.x, 0f, d.Layout.Center.y - lh.z).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, inland);
                Vector3 view = lh + inland * 45f + side * 25f;
                view.y = d.HeightAt(view.x, view.z) + 6f;
                yield return PhotoShot("B6_lighthouse_night", view, lh + Vector3.up * 12f);
                Log("showcase lighthouse beam " + F(Game.World.Lighthouse.BeamVisibility) + " angle " + F(Game.World.Lighthouse.Angle, "F0") + " spot " + F(Game.World.Lighthouse.Spot.intensity, "F0"));
                Game.Weather.SnapTo(WeatherState.Fog);
                Game.TimeOfDay.SetHour(20.5f);
                yield return PhotoShot("B7_lighthouse_fog", view, lh + Vector3.up * 12f);
                Game.Weather.SnapTo(WeatherState.Clear);
                Game.TimeOfDay.SetHour(15.5f);
                yield return PhotoShot("B8_lighthouse_day", view, lh + Vector3.up * 10f);
            }
            // Rain ripple rings on the water surface (I-08), seen from just above the bay.
            Game.TimeOfDay.SetHour(13f);
            Game.Weather.SnapTo(WeatherState.Rain);
            Vector2 bayMid = d.Layout.Center + d.Layout.BayDir * (GameConfig.IslandDiameter * 0.5f - GameConfig.BayInlandDepth * 0.5f);
            Vector3 rippleCam = new Vector3(bayMid.x, 2.2f, bayMid.y);
            yield return PhotoShot("B10_rain_ripples", rippleCam, rippleCam + new Vector3(d.Layout.BayDir.x * 4f, -2.2f, d.Layout.BayDir.y * 4f));
            Game.Weather.SnapTo(WeatherState.Clear);
            Game.TimeOfDay.SetHour(15.5f);

            // Rowboat in the bay.
            if (Game.World.Boat != null)
            {
                Vector3 b = Game.World.Boat.transform.position;
                Vector3 bv = b + new Vector3(4f, 2.2f, 4f);
                bv.y = Mathf.Max(bv.y, d.HeightAt(bv.x, bv.z) + 1.5f);
                yield return PhotoShot("B9_rowboat", bv, b);
                Log("showcase boat height " + F(Game.World.Boat.Bob));
            }
            Game.Photo.SetActive(false);
        }

        static string PngSize(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return "(missing)";
            byte[] head = new byte[24];
            using (var fs = File.OpenRead(path)) fs.Read(head, 0, 24);
            int w = (head[16] << 24) | (head[17] << 16) | (head[18] << 8) | head[19];
            int h = (head[20] << 24) | (head[21] << 16) | (head[22] << 8) | head[23];
            return "(" + w + "x" + h + ", " + new FileInfo(path).Length / 1024 + " KB)";
        }

        // ------------------------------------------------------------------ benchmark
        IEnumerator Perf()
        {
            QualitySettings.vSyncCount = 0;   // single uncapped benchmark run (<= 60 s, GPU monitored)
            Game.Quality.Apply(QualityPreset.High);
            Game.TimeOfDay.SetHour(12f);
            Game.TimeOfDay.Paused = true;
            Game.Weather.SnapTo(WeatherState.Clear);
            Aim(BayPoint());
            yield return new WaitForSeconds(4f);
            StartSampling();
            float t0 = Time.time;
            while (Time.time - t0 < 16f) { Game.CameraRig.Yaw += Time.deltaTime * 12f; yield return null; }
            Log("perf clear noon " + Screen.width + "x" + Screen.height + " High: " + StopSampling() + ", grass " + Game.Grass.DrawnBlades);
            yield return Shot("P1_clear_noon");

            Game.TimeOfDay.SetHour(23f);
            Game.Weather.SnapTo(WeatherState.Storm);
            yield return new WaitForSeconds(4f);
            StartSampling();
            t0 = Time.time;
            while (Time.time - t0 < 16f) { Game.CameraRig.Yaw += Time.deltaTime * 12f; yield return null; }
            Log("perf storm night " + Screen.width + "x" + Screen.height + " High: " + StopSampling() + ", rain " + Game.Rain.RainParticles +
                ", strikes " + Game.Lightning.StrikeCount);
            yield return Shot("P2_storm_night");
        }

        // ------------------------------------------------------------------ soak (normal play)
        IEnumerator Soak(int variant)
        {
            Game.TimeOfDay.Speed = 6f;
            // Unscaled time: the scenario also opens the pause menu (time scale 0).
            float t0 = Time.unscaledTime;
            int step = 0;
            WeatherState[] cycle = { WeatherState.Cloudy, WeatherState.Rain, WeatherState.Storm, WeatherState.Fog, WeatherState.Clear };
            if (variant == 3) Game.Weather.SetAutoMode(true);
            bool sampling10 = false;
            while (Time.unscaledTime - t0 < 104f)
            {
                float t = Time.unscaledTime - t0;
                // Frame statistics exclude the first 10 s (I-34: no hitches after the first 10 s).
                if (!sampling10 && t > 10f) { StartSampling(); sampling10 = true; }
                // Wander: change heading every 4 s, run in bursts, jump now and then.
                if ((int)(t / 4f) != step)
                {
                    step = (int)(t / 4f);
                    Game.CameraRig.Yaw += (variant == 2 ? 15f : 70f) * ((step % 3) - 1);
                    if (variant != 3 && step % 4 == 0) Game.Weather.SetState(cycle[(step / 4) % cycle.Length]);
                    if (step % 5 == 2) Game.UI.ToggleDebugOverlay();
                    if (variant == 1 && step == 10) { Game.UI.SetSettingsOpen(true); }
                    if (variant == 1 && step == 11) { Game.Quality.Apply(QualityPreset.Medium); Game.UI.SetSettingsOpen(false); }
                    if (variant == 1 && step == 16) Game.Quality.Apply(QualityPreset.Low);
                    if (variant == 1 && step == 20) Game.Quality.Apply(QualityPreset.High);
                    if (variant == 2 && step == 6) Game.Photo.SetActive(true);
                    if (variant == 2 && step == 8) { Game.Photo.CaptureHighRes(); Game.Photo.SetActive(false); }
                    if (variant == 2 && step == 9) Aim(BayPoint());
                    if (variant == 2 && step == 18) Game.CameraRig.Pitch = -30f;
                    if (variant == 2 && step == 20) Game.CameraRig.Pitch = 10f;
                    if (variant == 3 && step == 8) Log("soak3 regen 779 in " + F(FindAnyObjectByType<GameBootstrap>().Regenerate(779)) + " s: " + Game.World.ValidationSummary);
                    if (variant == 3 && step == 16) Log("soak3 regen 777 in " + F(FindAnyObjectByType<GameBootstrap>().Regenerate(777)) + " s: " + Game.World.ValidationSummary);
                    if (variant == 3 && step == 20) { Game.UI.SetPaused(true); }
                    if (variant == 3 && step == 21) { Game.UI.SetPaused(false); }
                    if (step % 6 == 3) yield return Shot("S" + variant + "_" + step);
                }
                GameInput.ScriptedMove = variant == 2 && step >= 9 && step < 18 ? Vector2.up : new Vector2(Mathf.Sin(t * 0.3f) * 0.3f, 1f);
                GameInput.ScriptedRun = (step % 3) == 1;
                GameInput.ScriptedJump = (Time.frameCount % 240) == 0;
                yield return null;
                GameInput.ScriptedJump = false;
            }
            GameInput.ScriptedMove = Vector2.zero;
            GameInput.ScriptedRun = false;
            Log("soak" + variant + " " + StopSampling() + " | time " + F(Game.TimeOfDay.Hour) + " h, weather " + Game.Weather.State +
                ", swimming " + Game.Player.IsSwimming + ", grass " + Game.Grass.DrawnBlades);
        }
    }
}
