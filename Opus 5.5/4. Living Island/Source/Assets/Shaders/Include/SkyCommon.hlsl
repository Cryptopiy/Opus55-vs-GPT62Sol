#ifndef ISLAND_SKY_COMMON_INCLUDED
#define ISLAND_SKY_COMMON_INCLUDED
// Procedural sky (I-14): zenith/horizon gradient, sun disc + halo, phase-less moon disc + glow, the 1500
// stars of the star-cell texture (fade in at dusk, twinkle), and two layers of wind-driven noise clouds
// lit by the sun (pink/orange at dawn/dusk because they use the time-of-day sun colour).
#include "IslandCommon.hlsl"

float4 _SkyZenithColor;
float4 _SkyHorizonColor;
float4 _CloudParams;     // x = coverage, y = density, z = darkness (storm)
float4 _CloudOffsets;    // xy = low layer scroll (m), zw = high layer scroll (m)
float4 _StarParams;      // x = visibility, y = rotation (rad), z = cells u, w = cells v
Texture2D<float4> _StarTex;
float4 _CloudHeights;    // x = low layer height, y = high layer height (GameConfig)

#define LOW_CLOUD_HEIGHT _CloudHeights.x
#define HIGH_CLOUD_HEIGHT _CloudHeights.y

float3 StarField(float3 dir, float pixelAngle)
{
    float s = sin(_StarParams.y), c = cos(_StarParams.y);
    float3 d = float3(dir.x * c - dir.z * s, dir.y, dir.x * s + dir.z * c);
    float2 cells = _StarParams.zw;
    float az = atan2(d.z, d.x) * (0.5 / PI) + 0.5;
    float2 g = float2(az * cells.x, d.y * cells.y);
    int2 cell = int2(floor(g));
    cell.x = (int)((uint)(cell.x + (int)cells.x) % (uint)cells.x);
    if (cell.y < 0 || cell.y >= (int)cells.y) return 0.0;
    float4 star = _StarTex.Load(int3(cell, 0));
    if (star.b <= 0.0) return 0.0;
    float2 sg = float2(cell) + star.rg;
    float saz = (sg.x / cells.x - 0.5) * 2.0 * PI;
    float sv = sg.y / cells.y;
    float cosE = sqrt(saturate(1.0 - sv * sv));
    float3 sd = float3(cos(saz) * cosE, sv, sin(saz) * cosE);
    float ang = length(d - sd);                       // ~angle in radians for tiny separations
    float radius = max(pixelAngle * 0.85, 0.00035);   // never thinner than about one pixel
    float core = exp(-ang * ang / (radius * radius));
    float twinkle = 0.7 + 0.3 * sin(_IslandTime * (1.7 + star.a * 4.0) + star.a * 60.0);
    float3 tint = lerp(float3(0.72, 0.82, 1.0), float3(1.0, 0.86, 0.68), star.a);
    return tint * (core * star.b * twinkle * 7.0);
}

// Low cumulus layer; returns cloud colour (rgb) and alpha.
float4 LowClouds(float3 dir)
{
    if (dir.y < 0.015) return 0.0;
    float tPlane = (LOW_CLOUD_HEIGHT - _WorldSpaceCameraPos.y) / dir.y;
    float2 pos = _WorldSpaceCameraPos.xz + dir.xz * tPlane;
    float2 uv = (pos + _CloudOffsets.xy) * 0.00042;
    float coverage = _CloudParams.x;
    float threshold = 1.0 - coverage * 0.92;
    float n = IslandFbm(uv, 5);
    float density = saturate((n - threshold) * (2.2 + _CloudParams.y * 3.0));
    if (density <= 0.0) return 0.0;
    // One step towards the sun approximates self-shadowing inside the cloud.
    float nSun = IslandFbm(uv + _SunDirection.xz * 0.02, 4);
    float dSun = saturate((nSun - threshold) * 3.0);
    float lit = exp(-dSun * 2.4);
    float dark = _CloudParams.z;
    float3 sunCol = _SunColorIsland.rgb;
    float3 ambient = _AmbientSkyIsland.rgb * 1.35 + _SkyZenithColor.rgb * 0.25;
    float3 col = ambient * lerp(1.0, 0.32, dark) + sunCol * lit * 0.9 * (1.0 - dark * 0.85);
    col += sunCol * pow(saturate(dot(dir, _SunDirection.xyz)), 10.0) * (1.0 - density) * 1.6;   // silver lining
    col += _MoonColorIsland.rgb * (0.05 * _MoonDirection.w * lit);
    float fade = saturate(dir.y * 9.0);
    return float4(col, density * fade * lerp(0.82, 1.0, _CloudParams.y));
}

