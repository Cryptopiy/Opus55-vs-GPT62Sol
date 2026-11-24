using System.Collections;
using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// "-autotest hitch" (I-34): normal play where every effect appears for the first time AFTER the first
    /// 10 s - each weather state incl. storm + lightning, dusk, night (stars, fireflies, beam), running,
    /// swimming, underwater camera - with no deliberate heavy actions (no regeneration, preset change or 4K
    /// capture). Reports the worst frame and how many frames took longer than 33 ms.
    /// </summary>
    public sealed partial class AutoTest
    {
        IEnumerator Hitch()
        {
            Game.TimeOfDay.SetHour(9f);
            Game.TimeOfDay.Speed = 1f;
            Game.Weather.SnapTo(WeatherState.Clear);
            float t0 = Time.unscaledTime;
            while (Time.unscaledTime - t0 < 10f) yield return null;     // startup + warm-up window

            int slowFrames = 0;
            float worst = 0f, sum = 0f;
            int frames = 0;
            // The five slowest frames with their time and phase, to attribute any spike.
            float[] topMs = new float[5];
            string[] topWhere = new string[5];
            WeatherState[] order = { WeatherState.Cloudy, WeatherState.Rain, WeatherState.Storm, WeatherState.Fog, WeatherState.Clear };
            Vector3 sea = OpenSeaPoint();
            float start = Time.unscaledTime;
            int phase = -1;
            while (Time.unscaledTime - start < 90f)
            {
                float t = Time.unscaledTime - start;
                int p = (int)(t / 9f);
                if (p != phase)
                {
                    phase = p;
                    if (p < order.Length) Game.Weather.SetState(order[p]);
                    if (p == 3) Game.Lightning.Strike();
                    if (p == 5) Game.TimeOfDay.SetHour(18.2f);       // dusk colours
                    if (p == 6) Game.TimeOfDay.SetHour(22.5f);       // night: stars, fireflies, beam
                    if (p == 8) Game.CameraRig.Pitch = -30f;         // camera under the water surface
                    if (p == 9) Game.CameraRig.Pitch = 10f;
                }
                // Run towards the open sea (beach -> shallows -> swimming).
                Aim(sea);
                GameInput.ScriptedMove = Vector2.up;
                GameInput.ScriptedRun = t < 30f;
                yield return null;
                float dt = Time.unscaledDeltaTime;
                worst = Mathf.Max(worst, dt);
                for (int k = 0; k < topMs.Length; k++)
                {
                    if (dt * 1000f <= topMs[k]) continue;
                    for (int m = topMs.Length - 1; m > k; m--) { topMs[m] = topMs[m - 1]; topWhere[m] = topWhere[m - 1]; }
                    topMs[k] = dt * 1000f;
                    topWhere[k] = string.Format(Inv, "{0:F1} ms at t={1:F1} s (phase {2}, frame {3}, GC count {4})",
                        dt * 1000f, Time.unscaledTime - start, phase, Time.frameCount, System.GC.CollectionCount(0));
                    break;
                }
                sum += dt;
                frames++;
                if (dt > 1f / 30f) slowFrames++;
            }
            GameInput.ScriptedMove = Vector2.zero;
            GameInput.ScriptedRun = false;
            Log(string.Format(Inv, "hitch: {0} frames after the first 10 s, average {1:F2} ms, worst frame {2:F1} ms, frames over 33 ms: {3} | swimming {4}, underwater seen {5}, strikes {6}, fireflies {7}",
                frames, sum / Mathf.Max(frames, 1) * 1000f, worst * 1000f, slowFrames, Game.Player.IsSwimming,
                Game.Atmosphere.CameraUnderwater || phase >= 8, Game.Lightning.StrikeCount, Game.Fireflies.Count));
            for (int k = 0; k < topMs.Length; k++) if (topWhere[k] != null) Log("hitch slow frame #" + (k + 1) + ": " + topWhere[k]);
        }
    }
}
