// Renders the procedural sky into one face of the environment cubemap (SkyController), the procedural
// substitute for a reflection probe that follows the time of day and the weather (I-16).
Shader "Hidden/Island/SkyCube"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "CubeFace"
            ZWrite Off
            ZTest Always
            Cull Off
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Include/SkyCommon.hlsl"

            float _CubeFace;

            struct Varyings { float4 positionCS : SV_POSITION; float2 ndc : TEXCOORD0; };

            Varyings Vert(uint id : SV_VertexID)
            {
                // Full-screen triangle.
                float2 uv = float2((id << 1) & 2, id & 2);
                Varyings o;
                o.ndc = uv * 2.0 - 1.0;
                o.positionCS = float4(o.ndc, 0.0, 1.0);
                return o;
            }

            float3 FaceDirection(float2 ndc, int face)
            {
                // Hardware cubemap convention: sc/tc in [-1,1], tc grows downwards from the top row.
                #if UNITY_UV_STARTS_AT_TOP
                    float sc = ndc.x, tc = -ndc.y;
                #else
                    float sc = ndc.x, tc = ndc.y;
                #endif
                float3 d;
                if (face == 0) d = float3(1.0, -tc, -sc);
                else if (face == 1) d = float3(-1.0, -tc, sc);
                else if (face == 2) d = float3(sc, 1.0, tc);
                else if (face == 3) d = float3(sc, -1.0, -tc);
                else if (face == 4) d = float3(sc, -tc, 1.0);
                else d = float3(-sc, -tc, -1.0);
                return normalize(d);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 dir = FaceDirection(i.ndc, (int)round(_CubeFace));
                return half4(IslandSkyColor(dir, 0.01, false), 1.0);
            }
            ENDHLSL
        }
    }
}
