// Generic lit material for the Blender props (lighthouse, boat, logs) and the primitive character.
// _EmissionColor glows always; _NightEmissionColor glows with the night factor (lit windows, lamp).
// _NoiseAmount adds subtle world-space variation so flat colours do not look plastic.
Shader "Island/Prop"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.7, 0.7, 0.7, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.3
        _Metallic ("Metallic", Range(0, 1)) = 0
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        [HDR] _NightEmissionColor ("Night Emission", Color) = (0, 0, 0, 1)
        _NoiseAmount ("Variation", Range(0, 1)) = 0
        _Porosity ("Wet darkening", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Include/IslandCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _Smoothness;
        float _Metallic;
        float4 _EmissionColor;
        float4 _NightEmissionColor;
        float _NoiseAmount;
        float _Porosity;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionOS : TEXCOORD2; };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.positionOS = v.positionOS.xyz;
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

            half4 Frag(Varyings i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 n = normalize(i.normalWS);
                // Object-space variation (moves with the object): wood grain / weathering streaks.
                float grain = IslandValueNoise(float2(i.positionOS.x * 1.5 + i.positionOS.z * 0.7, i.positionOS.y * 14.0 + i.positionOS.z * 9.0));
                float blot = IslandValueNoise(i.positionOS.xz * 3.1 + i.positionOS.y * 2.3);
                half3 albedo = _BaseColor.rgb * lerp(1.0, 0.78 + 0.3 * grain + 0.12 * blot, _NoiseAmount);
                half smoothV = _Smoothness;
                IslandApplyWetness(albedo, smoothV, n, _Porosity);
                IslandSurface s = IslandDefaultSurface(albedo, n);
                s.smoothness = smoothV;
                s.metallic = _Metallic;
                s.emission = _EmissionColor.rgb + _NightEmissionColor.rgb * _NightFactor;
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
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                return IslandShadowClip(TransformObjectToWorld(v.positionOS.xyz), TransformObjectToWorldNormal(v.normalOS));
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
