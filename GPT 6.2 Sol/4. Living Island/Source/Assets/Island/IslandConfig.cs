using UnityEngine;

public static class IslandConfig
{
    public const int Seed = 777, AlternateSeed = 778, TerrainSamples = 512, WaterSamples = 400, FbmOctaves = 6;
    public const float WorldSize = 500f, IslandDiameter = 280f, Seafloor = -12f, MountainHeight = 45f;
    public const float BayWidth = 60f, CliffLength = 40f, CliffSlope = 60f, BeachSlope = 8f, BeachHeight = 2f;
    public const float CliffBearing = 83f;
    public const float SeaLevel = 0f, FoamDepth = .6f, UnderwaterVisibility = 30f;
    public const int WaveCount = 4, Palms = 300, PalmVariants = 3, PalmSegments = 8, PalmFrondMin = 7, PalmFrondMax = 10;
    public const int RockVariants = 5, Rocks = 150, Bushes = 60, FlowerClusters = 20;
    public const int LighthouseTriangleLimit = 8000, RowboatTriangleLimit = 3000, RockTriangleLimit = 1500;
    public const int PalmLod0TriangleLimit = 3000, PalmLod1TriangleLimit = 800;
    public const int GrassLow = 20000, GrassMedium = 50000, GrassHigh = 80000, GrassBatch = 511;
    public const float GrassDistance = 80f, GrassCell = 16f, GroundTolerance = .2f, PalmMin = 5f, PalmMax = 9f;
    public const float PlayerBend = 1.5f, WindStrength = 1f;
    public static readonly Vector2 WindDirection = new Vector2(.91f, .42f).normalized;
    public static readonly Vector4[] Waves = new Vector4[WaveCount] {
        new Vector4(.88f,.475f,.19f,13.4f), new Vector4(-.31f,.951f,.115f,8.2f),
        new Vector4(.97f,-.243f,.067f,4.3f), new Vector4(.43f,.903f,.033f,2.1f)
    };
    public static readonly Color Shallow = new Color32(0x2f,0xd6,0xc9,255);
    public static readonly Color Deep = new Color32(0x06,0x30,0x4f,255);
    public const int StarCount = 1500, ShadowCascades = 4;
    public const float DaySeconds = 240f, HoursPerDay = 24f, StartHour = 12f, OrbitTilt = 20f, MoonIntensity = .05f;
    public const float HighShadows = 120f, MediumShadows = 83f, LowShadows = 52f;
    public const float WeatherBlend = 8f, AutoMin = 60f, AutoMax = 120f, FogVisibility = 60f;
    public const int RainCount = 4000, HeavyRainCount = RainCount * 2, Fireflies = 200;
    public const float StormWind = 3f, StormWaves = 1.8f, FlashSeconds = .15f;
    public const float LightningMin = 6f, LightningMax = 14f, ThunderMin = 1f, ThunderMax = 4f, FireflyRange = 40f;
    // Cloud cover, rain intensity, wind speed multiplier and sea-level extinction.
    public static readonly Vector4[] WeatherProfiles = {
        new Vector4(.12f,0,WindStrength,.0018f), new Vector4(.71f,0,1.32f,.0047f),
        new Vector4(.88f,.62f,1.65f,.0092f), new Vector4(.68f,0,.65f,3.912f/FogVisibility),
        new Vector4(1,1,StormWind,.0148f)
    };
    public const int LogSeats = 3;
    public const float LighthouseHeight = 18f, BeaconPeriod = 6f;
    public const float Walk = 3f, Run = 6f, Swim = 2.5f, SwimDepth = 1.2f, JumpHeight = 1.15f;
    public const float SwimAcceptanceDistance = 30f, StartHeading = 87f;
    public const float PlayerHeight = 1.76f, CameraArm = 5.2f, DefaultFov = 58f, MinFov = 20f, MaxFov = 100f;
    public const int CaptureWidth = 3840, CaptureHeight = 2160, BenchmarkWidth = 2560, BenchmarkHeight = 1440;
    public const float RenderWarmup = 10f, BenchmarkSeconds = 35f, SoakSeconds = 101f, MinimumFPS = 60f;
    public const int NormalWidth = 1600, NormalHeight = 900;
    public static readonly Vector2 Summit = new Vector2(48f,36f);
    public static readonly Vector2 LighthouseSite = new Vector2(52f,-118f);
    public static readonly Vector2 CampSite = new Vector2(-42f,-112f);
    public static readonly Vector2 SpawnSite = new Vector2(-52f,-118f);
    public static readonly Vector2 BoatSite = new Vector2(-18f,-117f);
}

