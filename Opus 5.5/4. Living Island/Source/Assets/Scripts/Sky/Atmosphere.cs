using UnityEngine;
using UnityEngine.Rendering;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Combines time of day, weather and lightning into the directional light (sun by day, moon by
    /// night, shadows always on), sky colours, ambient light, height fog and the underwater state, and
    /// pushes everything to the shader globals every frame (I-15, I-19, I-20, I-07).
    /// </summary>
    public sealed class Atmosphere : MonoBehaviour
    {
        public bool CameraUnderwater { get; private set; }
        public float CameraWaterHeight { get; private set; }
        /// <summary>Overall scene brightness 0 (dark night) .. 1 (clear noon); drives grading.</summary>
        public float LightLevel { get; private set; }
        public Light Sun { get; private set; }

        static readonly Color MoonColor = new Color(0.55f, 0.66f, 1f);
        static readonly Color FlashColor = new Color(0.80f, 0.86f, 1f);

        TimeOfDay tod;
        WeatherSystem weather;
        LightningSystem lightning;
        Transform cam;

        public void Init(TimeOfDay timeOfDay, WeatherSystem weatherSystem, LightningSystem lightningSystem, Light sunLight, Transform cameraTransform)
        {
            tod = timeOfDay;
            weather = weatherSystem;
            lightning = lightningSystem;
            Sun = sunLight;
            cam = cameraTransform;
            RenderSettings.fog = false;                 // replaced by the custom height fog in every island shader
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.sun = Sun;
            Shader.SetGlobalColor(ShaderIds.MoonColor, MoonColor);
        }

        static float Luma(Color c) => c.r * 0.2126f + c.g * 0.7152f + c.b * 0.0722f;

        static Color Overcast(Color c, float amount, float darken)
        {
            float l = Luma(c);
            Color grey = new Color(l * 0.92f, l * 0.96f, l * 1.04f);
            return Color.Lerp(c, grey, amount) * Mathf.Lerp(1f, 0.24f, darken);
        }

        void LateUpdate()
        {
            if (tod == null) return;
            WeatherParams w = weather.Current;
            float flash = lightning != null ? lightning.Flash : 0f;
            float cloud = w.CloudCoverage;
            float dark = w.CloudDarkness;

            // ---------------------------------------------------------------- main directional light
            bool sunDominant = tod.SunIntensity >= tod.MoonIntensity;
            Vector3 L = sunDominant ? tod.SunDirection : tod.MoonDirection;
            Color lightColor = sunDominant ? tod.SunColor : MoonColor;
            lightColor = Color.Lerp(lightColor, new Color(Luma(lightColor), Luma(lightColor), Luma(lightColor)), cloud * 0.45f);
            float intensity = sunDominant ? tod.SunIntensity * w.LightMultiplier
                                          : tod.MoonIntensity * Mathf.Lerp(1f, 0.4f, dark);
            float flashIntensity = flash * 2.4f;
            Sun.transform.rotation = Quaternion.LookRotation(-L, Mathf.Abs(L.y) > 0.99f ? Vector3.forward : Vector3.up);
            Sun.intensity = intensity + flashIntensity;
            Sun.color = Color.Lerp(lightColor, FlashColor, flashIntensity / (intensity + flashIntensity + 1e-4f));
            // Overcast skies diffuse the light: shadows stay on but get lighter.
            Sun.shadowStrength = Mathf.Lerp(1f, 0.5f, dark);

            // ---------------------------------------------------------------- sky colours
            Color zenith = Overcast(tod.Zenith, cloud * 0.8f, dark);
            Color horizon = Overcast(tod.Horizon, cloud * 0.7f, dark * 0.85f);
            zenith += FlashColor * (flash * 0.6f);
            horizon += FlashColor * (flash * 0.9f);
            Shader.SetGlobalColor(ShaderIds.SkyZenith, zenith);
            Shader.SetGlobalColor(ShaderIds.SkyHorizon, horizon);
            Shader.SetGlobalVector(ShaderIds.SunDirection, new Vector4(tod.SunDirection.x, tod.SunDirection.y, tod.SunDirection.z,
                IslandGenerator.SmoothStep(-6f, 2f, tod.SunElevation)));
            Shader.SetGlobalVector(ShaderIds.MoonDirection, new Vector4(tod.MoonDirection.x, tod.MoonDirection.y, tod.MoonDirection.z,
                tod.NightFactor));
            Color sunCol = tod.SunColor * Mathf.Max(tod.SunIntensity, 0.15f);
            Shader.SetGlobalColor(ShaderIds.SunColor, sunCol);
            Shader.SetGlobalFloat(ShaderIds.NightFactor, tod.NightFactor);

            // ---------------------------------------------------------------- ambient (hemisphere)
            Color ambSky = Overcast(tod.AmbientSky, cloud * 0.65f, dark * 0.75f) + FlashColor * (flash * 0.8f);
            Color ambEq = Overcast(tod.AmbientEquator, cloud * 0.6f, dark * 0.75f) + FlashColor * (flash * 0.5f);
            Color ambGround = Overcast(tod.AmbientGround, cloud * 0.5f, dark * 0.7f);
            Shader.SetGlobalColor(ShaderIds.AmbientSky, ambSky);
            Shader.SetGlobalColor(ShaderIds.AmbientEquator, ambEq);
            Shader.SetGlobalColor(ShaderIds.AmbientGround, ambGround);
            RenderSettings.ambientSkyColor = ambSky;
            RenderSettings.ambientEquatorColor = ambEq;
            RenderSettings.ambientGroundColor = ambGround;

            // ---------------------------------------------------------------- height fog
            float daylight = Mathf.Clamp01(tod.SunIntensity / C.NoonSunIntensity);
            Color fogColor = Color.Lerp(horizon, ambEq * 1.6f, 0.35f);
            // In thick fog the fog itself is lit: brighter and whiter by day.
            float thick = Mathf.Clamp01(w.FogDensity / (3.912f / C.FogWeatherVisibility));
            fogColor = Color.Lerp(fogColor, new Color(0.62f, 0.66f, 0.70f) * (0.12f + 0.88f * daylight) * Mathf.Lerp(1f, 0.5f, dark), thick * 0.65f);
            fogColor *= Mathf.Lerp(1f, 0.42f, dark);
            Shader.SetGlobalColor(ShaderIds.FogColor, fogColor);
            Shader.SetGlobalColor(ShaderIds.FogSunColor, tod.SunColor * (0.55f * daylight * (1f - dark)));
            Shader.SetGlobalVector(ShaderIds.FogParams, new Vector4(w.FogDensity, C.FogHeightFalloff, C.FogReferenceHeight, 0.985f));

            LightLevel = Mathf.Clamp01(daylight * w.LightMultiplier + tod.MoonIntensity * 4f);

            // ---------------------------------------------------------------- underwater state (I-07)
            if (cam != null)
            {
                Vector3 p = cam.position;
                CameraWaterHeight = WaveModel.HeightAt(p.x, p.z);
                // Small hysteresis so the camera hovering at the surface does not flicker between modes.
                CameraUnderwater = CameraUnderwater ? p.y < CameraWaterHeight + 0.03f : p.y < CameraWaterHeight - 0.03f;
            }
            float underwaterLight = 0.06f + 0.94f * Mathf.Clamp01(daylight * w.LightMultiplier * 1.2f) + tod.MoonIntensity * 2f;
            Shader.SetGlobalVector(ShaderIds.UnderwaterParams, new Vector4(CameraUnderwater ? 1f : 0f,
                3.912f / C.UnderwaterVisibility, CameraWaterHeight, 0f));
            Shader.SetGlobalColor(ShaderIds.UnderwaterColor, C.UnderwaterFogColor * underwaterLight);
        }
    }
}
