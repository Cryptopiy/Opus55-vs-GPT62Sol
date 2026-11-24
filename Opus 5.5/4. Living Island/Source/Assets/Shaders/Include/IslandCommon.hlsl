#ifndef ISLAND_COMMON_INCLUDED
#define ISLAND_COMMON_INCLUDED
// Shared by every lit island shader: URP lighting (main light with 4-cascade soft shadows, Forward+
// additional lights with shadows, SSAO), hemisphere ambient, the procedural environment cubemap,
// exponential height fog / underwater fog, wetness, wind gusts and sun-projected caustics.

// The game always renders cascaded soft main-light shadows ("shadows always on"), so these are fixed
// defines instead of keywords (halves the variant count; URP picks the soft quality per light at runtime).
#define _MAIN_LIGHT_SHADOWS_CASCADE 1
#define _SHADOWS_SOFT 1

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "IslandNoise.hlsl"

float  _IslandTime;
float4 _IslandWind;            // xy = direction, z = strength, w = gust strength
float4 _IslandWindParams;      // x = gust scroll speed, y = gust noise scale
float4 _PlayerPosition;        // xyz = player feet, w = 1 while swimming
float4 _PlayerParams;          // x = grass bend radius
float  _IslandWetness;
float  _RainIntensity;
float  _NightFactor;
float  _LightningFlash;
float4 _SunDirection;          // xyz = towards the sun, w = sun above horizon (0..1)
float4 _MoonDirection;         // xyz = towards the moon, w = night factor
float4 _SunColorIsland;
float4 _MoonColorIsland;
float4 _AmbientSkyIsland, _AmbientEquatorIsland, _AmbientGroundIsland;
float4 _IslandFogColor, _IslandFogSunColor;
float4 _IslandFogParams;       // x = density at reference height, y = height falloff, z = reference height, w = max opacity
float4 _UnderwaterParams;      // x = camera underwater, y = underwater density, z = water height at camera
float4 _UnderwaterColor;
float4 _WaterParams2;          // x = sea level, y = underwater visibility
TEXTURECUBE(_IslandEnvCube);
SAMPLER(sampler_IslandEnvCube);

struct IslandSurface
{
    half3 albedo;
    half3 normalWS;
    half smoothness;
    half metallic;
    half occlusion;
    half3 emission;
    half translucency;
};

IslandSurface IslandDefaultSurface(half3 albedo, half3 normalWS)
{
    IslandSurface s;
    s.albedo = albedo; s.normalWS = normalWS; s.smoothness = 0.2h; s.metallic = 0.0h;
    s.occlusion = 1.0h; s.emission = 0.0h; s.translucency = 0.0h;
    return s;
}

half3 IslandAmbient(half3 n)
{
    half up = n.y;
    return up >= 0.0h ? lerp(_AmbientEquatorIsland.rgb, _AmbientSkyIsland.rgb, up)
                      : lerp(_AmbientEquatorIsland.rgb, _AmbientGroundIsland.rgb, -up);
}

// Wet look through the global _IslandWetness (I-18): darker albedo, higher smoothness on exposed faces.
void IslandApplyWetness(inout half3 albedo, inout half smoothness, half3 normalWS, half porosity)
{
    half wet = _IslandWetness * saturate(normalWS.y * 1.4h + 0.25h);
    albedo *= lerp(1.0h, 1.0h - 0.45h * porosity, wet);
    smoothness = lerp(smoothness, max(smoothness, 0.68h), wet);
}

float IslandGust(float2 xz)
{
    float2 p = xz * _IslandWindParams.y - _IslandWind.xy * (_IslandTime * _IslandWindParams.x * _IslandWindParams.y);
    return IslandValueNoise(p) * 0.65 + IslandValueNoise(p * 2.7 + 13.1) * 0.35;
}

// Animated caustics on the sea floor, projected along the sun direction from the water surface (I-07).
float IslandCaustics(float3 posWS)
{
    float depth = _WaterParams2.x - posWS.y;
    if (depth <= 0.02) return 0.0;
    float3 L = _SunDirection.xyz;
    float2 uv = posWS.xz + L.xz / max(L.y, 0.25) * depth;
    float t = _IslandTime;
    float2 v1 = IslandVoronoi(uv * 0.42 + float2(t * 0.05, t * 0.03), t * 0.9);
    float2 v2 = IslandVoronoi(uv * 0.58 - float2(t * 0.04, -t * 0.02) + 7.3, t * 1.1 + 2.0);
    float c1 = 1.0 - smoothstep(0.0, 0.16, v1.y - v1.x);
    float c2 = 1.0 - smoothstep(0.0, 0.16, v2.y - v2.x);
    float c = c1 * c2 * 2.2 + (c1 + c2) * 0.18;
    return c * saturate(depth * 2.0) * exp(-depth * 0.11) * saturate(L.y * 3.0) * _SunDirection.w;
}

