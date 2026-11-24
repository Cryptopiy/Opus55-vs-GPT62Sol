using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// The ONE central configuration file. Every number given by the specification lives here
    /// (plus the tuning values derived from it). Systems and shaders read these values; shaders get
    /// them through global shader properties pushed by <see cref="ShaderGlobals"/>.
    /// The Blender prop script (Tools/Blender/build_island_props.py) parses the "Blender props" block.
    /// </summary>
    public static class GameConfig
    {
        // ------------------------------------------------------------------ World / terrain (I-01, I-02)
        public const int DefaultSeed = 777;
        public const int TerrainSamples = 512;            // heightmap samples per side
        public const float TerrainSize = 500f;            // metres covered by the heightmap (centred on 0,0)
        public const float SeaLevel = 0f;
        public const float IslandDiameter = 280f;
        public const float OceanFloorHeight = -12f;
        public const int HillFbmOctaves = 6;
        public const float MountainPeakHeight = 45f;
        public const float BayWidth = 60f;
        public const float CliffMinSlopeDeg = 60f;
        public const float CliffStretchLength = 40f;
        public const float BeachMaxSlopeDeg = 8f;
        public const float BeachMinHeight = 0f;
        public const float BeachMaxHeight = 2f;
        public const float RegenerationMaxSeconds = 5f;

        // Shape tuning of the height function (derived from the numbers above)
        public const float CoastNoiseAmplitude = 6f;      // metres of coastline wobble
        public const float BeachSlope = 0.1f;             // rise/run on the beach band = 5.7 deg (< 8 deg)
        public const float BeachBandWidth = (BeachMaxHeight - BeachMinHeight) / BeachSlope; // 20 m inland
        public const float SeabedShelfDepth = -1.8f;      // shallow sand shelf before the drop-off
        public const float SeabedDropDistance = 70f;      // metres offshore where the floor reaches -12 m
        public const float InlandRiseHeight = 5f;         // gentle plateau rise behind the beach
        public const float InlandRiseDistance = 35f;
        public const float HillsStart = 18f;              // inland distance where hills fade in ...
        public const float HillsFull = 55f;               // ... and reach full strength
        public const float HillAmplitude = 12f;
        public const float HillFrequency = 0.0085f;
        public const float WarpAmplitude = 10f;
        public const float MountainOffset = 28f;          // mountain centre offset towards the cliff side
        public const float MountainRadius = 80f;
        public const float MountainBaseHeight = 38f;
        public const float MountainKnee = 12f;            // heights above this are rescaled so the peak is exactly 45 m
        public const int RidgedOctaves = 5;
        public const float BayInlandDepth = 48f;          // how far the inlet cuts into the island
        public const float BayMouthOffshore = 20f;
        public const float BaySmoothing = 10f;            // smooth-min radius -> concave, rounded inlet
        public const float CliffHalfArcDeg = 16f;         // full-strength cliff arc (+/-) around the cliff direction
        public const float CliffBlendDeg = 8f;
        public const float CliffTopHeight = 21f;
        public const float CliffFaceTop = 2f;             // inland distance of the cliff edge
        public const float CliffFaceBottom = -5f;         // offshore distance of the cliff foot
        public const float CliffFootHeight = -7f;

        // ------------------------------------------------------------------ Shading (I-03)
        public const float WetSandHeight = 0.55f;
        public const float DrySandMaxHeight = 2.4f;
        public const float GrassMaxSlopeDeg = 32f;
        public const float RockMinSlopeDeg = 40f;
        public const float DirtMinSlopeDeg = 24f;
        public const float SnowlessRockHeight = 34f;      // bare rock near the peak

        // ------------------------------------------------------------------ Water (I-04 .. I-08)
        public const int WaterGridVertices = 400;         // per side
        public const float WaterGridHalfExtent = 2500f;   // metres from the camera to the grid edge
        public const float WaterGridLinearFraction = 0.035f; // share of the radius mapped linearly (dense core)
        public const float WaterGridPower = 3f;           // distance-based density falloff exponent
        public static readonly Color ShallowWaterColor = new Color32(0x2f, 0xd6, 0xc9, 0xff); // #2fd6c9
        public static readonly Color DeepWaterColor = new Color32(0x06, 0x30, 0x4f, 0xff);    // #06304f
        public const float ShallowToDeepDepth = 9f;       // metres of water over which the colour fades
        public const float FoamDepth = 0.6f;
        public const float UnderwaterVisibility = 30f;
        public static readonly Color UnderwaterFogColor = new Color(0.035f, 0.30f, 0.33f);
        public const float RefractionStrength = 0.55f;
        public const float ShoreWaveDampDepth = 3.5f;     // waves flatten in water shallower than this
        public const float WaveSteepness = 0.55f;         // Gerstner Q as fraction of the no-loop maximum
        // Four Gerstner waves: (direction x, direction z, amplitude m, wavelength m)
        public static readonly Vector4[] GerstnerWaves =
        {
            new Vector4(1.00f, 0.35f, 0.30f, 30.0f),
            new Vector4(0.60f, 1.00f, 0.19f, 17.0f),
            new Vector4(-0.45f, 1.00f, 0.10f, 9.5f),
            new Vector4(1.00f, -0.60f, 0.055f, 5.5f),
        };

        // ------------------------------------------------------------------ Vegetation & props (I-09 .. I-13)
        public const int PalmCount = 300;
        public const float PalmHeightMin = 5f;
        public const float PalmHeightMax = 9f;
        public const float PalmMinSpacing = 3.5f;
        public const float PalmMinGround = 0.9f;          // metres above sea level (keeps trunks out of the water)
        public const float PalmMaxSlopeDeg = 24f;
        public const int RockCount = 150;
        public const float RockScaleMin = 0.45f;
        public const float RockScaleMax = 2.3f;
        public const float RockMinGround = 0.8f;
        public const int BushCount = 60;
        public const int FlowerClusterCount = 20;
        public const int FlowersPerCluster = 70;
        public const float FlowerClusterRadius = 2.6f;
        public const float MaxFloatOrSink = 0.2f;         // I-13 tolerance
        public const float PlacementSink = 0.06f;         // trees/props are pushed this far into the ground

        // Grass (I-10, I-11, I-30)
        public const int GrassBudgetLow = 20000;
        public const int GrassBudgetMedium = 50000;
        public const int GrassBudgetHigh = 100000;        // "80k+" on High
        public const float GrassCullDistance = 80f;
        public const float GrassCellSize = 0.14f;         // candidate grid spacing around the camera (max ~51 blades/m2)
        public const float GrassFullDensityRadius = 9f;   // density falls with distance beyond this radius
        public const float GrassMinDensity = 0.05f;       // relative density at the cull distance
        public const float GrassMinGround = 1.9f;
        public const float GrassBladeHeight = 0.55f;
        public const float GrassBladeWidth = 0.045f;
        public const float PlayerGrassBendRadius = 1.5f;

        // Wind (I-11, I-17, I-19)
        public static readonly Vector2 WindDirection = new Vector2(0.8f, 0.6f);
        public const float WindStrength = 1f;             // base strength in Clear weather
        public const float GustSpeed = 6f;                // m/s that gust noise scrolls along the wind
        public const float GustScale = 0.035f;

        // ------------------------------------------------------------------ Sky / time of day (I-14 .. I-16)
        public const int StarCount = 1500;
        public const float DayLengthSeconds = 240f;       // full cycle = 4 real minutes
        public const float StartHour = 12f;               // launch at noon
        public const float SunPathTiltDeg = 20f;
        public const float SunriseHour = 6f;
        public const float NoonSunIntensity = 1.65f;
        public const float MoonLightIntensity = 0.05f;
        public const float TimeSpeedMax = 20f;            // speed slider range 0..20x
        public const float CloudLayerLowHeight = 1400f;
        public const float CloudLayerHighHeight = 4200f;
        public const int ShadowCascades = 4;
        public const float ShadowDistanceHigh = 120f;
        public const float ShadowDistanceMedium = 80f;
        public const float ShadowDistanceLow = 50f;
        public const int EnvCubemapSize = 128;
        public const float EnvCubemapRefreshSeconds = 0.5f;

        // ------------------------------------------------------------------ Weather (I-17 .. I-20)
        public const float WeatherTransitionSeconds = 8f;
        public const float AutoWeatherMinSeconds = 60f;
        public const float AutoWeatherMaxSeconds = 120f;
        public const float StormWindMultiplier = 3f;
        public const float StormWaveMultiplier = 1.8f;
        public const float LightningMinInterval = 6f;
        public const float LightningMaxInterval = 14f;
        public const float LightningFlashSeconds = 0.15f;
        public const float ThunderMinDelay = 1f;
        public const float ThunderMaxDelay = 4f;
        public const float SpeedOfSound = 343f;
        public const float FogWeatherVisibility = 60f;
        public const float ClearVisibility = 6000f;
        public const float CloudyVisibility = 3000f;
        public const float RainVisibility = 700f;
        public const float StormVisibility = 400f;
        public const float FogHeightFalloff = 0.035f;     // exponential height fog falloff per metre
        public const float FogReferenceHeight = 1.6f;     // visibility numbers are measured at eye height
        public const int RainParticles = 4500;            // >= 4000 alive around the camera
        public const float RainAreaSize = 26f;
        public const float RainFallSpeed = 11f;
        public const int RainSplashesPerSecond = 900;
        public const float WetnessRiseSeconds = 12f;
        public const float WetnessDrySeconds = 40f;

        // ------------------------------------------------------------------ Night life / POI (I-21 .. I-24)
        public const int FireflyCount = 200;
        public const float FireflyRadius = 40f;
        public const float CampfireLightRange = 14f;
        public const float CampfireLightIntensity = 2.6f;
        public const int CampfireSeatCount = 3;
        public const int CampfireRingRocks = 10;
        public const float LighthouseHeight = 18f;
        public const float LighthouseRotationSeconds = 6f;
        public const float LighthouseSpotRange = 260f;
        public const float LighthouseSpotAngle = 14f;
        public const float BoatBobAmount = 1f;            // multiplier on the sampled wave motion

        // ------------------------------------------------------------------ Player / camera (I-25 .. I-27)
        public const float WalkSpeed = 3f;
        public const float RunSpeed = 6f;
        public const float SwimSpeed = 2.5f;
        public const float SwimDepthThreshold = 1.2f;
        public const float JumpHeight = 1.1f;
        public const float Gravity = -19.6f;
        public const float PlayerHeight = 1.8f;
        public const float PlayerRadius = 0.32f;
        public const float WorldBoundary = TerrainSize * 0.5f - 6f; // invisible wall far out at sea
        public const float CameraDistance = 4.3f;
        public const float CameraPivotHeight = 1.55f;
        public const float CameraMinPitch = -40f;
        public const float CameraMaxPitch = 75f;
        public const float MouseSensitivity = 0.12f;
        public const float CameraFollowSmoothing = 0.08f;
        public const float CameraCollisionRadius = 0.25f;
        public const float StepDistanceWalk = 0.75f;      // metres per footstep
        public const float StepDistanceRun = 1.15f;

        // ------------------------------------------------------------------ Post / photo / quality (I-28 .. I-30, I-33)
        public const float PhotoFovMin = 20f;
        public const float PhotoFovMax = 100f;
        public const float DefaultFov = 60f;
        public const int PhotoCaptureWidth = 3840;
        public const int PhotoCaptureHeight = 2160;
        public const float PhotoFlySpeed = 8f;
        public const float FilmGrainIntensity = 0.06f;    // "very low"
        public const float RenderScaleHigh = 1f;
        public const float RenderScaleMedium = 0.85f;
        public const float RenderScaleLow = 0.7f;
        public const float LodBiasHigh = 2f;
        public const float LodBiasMedium = 1.2f;
        public const float LodBiasLow = 0.6f;
        public const int TargetFps = 60;
        public const int BenchmarkWidth = 2560;
        public const int BenchmarkHeight = 1440;

        // ------------------------------------------------------------------ Audio (I-31)
        public const int AudioSampleRate = 44100;
        public const float OceanFullVolumeDistance = 8f;  // metres from the shore line
        public const float OceanSilentDistance = 140f;

        // ------------------------------------------------------------------ Blender props (section 9)
        // Parsed by Tools/Blender/build_island_props.py (keep the "public const <type> <Name> = <value>;" form).
        public const int PalmVariants = 3;
        public const int PalmTrisLod0Max = 3000;
        public const int PalmTrisLod1Max = 800;
        public const int PalmFrondsMin = 7;
        public const int PalmFrondsMax = 10;
        public const int PalmTrunkSegmentsMin = 8;
        public const float PalmModelHeight = 7f;          // modelled height; scaled to 5-9 m when placed
        public const int RockVariants = 5;
        public const int RockTrisMax = 1500;
        public const int LighthouseTrisMax = 8000;
        public const float LighthouseModelHeight = 18f;
        public const int RowboatTrisMax = 3000;
        public const int DriftwoodTrisMax = 1500;
        public const int CampfireLogsTrisMax = 1500;
    }
}
