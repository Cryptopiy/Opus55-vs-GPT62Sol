// Expanding explosion shockwave: additive fresnel shell (bright rim, transparent centre).
Shader "DestructionSandbox/Shockwave"
{
    Properties
    {
        [HDR] _Color("Color", Color) = (2.5, 1.8, 1.1, 1)
        _FresnelPower("Fresnel Power", Float) = 2.5
        _Fade("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DS_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _FresnelPower;
                float _Fade;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; half3 normalWS : TEXCOORD1; float3 positionOS : TEXCOORD2; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.positionOS = input.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize((float3)input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float rim = pow(1.0 - saturate(abs(dot(n, v))), _FresnelPower);
                float streaks = 0.75 + 0.25 * DSValueNoise(input.positionOS * 9.0);
                float a = saturate(rim * streaks) * _Fade;
                return half4(_Color.rgb * a, a);
            }
            ENDHLSL
        }
    }
}
