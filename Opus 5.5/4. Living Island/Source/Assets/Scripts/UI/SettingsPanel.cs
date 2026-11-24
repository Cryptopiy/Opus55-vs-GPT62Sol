using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Tab settings panel: island seed (shown, editable, Regenerate), time-of-day and day-speed sliders,
    /// time pause toggle, and the Low / Medium / High quality presets applied live (I-01, I-15, I-30).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        InputField seedField;
        Text status, timeValue, speedValue;
        Slider timeSlider, speedSlider;
        Toggle pauseToggle;
        readonly Button[] qualityButtons = new Button[3];
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public void Build(Transform canvas)
        {
            UIFactory.Rect(gameObject, canvas, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(24, 0), new Vector2(440, 560));
            var bg = UIFactory.Panel(transform, "Background", UIFactory.PanelColor);
            UIFactory.Rect(bg.gameObject, transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var t = transform;
            UIFactory.Label(t, "Settings", 26, TextAnchor.MiddleLeft, UIFactory.TextColor, 24, 14, 300, 36).fontStyle = FontStyle.Bold;
            UIFactory.Label(t, "Tab to close", 14, TextAnchor.MiddleRight, UIFactory.MutedText, 240, 20, 176, 24);

            UIFactory.Label(t, "Island seed", 16, TextAnchor.MiddleLeft, UIFactory.MutedText, 24, 62, 200, 22);
            seedField = UIFactory.Input(t, C.DefaultSeed.ToString(Inv), 24, 88, 220, 38);
            UIFactory.Button(t, "Regenerate", 256, 88, 160, 38, Regenerate);
            status = UIFactory.Label(t, "", 14, TextAnchor.UpperLeft, UIFactory.MutedText, 24, 132, 392, 62);

            timeSlider = UIFactory.Slider(t, "Time of day", 0f, 24f, C.StartHour, 24, 200, 392,
                v => { if (Game.TimeOfDay != null) Game.TimeOfDay.SetHour(v); }, out timeValue);
            speedSlider = UIFactory.Slider(t, "Day speed (1x = 4 min per day)", 0f, C.TimeSpeedMax, 1f, 24, 262, 392,
                v => { if (Game.TimeOfDay != null) Game.TimeOfDay.Speed = v; }, out speedValue);
            pauseToggle = UIFactory.Toggle(t, "Pause time (T)", false, 24, 324, v => { if (Game.TimeOfDay != null) Game.TimeOfDay.Paused = v; });

            UIFactory.Label(t, "Quality preset (applied live)", 16, TextAnchor.MiddleLeft, UIFactory.MutedText, 24, 368, 392, 22);
            for (int i = 0; i < 3; i++)
            {
                var preset = (QualityPreset)i;
                qualityButtons[i] = UIFactory.Button(t, QualityManager.Name(preset), 24 + i * 132, 396, 124, 40, () => { Game.Quality.Apply(preset); Refresh(); });
            }
            UIFactory.Label(t, "Weather: keys 1 Clear, 2 Cloudy, 3 Rain, 4 Fog, 5 Storm, 0 auto mode\nF1 debug overlay, F12 screenshot, P photo mode, Esc pause",
                14, TextAnchor.UpperLeft, UIFactory.MutedText, 24, 456, 392, 80);
        }

        void Regenerate()
        {
            if (!int.TryParse(seedField.text, NumberStyles.Integer, Inv, out int seed)) { status.text = "Enter an integer seed."; return; }
            var boot = FindAnyObjectByType<GameBootstrap>();
            float seconds = boot.Regenerate(seed);
            var w = Game.World;
            status.text = string.Format(Inv, "Seed {0} regenerated in {1:F2} s (limit {2:F0} s)\npalms {3}, rocks {4}, bushes {5}, flowers {6} | floating {7}, in water {8}",
                seed, seconds, C.RegenerationMaxSeconds, w.Placer.Palms, w.Placer.Rocks, w.Placer.Bushes, w.Placer.FlowerClusters,
                w.Placer.Validator.Floating, w.Placer.Validator.InWater);
        }

        public void Refresh()
        {
            if (Game.World != null && !seedField.isFocused) seedField.text = Game.World.Data.Seed.ToString(Inv);
            for (int i = 0; i < 3; i++)
                qualityButtons[i].GetComponent<Image>().color = (int)Game.Quality.Current == i ? UIFactory.Accent * 0.8f : UIFactory.ButtonColor;
        }

        void Update()
        {
            GameInput.TextEntryActive = seedField != null && seedField.isFocused;
            if (Game.TimeOfDay == null) return;
            timeSlider.SetValueWithoutNotify(Game.TimeOfDay.Hour);
            speedSlider.SetValueWithoutNotify(Game.TimeOfDay.Speed);
            pauseToggle.SetIsOnWithoutNotify(Game.TimeOfDay.Paused);
            // Rebuild the value labels only when they change (no per-frame string allocations).
            int minute = Game.TimeOfDay.MinuteOfDay;
            if (minute != shownMinute)
            {
                shownMinute = minute;
                timeValue.text = (minute / 60).ToString("00", Inv) + ":" + (minute % 60).ToString("00", Inv);
            }
            if (!Mathf.Approximately(shownSpeed, Game.TimeOfDay.Speed))
            {
                shownSpeed = Game.TimeOfDay.Speed;
                speedValue.text = "x" + shownSpeed.ToString("F1", Inv);
            }
        }

        int shownMinute = -1;
        float shownSpeed = -1f;

        void OnDisable() => GameInput.TextEntryActive = false;
    }
}
