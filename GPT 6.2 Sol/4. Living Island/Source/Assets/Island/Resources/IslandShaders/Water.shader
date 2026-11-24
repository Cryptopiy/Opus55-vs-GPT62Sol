Shader "Living Island/Gerstner Ocean"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-10" "RenderType"="Transparent" }
        Cull Off ZWrite Off
        Pass
        {
            Name "Ocean optics"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "IslandCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"
            float4 _IslandWaves[8], _IslandShallow, _IslandDeep, _IslandWater;
            int _IslandWaveCount;
            float _IslandFoamDepth,_IslandWaterQuality;
            struct A {float4 pos:POSITION;};
            struct V {float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 surface:TEXCOORD1; float2 grid:TEXCOORD2; float crest:TEXCOORD3; };

            V Vert(A i)
            {
                V o;
                float3 ws=TransformObjectToWorld(i.pos.xyz);
                o.grid=ws.xz;
                float3 dx=float3(1,0,0),dz=float3(0,0,1);
                float crest=0;
                [loop] for(int j=0;j<min(_IslandWaveCount,8);j++)
                {
                    float4 wave=_IslandWaves[j];
                    float k=6.283185307/wave.w;
                    float amplitude=wave.z*_IslandWater.x;
                    float f=k*dot(wave.xy,o.grid)-sqrt(9.81*k)*_IslandWeatherTime;
                    float s,c; sincos(f,s,c);
                    float qa=amplitude*.65;
                    ws+=float3(wave.x*qa*c,amplitude*s,wave.y*qa*c);
                    dx+=float3(-wave.x*wave.x*qa*k*s,wave.x*amplitude*k*c,-wave.x*wave.y*qa*k*s);
                    dz+=float3(-wave.x*wave.y*qa*k*s,wave.y*amplitude*k*c,-wave.y*wave.y*qa*k*s);
                    crest+=amplitude*s;
                }
                o.world=ws;
                o.surface=normalize(cross(dz,dx));
                o.pos=TransformWorldToHClip(ws);
                o.crest=crest;
                return o;
            }

            float Drop(float2 p,float2 cell)
            {
                float h=Hash21(cell);
                float phase=frac(_IslandWeatherTime*1.31+h);
                float2 center=cell+float2(.16+.68*h,.17+.66*Hash21(cell+21));
                float ring=abs(length(p-center)-phase*.58);
                float width=.045+fwidth(ring)*.53;
                return (1-smoothstep(.018,width+ .02,ring))*(1-phase)*smoothstep(.02,.13,phase);
            }

            half4 Frag(V i, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                float2 uv=GetNormalizedScreenSpaceUV(i.pos);
                float3 cam=GetCameraPositionWS();
                float3 view=normalize(cam-i.world);
                float dist=length(cam-i.world);
                float eyeZ=abs(TransformWorldToView(i.world).z);
                float baseDepth=LinearEyeDepth(SampleSceneDepth(uv),_ZBufferParams);
                float depth=max(0,(baseDepth-eyeZ)*dist/max(.1,eyeZ));
                float2 grid=i.grid;
                float t=_IslandWeatherTime;
                float fade=1-smoothstep(31,190,dist);
                float2 op=float2(dot(grid,float2(1.27,.53)),dot(grid,float2(-.43,1.17)));
                float2 rip=float2(sin(op.x*4.7+t*1.8)+sin(op.y*6.3-t*1.9),
                                  sin(op.y*4.4+t*1.45)+sin(op.x*8.1+t*2.8))*.023;
                rip+=(float2(Noise2(op*2.7+t*.47),Noise2(op.yx*2.8-t*.34))-.5)*.092*fade;
                float drop=0;
                if(_IslandWet>.035 && dist<lerp(31,90,_IslandWaterQuality))
                    drop=Drop(grid*1.92,floor(grid*1.92))*_IslandWet*(1-smoothstep(25,lerp(31,90,_IslandWaterQuality),dist));
                float3 n=normalize(i.surface+float3(rip.x,0,rip.y)*(1+_IslandWater.y)+float3(drop*-.13,0,drop*.19));
                if(_IslandUnderwater>.45)
                {
                    float3 under=lerp(_IslandUnderwaterColor.rgb*.63,SkyBase(reflect(-view,-n))*.52,saturate(dot(view,-n)));
                    return half4(FogIsland(under,i.world),1);
                }
                float nv=saturate(dot(n,view));
                float f=.025+.975*pow(1-nv,4.4);
                float3 reflected=reflect(-view,n);
                reflected.y=abs(reflected.y);
                float3 reflectSky=SkyBase(reflected);
                if(_IslandWaterQuality>.55)
                {
                    float cloud=Fractal(reflected.xz/(reflected.y+.29)*1.65-_IslandWind.xy*t*.013);
                    reflectSky=lerp(reflectSky,_IslandCloudLight.rgb*.53,smoothstep(.68-_IslandCloud*.25,.94-_IslandCloud*.23,cloud)*.59);
                }
                float2 refractUV=saturate(uv+n.xz*.013*saturate(depth*.78)*fade*_IslandWaterQuality);
                float compare=LinearEyeDepth(SampleSceneDepth(refractUV),_ZBufferParams);
                refractUV=lerp(uv,refractUV,smoothstep(0,.31,compare-eyeZ));
                float3 scene=SampleSceneColor(refractUV);
                float soak=1-exp(-depth*.145);
                float3 ocean=lerp(_IslandShallow.rgb,_IslandDeep.rgb,soak);
                ocean*=lerp(.085,1,_IslandDay)*(1-_IslandCloud*.24);
                float3 transmission=scene*lerp(float3(.95,1.0,.98),float3(.43,.93,.83),saturate(depth*.40));
                float3 c=lerp(transmission,ocean,saturate(1-exp(-depth*.48)));
                c=lerp(c,reflectSky,f*(.92-_IslandWater.y*.15));
                float glint=pow(saturate(dot(n,normalize(view+_IslandSun.xyz))),lerp(357,67,_IslandWater.y));
                float sparkle=pow(saturate(dot(reflected,_IslandSun.xyz)),16)*max(0,sin(op.x*13.2+t*1.35)*sin(op.y*18.3-t))*fade;
                c+=(glint*(2.1-_IslandWater.y)+sparkle*.26)*_IslandCloudLight.rgb*_IslandDay;
                c+=pow(saturate(i.crest*.94+dot(n,_IslandSun.xyz)*.33),2)*float3(.039,.151,.093)*_IslandDay;
                float lace=Fractal(grid*2.6-float2(t*.13,t*.21));
                float foam=saturate(1-depth/max(.01,_IslandFoamDepth));
                foam=smoothstep(.07,.75,foam)*smoothstep(.18,.76,lace+foam*.46);
                float nearShore=exp(-abs(depth-_IslandFoamDepth*.82)*8.4);
                foam=max(foam,nearShore*smoothstep(.59,.77,lace)*.32);
                float swimming=smoothstep(.38,.63,length(grid-_IslandPlayer.xz))*
                    (1-smoothstep(.63,.91,length(grid-_IslandPlayer.xz)))*_IslandPlayer.w;
                foam=max(foam,swimming*smoothstep(.18,.75,lace));
                float3 white=lerp(float3(.05,.12,.17),float3(.89,.99,.93),_IslandDay);
                c=lerp(c,white,saturate(foam*.93+drop*.19));
                return half4(FogIsland(c,i.world),1);
            }
            ENDHLSL
        }
    }
}

