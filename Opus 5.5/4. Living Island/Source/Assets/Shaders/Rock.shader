// Rocks (Blender SM_Rock_*): triplanar procedural relief and colour variation in world space, moss on
// upward faces inland and sand on low rocks, wet look in rain. No textures, so no tiling.
Shader "Island/Rock"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.45, 0.43, 0.40, 1)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.2
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        HLSLINCLUDE
        #include "Include/IslandCommon.hlsl"

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _Smoothness;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
        struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.positionCS = TransformWorldToHClip(o.positionWS);
            return o;
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
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            half4 Frag(Varyings i) : SV_Target
            {
                float3 p = i.positionWS;
                float3 nGeo = normalize(i.normalWS);
                float3 viewDir = normalize(_WorldSpaceCameraPos - p);
                float detail = saturate(1.0 - distance(p, _WorldSpaceCameraPos) / 80.0);
                float3 w = pow(abs(nGeo), 4.0);
                w /= (w.x + w.y + w.z);
                float3 gx = IslandFbmD(p.zy * 1.1, 3), gy = IslandFbmD(p.xz * 1.1 + 5.1, 3), gz = IslandFbmD(p.xy * 1.1 + 9.7, 3);
                float3 bump = float3(0.0, gx.z, gx.y) * sign(nGeo.x) * w.x + float3(gy.y, 0.0, gy.z) * sign(nGeo.y) * w.y + float3(gz.y, gz.z, 0.0) * sign(nGeo.z) * w.z;
                float3 n = normalize(nGeo - bump * 0.8 * (0.3 + 0.7 * detail));
                float tone = gx.x * w.x + gy.x * w.y + gz.x * w.z;
                float speck = IslandValueNoise(p.xz * 9.0 + p.y * 3.0);
                half3 albedo = _BaseColor.rgb * (0.7 + 0.5 * tone) * (0.92 + 0.12 * speck);
                // Moss on top faces of rocks above the beach, wet sand tint on the lower part of beach rocks.
                float top = saturate((n.y - 0.55) * 3.0);
                float moss = top * smoothstep(3.0, 6.0, p.y) * smoothstep(0.45, 0.65, IslandValueNoise(p.xz * 0.6) + tone * 0.3);
                albedo = lerp(albedo, half3(0.16, 0.24, 0.08), moss * 0.85);
                float sandy = smoothstep(2.6, 0.6, p.y) * smoothstep(0.3, 0.0, p.y - (UNITY_MATRIX_M._m13 + 0.25));
                albedo = lerp(albedo, half3(0.68, 0.6, 0.45), sandy * 0.5);
                half smoothV = _Smoothness;
                IslandApplyWetness(albedo, smoothV, n, 0.9);
                IslandSurface s = IslandDefaultSurface(albedo, n);
                s.smoothness = smoothV;
                s.occlusion = 0.7 + 0.3 * saturate(tone * 1.4);
                half3 color = IslandLighting(s, p, i.positionCS, viewDir);
                float caustic = IslandCaustics(p);
                if (caustic > 0.0) color += albedo * caustic * _SunColorIsland.rgb;
                return half4(IslandApplyFog(color, p), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            float4 ShadowVert(Attributes v) : SV_POSITION
            {
                return IslandShadowClip(TransformObjectToWorld(v.positionOS.xyz), TransformObjectToWorldNormal(v.normalOS));
            }
            half4 ShadowFrag() : SV_Target { return 0; }
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
            half4 DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
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
            half4 NormalsFrag(Varyings i) : SV_Target { return half4(NormalizeNormalPerPixel(i.normalWS), 0.0); }
            ENDHLSL
        }
    }
}
