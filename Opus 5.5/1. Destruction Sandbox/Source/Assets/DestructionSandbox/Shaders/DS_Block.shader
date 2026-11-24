// Opaque lit block shader with procedural patterns (stone speckle, wood grain, concrete, metal).
// Pattern coordinates are object-space metres (object scale is folded back in), so a scaled unit cube
// and every shard size get the same texel density. Per-block variation comes from the renderer user value.
Shader "DestructionSandbox/Block"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (0.6, 0.6, 0.6, 1)
        _ColorB("Secondary Color", Color) = (0.4, 0.4, 0.4, 1)
        _Pattern("Pattern (0 stone, 1 wood, 2 concrete, 3 metal)", Float) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.2
        _Metallic("Metallic", Range(0, 1)) = 0
        _Variation("Brightness Variation", Float) = 0.06
        _EdgeDarken("Edge Darken", Range(0, 1)) = 0.35
        _EdgeWidth("Edge Width (m)", Float) = 0.03
        [HDR] _EmissionColor("Emission", Color) = (0, 0, 0, 1)
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
            float _Pattern;
            half _Smoothness;
            half _Metallic;
            float _Variation;
            half _EdgeDarken;
            float _EdgeWidth;
            half4 _EmissionColor;
        CBUFFER_END

        // Object scale from the model matrix columns: converts unit-cube positions back to metres.
        float3 DSObjectScale()
        {
            return float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
        }
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

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 patternPos : TEXCOORD2;   // xyz metres in object space, w = 1 for unit-cube boxes
                float3 halfExtents : TEXCOORD3;
                half fogFactor : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = (half3)TransformObjectToWorldNormal(input.normalOS);
                float3 scale = DSObjectScale();
                o.patternPos = float4(input.positionOS.xyz * scale, input.color.a);
                o.halfExtents = scale * 0.5;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            float3 StonePattern(float3 p, out half smooth)
            {
                float mottle = DSFbm(p * 2.2);
                float speck = DSValueNoise(p * 24.0);
                float fleck = DSValueNoise(p * 37.0 + 11.7);
                float3 col = lerp(_ColorB.rgb, _BaseColor.rgb, saturate(mottle * 1.3 - 0.15));
                col *= 1.0 - smoothstep(0.70, 0.80, speck) * 0.38;
                col += smoothstep(0.80, 0.88, fleck) * 0.10;
                smooth = _Smoothness;
                return col;
            }

            float3 WoodPattern(float3 p, out half smooth)
            {
                // Grain runs along local X: growth rings around an axis offset outside the block, warped by noise.
                float warp = DSFbm(p * float3(0.5, 2.6, 2.6));
                float r = length(p.yz + float2(2.3, -1.9)) * 11.0 + warp * 3.0;
                float ring = frac(r);
                float late = smoothstep(0.55, 0.85, ring) * (1.0 - smoothstep(0.9, 1.0, ring));
                float streak = DSValueNoise(p * float3(1.2, 46.0, 46.0));
                float3 col = lerp(_BaseColor.rgb, _ColorB.rgb, saturate(late * 0.75 + streak * 0.35));
                smooth = _Smoothness;
                return col;
            }

            float3 ConcretePattern(float3 p, out half smooth)
            {
                float n = DSFbm(p * 3.0);
                float pores = smoothstep(0.78, 0.84, DSValueNoise(p * 40.0));
                float3 col = lerp(_ColorB.rgb, _BaseColor.rgb, n) * (1.0 - pores * 0.25);
                smooth = _Smoothness;
                return col;
            }

            float3 MetalPattern(float3 p, out half smooth)
            {
                float n = DSFbm(p * 9.0);
                float3 col = lerp(_ColorB.rgb, _BaseColor.rgb, n);
                smooth = _Smoothness * (0.8 + 0.4 * n);
                return col;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                uint userValue = unity_RendererUserValue;
                float3 p = input.patternPos.xyz + DSSeedOffset(userValue);

                half smooth;
                float3 albedo;
                if (_Pattern < 0.5)      albedo = StonePattern(p, smooth);
                else if (_Pattern < 1.5) albedo = WoodPattern(p, smooth);
                else if (_Pattern < 2.5) albedo = ConcretePattern(p, smooth);
                else                     albedo = MetalPattern(p, smooth);

                albedo *= DSBrightness(userValue, _Variation);

                // Bevel-like edge darkening for boxes: the middle of the three face distances is the distance
                // to the nearest edge of the face this fragment lies on.
                half edge = 0;
                if (input.patternPos.w > 0.5)
                {
                    float3 d = input.halfExtents - abs(input.patternPos.xyz);
                    float lo = min(d.x, min(d.y, d.z));
                    float hi = max(d.x, max(d.y, d.z));
                    float mid = d.x + d.y + d.z - lo - hi;
                    edge = (half)(1.0 - smoothstep(0.0, _EdgeWidth, mid));
                }
                albedo *= 1.0 - edge * _EdgeDarken;

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
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = (half3)albedo;
                surface.metallic = _Metallic;
                surface.specular = half3(0, 0, 0);
                surface.smoothness = smooth * (1.0 - edge * 0.5);
                surface.normalTS = half3(0, 0, 1);
                surface.emission = _EmissionColor.rgb;
                surface.occlusion = 1.0 - edge * 0.25;
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
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings ShadowVert(Attributes input)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                o.positionCS = ApplyShadowClamping(positionCS);
                return o;
            }

            half4 ShadowFrag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings DepthVert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

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
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remapped), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
}
