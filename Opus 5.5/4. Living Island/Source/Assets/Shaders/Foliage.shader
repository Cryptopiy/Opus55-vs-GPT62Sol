// Alpha-clipped foliage cards with procedural leaf textures (I-09, I-11, I-12):
//   _LeafPattern 0 = palm frond (pinnate leaflets either side of the midrib, UV.x across, UV.y along)
//   _LeafPattern 1 = bush leaf card (three pointed leaves per card)
// Double sided, translucent, alpha-to-coverage with MSAA, vertex-colour wind (flutter + trunk bend).
Shader "Island/Foliage"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.24, 0.46, 0.13, 1)
        _LeafPattern ("Leaf Pattern (0 frond, 1 bush)", Float) = 0
        _WindScale ("Wind Scale", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Include/IslandCommon.hlsl"
        #include "Include/VegetationWind.hlsl"
        #if defined(LOD_FADE_CROSSFADE)
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
        #endif

        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _LeafPattern;
        float _WindScale;
        CBUFFER_END

        struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; float4 color : COLOR; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float2 uv : TEXCOORD2;
            float4 color : TEXCOORD3;
        };

        Varyings Vert(Attributes v)
        {
            Varyings o;
            float3 p = TransformObjectToWorld(v.positionOS.xyz);
            float windScale = _WindScale * (_LeafPattern > 0.5 ? 0.35 : 1.0);
            o.positionWS = VegetationWind(p, v.color, windScale);
            o.normalWS = TransformObjectToWorldNormal(v.normalOS);
            o.uv = v.uv;
            o.color = v.color;
            o.positionCS = TransformWorldToHClip(o.positionWS);
            return o;
        }

        float LeafMask(float2 uv, float4 vcol)
        {
            float result = 0.0;
            if (_LeafPattern < 0.5)
            {
                // Palm frond: leaflets angled ~40 deg from the midrib, longest mid-frond, short at base/tip.
                float across = abs(uv.x - 0.5) * 2.0;                         // 0 at the midrib .. 1 at the edge
                float profile = sin(saturate(uv.y) * PI) * 0.92 + 0.08;      // frond outline
                profile *= 1.0 - 0.3 * saturate((uv.y - 0.78) * 4.5);
                float leafCoord = uv.y * 30.0 - across * 2.4 + vcol.b * 3.0;
                float f = frac(leafCoord);
                // Broad leaflets (~70% of each period), narrowing towards their tips at the frond edge.
                float width = lerp(0.8, 0.5, across);
                float leaflet = smoothstep(0.0, 0.05, f) * smoothstep(width, width - 0.08, f);
                // Once leaflets shrink below a pixel the frond fills in instead of turning into a thin comb.
                leaflet = lerp(leaflet, 0.85, saturate(fwidth(leafCoord) * 1.5 - 0.3));
                float edge = smoothstep(profile, profile - 0.1, across);
                float rib = smoothstep(0.07, 0.03, across);
                result = saturate(max(leaflet * edge, rib * step(uv.y, 0.985)));
            }
            else
            {
            // Bush: three pointed leaves fanned around the card centre.
            float m = 0.0;
            [unroll] for (int k = 0; k < 3; k++)
            {
                float a = (k - 1) * 0.9 + vcol.b * 2.0;
                float2 d = uv - 0.5;
                float2 r = float2(cos(a) * d.x - sin(a) * d.y, sin(a) * d.x + cos(a) * d.y);
                r.y += 0.12;
                float len = 0.42, wid = 0.16;
                float t = saturate(r.y / len);
                float w = wid * sin(t * PI) * (1.0 - t * 0.3);
                m = max(m, step(0.0, r.y) * step(r.y, len) * smoothstep(w, w - 0.03, abs(r.x)));
            }
            result = m;
            }
            return result;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            AlphaToMask On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            half4 Frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                float mask = LeafMask(i.uv, i.color);
                clip(mask - 0.2);
                // Sharpened alpha for alpha-to-coverage: crisp but anti-aliased leaf edges under MSAA.
                float alpha = saturate((mask - 0.5) / max(fwidth(mask), 1e-4) + 0.5);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 n = normalize(i.normalWS);
                if (!isFront) n = -n;
                float varN = IslandValueNoise(i.positionWS.xz * 0.7 + i.color.b * 13.0);
                half3 albedo = _BaseColor.rgb * (0.8 + 0.35 * varN);
                if (_LeafPattern < 0.5)
                {
                    // Yellower at the base and the dry tips, darker along the midrib.
                    albedo = lerp(albedo, albedo * half3(1.35, 1.2, 0.6), saturate(1.0 - i.uv.y * 5.0) * 0.6 + saturate((i.uv.y - 0.85) * 6.0) * 0.5);
                    albedo *= 0.85 + 0.15 * smoothstep(0.0, 0.2, abs(i.uv.x - 0.5));
                }
                half smoothV = 0.35;
                IslandApplyWetness(albedo, smoothV, n, 0.5);
                IslandSurface s = IslandDefaultSurface(albedo, n);
                s.smoothness = smoothV;
                s.translucency = 0.65;
                s.occlusion = _LeafPattern > 0.5 ? lerp(0.45, 1.0, i.color.a) : 1.0;
                half3 color = IslandLighting(s, i.positionWS, i.positionCS, viewDir);
                return half4(IslandApplyFog(color, i.positionWS), alpha);
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
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            struct SV { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : TEXCOORD1; };
            SV ShadowVert(Attributes v)
            {
                SV o;
                float windScale = _WindScale * (_LeafPattern > 0.5 ? 0.35 : 1.0);
                float3 p = VegetationWind(TransformObjectToWorld(v.positionOS.xyz), v.color, windScale);
                o.positionCS = IslandShadowClip(p, TransformObjectToWorldNormal(v.normalOS));
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            half4 ShadowFrag(SV i) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                clip(LeafMask(i.uv, i.color) - 0.5);
                return 0;
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
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                clip(LeafMask(i.uv, i.color) - 0.5);
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma multi_compile _ LOD_FADE_CROSSFADE
            #pragma vertex Vert
            #pragma fragment NormalsFrag
            half4 NormalsFrag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(i.positionCS);
                #endif
                clip(LeafMask(i.uv, i.color) - 0.5);
                float3 n = normalize(i.normalWS);
                return half4(isFront ? n : -n, 0.0);
            }
            ENDHLSL
        }
    }
}
