// Alpha-blended particles: smoke, rain splashes, swim splashes. _Shape 0 = soft droplet/splash,
// 1 = billowing smoke puff. Lit by ambient + sun, soft against the depth texture, fogged.
Shader "Island/ParticleAlpha"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Shape ("Shape (0 soft, 1 smoke)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Particles"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Include/IslandCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;
            float _Shape;
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
                clip(i.positionWS.y + 0.3);     // splash droplets vanish once they fall back into the sea
                float2 d = i.uv * 2.0 - 1.0;
                float r = length(d);
                float alpha;
                if (_Shape < 0.5) alpha = smoothstep(1.0, 0.35, r);
                else
                {
                    float n = IslandFbm(i.uv * 3.0 + i.positionWS.xy * 0.4 + _IslandTime * 0.05, 3);
                    alpha = smoothstep(1.0, 0.2, r + (n - 0.5) * 0.7) * (0.6 + 0.5 * n);
                }
                float sceneEye = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)), _ZBufferParams);
                float fragEye = -TransformWorldToView(i.positionWS).z;
                alpha *= saturate((sceneEye - fragEye) * 2.0) * i.color.a * _BaseColor.a;
                Light ml = GetMainLight();
                float3 light = IslandAmbient(half3(0, 1, 0)) + ml.color * 0.55;
                float3 color = IslandApplyFog(_BaseColor.rgb * i.color.rgb * light, i.positionWS);
                return half4(color, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
