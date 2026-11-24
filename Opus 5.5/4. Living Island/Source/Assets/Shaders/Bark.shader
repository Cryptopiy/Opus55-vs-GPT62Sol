// Palm trunk (Blender SM_Palm_*): procedural ring bark from UV.y, vertex-colour driven trunk bend (I-11).
Shader "Island/Bark"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.42, 0.33, 0.24, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.15
        _WindScale ("Wind Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Include/IslandCommon.hlsl"
        #include "Include/VegetationWind.hlsl"
        #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #endif

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _Smoothness;
        float _WindScale;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
        };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            float3 p = TransformObjectToWorld(v.positionOS.xyz);
            o.positionWS = VegetationWind(p, v.color, _WindScale);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.uv = v.uv;
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
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            half4 Frag(Varyings i) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                // Palm trunks show stacked leaf-scar rings: a saw-tooth profile along the height.
                float ringCoord = i.uv.y * 38.0 + IslandValueNoise(float2(i.uv.x * 6.0, i.uv.y * 20.0)) * 0.35;
                float ring = frac(ringCoord);
                float groove = smoothstep(0.0, 0.18, ring) * smoothstep(1.0, 0.75, ring);
                float fibre = IslandValueNoise(float2(i.uv.x * 40.0, i.uv.y * 160.0));
                half3 albedo = _BaseColor.rgb * (0.62 + 0.38 * groove) * (0.88 + 0.24 * fibre);
                albedo = lerp(albedo, albedo * half3(0.85, 0.9, 0.8), saturate(1.0 - i.uv.y * 4.0) * 0.5); // weathered base
                float3 n = normalize(i.normalWS);
                // Rings bulge the surface: tilt the normal up/down on each saw-tooth flank.
                n = normalize(n + float3(0, 1, 0) * ((ring - 0.5) * 0.6 * (1.0 - groove)));
                half smoothV = _Smoothness;
                IslandApplyWetness(albedo, smoothV, n, 0.8);
                IslandSurface s = IslandDefaultSurface(albedo, n);
                s.smoothness = smoothV;
                s.occlusion = 0.75 + 0.25 * groove;
                half3 color = IslandLighting(s, i.positionWS, i.positionCS, viewDir);
                return half4(IslandApplyFog(color, i.positionWS), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            struct SV { float4 positionCS : SV_POSITION; };
            SV ShadowVert(Attributes v)
            {
                SV o;
                float3 p = VegetationWind(TransformObjectToWorld(v.positionOS.xyz), v.color, _WindScale);
                o.positionCS = IslandShadowClip(p, TransformObjectToWorldNormal(v.normalOS));
                return o;
            }
            half4 ShadowFrag(SV i) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            half4 NormalsFrag(Varyings i) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                return half4(NormalizeNormalPerPixel(i.normalWS), 0.0);
            }
            ENDHLSL
        }
    }
}
