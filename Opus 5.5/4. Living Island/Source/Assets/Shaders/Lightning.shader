// Procedural lightning bolt ribbons (I-19): hot white core with a blue glow across the ribbon width.
Shader "Island/Lightning"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Bolt"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Include/IslandCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float x = (i.uv.x - 0.5) * 2.0;
                float core = exp(-x * x * 12.0);
                float glow = exp(-x * x * 1.5) * 0.35;
                float3 dummy;
                float fog = IslandFogAmount(i.positionWS, dummy);
                float3 color = (float3(1.0, 1.0, 1.0) * core * 9.0 + float3(0.55, 0.65, 1.0) * glow * 4.0) * _Intensity * (1.0 - fog * 0.75);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