// ---------------------------------------------------------------- fog
float IslandFogAmount(float3 posWS, out float3 dir)
{
    float3 cam = _WorldSpaceCameraPos;
    float3 v = posWS - cam;
    float dist = length(v);
    dir = v / max(dist, 1e-3);
    float b = _IslandFogParams.y;
    float rhoCam = _IslandFogParams.x * exp(-b * (max(cam.y, -2.0) - _IslandFogParams.z));
    float k = b * v.y;
    // Analytic integral of an exponential height fog along the view ray.
    float integral = abs(k) > 1e-3 ? (1.0 - exp(-k)) / k : 1.0 - 0.5 * k;
    float tau = rhoCam * dist * integral;
    return min(1.0 - exp(-tau), _IslandFogParams.w);
}

float3 IslandFogColor(float3 dir)
{
    float sun = pow(saturate(dot(dir, _SunDirection.xyz)), 6.0);
    return _IslandFogColor.rgb + _IslandFogSunColor.rgb * sun;
}

float3 IslandApplyFog(float3 color, float3 posWS)
{
    if (_UnderwaterParams.x > 0.5)
    {
        float dist = distance(posWS, _WorldSpaceCameraPos);
        return lerp(color, _UnderwaterColor.rgb, 1.0 - exp(-dist * _UnderwaterParams.y));
    }
    float3 dir;
    float fog = IslandFogAmount(posWS, dir);
    return lerp(color, IslandFogColor(dir), fog);
}

// Fog towards infinity (sky): integral of the height fog to infinity along an upward ray.
float3 IslandApplySkyFog(float3 color, float3 dir)
{
    if (_UnderwaterParams.x > 0.5 && dir.y < 0.02) return _UnderwaterColor.rgb;
    float b = _IslandFogParams.y;
    float rhoCam = _IslandFogParams.x * exp(-b * (max(_WorldSpaceCameraPos.y, -2.0) - _IslandFogParams.z));
    float dy = max(dir.y, 0.0);
    float tau = min(rhoCam / (b * max(dy, 1e-3)), rhoCam * 6000.0);
    float fog = min(1.0 - exp(-tau), _IslandFogParams.w);
    return lerp(color, IslandFogColor(dir), fog);
}

// ---------------------------------------------------------------- lighting
half3 IslandLighting(IslandSurface s, float3 positionWS, float4 positionCS, half3 viewDirWS)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalWS = s.normalWS;
    inputData.viewDirectionWS = viewDirWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    half aoDirect = 1.0h, aoIndirect = 1.0h;
    #if defined(_SCREEN_SPACE_OCCLUSION)
        AmbientOcclusionFactor ao = GetScreenSpaceAmbientOcclusion(inputData.normalizedScreenSpaceUV);
        aoDirect = ao.directAmbientOcclusion;
        aoIndirect = ao.indirectAmbientOcclusion;
    #endif

    BRDFData brdf;
    half alpha = 1.0h;
    InitializeBRDFData(s.albedo, s.metallic, half3(0, 0, 0), s.smoothness, alpha, brdf);

    Light mainLight = GetMainLight(inputData.shadowCoord, positionWS, inputData.shadowMask);
    half3 color = LightingPhysicallyBased(brdf, mainLight, s.normalWS, viewDirWS) * aoDirect;
    // Thin foliage transmits light: back-lit leaves and grass glow.
    half3 L = mainLight.direction;
    half back = pow(saturate(dot(-viewDirWS, L)), 4.0h) * 0.8h + saturate(dot(-s.normalWS, L)) * 0.35h;
    color += s.albedo * s.translucency * mainLight.color * (mainLight.shadowAttenuation * mainLight.distanceAttenuation * back);

    #if defined(_ADDITIONAL_LIGHTS)
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, inputData.shadowMask);
        color += LightingPhysicallyBased(brdf, light, s.normalWS, viewDirWS);
        color += s.albedo * s.translucency * light.color * (light.distanceAttenuation * light.shadowAttenuation * saturate(dot(-s.normalWS, light.direction)) * 0.5h);
    LIGHT_LOOP_END
    #endif

    half3 indirectDiffuse = IslandAmbient(s.normalWS) * (s.occlusion * aoIndirect);
    half3 R = reflect(-viewDirWS, s.normalWS);
    half mip = PerceptualRoughnessToMipmapLevel(brdf.perceptualRoughness);
    half3 indirectSpecular = SAMPLE_TEXTURECUBE_LOD(_IslandEnvCube, sampler_IslandEnvCube, R, mip).rgb * (s.occlusion * aoIndirect);
    half fresnel = Pow4(1.0h - saturate(dot(s.normalWS, viewDirWS)));
    color += EnvironmentBRDF(brdf, indirectDiffuse, indirectSpecular, fresnel);
    color += s.emission;
    return color;
}

// Shadow-caster clip position with URP's depth/normal bias (directional or punctual light).
float3 _LightDirection;
float3 _LightPosition;
float4 IslandShadowClip(float3 positionWS, float3 normalWS)
{
    #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
        float3 lightDir = normalize(_LightPosition - positionWS);
    #else
        float3 lightDir = _LightDirection;
    #endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDir));
    return ApplyShadowClamping(positionCS);
}

#endif
