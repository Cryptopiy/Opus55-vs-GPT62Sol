using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

namespace WaveSurvival
{
    /// <summary>
    /// Debug keys (section 4 + acceptance script): F1 overlay, F2 AI debug, F3 jump to wave 5 (boss),
    /// F4 god mode (invulnerable, for testing), F12 PNG screenshot into &lt;project&gt;/Screenshots/.
    /// </summary>
    public sealed class DevTools : MonoBehaviour
    {
        public static DevTools Create(Transform parent)
        {
            var go = new GameObject("DevTools");
            go.transform.SetParent(parent, false);
            return go.AddComponent<DevTools>();
        }

        void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) return;
            if (kb.f1Key.wasPressedThisFrame) Game.Hud.Overlay.Toggle();
            if (kb.f2Key.wasPressedThisFrame) Game.AiDebug.Toggle();
            if (kb.f3Key.wasPressedThisFrame) CheatBossWave();
            if (kb.f4Key.wasPressedThisFrame) ToggleGodMode();
            if (kb.f12Key.wasPressedThisFrame) Screenshot.Capture();
        }

        public static void CheatBossWave()
        {
            if (!Game.IsPlaying) return;
            Game.Waves.JumpToWave(GameConfig.Waves.BossWave);
            Game.Hud.Notify("CHEAT: JUMP TO WAVE 5", new Color(1f, 0.85f, 0.2f));
        }

        public static void ToggleGodMode()
        {
            Game.GodMode = !Game.GodMode;
            Game.Hud.Notify(Game.GodMode ? "GOD MODE ON" : "GOD MODE OFF", new Color(1f, 0.85f, 0.2f));
        }
    }

    /// <summary>Writes timestamped PNG screenshots to &lt;project&gt;/Screenshots/ (next to Assets in the editor, next to the exe in a build).</summary>
    public static class Screenshot
    {
        public static string Folder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Screenshots"));

        public static string Capture(string tag = null)
        {
            Directory.CreateDirectory(Folder);
            string name = "WaveSurvival_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + (string.IsNullOrEmpty(tag) ? "" : "_" + tag) + ".png";
            string file = Path.Combine(Folder, name);
            ScreenCapture.CaptureScreenshot(file);
            Debug.Log("Screenshot saved: " + file);
            return file;
        }
    }
}
