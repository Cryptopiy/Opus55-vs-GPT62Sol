Shader "Destruction Range/Edge Glass"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (.3,.8,.9,1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-30" "RenderType"="Transparent" }
        Pass
        {
            Name "Glass"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END

            struct Input
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Output
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                float2 uv : TEXCOORD2;
            };

            Output Vert(Input i)
            {
                UNITY_SETUP_INSTANCE_ID(i);
                Output o;
                o.world = TransformObjectToWorld(i.position.xyz);
                o.clip = TransformWorldToHClip(o.world);
                o.normal = TransformObjectToWorldNormal(i.normal);
                o.uv = i.uv;
                return o;
            }

            half4 Frag(Output i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half3 view = GetWorldSpaceNormalizeViewDir(i.world);
                half edge = pow(1 - saturate(dot(view,n)), 2.7);
                float2 margin = min(i.uv, 1 - i.uv);
                half bevel = 1 - smoothstep(.012, .068, min(margin.x, margin.y));
                half gleam = pow(saturate(dot(reflect(-view,n), normalize(half3(-.35,.90,-.3)))), 18);
                half sweep = smoothstep(.75, .90, sin(i.uv.x * 6.4 + i.uv.y * 5.5)) * .14;
                half3 color = _BaseColor.rgb * (.56 + edge * .83 + bevel * .69 + sweep) +
                    half3(.58,.83,.87) * (bevel * .36 + gleam * .55);
                return half4(color, saturate(.20 + edge * .50 + bevel * .54 + gleam * .22 + sweep));
            }
            ENDHLSL
        }
    }
}
