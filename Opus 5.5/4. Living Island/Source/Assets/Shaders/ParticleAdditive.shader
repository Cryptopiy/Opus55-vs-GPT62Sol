// Additive particles: fire, sparks, embers, fireflies. _Shape 0 = soft glow dot, 1 = flickering flame
// tongue. Soft-particle fade against the depth texture; dimmed by the height/underwater fog.
Shader "Island/ParticleAdditive"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Shape ("Shape (0 glow, 1 flame)", Float) = 0
        _Intensity ("Intensity", Float) = 3
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Particles"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
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
            float _Intensity;
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
                float2 d = i.uv * 2.0 - 1.0;
                float shape;
                if (_Shape < 0.5)
                {
                    float r2 = dot(d, d);
                    shape = exp(-r2 * 5.0) + exp(-r2 * 40.0) * 0.8;
                }
                else
                {
                    // Flame tongue: narrow towards the top, edge distorted by rising noise.
                    float n = IslandValueNoise(float2(d.x * 2.5, d.y * 2.0 - _IslandTime * 3.0) + i.positionWS.xz * 3.0);
                    float width = lerp(0.75, 0.15, saturate(d.y * 0.5 + 0.5)) * (0.7 + 0.6 * n);
                    shape = smoothstep(width, width * 0.3, abs(d.x)) * smoothstep(1.0, 0.3, abs(d.y)) * (0.6 + 0.6 * n);
                }
                float sceneEye = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)), _ZBufferParams);
                float fragEye = -TransformWorldToView(i.positionWS).z;
                float soft = saturate((sceneEye - fragEye) * 3.0);
                float3 dummyDir;
                float fog = _UnderwaterParams.x > 0.5 ? 1.0 - exp(-distance(i.positionWS, _WorldSpaceCameraPos) * _UnderwaterParams.y)
                                                       : IslandFogAmount(i.positionWS, dummyDir);
                float3 color = _BaseColor.rgb * i.color.rgb * (shape * i.color.a * soft * _Intensity * (1.0 - fog));
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
