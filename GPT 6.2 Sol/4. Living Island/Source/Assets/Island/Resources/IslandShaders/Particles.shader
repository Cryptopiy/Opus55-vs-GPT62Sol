Shader "Living Island/Air and Spray"
{
    Properties
    {
        _DstBlend("Destination blend",Float)=10
        _NearFade("Camera fade distance",Float)=0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Blend SrcAlpha [_DstBlend]
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "IslandCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float _NearFade;
            CBUFFER_END
            struct A {float4 pos:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
            struct V {float4 pos:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float3 world:TEXCOORD1;};
            V Vert(A a) {V o;o.world=TransformObjectToWorld(a.pos.xyz);o.pos=TransformWorldToHClip(o.world);o.uv=a.uv;o.color=a.color;return o;}
            half4 Frag(V i):SV_Target
            {
                float2 p=i.uv*2-1;
                float radial=saturate(1-dot(p,p));
                float3 color=i.color.rgb*(1+1.65*pow(radial,4));
                float fade=_NearFade>0?smoothstep(.24,_NearFade,distance(GetCameraPositionWS(),i.world)):1;
                if(_NearFade>0 && _IslandUnderwater>.5 && i.world.y>0) fade=0;
                return half4(FogIsland(color,i.world),i.color.a*fade*radial*radial);
            }
            ENDHLSL
        }
    }
}

