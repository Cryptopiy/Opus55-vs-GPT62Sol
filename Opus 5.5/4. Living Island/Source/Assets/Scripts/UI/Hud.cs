using UnityEngine;
using UnityEngine.UI;

namespace LivingIsland
{
    /// <summary>
    /// Minimal HUD (I-32): HH:MM clock and a procedural weather icon in the top-right corner.
    /// All 1440 clock strings are built once, so updating the clock never allocates.
    /// </summary>
    public sealed class Hud : MonoBehaviour
    {
        Text clock, weatherName;
        RawImage icon;
        readonly Texture2D[] icons = new Texture2D[5];
        static string[] clockStrings;
        int lastMinute = -1;
        WeatherState lastState = (WeatherState)(-1);
        bool lastAuto;

        public void Build(Transform canvas)
        {
            if (clockStrings == null)
            {
                clockStrings = new string[24 * 60];
                for (int m = 0; m < clockStrings.Length; m++) clockStrings[m] = (m / 60).ToString("00") + ":" + (m % 60).ToString("00");
            }
            for (int i = 0; i < 5; i++) icons[i] = IconFactory.Build((WeatherState)i);

            var rt = GetComponent<RectTransform>();
            UIFactory.Rect(gameObject, canvas, Vector2.one, Vector2.one, Vector2.one, new Vector2(-24, -20), new Vector2(230, 72));
            var bg = UIFactory.Panel(transform, "Background", new Color(0.04f, 0.06f, 0.08f, 0.45f));
            UIFactory.Rect(bg.gameObject, transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);

            var iconGo = new GameObject("WeatherIcon", typeof(RectTransform), typeof(RawImage));
            UIFactory.TopLeft(iconGo, transform, 12, 8, 56, 56);
            icon = iconGo.GetComponent<RawImage>();
            icon.raycastTarget = false;
            clock = UIFactory.Label(transform, "12:00", 34, TextAnchor.MiddleRight, UIFactory.TextColor, 76, 2, 140, 44);
            clock.fontStyle = FontStyle.Bold;
            weatherName = UIFactory.Label(transform, "Clear", 15, TextAnchor.UpperRight, UIFactory.MutedText, 76, 44, 140, 22);
            rt.SetAsFirstSibling();
        }

        void Update()
        {
            if (Game.TimeOfDay == null || Game.Weather == null) return;
            int minute = Game.TimeOfDay.MinuteOfDay;
            if (minute != lastMinute) { lastMinute = minute; clock.text = clockStrings[minute]; }
            if (Game.Weather.State != lastState || Game.Weather.AutoMode != lastAuto)
            {
                lastState = Game.Weather.State;
                lastAuto = Game.Weather.AutoMode;
                icon.texture = icons[(int)lastState];
                weatherName.text = WeatherSystem.DisplayName(lastState) + (lastAuto ? "  (auto)" : "");
            }
        }
    }
}
