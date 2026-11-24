// Gravity orb: swirls and pinches the opaque scene colour behind it (screen-space distortion) and adds a
// pulsing violet glow core with a fresnel rim.
Shader "DestructionSandbox/GravityOrb"
{
    Properties
    {
        [HDR] _GlowColor("Glow Color", Color) = (1.6, 0.7, 3.2, 1)
        [HDR] _RimColor("Rim Color", Color) = (0.6, 0.9, 3.0, 1)
        _Swirl("Swirl Strength", Float) = 3.0
        _Pinch("Pinch", Range(0, 1)) = 0.55
        _SpinSpeed("Spin Speed", Float) = 2.5
        _CoreSize("Core Size", Range(0, 1)) = 0.35
        _Activity("Activity", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+20" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            #include "DS_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _GlowColor;
                half4 _RimColor;
                float _Swirl;
                float _Pinch;
                float _SpinSpeed;
                float _CoreSize;
                float _Activity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 screenPos : TEXCOORD2;
                float4 centerScreen : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.screenPos = ComputeScreenPos(pos.positionCS);
                o.centerScreen = ComputeScreenPos(TransformObjectToHClip(float3(0, 0, 0)));
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize((float3)input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float facing = saturate(dot(n, v));            // 1 at the centre of the disc, 0 at the silhouette

                float2 suv = input.screenPos.xy / input.screenPos.w;
                float2 center = input.centerScreen.xy / input.centerScreen.w;
                float2 d = suv - center;

                // Swirl strongest at the centre, animated; pinch pulls the sampled colour toward the centre.
                float strength = facing * facing * _Activity;
                float angle = strength * _Swirl + _Time.y * _SpinSpeed * strength;
                float s = sin(angle), c = cos(angle);
                float2 dr = float2(d.x * c - d.y * s, d.x * s + d.y * c) * (1.0 - strength * _Pinch);
                float3 scene = SampleSceneColor(center + dr);

                float pulse = 0.8 + 0.2 * sin(_Time.y * 9.0);
                float core = smoothstep(1.0 - _CoreSize, 1.0, facing) * pulse;
                float rim = pow(1.0 - facing, 3.0);
                float noise = DSValueNoise(input.positionWS * 3.0 + _Time.y * 1.7);

                float3 color = scene * (1.0 - core * 0.6) + _GlowColor.rgb * core + _RimColor.rgb * rim * (0.6 + 0.4 * noise);
                float alpha = saturate(0.15 + facing * 1.4);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
