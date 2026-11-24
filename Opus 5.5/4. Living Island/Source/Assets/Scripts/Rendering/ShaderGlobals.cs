using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Pushes the static numbers of <see cref="GameConfig"/> that shaders need (water colours, foam depth,
    /// underwater visibility, grass rules, terrain shading thresholds, ...) as global shader properties,
    /// so no shader hard-codes a specification number.
    /// </summary>
    public static class ShaderGlobals
    {
        static readonly int GrassBladeWidthId = Shader.PropertyToID("_GrassBladeWidth");
        static readonly int CloudHeightsId = Shader.PropertyToID("_CloudHeights");

        public static void PushStatic()
        {
            Shader.SetGlobalColor(ShaderIds.WaterShallow, C.ShallowWaterColor.linear);
            Shader.SetGlobalColor(ShaderIds.WaterDeep, C.DeepWaterColor.linear);
            // x: foam depth, y: shallow->deep depth, z: refraction strength, w: shore wave damping depth
            Shader.SetGlobalVector(ShaderIds.WaterParams, new Vector4(C.FoamDepth, C.ShallowToDeepDepth, C.RefractionStrength, C.ShoreWaveDampDepth));
            // x: sea level, y: underwater visibility (m)
            Shader.SetGlobalVector(ShaderIds.WaterParams2, new Vector4(C.SeaLevel, C.UnderwaterVisibility, 0f, 0f));
            // Terrain splat thresholds (I-03): wet sand height, dry sand max height, grass max slope, rock min slope
            Shader.SetGlobalVector(ShaderIds.TerrainParams, new Vector4(C.WetSandHeight, C.DrySandMaxHeight, C.GrassMaxSlopeDeg, C.RockMinSlopeDeg));
            // dirt min slope, bare-rock height, grass min ground height, grass blade height
            Shader.SetGlobalVector(ShaderIds.TerrainParams2, new Vector4(C.DirtMinSlopeDeg, C.SnowlessRockHeight, C.GrassMinGround, C.GrassBladeHeight));
            Shader.SetGlobalVector(ShaderIds.PlayerParams, new Vector4(C.PlayerGrassBendRadius, 0f, 0f, 0f));
            Shader.SetGlobalFloat(GrassBladeWidthId, C.GrassBladeWidth);
            Shader.SetGlobalVector(CloudHeightsId, new Vector4(C.CloudLayerLowHeight, C.CloudLayerHighHeight, 0f, 0f));
            WaveModel.PushToShaders();
        }

        /// <summary>Per-frame globals shared by many shaders (clock, player position).</summary>
        public static void PushFrame(float time, Vector3 playerPos, bool swimming)
        {
            Shader.SetGlobalFloat(ShaderIds.IslandTime, time);
            Shader.SetGlobalVector(ShaderIds.PlayerPosition, new Vector4(playerPos.x, playerPos.y, playerPos.z, swimming ? 1f : 0f));
        }
    }
}