// High, streaky cirrus layer stretched along the wind.
float4 HighClouds(float3 dir)
{
    if (dir.y < 0.02) return 0.0;
    float tPlane = (HIGH_CLOUD_HEIGHT - _WorldSpaceCameraPos.y) / dir.y;
    float2 pos = _WorldSpaceCameraPos.xz + dir.xz * tPlane + _CloudOffsets.zw;
    float2 w = normalize(_IslandWind.xy + 1e-4);
    float2 uv = float2(dot(pos, w) * 0.00009, dot(pos, float2(-w.y, w.x)) * 0.00042);
    float n = IslandFbm(uv + IslandFbm(uv * 3.0, 2) * 0.35, 4);
    float a = saturate((n - 0.48) * 2.6) * (0.35 + 0.45 * _CloudParams.x) * saturate(dir.y * 6.0);
    float3 col = _SunColorIsland.rgb * 0.85 + _SkyHorizonColor.rgb * 0.45 + _AmbientSkyIsland.rgb * 0.5;
    col *= lerp(1.0, 0.4, _CloudParams.z);
    return float4(col, a * (1.0 - _CloudParams.z * 0.6));
}

float3 IslandSkyColor(float3 dir, float pixelAngle, bool stars)
{
    float h = dir.y;
    float t = pow(saturate(h), 0.45);
    float3 col = lerp(_SkyHorizonColor.rgb, _SkyZenithColor.rgb, t);
    if (h < 0.0) col = _SkyHorizonColor.rgb * lerp(1.0, 0.6, saturate(-h * 4.0));

    float sunVis = _SunDirection.w;
    float cosSun = dot(dir, _SunDirection.xyz);
    col += _SunColorIsland.rgb * (pow(saturate(cosSun), 6.0) * 0.18 + pow(saturate(cosSun), 90.0) * 0.7) * sunVis;

    if (stars && _StarParams.x > 0.001 && h > 0.0) col += StarField(dir, pixelAngle) * _StarParams.x;

    // Moon: disc with soft maria shading plus a wide glow (no phases).
    float cosMoon = dot(dir, _MoonDirection.xyz);
    float moonVis = _MoonDirection.w * saturate(_MoonDirection.y * 10.0 + 0.3);
    if (moonVis > 0.0)
    {
        float disc = smoothstep(0.99986, 0.99990, cosMoon);        // ~0.9 deg radius
        float maria = 1.0;
        if (disc > 0.0)                                             // surface detail only on the disc itself
        {
            float3 tangent = normalize(cross(_MoonDirection.xyz, float3(0, 1, 0.001)));
            float2 local = float2(dot(dir, tangent), dot(dir, cross(tangent, _MoonDirection.xyz))) * 70.0;
            maria = 0.75 + 0.25 * IslandFbm(local * 2.0 + 3.0, 3);
        }
        float glow = pow(saturate(cosMoon), 900.0) * 0.5 + pow(saturate(cosMoon), 45.0) * 0.07;
        col += _MoonColorIsland.rgb * (disc * 2.2 * maria + glow) * moonVis;
    }
    // Sun disc: very bright HDR so it blooms.
    float sunDisc = smoothstep(0.99994, 0.99996, cosSun);
    col += _SunColorIsland.rgb * sunDisc * 45.0 * sunVis;

    float4 high = HighClouds(dir);
    col = lerp(col, high.rgb, high.a);
    float4 low = LowClouds(dir);
    col = lerp(col, low.rgb, low.a);
    col += _LightningFlash * float3(0.55, 0.6, 0.75) * (0.35 + 1.2 * low.a);

    return IslandApplySkyFog(col, dir);
}

#endif
