using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>Photo mode panel (I-29): FOV, DOF focus distance and aperture, exposure, time of day.</summary>
    public sealed class PhotoPanel : MonoBehaviour
    {
        Slider fov, focus, aperture, exposure, time;
        Text fovV, focusV, apertureV, exposureV, timeV, saved;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public void Build(Transform canvas)
        {
            UIFactory.Rect(gameObject, canvas, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-24, 0), new Vector2(380, 520));
            var bg = UIFactory.Panel(transform, "Background", UIFactory.PanelColor);
            UIFactory.Rect(bg.gameObject, transform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            var t = transform;
            UIFactory.Label(t, "Photo mode", 26, TextAnchor.MiddleLeft, UIFactory.TextColor, 22, 12, 300, 36).fontStyle = FontStyle.Bold;
            fov = UIFactory.Slider(t, "Field of view", C.PhotoFovMin, C.PhotoFovMax, C.DefaultFov, 22, 60, 336, v => Game.Photo.Fov = v, out fovV);
            focus = UIFactory.Slider(t, "DOF focus distance", 0.5f, 80f, 8f, 22, 118, 336, v => Game.Post.DofFocusDistance = v, out focusV);
            aperture = UIFactory.Slider(t, "DOF aperture (f-stop)", 1.4f, 22f, 4f, 22, 176, 336, v => Game.Post.DofAperture = v, out apertureV);
            exposure = UIFactory.Slider(t, "Exposure (EV)", -3f, 3f, 0f, 22, 234, 336, v => Game.Photo.Exposure = v, out exposureV);
            time = UIFactory.Slider(t, "Time of day", 0f, 24f, 12f, 22, 292, 336, v => Game.TimeOfDay.SetHour(v), out timeV);
            UIFactory.Label(t, "WASD move   Q / E down / up   Shift faster\nHold right mouse button to look around\nF12 saves a 3840 x 2160 PNG   P exits",
                14, TextAnchor.UpperLeft, UIFactory.MutedText, 22, 356, 336, 70);
            saved = UIFactory.Label(t, "", 13, TextAnchor.UpperLeft, UIFactory.Accent, 22, 432, 336, 70);
        }

        public void Refresh()
        {
            fov.SetValueWithoutNotify(Game.Photo.Fov);
            focus.SetValueWithoutNotify(Game.Post.DofFocusDistance);
            aperture.SetValueWithoutNotify(Game.Post.DofAperture);
            exposure.SetValueWithoutNotify(Game.Photo.Exposure);
            time.SetValueWithoutNotify(Game.TimeOfDay.Hour);
        }

        Vector4 shown = new Vector4(-1f, -1f, -1f, -99f);
        int shownMinute = -1;
        string shownCapture;

        void Update()
        {
            // Texts are rebuilt only when a value changes (no per-frame string allocations).
            var now = new Vector4(Game.Photo.Fov, Game.Post.DofFocusDistance, Game.Post.DofAperture, Game.Photo.Exposure);
            if (now != shown)
            {
                shown = now;
                fovV.text = now.x.ToString("F0", Inv) + " deg";
                focusV.text = now.y.ToString("F1", Inv) + " m";
                apertureV.text = "f/" + now.z.ToString("F1", Inv);
                exposureV.text = (now.w >= 0f ? "+" : "") + now.w.ToString("F1", Inv);
            }
            int minute = Game.TimeOfDay.MinuteOfDay;
            if (minute != shownMinute)
            {
                shownMinute = minute;
                timeV.text = (minute / 60).ToString("00", Inv) + ":" + (minute % 60).ToString("00", Inv);
            }
            if (!ReferenceEquals(shownCapture, Game.Photo.LastCapture))
            {
                shownCapture = Game.Photo.LastCapture;
                saved.text = string.IsNullOrEmpty(shownCapture) ? "" : "Saved: " + shownCapture;
            }
        }
    }
}
