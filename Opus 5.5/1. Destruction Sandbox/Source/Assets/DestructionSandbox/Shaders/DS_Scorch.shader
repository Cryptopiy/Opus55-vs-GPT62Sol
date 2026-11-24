// Ground scorch decal drawn on a flat quad: noisy charred disc with a soot ring and dying embers.
// _Fade goes 1 -> 0 over the decal lifetime (driven from C#).
Shader "DestructionSandbox/Scorch"
{
    Properties
    {
        _Color("Char Color", Color) = (0.05, 0.045, 0.04, 1)
        [HDR] _EmberColor("Ember Color", Color) = (3.0, 0.9, 0.2, 1)
        _Fade("Fade", Range(0, 1)) = 1
        _Embers("Embers", Range(0, 1)) = 0
        _Seed("Seed", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            Offset -1, -1

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DS_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half4 _EmberColor;
                float _Fade;
                float _Embers;
                float _Seed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 c = input.uv * 2.0 - 1.0;
                float r = length(c);
                float n = DSValueNoise2(c * 3.0 + _Seed) * 0.6 + DSValueNoise2(c * 9.0 - _Seed) * 0.4;
                float edge = r + (n - 0.5) * 0.45;
                float body = 1.0 - smoothstep(0.45, 0.95, edge);
                float streaks = 0.75 + 0.25 * DSValueNoise2(float2(atan2(c.y, c.x) * 6.0, r * 2.0) + _Seed);
                float alpha = saturate(body * streaks) * 0.9 * _Fade;
                float ember = smoothstep(0.72, 0.9, DSValueNoise2(c * 14.0 + _Seed * 3.1)) * body * _Embers;
                float3 color = _Color.rgb + _EmberColor.rgb * ember;
                return half4(color, saturate(alpha + ember * _Fade));
            }
            ENDHLSL
        }
    }
}
