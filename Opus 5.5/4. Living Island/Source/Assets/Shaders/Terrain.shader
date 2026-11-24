// Island terrain (I-03): splat by height and slope computed in the shader (shared rules in
// TerrainCommon.hlsl): wet sand, dry sand, two grass tints, dirt and rock, blended with noise-perturbed
// thresholds. Procedural normal detail: triplanar derivative noise on rock, wind ripples on sand, fine
// bumps on grass/dirt. Everything is computed, so there is no texture tiling at any distance.
Shader "Island/Terrain"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Include/IslandCommon.hlsl"
        #include "Include/TerrainCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float _TerrainPad;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
        };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            // Triplanar normal perturbation from derivative noise (whiteout blend), for rock faces.
            float3 TriplanarBump(float3 p, float3 n, float scale, float strength)
            {
                float3 w = pow(abs(n), 4.0);
                w /= (w.x + w.y + w.z);
                float3 gx = IslandFbmD(p.zy * scale, 3);
                float3 gy = IslandFbmD(p.xz * scale + 11.7, 3);
                float3 gz = IslandFbmD(p.xy * scale + 23.1, 3);
                float3 nx = float3(0.0, gx.z, gx.y) * sign(n.x);
                float3 ny = float3(gy.y, 0.0, gy.z) * sign(n.y);
                float3 nz = float3(gz.y, gz.z, 0.0) * sign(n.z);
                return normalize(n - (nx * w.x + ny * w.y + nz * w.z) * strength);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 p = i.positionWS;
                float3 nGeo = normalize(i.normalWS);
                float3 viewDir = normalize(_WorldSpaceCameraPos - p);
                float dist = distance(p, _WorldSpaceCameraPos);
                float detail = saturate(1.0 - dist / 110.0);      // fade micro detail with distance (no shimmer)
                IslandSplat sp = IslandComputeSplat(p, nGeo);

                float nFine = IslandValueNoise(p.xz * 1.7);
                float nMed = IslandValueNoise(p.xz * 0.21 + 4.4);
                float nBig = IslandValueNoise(p.xz * 0.035 + 9.1);

                // ---------------- normals
                float3 n = nGeo;
                // Sand: wind ripples perpendicular to the wind, broken up by noise.
                float2 wdir = normalize(_IslandWind.xy + 1e-4);
                float rippleCoord = dot(p.xz, wdir) * 5.5 + IslandValueNoise(p.xz * 0.6) * 5.0 + IslandValueNoise(p.xz * 2.1) * 1.5;
                float rippleAmp = smoothstep(0.3, 0.7, IslandValueNoise(p.xz * 0.15 + 2.0));   // patchy, not corduroy
                float ripple = cos(rippleCoord) * rippleAmp * (1.0 - sp.wetSand) * (1.0 - sp.grass);
                n = normalize(n + float3(wdir.x, 0.0, wdir.y) * ripple * 0.05 * detail);
                // Grass/dirt ground: fine bumps.
                float3 gb = IslandFbmD(p.xz * 2.3, 2);
                n = normalize(n - float3(gb.y, 0.0, gb.z) * 0.06 * detail * sp.grass);
                // Rock: triplanar noise relief.
                if (sp.rock > 0.01)
                {
                    float3 rn = TriplanarBump(p, n, 0.55, 0.9 * (0.35 + 0.65 * detail));
                    n = normalize(lerp(n, rn, sp.rock));
                }

                // ---------------- albedo / smoothness
                half3 drySand = half3(0.50, 0.43, 0.31) * (0.9 + 0.12 * nFine + 0.1 * nMed);
                half3 wetSand = half3(0.25, 0.21, 0.15) * (0.95 + 0.1 * nFine);
                half3 sand = lerp(drySand, wetSand, sp.wetSand);
                half smoothV = lerp(0.12, 0.45, sp.wetSand);

                half3 grassLush = half3(0.19, 0.34, 0.08);
                half3 grassDry = half3(0.40, 0.41, 0.16);
                half3 grass = lerp(grassLush, grassDry, sp.grassTint) * (0.82 + 0.3 * nMed + 0.12 * nFine);
                half3 dirt = half3(0.32, 0.24, 0.16) * (0.85 + 0.3 * nFine);
                float strata = 0.92 + 0.06 * sin(p.y * 1.3 + nMed * 4.0 + nBig * 3.0) + 0.1 * (nBig - 0.5);
                half3 rock = half3(0.41, 0.39, 0.36) * strata * (0.85 + 0.25 * IslandValueNoise(p.xz * 0.9 + p.y));

                half3 albedo = lerp(sand, grass, sp.grass);
                smoothV = lerp(smoothV, 0.08, sp.grass);
                albedo = lerp(albedo, dirt, sp.dirt);
                smoothV = lerp(smoothV, 0.1, sp.dirt);
                albedo = lerp(albedo, rock, sp.rock);
                smoothV = lerp(smoothV, 0.25, sp.rock);

                // Underwater ground: absorbed light makes it darker and greener with depth.
                float depth = _WaterParams2.x - p.y;
                if (depth > 0.0)
                {
                    albedo *= lerp(half3(1, 1, 1), half3(0.55, 0.78, 0.8), saturate(depth / 6.0));
                    smoothV = max(smoothV, 0.4);
                }
                IslandApplyWetness(albedo, smoothV, n, 1.0);

                IslandSurface s = IslandDefaultSurface(albedo, n);
                s.smoothness = smoothV;
                s.occlusion = lerp(1.0, 0.75 + 0.25 * nFine, sp.grass * detail);
                half3 color = IslandLighting(s, p, i.positionCS, viewDir);

                // Caustics are light focused by the waves: add them as extra sunlight on the floor.
                float caustic = IslandCaustics(p);
                if (caustic > 0.0)
                {
                    Light ml = GetMainLight(TransformWorldToShadowCoord(p), p, half4(1, 1, 1, 1));
                    color += albedo * caustic * _SunColorIsland.rgb * ml.shadowAttenuation * 1.4;
                }
                color = IslandApplyFog(color, p);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                return IslandShadowClip(p, TransformObjectToWorldNormal(v.normalOS));
            }
            half4 ShadowFrag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            half4 NormalsFrag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
}
