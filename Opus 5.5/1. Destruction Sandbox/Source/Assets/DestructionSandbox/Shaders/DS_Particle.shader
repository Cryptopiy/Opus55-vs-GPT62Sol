// Unlit particle / trail shader with procedural sprite shapes (no textures):
// 0 soft puff, 1 spark streak, 2 glass glint, 3 trail ribbon, 4 solid (mesh particles).
// Output is premultiplied: alpha blend = (One, OneMinusSrcAlpha), additive = (One, One).
Shader "DestructionSandbox/Particle"
{
    Properties
    {
        _TintColor("Tint", Color) = (1, 1, 1, 1)
        _Intensity("Intensity", Float) = 1
        _Shape("Shape", Float) = 0
        _SoftDistance("Soft Particle Distance", Float) = 0.4
        [HideInInspector] _SrcBlend("Src Blend", Float) = 1
        [HideInInspector] _DstBlend("Dst Blend", Float) = 10
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "DS_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TintColor;
                float _Intensity;
                float _Shape;
                float _SoftDistance;
                float _SrcBlend;
                float _DstBlend;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
                float eyeDepth : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.color = input.color;
                o.uv = input.uv;
                o.screenPos = ComputeScreenPos(pos.positionCS);
                o.eyeDepth = -pos.positionVS.z;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 c = input.uv * 2.0 - 1.0;
                float alpha;
                if (_Shape < 0.5)
                {
                    // Soft cloud puff: quadratic falloff broken up by noise so puffs never read as discs.
                    float r2 = saturate(dot(c, c));
                    float n = DSValueNoise2(input.uv * 4.0 + input.color.a * 5.3) * 0.6 + DSValueNoise2(input.uv * 9.0 - 3.1) * 0.4;
                    alpha = (1.0 - r2) * (1.0 - r2) * (0.45 + 0.55 * n);
                }
                else if (_Shape < 1.5)
                {
                    alpha = saturate(1.0 - abs(c.y)) * saturate(1.0 - abs(c.x) * abs(c.x));
                    alpha *= alpha;
                }
                else if (_Shape < 2.5)
                {
                    float d = abs(c.x) + abs(c.y);
                    alpha = saturate(1.0 - d) * 1.6 + saturate(1.0 - length(c) * 4.0);
                }
                else if (_Shape < 3.5)
                {
                    alpha = saturate(1.0 - abs(c.y));
                    alpha *= alpha;
                }
                else
                {
                    alpha = 1.0;
                }

                // Soft particles: fade where the sprite intersects opaque geometry.
                float2 suv = input.screenPos.xy / input.screenPos.w;
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(suv), _ZBufferParams);
                float soft = saturate((sceneDepth - input.eyeDepth) / max(_SoftDistance, 1e-3));
                if (_Shape > 3.5) soft = 1.0;

                float a = saturate(alpha * input.color.a * _TintColor.a * soft);
                float3 rgb = input.color.rgb * _TintColor.rgb * _Intensity;
                return half4(rgb * a, a);
            }
            ENDHLSL
        }
    }
}
