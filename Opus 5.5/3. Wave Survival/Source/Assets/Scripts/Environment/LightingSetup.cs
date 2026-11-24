using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace WaveSurvival
{
    /// <summary>
    /// S-03 atmosphere: dim trilight ambient, light exponential fog, a procedural reflection cubemap and the global
    /// post-processing volume (bloom, vignette, ACES tonemapping, colour grading).
    /// </summary>
    public static class LightingSetup
    {
        static Cubemap reflection;

        public static VolumeProfile Apply(Transform root)
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = GameConfig.Lighting.AmbientSky;
            RenderSettings.ambientEquatorColor = GameConfig.Lighting.AmbientEquator;
            RenderSettings.ambientGroundColor = GameConfig.Lighting.AmbientGround;
            // Set the ambient probe explicitly so the runtime ambient matches the trilight colours.
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(GameConfig.Lighting.AmbientEquator);
            sh.AddDirectionalLight(Vector3.up, GameConfig.Lighting.AmbientSky - GameConfig.Lighting.AmbientEquator, 0.9f);
            RenderSettings.ambientProbe = sh;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = GameConfig.Lighting.FogDensity;
            RenderSettings.fogColor = GameConfig.Lighting.FogColor;

            if (reflection == null) reflection = AssetTracker.Track(BuildReflection(32));
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = reflection;
            RenderSettings.reflectionIntensity = 1f;

            var go = new GameObject("PostProcessVolume");
            go.transform.SetParent(root, false);
            Volume volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10f;
            VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RuntimePostFX";
            volume.sharedProfile = profile;

            Tonemapping tone = profile.Add<Tonemapping>();
            tone.mode.Override(TonemappingMode.ACES);
            Bloom bloom = profile.Add<Bloom>();
            bloom.threshold.Override(GameConfig.Lighting.BloomThreshold);
            bloom.intensity.Override(GameConfig.Lighting.BloomIntensity);
            bloom.scatter.Override(GameConfig.Lighting.BloomScatter);
            bloom.highQualityFiltering.Override(true);
            Vignette vignette = profile.Add<Vignette>();
            vignette.intensity.Override(GameConfig.Lighting.VignetteIntensity);
            vignette.smoothness.Override(GameConfig.Lighting.VignetteSmoothness);
            vignette.color.Override(Color.black);
            ColorAdjustments grade = profile.Add<ColorAdjustments>();
            grade.postExposure.Override(GameConfig.Lighting.PostExposure);
            grade.contrast.Override(GameConfig.Lighting.Contrast);
            grade.saturation.Override(GameConfig.Lighting.Saturation);
            return profile;
        }

        /// <summary>Tiny cubemap approximating the hall (dark ceiling with warm lamp glow, darker floor).</summary>
        static Cubemap BuildReflection(int size)
        {
            var cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = "ArenaReflection" };
            var px = new Color[size * size];
            for (int f = 0; f < 6; f++)
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    Vector3 d;
                    switch ((CubemapFace)f)
                    {
                        case CubemapFace.PositiveX: d = new Vector3(1f, -v, -u); break;
                        case CubemapFace.NegativeX: d = new Vector3(-1f, -v, u); break;
                        case CubemapFace.PositiveY: d = new Vector3(u, 1f, v); break;
                        case CubemapFace.NegativeY: d = new Vector3(u, -1f, -v); break;
                        case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1f); break;
                        default: d = new Vector3(-u, -v, -1f); break;
                    }
                    d.Normalize();
                    float up = Mathf.Clamp01(d.y);
                    float horizon = 1f - Mathf.Abs(d.y);
                    Color c = new Color(0.1f, 0.104f, 0.12f) * (0.6f + horizon * 0.8f);
                    c += new Color(0.55f, 0.42f, 0.28f) * Mathf.Pow(up, 3f) * (0.6f + 0.4f * Mathf.Sin(d.x * 9f) * Mathf.Sin(d.z * 9f));
                    if (d.y < 0f) c *= 0.6f;
                    px[y * size + x] = c;
                }
                cube.SetPixels(px, (CubemapFace)f);
            }
            cube.Apply(true, true);
            return cube;
        }
    }
}
