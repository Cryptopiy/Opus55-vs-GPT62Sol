Shader "Living Island/Beacon Volume"
{
    Properties { _Strength("Scattering",Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+8" }
        Blend One One
        ZWrite Off Cull Front
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "IslandCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _Strength;
            CBUFFER_END
            struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
            struct V{float4 p:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1;float2 uv:TEXCOORD2;};
            V Vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformWorldToHClip(o.world);o.normal=TransformObjectToWorldNormal(a.n);o.uv=a.uv;return o;}
            half4 Frag(V i):SV_Target
            {
                float3 view=normalize(GetCameraPositionWS()-i.world);
                float interior=pow(saturate(abs(dot(view,normalize(i.normal)))),1.65);
                float falloff=(1-smoothstep(.19,1,i.uv.y))*exp(-i.uv.y*1.55);
                float fog=exp(-distance(GetCameraPositionWS(),i.world)*_IslandFogDensity*.52);
                return half4(float3(1,.665,.267)*interior*falloff*_Strength*fog,1);
            }
            ENDHLSL
        }
    }
}

