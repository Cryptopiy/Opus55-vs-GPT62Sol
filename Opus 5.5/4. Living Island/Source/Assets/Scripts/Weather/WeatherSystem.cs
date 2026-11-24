using UnityEngine;
using UnityEngine.InputSystem;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    public enum WeatherState { Clear, Cloudy, Rain, Fog, Storm }

    /// <summary>Every quantity a weather state controls; blended linearly during transitions.</summary>
    public struct WeatherParams
    {
        public float CloudCoverage, CloudDensity, CloudDarkness;
        public float FogDensity;        // extinction per metre at eye height (3.912 / visibility)
        public float LightMultiplier;
        public float Wind;              // multiplier of the base wind strength (Storm = 3)
        public float WaveScale;         // Gerstner amplitude multiplier (Storm = 1.8)
        public float WaterRoughness;    // detail-normal strength of the water
        public float Rain;              // 0..1 rain intensity
        public float Lightning;         // 0..1, strikes are enabled above 0.5

        public static WeatherParams Lerp(in WeatherParams a, in WeatherParams b, float t)
        {
            return new WeatherParams
            {
                CloudCoverage = Mathf.Lerp(a.CloudCoverage, b.CloudCoverage, t),
                CloudDensity = Mathf.Lerp(a.CloudDensity, b.CloudDensity, t),
                CloudDarkness = Mathf.Lerp(a.CloudDarkness, b.CloudDarkness, t),
                FogDensity = Mathf.Lerp(a.FogDensity, b.FogDensity, t),
                LightMultiplier = Mathf.Lerp(a.LightMultiplier, b.LightMultiplier, t),
                Wind = Mathf.Lerp(a.Wind, b.Wind, t),
                WaveScale = Mathf.Lerp(a.WaveScale, b.WaveScale, t),
                WaterRoughness = Mathf.Lerp(a.WaterRoughness, b.WaterRoughness, t),
                Rain = Mathf.Lerp(a.Rain, b.Rain, t),
                Lightning = Mathf.Lerp(a.Lightning, b.Lightning, t),
            };
        }
    }

    /// <summary>
    /// Weather state machine (I-17): keys 1-5 select Clear/Cloudy/Rain/Fog/Storm, 0 toggles auto mode
    /// (random state every 60-120 s). Every change blends all parameters smoothly over 8 s.
    /// </summary>
    public sealed class WeatherSystem : MonoBehaviour
    {
        public WeatherState State { get; private set; } = WeatherState.Clear;
        public WeatherParams Current { get; private set; }
        public bool AutoMode { get; private set; }
        public float TransitionProgress => Mathf.Clamp01(transitionTime / C.WeatherTransitionSeconds);
        public float Wetness { get; private set; }
        public float AutoTimeLeft => autoTimer;

        WeatherParams from, to;
        float transitionTime = C.WeatherTransitionSeconds;
        float autoTimer;
        DetRandom rng = new DetRandom(C.DefaultSeed, 901);

        public static WeatherParams Preset(WeatherState s)
        {
            switch (s)
            {
                case WeatherState.Cloudy:
                    return Make(0.74f, 0.82f, 0.25f, C.CloudyVisibility, 0.55f, 1.6f, 1.25f, 0.5f, 0f, 0f);
                case WeatherState.Rain:
                    return Make(0.92f, 0.95f, 0.55f, C.RainVisibility, 0.32f, 2.0f, 1.4f, 0.75f, 0.62f, 0f);
                case WeatherState.Fog:
                    return Make(0.55f, 0.7f, 0.15f, C.FogWeatherVisibility, 0.5f, 0.45f, 0.8f, 0.2f, 0f, 0f);
                case WeatherState.Storm:
                    return Make(1f, 1f, 0.85f, C.StormVisibility, 0.16f, C.StormWindMultiplier, C.StormWaveMultiplier, 1f, 1f, 1f);
                default:
                    return Make(0.42f, 0.55f, 0f, C.ClearVisibility, 1f, 1f, 1f, 0.3f, 0f, 0f);
            }
        }

        static WeatherParams Make(float cover, float dens, float dark, float visibility, float light, float wind,
                                  float waves, float rough, float rain, float lightning)
        {
            return new WeatherParams
            {
                CloudCoverage = cover, CloudDensity = dens, CloudDarkness = dark,
                FogDensity = 3.912f / visibility, // Koschmieder: 2% contrast threshold at the visibility distance
                LightMultiplier = light, Wind = wind, WaveScale = waves, WaterRoughness = rough,
                Rain = rain, Lightning = lightning
            };
        }

        void Awake()
        {
            Current = from = to = Preset(State);
        }

        public void SetState(WeatherState s)
        {
            from = Current;          // start from wherever a running transition currently is
            to = Preset(s);
            State = s;
            transitionTime = 0f;
        }

        /// <summary>Applies a state instantly without the 8 s blend (used by the benchmark run only).</summary>
        public void SnapTo(WeatherState s)
        {
            SetState(s);
            transitionTime = C.WeatherTransitionSeconds;
            Current = to;
        }

        public void SetAutoMode(bool on)
        {
            AutoMode = on;
            if (on) autoTimer = rng.Range(C.AutoWeatherMinSeconds, C.AutoWeatherMaxSeconds);
        }

        void Update()
        {
            if (GameInput.Down(Key.Digit1)) { SetAutoMode(false); SetState(WeatherState.Clear); }
            if (GameInput.Down(Key.Digit2)) { SetAutoMode(false); SetState(WeatherState.Cloudy); }
            if (GameInput.Down(Key.Digit3)) { SetAutoMode(false); SetState(WeatherState.Rain); }
            if (GameInput.Down(Key.Digit4)) { SetAutoMode(false); SetState(WeatherState.Fog); }
            if (GameInput.Down(Key.Digit5)) { SetAutoMode(false); SetState(WeatherState.Storm); }
            if (GameInput.Down(Key.Digit0)) SetAutoMode(!AutoMode);

            float dt = Time.deltaTime;
            if (AutoMode)
            {
                autoTimer -= dt;
                if (autoTimer <= 0f)
                {
                    var next = (WeatherState)rng.Range(0, 5);
                    if (next == State) next = (WeatherState)(((int)next + 1 + rng.Range(0, 4)) % 5);
                    SetState(next);
                    autoTimer = rng.Range(C.AutoWeatherMinSeconds, C.AutoWeatherMaxSeconds);
                }
            }

            transitionTime = Mathf.Min(transitionTime + dt, C.WeatherTransitionSeconds);
            float t = transitionTime / C.WeatherTransitionSeconds;
            Current = WeatherParams.Lerp(from, to, t * t * (3f - 2f * t));

            // Surfaces get wet over ~12 s of rain and dry over ~40 s.
            float targetWet = Mathf.Clamp01(Current.Rain * 1.6f);
            float rate = targetWet > Wetness ? 1f / C.WetnessRiseSeconds : 1f / C.WetnessDrySeconds;
            Wetness = Mathf.MoveTowards(Wetness, targetWet, rate * dt);

            PushGlobals();
        }

        void PushGlobals()
        {
            var w = Current;
            Vector2 dir = C.WindDirection.normalized;
            float strength = C.WindStrength * w.Wind;
            Shader.SetGlobalVector(ShaderIds.IslandWind, new Vector4(dir.x, dir.y, strength, Mathf.Clamp01(0.35f + 0.25f * w.Wind)));
            Shader.SetGlobalVector(ShaderIds.IslandWindParams, new Vector4(C.GustSpeed * Mathf.Sqrt(w.Wind), C.GustScale, 0f, 0f));
            Shader.SetGlobalFloat(ShaderIds.RainIntensity, w.Rain);
            Shader.SetGlobalFloat(ShaderIds.IslandWetness, Wetness);
            Shader.SetGlobalFloat(ShaderIds.WaveAmplitudeScale, w.WaveScale);
            Shader.SetGlobalFloat(ShaderIds.WaterRoughness, w.WaterRoughness);
            Shader.SetGlobalVector(ShaderIds.CloudParams, new Vector4(w.CloudCoverage, w.CloudDensity, w.CloudDarkness, 0f));
            WaveModel.AmplitudeScale = w.WaveScale;
        }

        public static string DisplayName(WeatherState s)
        {
            switch (s)
            {
                case WeatherState.Cloudy: return "Cloudy";
                case WeatherState.Rain: return "Rain";
                case WeatherState.Fog: return "Fog";
                case WeatherState.Storm: return "Storm";
                default: return "Clear";
            }
        }
    }
}
