// Rain streaks for stretched-billboard particles (I-18): thin bright line, lit by the sky and the
// lightning flash, fading at both ends.
Shader "Island/Rain"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Rain"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Include/IslandCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _RainPad;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; float3 positionWS : TEXCOORD1; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float across = 1.0 - abs(i.uv.x * 2.0 - 1.0);
                float along = smoothstep(0.0, 0.3, i.uv.y) * smoothstep(1.0, 0.6, i.uv.y);
                float alpha = across * across * along * i.color.a * 0.7;
                // Streaks catch the sky light; a small floor keeps them readable in dark storms.
                float3 light = IslandAmbient(half3(0, 1, 0)) * 2.2 + _MainLightColor.rgb * 0.25 + 0.06 + _LightningFlash * 2.5;
                float3 color = IslandApplyFog(i.color.rgb * light, i.positionWS);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
