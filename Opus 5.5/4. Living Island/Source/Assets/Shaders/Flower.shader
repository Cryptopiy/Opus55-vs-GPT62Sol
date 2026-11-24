// Flower patches (I-12): each flower is a thin camera-facing stem plus a billboarded coloured dot with a
// pollen centre, swaying with the wind. Vertices arrive collapsed at the flower base (see FoliageFactory).
Shader "Island/Flower"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Include/IslandCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float _FlowerPad;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float2 corner : TEXCOORD0; float4 data : TEXCOORD1; float4 color : COLOR; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float2 corner : TEXCOORD1;
            float4 color : TEXCOORD2;
            float part : TEXCOORD3;
        };

        Varyings Vert(Attributes v)
        {
            float3 basePos = TransformObjectToWorld(v.positionOS.xyz);
            float part = v.data.x, height = v.data.y, size = v.data.z, phase = v.data.w;
            float gust = IslandGust(basePos.xz);
            float2 sway2 = _IslandWind.xy * (gust * 0.07 * _IslandWind.z + sin(_IslandTime * 2.6 + phase * 6.2831) * 0.02 * _IslandWind.z);
            float3 sway = float3(sway2.x, 0.0, sway2.y) * height * 4.0;
            float3 camRight = UNITY_MATRIX_V[0].xyz;
            float3 camUp = UNITY_MATRIX_V[1].xyz;
            float3 pos;
            if (part < 0.5)
            {
                float3 toCam = normalize(_WorldSpaceCameraPos - basePos);
                float3 side = normalize(cross(float3(0, 1, 0), toCam));
                float t = v.corner.y * 0.5 + 0.5;
                pos = basePos + side * (v.corner.x * 0.004) + float3(0, t * height, 0) + sway * t * t;
            }
            else
            {
                pos = basePos + float3(0, height, 0) + sway + (camRight * v.corner.x + camUp * v.corner.y) * size;
            }
            Varyings o;
            o.positionWS = pos;
            o.corner = v.corner;
            o.color = v.color;
            o.part = part;
            o.positionCS = TransformWorldToHClip(pos);
            return o;
        }

        float FlowerMask(Varyings i)
        {
            return i.part < 0.5 ? 1.0 : smoothstep(1.0, 0.85, length(i.corner));
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS

            half4 Frag(Varyings i) : SV_Target
            {
                clip(FlowerMask(i) - 0.5);
                half3 albedo = i.color.rgb;
                if (i.part > 0.5)
                {
                    float r = length(i.corner);
                    albedo = lerp(half3(1.0, 0.78, 0.15), albedo, smoothstep(0.22, 0.38, r));   // pollen centre
                }
                IslandSurface s = IslandDefaultSurface(albedo, half3(0, 1, 0));
                s.translucency = 0.4;
                half3 color = IslandLighting(s, i.positionWS, i.positionCS, normalize(_WorldSpaceCameraPos - i.positionWS));
                return half4(IslandApplyFog(color, i.positionWS), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i) : SV_Target { clip(FlowerMask(i) - 0.5); return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            half4 NormalsFrag(Varyings i) : SV_Target { clip(FlowerMask(i) - 0.5); return half4(0, 1, 0, 0); }
            ENDHLSL
        }
    }
}
