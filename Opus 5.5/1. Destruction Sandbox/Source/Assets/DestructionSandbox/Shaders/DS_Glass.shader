// Translucent glass: tinted body, fresnel edge (reflection of the same gradient sky as the skybox),
// sharp sun highlight and brighter frosted box edges. Transparent queue, no depth write, no shadows.
Shader "DestructionSandbox/Glass"
{
    Properties
    {
        _BaseColor("Tint", Color) = (0.62, 0.86, 0.95, 0.22)
        _EdgeColor("Edge Color", Color) = (0.85, 0.97, 1.0, 0.75)
        _FresnelPower("Fresnel Power", Float) = 3.5
        _FresnelStrength("Fresnel Strength", Float) = 0.85
        _Variation("Brightness Variation", Float) = 0.06
        _EdgeWidth("Edge Width (m)", Float) = 0.035
        _SkyZenith("Sky Zenith", Color) = (0.2, 0.42, 0.78, 1)
        _SkyHorizon("Sky Horizon", Color) = (0.78, 0.84, 0.9, 1)
        _SkyGround("Sky Ground", Color) = (0.33, 0.31, 0.29, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "DS_Common.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _FresnelPower;
                float _FresnelStrength;
                float _Variation;
                float _EdgeWidth;
                half4 _SkyZenith;
                half4 _SkyHorizon;
                half4 _SkyGround;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float4 patternPos : TEXCOORD2;
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
                float3 scale = float3(length(UNITY_MATRIX_M._m00_m10_m20), length(UNITY_MATRIX_M._m01_m11_m21), length(UNITY_MATRIX_M._m02_m12_m22));
                o.patternPos = float4(input.positionOS.xyz * scale, input.color.a);
                o.halfExtents = scale * 0.5;
                o.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return o;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 n = normalize((float3)input.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float nv = saturate(dot(n, v));
                float fresnel = pow(1.0 - nv, _FresnelPower) * _FresnelStrength;

                float3 r = reflect(-v, n);
                float3 sky = DSSkyColor(r, _SkyZenith.rgb, _SkyHorizon.rgb, _SkyGround.rgb);

                Light mainLight = GetMainLight();
                float3 h = normalize(mainLight.direction + v);
                float spec = pow(saturate(dot(n, h)), 180.0) * 4.0;
                float diffuse = saturate(dot(n, mainLight.direction)) * 0.35 + 0.65;

                float bright = DSBrightness(unity_RendererUserValue, _Variation);
                float3 body = _BaseColor.rgb * diffuse * bright * mainLight.color;

                half edge = 0;
                if (input.patternPos.w > 0.5)
                {
                    float3 d = input.halfExtents - abs(input.patternPos.xyz);
                    float lo = min(d.x, min(d.y, d.z));
                    float hi = max(d.x, max(d.y, d.z));
                    float mid = d.x + d.y + d.z - lo - hi;
                    edge = (half)(1.0 - smoothstep(0.0, _EdgeWidth, mid));
                }

                float3 color = lerp(body, sky, saturate(fresnel)) + spec * mainLight.color;
                color = lerp(color, _EdgeColor.rgb * bright, edge * 0.8);
                float alpha = saturate(_BaseColor.a + fresnel * 0.6 + spec * 0.5 + edge * _EdgeColor.a);
                color = MixFog(color, InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor));
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
}
