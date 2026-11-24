using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using Wpn = DestructionSandbox.GameConfig.Weapons;
using Ph = DestructionSandbox.GameConfig.Physics;

namespace DestructionSandbox
{
    /// <summary>
    /// Self-test that plays the acceptance script through the same action methods the keyboard uses.
    /// Only active when the process is started with "-autotest &lt;scenario&gt;" (never in normal play).
    /// Without a graphics device (batch mode) every frame advances exactly 1/60 s of game time
    /// (Time.captureDeltaTime) so long physics checks run faster than real time and deterministically.
    /// Writes a report to &lt;project&gt;/TestResults and quits.
    /// </summary>
    public sealed partial class AutoTest : MonoBehaviour
    {
        private readonly StringBuilder report = new StringBuilder(8192);
        private int failures;
        private int errorsLogged;
        private int warningsLogged;
        private bool headless;
        private string scenario;
        private float startReal;
        private float maxBodySpeed;
        private int belowGround;
        private int nanBodies;
        private bool finished;

        public static bool Requested(out string scenarioName)
        {
            scenarioName = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autotest")
                {
                    scenarioName = i + 1 < args.Length && !args[i + 1].StartsWith("-") ? args[i + 1] : "acceptance";
                    return true;
                }
            }
            return false;
        }

        private static float ArgFloat(string name, float fallback)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name && float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v))
                    return v;
            return fallback;
        }

        public void Begin(string scenarioName)
        {
            scenario = scenarioName;
            headless = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null;
            if (headless || ArgFloat("-fixedframes", 0f) > 0f) Time.captureDeltaTime = GameConfig.Timing.FixedTimestep;
            // Benchmark only (one short monitored run): measure without the VSync cap.
            if (ArgFloat("-uncapped", 0f) > 0f) QualitySettings.vSyncCount = 0;
            Application.logMessageReceived += OnLog;
            startReal = Time.realtimeSinceStartup;
            Line("AutoTest scenario '" + scenario + "'  headless=" + headless + "  device=" + SystemInfo.graphicsDeviceName +
                 "  screen=" + Screen.width + "x" + Screen.height + "  " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLog;
        }

        // Safety watchdog: a self-test process must never run longer than the allowed test time.
        private void Update()
        {
            if (finished || scenario == null) return;
            if (Time.realtimeSinceStartup - startReal > ArgFloat("-maxreal", 115f))
            {
                StopAllCoroutines();
                failures++;
                Line("ABORTED: real-time limit reached");
                Finish();
            }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            {
                errorsLogged++;
                report.Append("    [console ").Append(type).Append("] ").Append(condition).Append('\n');
            }
            else if (type == LogType.Warning)
            {
                warningsLogged++;
                report.Append("    [console warning] ").Append(condition).Append('\n');
            }
        }

        private void Line(string text)
        {
            report.Append(text).Append('\n');
            Debug.Log("[AutoTest] " + text);
        }

        private void Check(string id, bool pass, string evidence)
        {
            if (!pass) failures++;
            Line((pass ? "PASS " : "FAIL ") + id + " - " + evidence);
        }

        private IEnumerator Run()
        {
            yield return null;   // let every system finish its first frame
            switch (scenario)
            {
                case "stability": yield return Stability(ArgFloat("-watch", Ph.StabilityWatchTime)); break;
                case "acceptance": yield return Acceptance(ArgFloat("-watch", Ph.StabilityWatchTime)); break;
                case "collapse": yield return Collapse(ArgFloat("-measure", 20f)); break;
                case "soak": yield return Soak(ArgFloat("-minutes", 5f)); break;
                case "showcase": yield return Showcase(); break;
                case "ui": yield return UiToggle(); break;
                default: Line("Unknown scenario " + scenario); failures++; break;
            }
            Finish();
        }

        // ------------------------------------------------------------------ helpers

        private IEnumerator WaitGame(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end)
            {
                SampleBodies();
                yield return null;
            }
        }

        private void SampleBodies()
        {
            var blocks = Game.Structures.ActiveBlocks;
            for (int i = 0; i < blocks.Count; i++) Inspect(blocks[i].Body);
            for (Shard s = Game.Shards.Head; s != null; s = s.Next) if (!s.Shrinking) Inspect(s.Body);
        }

        private void Inspect(Rigidbody rb)
        {
            if (rb.IsSleeping()) return;
            Vector3 p = rb.position;
            if (float.IsNaN(p.x) || float.IsNaN(p.y) || float.IsNaN(p.z)) { nanBodies++; return; }
            maxBodySpeed = Mathf.Max(maxBodySpeed, rb.linearVelocity.magnitude);
            if (p.y < -0.6f) belowGround++;
        }

        private IEnumerator FireAt(WeaponType type, Vector3 target)
        {
            Game.Weapons.Select(type);
            Game.Player.LookAt(target);
            if (headless)
            {
                // Batch mode runs faster than real time while cooldowns use real time: wait the cooldown in game time.
                float wait = Game.Weapons.CooldownFraction(type) * WeaponController.Cooldown(type);
                if (wait > 0f) yield return WaitGame(wait);
                Game.Weapons.ResetCooldowns();
            }
            while (Game.Weapons.CooldownFraction(type) > 0f) yield return null;
            yield return null;
            bool fired = Game.Weapons.TryFire();
            if (!fired) Line("  (could not fire " + type + ")");
        }

        private void Screenshot(string tag)
        {
            if (headless) return;
            string path = ScreenshotCapture.Capture(scenario + "_" + tag);
            Line("  screenshot " + Path.GetFileName(path));
        }

        /// <summary>Screenshot, then wait two frames so the capture shows this state and not the next action.</summary>
        private IEnumerator Shot(string tag)
        {
            Screenshot(tag);
            yield return null;
            yield return null;
        }

        private int GameObjectCount()
        {
            return FindObjectsByType<Transform>(FindObjectsInactive.Include).Length;
        }

        private void Finish()
        {
            if (finished) return;
            finished = true;
            Line("");
            Check("D-27", maxBodySpeed <= Ph.MaxLinearVelocity + 0.5f && nanBodies == 0 && belowGround == 0,
                "max body speed " + maxBodySpeed.ToString("0.0") + " m/s (clamp 80), NaN bodies " + nanBodies + ", bodies below ground " + belowGround);
            Check("CONSOLE", errorsLogged == 0 && warningsLogged == 0, errorsLogged + " errors, " + warningsLogged + " warnings logged during the run");
            Line("Real time " + (Time.realtimeSinceStartup - startReal).ToString("0.0") + " s, game time " + Time.time.ToString("0.0") + " s");
            Line("RESULT " + (failures == 0 ? "ALL PASS" : failures + " FAILURE(S)"));

            string dir = Path.Combine(ScreenshotCapture.ProjectRoot, "TestResults");
            Directory.CreateDirectory(dir);
            string file = Path.Combine(dir, "AutoTest_" + scenario + "_" + (headless ? "headless_" : "gpu_") + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
            File.WriteAllText(file, report.ToString());
            Debug.Log("[AutoTest] report written to " + file);
            Time.captureDeltaTime = 0f;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(0);
#else
            Application.Quit(0);
#endif
        }
    }
}
