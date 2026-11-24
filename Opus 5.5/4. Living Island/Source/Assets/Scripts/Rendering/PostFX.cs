using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Global URP Volume (I-28): Bloom, ACES tonemapping, colour adjustments graded by time of day and
    /// weather, vignette, very low film grain, white balance, and depth of field that is only enabled in
    /// photo mode. SSAO is the renderer feature configured by the editor setup.
    /// </summary>
    public sealed class PostFX : MonoBehaviour
    {
        public float PhotoExposure { get; set; }
        public bool DofEnabled { get; set; }
        public float DofFocusDistance { get; set; } = 8f;
        public float DofAperture { get; set; } = 4f;

        VolumeProfile profile;
        Bloom bloom;
        ColorAdjustments color;
        Vignette vignette;
        WhiteBalance whiteBalance;
        DepthOfField dof;
        FilmGrain grain;

        public void Init()
        {
            var volume = gameObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "IslandPostFX";
            volume.sharedProfile = profile;

            bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.7f);
            bloom.highQualityFiltering.Override(true);

            var tone = profile.Add<Tonemapping>(true);
            tone.mode.Override(TonemappingMode.ACES);

            color = profile.Add<ColorAdjustments>(true);
            vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.22f);
            vignette.smoothness.Override(0.45f);
            vignette.rounded.Override(false);

            grain = profile.Add<FilmGrain>(true);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(C.FilmGrainIntensity);
            grain.response.Override(0.8f);

            whiteBalance = profile.Add<WhiteBalance>(true);

            dof = profile.Add<DepthOfField>(true);
            dof.mode.Override(DepthOfFieldMode.Off);
            dof.focalLength.Override(50f);
            dof.bladeCount.Override(6);
            dof.bladeCurvature.Override(0.8f);
        }

        void LateUpdate()
        {
            if (Game.TimeOfDay == null || Game.Weather == null) return;
            var tod = Game.TimeOfDay;
            var w = Game.Weather.Current;
            float night = tod.NightFactor;
            float golden = IslandGenerator.SmoothStep(18f, 2f, tod.SunElevation) * (1f - night);
            bool underwater = Game.Atmosphere != null && Game.Atmosphere.CameraUnderwater;
            float storm = Mathf.Clamp01(w.CloudDarkness);

            // Day: crisp and saturated. Golden hour: warm. Night: cool, desaturated, lifted exposure.
            float exposure = Mathf.Lerp(0f, 1.15f, night) - 0.15f * storm + PhotoExposure;
            float saturation = 12f - 32f * night - 22f * storm - 10f * Mathf.Clamp01(w.FogDensity * 40f);
            float contrast = 14f + 8f * storm;
            Color filter = Color.Lerp(Color.white, new Color(1f, 0.93f, 0.84f), golden * 0.6f);
            filter = Color.Lerp(filter, new Color(0.8f, 0.88f, 1f), night * 0.8f);
            float temperature = Mathf.Lerp(0f, -18f, night) + golden * 8f;
            float vig = 0.22f;
            if (underwater)
            {
                filter = new Color(0.55f, 0.92f, 0.95f);
                saturation -= 10f;
                vig = 0.38f;
            }
            color.postExposure.Override(exposure);
            color.saturation.Override(saturation);
            color.contrast.Override(contrast);
            color.colorFilter.Override(filter);
            whiteBalance.temperature.Override(temperature);
            vignette.intensity.Override(vig);
            bloom.intensity.Override(Mathf.Lerp(0.55f, 0.8f, night));

            if (DofEnabled)
            {
                dof.mode.Override(DepthOfFieldMode.Bokeh);
                dof.focusDistance.Override(DofFocusDistance);
                dof.aperture.Override(DofAperture);
            }
            else dof.mode.Override(DepthOfFieldMode.Off);
        }

        void OnDestroy()
        {
            if (profile != null) Destroy(profile);
        }
    }
}
