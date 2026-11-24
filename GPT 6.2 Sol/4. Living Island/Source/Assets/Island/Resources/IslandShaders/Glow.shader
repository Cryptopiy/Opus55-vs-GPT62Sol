Shader "Living Island/Additive Glow"
{
    Properties { [HDR] _Color("Glow",Color)=(1,1,1,1) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+15" "RenderType"="Transparent" }
        Blend One One
        ZWrite Off Cull Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "IslandCommon.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            struct A { float4 pos:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct V { float4 pos:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            V Vert(A a) {V o;o.pos=TransformObjectToHClip(a.pos.xyz);o.color=a.color;o.uv=a.uv;return o;}
            half4 Frag(V i):SV_Target {return half4(_Color.rgb*_Color.a*i.color.rgb*i.color.a,0);}
            ENDHLSL
        }
    }
}

