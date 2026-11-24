Shader "Destruction Range/Effects"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _Mode ("Mode", Float) = 0
        _Phase ("Phase", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Effects"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _Mode, _Phase;
            CBUFFER_END

            struct Input
            {
                float4 position : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Output
            {
                float4 clip : SV_POSITION;
                float3 world : TEXCOORD0;
                half3 normal : TEXCOORD1;
                float4 screen : TEXCOORD2;
                float2 uv : TEXCOORD3;
                half4 color : COLOR;
            };

            Output Vert(Input i)
            {
                Output o;
                o.world = TransformObjectToWorld(i.position.xyz);
                o.clip = TransformWorldToHClip(o.world);
                o.screen = ComputeScreenPos(o.clip);
                o.normal = TransformObjectToWorldNormal(i.normal);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }

            half4 Frag(Output i) : SV_Target
            {
                float2 p = i.uv * 2 - 1;
                float radial = dot(p,p);
                if (_Mode < .5)
                {
                    half opacity = saturate(1 - radial);
                    return half4(i.color.rgb * _BaseColor.rgb, opacity * opacity * i.color.a * _BaseColor.a);
                }

                if (_Mode > 2.5)
                {
                    half edge = .84 + .08 * sin(41 * p.x + 17 * p.y) + .045 * sin(84 * p.y - 9 * p.x);
                    half opacity = 1 - smoothstep(.12, edge, radial);
                    return half4(_BaseColor.rgb, opacity * _BaseColor.a * (1 - _Phase));
                }

                half3 n = normalize(i.normal);
                half3 toCamera = GetWorldSpaceNormalizeViewDir(i.world);
                half fresnel = pow(1 - saturate(abs(dot(n,toCamera))), 3);
                if (_Mode < 1.5)
                {
                    half alpha = (fresnel * .91 + .028) * pow(saturate(1 - _Phase), .68) * _BaseColor.a;
                    return half4(_BaseColor.rgb, alpha);
                }

                float spin = i.uv.x * 23 + sin(i.uv.y * 14 + _Time.y * 4) * 3.3 - _Time.y * 9;
                half streak = pow(.5 + .5 * sin(spin), 6);
                half veins = pow(.5 + .5 * sin(spin * .63 + i.uv.y * 36), 12);
                float2 warp = float2(sin(spin), cos(spin * .71)) * (.005 + .021 * fresnel);
                half3 behind = SampleSceneColor(i.screen.xy / i.screen.w + warp);
                half3 energy = _BaseColor.rgb * (.15 + veins * .20 + streak * .52 + fresnel * .76);
                return half4(behind * .53 + energy, (.73 + fresnel * .22) * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
