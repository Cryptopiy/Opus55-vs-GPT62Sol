#ifndef ISLAND_TERRAIN_COMMON_INCLUDED
#define ISLAND_TERRAIN_COMMON_INCLUDED
// Shared by Terrain.shader (splat shading, I-03) and GrassPlacement.compute (grass mask, I-10), so the
// blades grow exactly where the ground is painted as grass. Plain HLSL (no URP macros).
#include "IslandNoise.hlsl"

Texture2D<float> _IslandHeightTex;
float4 _IslandHeightParams;   // x = origin, y = spacing, z = samples per side, w = 1 / spacing
float4 _TerrainParams;        // x = wet sand height, y = dry sand max height, z = grass max slope, w = rock min slope
float4 _TerrainParams2;       // x = dirt min slope, y = bare rock height, z = grass min ground, w = blade height

float IslandHeightSample(int2 c)
{
    int n = (int)_IslandHeightParams.z;
    c = clamp(c, int2(0, 0), int2(n - 1, n - 1));
    return _IslandHeightTex.Load(int3(c, 0));
}

// Exact height of the triangulated terrain mesh (same diagonal split as TerrainBuilder.cs).
float IslandHeight(float2 xz)
{
    float2 g = (xz - _IslandHeightParams.x) * _IslandHeightParams.w;
    float n1 = _IslandHeightParams.z - 1.0001;
    g = clamp(g, 0.0, n1);
    int2 c = (int2)floor(g);
    float2 f = g - c;
    float h00 = IslandHeightSample(c);
    float h10 = IslandHeightSample(c + int2(1, 0));
    float h01 = IslandHeightSample(c + int2(0, 1));
    float h11 = IslandHeightSample(c + int2(1, 1));
    return f.x >= f.y ? h00 + (h10 - h00) * f.x + (h11 - h10) * f.y
                      : h00 + (h11 - h01) * f.x + (h01 - h00) * f.y;
}

float3 IslandSampleNormal(int2 c)
{
    float dx = IslandHeightSample(c - int2(1, 0)) - IslandHeightSample(c + int2(1, 0));
    float dz = IslandHeightSample(c - int2(0, 1)) - IslandHeightSample(c + int2(0, 1));
    return normalize(float3(dx, 2.0 * _IslandHeightParams.y, dz));
}

// Bilinear blend of the per-sample normals (identical to IslandData.NormalAt on the CPU).
float3 IslandNormal(float2 xz)
{
    float2 g = clamp((xz - _IslandHeightParams.x) * _IslandHeightParams.w, 0.0, _IslandHeightParams.z - 1.0001);
    int2 c = (int2)floor(g);
    float2 f = g - c;
    float3 a = lerp(IslandSampleNormal(c), IslandSampleNormal(c + int2(1, 0)), f.x);
    float3 b = lerp(IslandSampleNormal(c + int2(0, 1)), IslandSampleNormal(c + int2(1, 1)), f.x);
    return normalize(lerp(a, b, f.y));
}

struct IslandSplat
{
    float wetSand;    // 0..1 darker, smoother sand near the waterline
    float grass;      // 0 = sand, 1 = vegetated ground
    float grassTint;  // 0 = lush green, 1 = dry yellow-green
    float dirt;
    float rock;
};

IslandSplat IslandComputeSplat(float3 p, float3 n)
{
    IslandSplat s;
    float slope = degrees(acos(saturate(n.y)));
    float nLarge = IslandValueNoise(p.xz * 0.011 + 5.3);
    float nMed = IslandValueNoise(p.xz * 0.075 + 17.3);
    float nFine = IslandValueNoise(p.xz * 0.33 + 3.1);

    float sandLine = _TerrainParams.y + (nMed - 0.5) * 0.9 + (nLarge - 0.5) * 0.8;
    s.grass = smoothstep(sandLine - 0.3, sandLine + 0.3, p.y);

    float dirtSlope = smoothstep(_TerrainParams2.x - 5.0, _TerrainParams2.x + 3.0, slope + (nMed - 0.5) * 8.0);
    float dirtPatch = smoothstep(0.66, 0.76, nLarge * 0.7 + nMed * 0.3) * s.grass;
    s.dirt = saturate(max(dirtSlope, dirtPatch));

    float rockSlope = smoothstep(_TerrainParams.w - 6.0, _TerrainParams.w + 3.0, slope + (nMed - 0.5) * 10.0);
    float rockPeak = smoothstep(_TerrainParams2.y - 3.0, _TerrainParams2.y + 3.0, p.y + (nMed - 0.5) * 6.0) * smoothstep(6.0, 18.0, slope);
    s.rock = saturate(max(rockSlope, rockPeak));

    s.wetSand = smoothstep(_TerrainParams.x + 0.3, _TerrainParams.x - 0.15, p.y + (nFine - 0.5) * 0.15);
    s.grassTint = smoothstep(0.35, 0.65, nLarge + (nMed - 0.5) * 0.3);
    return s;
}

// Where grass blades may grow: painted grass, not dirt/rock, above the configured minimum height.
float IslandGrassMask(float3 p, float3 n)
{
    IslandSplat s = IslandComputeSplat(p, n);
    float slope = degrees(acos(saturate(n.y)));
    float slopeOk = 1.0 - smoothstep(_TerrainParams.z - 4.0, _TerrainParams.z, slope);
    return s.grass * (1.0 - s.dirt) * (1.0 - s.rock) * slopeOk * step(_TerrainParams2.z, p.y);
}

#endif
