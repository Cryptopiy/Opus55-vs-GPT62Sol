// Lighthouse lamp-room glass: transparent, fresnel reflection of the sky cubemap, warm inner glow at night.
Shader "Island/Glass"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (0.6, 0.75, 0.8, 1)
        [HDR] _NightEmissionColor ("Night Glow", Color) = (1.2, 0.9, 0.5, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Glass"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Include/IslandCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float4 _NightEmissionColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 n = normalize(i.normalWS);
                float fresnel = 0.06 + 0.94 * pow(1.0 - saturate(dot(n, V)), 5.0);
                float3 R = reflect(-V, n);
                float3 sky = SAMPLE_TEXTURECUBE_LOD(_IslandEnvCube, sampler_IslandEnvCube, R, 0).rgb;
                float3 glow = _NightEmissionColor.rgb * _NightFactor;
                float3 color = sky * fresnel + _BaseColor.rgb * IslandAmbient(half3(0, 1, 0)) * 0.3 + glow;
                float alpha = saturate(0.18 + fresnel * 0.7 + _NightFactor * 0.25);
                return half4(IslandApplyFog(color, i.positionWS), alpha);
            }
            ENDHLSL
        }
    }
}
