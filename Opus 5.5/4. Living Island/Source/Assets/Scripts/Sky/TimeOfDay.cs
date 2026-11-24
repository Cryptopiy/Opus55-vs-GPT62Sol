using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// Day/night clock (I-15): a full day lasts DayLengthSeconds (4 min) at speed 1, the speed and the
    /// time itself are set from the settings/photo panels, T pauses. Sun and moon move on a path tilted
    /// 20 deg from vertical; colour curves are keyed by sun elevation (separate dawn/dusk palettes).
    /// </summary>
    public sealed class TimeOfDay : MonoBehaviour
    {
        public float Hour { get; private set; } = GameConfig.StartHour;
        public float Speed { get; set; } = 1f;
        public bool Paused { get; set; }

        public Vector3 SunDirection { get; private set; }   // unit vector towards the sun
        public Vector3 MoonDirection { get; private set; }
        public float SunElevation { get; private set; }     // degrees
        public float MoonElevation { get; private set; }
        /// <summary>0 by day, 1 at full night (driven by sun elevation, smooth through dusk).</summary>
        public float NightFactor { get; private set; }

        // Colour palettes keyed by sun elevation in degrees (linear colour space, HDR-ish).
        static readonly float[] ElevKeys = { -18f, -8f, -3f, 0f, 4f, 10f, 25f, 50f };
        static readonly ColorCurve SunColorDawn = new ColorCurve(ElevKeys, new[]
        {
            new Color(1f, 0.45f, 0.35f), new Color(1f, 0.45f, 0.35f), new Color(1f, 0.48f, 0.38f), new Color(1f, 0.55f, 0.42f),
            new Color(1f, 0.68f, 0.50f), new Color(1f, 0.80f, 0.64f), new Color(1f, 0.93f, 0.84f), new Color(1f, 0.98f, 0.95f)
        });
        static readonly ColorCurve SunColorDusk = new ColorCurve(ElevKeys, new[]
        {
            new Color(1f, 0.36f, 0.16f), new Color(1f, 0.36f, 0.16f), new Color(1f, 0.40f, 0.17f), new Color(1f, 0.47f, 0.20f),
            new Color(1f, 0.60f, 0.30f), new Color(1f, 0.75f, 0.50f), new Color(1f, 0.92f, 0.80f), new Color(1f, 0.98f, 0.95f)
        });
        static readonly ColorCurve ZenithCurve = new ColorCurve(ElevKeys, new[]
        {
            new Color(0.004f, 0.007f, 0.022f), new Color(0.012f, 0.018f, 0.055f), new Color(0.05f, 0.07f, 0.17f), new Color(0.12f, 0.16f, 0.34f),
            new Color(0.15f, 0.24f, 0.48f), new Color(0.12f, 0.27f, 0.60f), new Color(0.09f, 0.25f, 0.64f), new Color(0.07f, 0.22f, 0.62f)
        });
        static readonly ColorCurve HorizonDawn = new ColorCurve(ElevKeys, new[]
        {
            new Color(0.012f, 0.018f, 0.04f), new Color(0.06f, 0.06f, 0.12f), new Color(0.40f, 0.26f, 0.34f), new Color(0.95f, 0.52f, 0.46f),
            new Color(1.05f, 0.72f, 0.55f), new Color(0.88f, 0.80f, 0.74f), new Color(0.62f, 0.74f, 0.90f), new Color(0.55f, 0.70f, 0.90f)
        });
        static readonly ColorCurve HorizonDusk = new ColorCurve(ElevKeys, new[]
        {
            new Color(0.012f, 0.018f, 0.04f), new Color(0.07f, 0.05f, 0.10f), new Color(0.48f, 0.22f, 0.20f), new Color(1.10f, 0.45f, 0.20f),
            new Color(1.10f, 0.62f, 0.32f), new Color(0.92f, 0.76f, 0.60f), new Color(0.62f, 0.74f, 0.90f), new Color(0.55f, 0.70f, 0.90f)
        });
        static readonly ColorCurve AmbientSkyCurve = new ColorCurve(ElevKeys, new[]
        {
            new Color(0.020f, 0.028f, 0.060f), new Color(0.028f, 0.035f, 0.075f), new Color(0.08f, 0.09f, 0.16f), new Color(0.20f, 0.20f, 0.30f),
            new Color(0.26f, 0.29f, 0.40f), new Color(0.28f, 0.34f, 0.48f), new Color(0.29f, 0.37f, 0.52f), new Color(0.31f, 0.40f, 0.56f)
        });
        static readonly ColorCurve AmbientEquatorCurve = new ColorCurve(ElevKeys, new[]
        {
            new Color(0.014f, 0.018f, 0.040f), new Color(0.020f, 0.024f, 0.050f), new Color(0.07f, 0.06f, 0.09f), new Color(0.24f, 0.17f, 0.17f),
            new Color(0.30f, 0.24f, 0.21f), new Color(0.30f, 0.30f, 0.32f), new Color(0.30f, 0.33f, 0.37f), new Color(0.31f, 0.35f, 0.39f)
        });
        static readonly ColorCurve AmbientGroundCurve = new ColorCurve(ElevKeys, new[]
        {
            new Color(0.008f, 0.009f, 0.014f), new Color(0.010f, 0.011f, 0.016f), new Color(0.03f, 0.03f, 0.035f), new Color(0.08f, 0.07f, 0.06f),
            new Color(0.11f, 0.09f, 0.07f), new Color(0.14f, 0.12f, 0.10f), new Color(0.16f, 0.14f, 0.11f), new Color(0.17f, 0.15f, 0.12f)
        });

        public void SetHour(float hour) => Hour = Mathf.Repeat(hour, 24f);

        void Update()
        {
            if (!Paused)
                Hour = Mathf.Repeat(Hour + Time.deltaTime * Speed * 24f / GameConfig.DayLengthSeconds, 24f);
            ComputeDirections();
        }

        public void ComputeDirections()
        {
            // theta = 0 at sunrise (east), pi/2 at noon. The orbit plane is tilted 20 deg towards -Z (south).
            float theta = (Hour - GameConfig.SunriseHour) / 24f * Mathf.PI * 2f;
            float tilt = GameConfig.SunPathTiltDeg * Mathf.Deg2Rad;
            float s = Mathf.Sin(theta);
            SunDirection = new Vector3(Mathf.Cos(theta), s * Mathf.Cos(tilt), -s * Mathf.Sin(tilt)).normalized;
            // Full ("phase-less") moon opposite the sun, nudged in azimuth so it is never exactly antipodal.
            MoonDirection = (Quaternion.Euler(0f, 12f, 0f) * -SunDirection).normalized;
            SunElevation = Mathf.Asin(SunDirection.y) * Mathf.Rad2Deg;
            MoonElevation = Mathf.Asin(MoonDirection.y) * Mathf.Rad2Deg;
            NightFactor = Mathf.Clamp01((2f - SunElevation) / 12f);
        }

        bool Morning => Hour < 12f;

        public Color SunColor => Morning ? SunColorDawn.Evaluate(SunElevation) : SunColorDusk.Evaluate(SunElevation);
        public Color Zenith => ZenithCurve.Evaluate(SunElevation);
        public Color Horizon => Morning ? HorizonDawn.Evaluate(SunElevation) : HorizonDusk.Evaluate(SunElevation);
        public Color AmbientSky => AmbientSkyCurve.Evaluate(SunElevation);
        public Color AmbientEquator => AmbientEquatorCurve.Evaluate(SunElevation);
        public Color AmbientGround => AmbientGroundCurve.Evaluate(SunElevation);

        /// <summary>Clear-sky sun intensity curve: 0 below -2 deg, warm low sun, 1.65 at noon.</summary>
        public float SunIntensity
        {
            get
            {
                float t = Mathf.Clamp01((SunElevation + 2f) / 40f);
                return GameConfig.NoonSunIntensity * Mathf.Pow(t, 0.75f) * IslandGenerator.SmoothStep(-2f, 3f, SunElevation);
            }
        }

        /// <summary>Night moon light: blue, 0.05 intensity once the moon is clearly up.</summary>
        public float MoonIntensity => GameConfig.MoonLightIntensity * IslandGenerator.SmoothStep(0f, 10f, MoonElevation);

        /// <summary>Minute of the day; the HUD maps it to a pre-built "HH:MM" string (no per-frame allocation).</summary>
        public int MinuteOfDay => (int)(Hour * 60f) % (24 * 60);
    }
}
