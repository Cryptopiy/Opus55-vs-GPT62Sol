// Procedural gradient skybox (zenith / horizon / ground) with a soft sun disc and halo.
Shader "DestructionSandbox/SkyGradient"
{
    Properties
    {
        _SkyZenith("Zenith", Color) = (0.2, 0.42, 0.78, 1)
        _SkyHorizon("Horizon", Color) = (0.78, 0.84, 0.9, 1)
        _SkyGround("Ground", Color) = (0.33, 0.31, 0.29, 1)
        _SunDirection("Direction To Sun", Vector) = (0.4, 0.6, -0.5, 0)
        _SunColor("Sun Color", Color) = (1, 0.9, 0.76, 1)
        _SunSize("Sun Disc Cosine", Float) = 0.9995
        _Exposure("Exposure", Float) = 1
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "DS_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyZenith;
                half4 _SkyHorizon;
                half4 _SkyGround;
                float4 _SunDirection;
                half4 _SunColor;
                float _SunSize;
                float _Exposure;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.dir = input.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.dir);
                float3 col = DSSkyColor(dir, _SkyZenith.rgb, _SkyHorizon.rgb, _SkyGround.rgb);
                float3 sunDir = normalize(_SunDirection.xyz);
                float cosAngle = dot(dir, sunDir);
                float disc = smoothstep(_SunSize - 0.0004, _SunSize, cosAngle);
                float halo = pow(saturate(cosAngle), 64.0) * 0.35 + pow(saturate(cosAngle), 8.0) * 0.08;
                float aboveHorizon = saturate(dir.y * 20.0 + 0.5);
                col += _SunColor.rgb * (disc * 12.0 + halo) * aboveHorizon;
                return half4(col * _Exposure, 1);
            }
            ENDHLSL
        }
    }
}
