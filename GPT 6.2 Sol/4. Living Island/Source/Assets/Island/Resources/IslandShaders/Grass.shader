Shader "Living Island/Instanced Meadow"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        Cull Off
        Pass
        {
            Name "Wind and shaded blades"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "IslandCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float _IslandGrassDistance;
            struct A { float4 pos:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float2 uv:TEXCOORD1; half variant:TEXCOORD2; };
            V Vert(A a)
            {
                UNITY_SETUP_INSTANCE_ID(a);
                V o;
                float3 root=GetObjectToWorldMatrix()._m03_m13_m23;
                o.world=TransformObjectToWorld(a.pos.xyz);
                float gust=Noise2(root.xz*.15-_IslandWind.xy*_IslandWeatherTime*.82);
                float vibration=sin(dot(root.xz,float2(.57,.32))-_IslandWeatherTime*3.2);
                float2 push=root.xz-_IslandPlayer.xz;
                float d=length(push);
                float contact=(1-smoothstep(.21,_IslandWind.w,d))*saturate(1-abs(root.y-_IslandPlayer.y)*1.3);
                o.world.xz+=a.uv.y*a.uv.y*(_IslandWind.xy*_IslandWind.z*(gust*.39+vibration*.074)+
                    push/max(.1,d)*contact*.62);
                o.world.y-=contact*a.uv.y*.14;
                o.pos=distance(root,GetCameraPositionWS())<_IslandGrassDistance ? TransformWorldToHClip(o.world) : float4(2,2,1,1);
                o.uv=a.uv;
                o.variant=Hash21(root.xz*.42);
                return o;
            }
            half4 Frag(V i):SV_Target
            {
                Light sun=GetMainLight(TransformWorldToShadowCoord(i.world));
                float3 root=float3(.069,.145,.031);
                float3 tip=lerp(float3(.17,.29,.050),float3(.44,.48,.108),i.variant);
                float3 pigment=lerp(root,tip,pow(saturate(i.uv.y),.7))*(1-.31*_IslandWet);
                float3 light=.031+_IslandHorizon.rgb*.37+sun.color*(sun.shadowAttenuation*(.35+.39*i.uv.y));
                return half4(FogIsland(pigment*light,i.world),1);
            }
            ENDHLSL
        }
    }
}

