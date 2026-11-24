using UnityEngine;

namespace LivingIsland
{
    /// <summary>Cached shader property IDs for every global the island shaders read.</summary>
    public static class ShaderIds
    {
        // Terrain
        public static readonly int IslandHeightTex = Shader.PropertyToID("_IslandHeightTex");
        public static readonly int IslandHeightParams = Shader.PropertyToID("_IslandHeightParams");
        public static readonly int TerrainParams = Shader.PropertyToID("_TerrainParams");
        public static readonly int TerrainParams2 = Shader.PropertyToID("_TerrainParams2");

        // Time, wind, player
        public static readonly int IslandTime = Shader.PropertyToID("_IslandTime");
        public static readonly int IslandWind = Shader.PropertyToID("_IslandWind");
        public static readonly int IslandWindParams = Shader.PropertyToID("_IslandWindParams");
        public static readonly int PlayerPosition = Shader.PropertyToID("_PlayerPosition");
        public static readonly int PlayerParams = Shader.PropertyToID("_PlayerParams");

        // Weather
        public static readonly int IslandWetness = Shader.PropertyToID("_IslandWetness");
        public static readonly int RainIntensity = Shader.PropertyToID("_RainIntensity");
        public static readonly int WaveAmplitudeScale = Shader.PropertyToID("_WaveAmplitudeScale");
        public static readonly int WaterRoughness = Shader.PropertyToID("_WaterRoughness");
        public static readonly int CloudParams = Shader.PropertyToID("_CloudParams");
        public static readonly int CloudOffsets = Shader.PropertyToID("_CloudOffsets");
        public static readonly int LightningFlash = Shader.PropertyToID("_LightningFlash");

        // Sky / lighting
        public static readonly int SunDirection = Shader.PropertyToID("_SunDirection");
        public static readonly int MoonDirection = Shader.PropertyToID("_MoonDirection");
        public static readonly int SunColor = Shader.PropertyToID("_SunColorIsland");
        public static readonly int MoonColor = Shader.PropertyToID("_MoonColorIsland");
        public static readonly int SkyZenith = Shader.PropertyToID("_SkyZenithColor");
        public static readonly int SkyHorizon = Shader.PropertyToID("_SkyHorizonColor");
        public static readonly int AmbientSky = Shader.PropertyToID("_AmbientSkyIsland");
        public static readonly int AmbientEquator = Shader.PropertyToID("_AmbientEquatorIsland");
        public static readonly int AmbientGround = Shader.PropertyToID("_AmbientGroundIsland");
        public static readonly int StarParams = Shader.PropertyToID("_StarParams");
        public static readonly int StarTex = Shader.PropertyToID("_StarTex");
        public static readonly int EnvCube = Shader.PropertyToID("_IslandEnvCube");
        public static readonly int NightFactor = Shader.PropertyToID("_NightFactor");
        public static readonly int CubeFace = Shader.PropertyToID("_CubeFace");

        // Fog / underwater
        public static readonly int FogColor = Shader.PropertyToID("_IslandFogColor");
        public static readonly int FogSunColor = Shader.PropertyToID("_IslandFogSunColor");
        public static readonly int FogParams = Shader.PropertyToID("_IslandFogParams");
        public static readonly int UnderwaterParams = Shader.PropertyToID("_UnderwaterParams");
        public static readonly int UnderwaterColor = Shader.PropertyToID("_UnderwaterColor");

        // Water
        public static readonly int WaterShallow = Shader.PropertyToID("_WaterShallowColor");
        public static readonly int WaterDeep = Shader.PropertyToID("_WaterDeepColor");
        public static readonly int WaterParams = Shader.PropertyToID("_WaterParams");
        public static readonly int WaterParams2 = Shader.PropertyToID("_WaterParams2");
        public static readonly int GerstnerWaves = Shader.PropertyToID("_GerstnerWaves");
        public static readonly int GerstnerQ = Shader.PropertyToID("_GerstnerQ");
        public static readonly int GerstnerOmega = Shader.PropertyToID("_GerstnerOmega");
        public static readonly int WaterGridParams = Shader.PropertyToID("_WaterGridParams");

        // Grass
        public static readonly int GrassInstances = Shader.PropertyToID("_GrassInstances");
        public static readonly int GrassParams = Shader.PropertyToID("_GrassParams");
        public static readonly int GrassParams2 = Shader.PropertyToID("_GrassParams2");
        public static readonly int GrassState = Shader.PropertyToID("_GrassState");
        public static readonly int GrassCounter = Shader.PropertyToID("_GrassCounter");
        public static readonly int GrassCameraPos = Shader.PropertyToID("_GrassCameraPos");

        // Materials (per-material properties)
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        public static readonly int Intensity = Shader.PropertyToID("_Intensity");
        public static readonly int LodFade = Shader.PropertyToID("_LodFade");
    }
}
