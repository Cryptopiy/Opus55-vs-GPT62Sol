// Lit ground with a procedural, anti-aliased grid: thin darker lines every 1 m, bolder lines every 10 m.
// Lines are computed from world XZ with screen-space derivatives, so they stay crisp at any distance.
Shader "DestructionSandbox/Ground"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.46, 0.46, 0.44, 1)
        _ColorB("Secondary Color", Color) = (0.40, 0.40, 0.385, 1)
        _MinorCell("Minor Cell (m)", Float) = 1
        _MajorCell("Major Cell (m)", Float) = 10
        _MinorWidth("Minor Line Width (m)", Float) = 0.025
        _MajorWidth("Major Line Width (m)", Float) = 0.07
        _MinorDark("Minor Line Darkness", Range(0, 1)) = 0.28
        _MajorDark("Major Line Darkness", Range(0, 1)) = 0.5
        _Smoothness("Smoothness", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "DS_Common.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ColorB;
            float _MinorCell;
            float _MajorCell;
            float _MinorWidth;
            float _MajorWidth;
            half _MinorDark;
            half _MajorDark;
            half _Smoothness;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            // Coverage of grid lines of the given width (metres), anti-aliased with the pixel footprint.
            float GridLine(float2 p, float cell, float width)
            {
                float2 coord = p / cell;
                float2 dist = abs(frac(coord - 0.5) - 0.5) * cell;     // distance to the nearest line (m)
                float2 aa = max(fwidth(p), 1e-5);
                float2 l = 1.0 - smoothstep(width * 0.5 - aa, width * 0.5 + aa, dist);
                return max(l.x, l.y);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 p = input.positionWS.xz;
                float n = DSValueNoise2(p * 0.35) * 0.6 + DSValueNoise2(p * 3.1) * 0.4;
                float3 albedo = lerp(_ColorB.rgb, _BaseColor.rgb, n);
                float minor = GridLine(p, _MinorCell, _MinorWidth);
                float major = GridLine(p, _MajorCell, _MajorWidth);
                albedo *= 1.0 - max(minor * _MinorDark, major * _MajorDark);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                #if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    inputData.shadowCoord = float4(0, 0, 0, 0);
                #endif
                inputData.fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = (half3)albedo;
                surface.smoothness = _Smoothness;
                surface.normalTS = half3(0, 0, 1);
                surface.occlusion = 1;
                surface.alpha = 1;

                half4 color = UniversalFragmentPBR(inputData, surface);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                color.a = 1;
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };
            Varyings DepthVert(Attributes input) { Varyings o; o.positionCS = TransformObjectToHClip(input.positionOS.xyz); return o; }
            half DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; half3 normalWS : TEXCOORD0; };
            Varyings DepthNormalsVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                return o;
            }
            half4 DepthNormalsFrag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize((float3)input.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 remapped = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
