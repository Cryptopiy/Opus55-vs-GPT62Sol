// Skybox material shader (RenderSettings.skybox). Single pass on purpose: skybox rendering draws every
// pass of the shader, so the cubemap-face variant lives in SkyCube.shader.
Shader "Island/Sky"
{
    Properties { }
    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Sky"
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Include/SkyCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                o.dir = v.positionOS.xyz;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float pixelAngle = length(fwidth(dir));
                return half4(IslandSkyColor(dir, pixelAngle, true), 1.0);
            }
            ENDHLSL
        }
    }
}
