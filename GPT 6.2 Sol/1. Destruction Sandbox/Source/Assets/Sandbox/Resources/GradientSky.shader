Shader "Destruction Range/Gradient Sky"
{
    Properties
    {
        _Zenith ("Zenith", Color) = (0.12,0.29,0.44,1)
        _Horizon ("Horizon", Color) = (0.68,0.80,0.83,1)
        _Nadir ("Nadir", Color) = (0.17,0.23,0.25,1)
        _SunDirection ("Sun direction", Vector) = (-0.4,0.6,-0.2,0)
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _Nadir;
                float4 _SunDirection;
            CBUFFER_END

            struct Interpolators { float4 position : SV_POSITION; float3 direction : TEXCOORD0; };

            Interpolators Vert(float4 position : POSITION)
            {
                Interpolators o;
                o.position = TransformObjectToHClip(position.xyz);
                o.direction = position.xyz;
                return o;
            }

            half4 Frag(Interpolators i) : SV_Target
            {
                float3 dir = normalize(i.direction);
                half height = saturate(dir.y);
                half3 gradient = lerp(_Horizon.rgb, _Zenith.rgb, pow(height, .50));
                gradient = lerp(gradient, _Nadir.rgb, saturate(-dir.y * 4));
                half sun = saturate(dot(dir, normalize(_SunDirection.xyz)));
                gradient += half3(.42,.27,.125) * pow(sun, 9);
                gradient += half3(2.5,1.7,.8) * smoothstep(.99945,.99972,sun);
                return half4(gradient,1);
            }
            ENDHLSL
        }
    }
}
