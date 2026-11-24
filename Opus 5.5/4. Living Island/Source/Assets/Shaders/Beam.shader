// Lighthouse beam (I-23): additive cone that reads as volumetric - soft silhouette edges from the view
// angle, falloff along the beam, drifting dust noise, soft intersections with the depth texture and
// extra scattering when the fog is dense. _Intensity follows night/fog (Lighthouse.cs).
Shader "Island/Beam"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 0.88, 0.62, 1)
        _Intensity ("Intensity", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Beam"
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
            float _Intensity;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; float2 uv : TEXCOORD2; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = v.uv;
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 Frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                float3 V = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 n = normalize(i.normalWS) * (isFront ? 1.0 : -1.0);
                float edge = pow(saturate(abs(dot(n, V))), 1.6);           // thicker looking through the middle
                float along = pow(saturate(1.0 - i.uv.y), 2.0) + exp(-i.uv.y * 25.0) * 1.5;
                float dust = 0.65 + 0.35 * IslandFbm(i.positionWS.xz * 0.12 + i.positionWS.y * 0.05 + _IslandTime * 0.08, 3);
                float sceneEye = LinearEyeDepth(SampleSceneDepth(GetNormalizedScreenSpaceUV(i.positionCS)), _ZBufferParams);
                float fragEye = -TransformWorldToView(i.positionWS).z;
                float soft = saturate((sceneEye - fragEye) * 0.5);
                float fogBoost = 1.0 + saturate(_IslandFogParams.x * 15.0) * 1.5;   // more scattering in fog
                float3 dummy;
                float fog = IslandFogAmount(i.positionWS, dummy);
                float3 color = _BaseColor.rgb * (edge * along * dust * soft * _Intensity * 0.24 * fogBoost * (1.0 - fog * 0.6));
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}
