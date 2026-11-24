Shader "Living Island/Coastal Props"
{
    Properties
    {
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Vegetation("Palm wind",Float)=0
        _Glow("Night emission",Float)=0
        _UseVertexColor("Imported vertex tint",Float)=1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        HLSLINCLUDE
        #include "IslandLighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
        float4 _BaseColor;
        float _Vegetation,_Glow,_UseVertexColor;
        CBUFFER_END
        TEXTURE2D(_IslandFrond); SAMPLER(sampler_IslandFrond);
        float3 _LightDirection, _LightPosition;
        struct Attributes { float4 position:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
        struct Varyings { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; float2 uv:TEXCOORD2; float4 color:TEXCOORD3; };
        float3 Sway(Attributes v)
        {
            float3 ws=TransformObjectToWorld(v.position.xyz);
            if(_Vegetation>.5)
            {
                float h=saturate(v.position.y/6.6);
                float phase=dot(GetObjectToWorldMatrix()._m03_m13_m23.xz,float2(.17,.12));
                float wave=sin(_IslandWeatherTime*1.28+phase)*.23+sin(_IslandWeatherTime*2.42+phase*.72)*.093;
                ws.xz+=_IslandWind.xy*_IslandWind.z*wave*h*h;
                if(v.color.a<.8) ws.y+=sin(_IslandWeatherTime*4.7+v.uv.x*8+phase)*v.uv.x*_IslandWind.z*.056;
            }
            return ws;
        }
        void Frond(float4 color,float2 uv)
        {
            if(_Vegetation>.5 && color.a<.8) clip(SAMPLE_TEXTURE2D(_IslandFrond,sampler_IslandFrond,uv).r-.43);
        }
        Varyings Vert(Attributes v)
        {
            UNITY_SETUP_INSTANCE_ID(v);
            Varyings o;
            o.world=Sway(v); o.pos=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(v.normal);
            o.color=v.color; o.uv=v.uv;
            return o;
        }
        Varyings ShadowVert(Attributes v)
        {
            Varyings o=Vert(v);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 direction=normalize(_LightPosition-o.world);
            #else
                float3 direction=_LightDirection;
            #endif
            o.pos=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.world,o.normal,direction)));
            return o;
        }
        half4 Frag(Varyings i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
        {
            Frond(i.color,i.uv);
            float frond=_Vegetation>.5 && i.color.a<.8;
            float3 n=normalize(i.normal)*IS_FRONT_VFACE(face,1,-1);
            float fleck=Noise2(i.world.xz*3.42+i.world.y*.73);
            float3 albedo=lerp(1,i.color.rgb,_UseVertexColor)*_BaseColor.rgb*lerp(.76,1.15,fleck);
            albedo*=1-.30*_IslandWet;
            float3 color=IslandLighting(albedo,n,i.world,i.pos,.19+_IslandWet*.5,frond*.52);
            color+=albedo*_Glow*(1-_IslandDay)*.92;
            return half4(FogIsland(color,i.world),1);
        }
        half4 Mask(Varyings i):SV_Target { Frond(i.color,i.uv); return 0; }
        ENDHLSL
        Pass
        {
            Name "Coastal lighting"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            ENDHLSL
        }
        Pass
        {
            Name "Coastal shadows"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex ShadowVert
            #pragma fragment Mask
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "Coastal depth"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Mask
            #pragma multi_compile_instancing
            ENDHLSL
        }
    }
}

