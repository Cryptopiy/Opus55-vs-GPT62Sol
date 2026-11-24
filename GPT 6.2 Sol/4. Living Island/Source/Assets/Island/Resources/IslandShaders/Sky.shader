Shader "Living Island/Sky"
{
    Properties { _Stars("Seeded star map",2D)="black" {} }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "IslandCommon.hlsl"
            TEXTURE2D(_Stars); SAMPLER(sampler_Stars);
            struct A {float4 pos:POSITION;};
            struct V {float4 pos:SV_POSITION; float3 direction:TEXCOORD0;};
            V Vert(A i) { V o; o.pos=TransformObjectToHClip(i.pos.xyz); o.direction=i.pos.xyz; return o; }
            half4 Frag(V i):SV_Target
            {
                float3 dir=normalize(i.direction);
                if(_IslandUnderwater>.75) return half4(_IslandUnderwaterColor.rgb,1);
                float3 c=SkyBase(dir);
                float day=max(_IslandDay,0);
                float sun=dot(dir,_IslandSun.xyz);
                float moon=dot(dir,_IslandMoon.xyz);
                float night=1-smoothstep(.015,.33,day);
                float2 uv=float2(frac(atan2(dir.z,dir.x)/6.2831853+.5),saturate(asin(dir.y)/3.14159265+.5));
                float stars=SAMPLE_TEXTURE2D_LOD(_Stars,sampler_Stars,uv,0).r;
                c+=float3(.63,.79,1.05)*stars*night*smoothstep(0,.10,dir.y)*2.1;
                c+=smoothstep(.99980,.99992,sun)*_IslandCloudLight.rgb*(.5+day*4.8)*smoothstep(-.03,.02,_IslandSun.y);
                c+=smoothstep(.99979,.99988,moon)*float3(.84,.92,1.30)*night*1.75;
                if(dir.y>.006)
                {
                    float2 plane=dir.xz/(dir.y+.21);
                    float2 drift=_IslandWind.xy*_IslandWeatherTime;
                    float high=Fractal(plane*.82+drift*.009+float2(31,70));
                    high=smoothstep(.59-_IslandCloud*.19,.85-_IslandCloud*.19,high)*.57;
                    float2 p=plane*1.49-drift*.019+float2(11.6,26.2);
                    float density=Fractal(p)+.093*Noise2(p*7.6);
                    float edge=.67-.32*_IslandCloud;
                    float mass=smoothstep(edge,edge+.14,density);
                    float displaced=Fractal(p+_IslandSun.xz*.28);
                    float rim=saturate(.43+(density-displaced)*2.9);
                    float dayCloud=saturate(day*2.1);
                    float3 base=lerp(float3(.014,.027,.052),float3(.57,.64,.66),dayCloud);
                    float3 lit=lerp(float3(.049,.080,.17),_IslandCloudLight.rgb*.77,dayCloud);
                    float3 clouds=lerp(base*.71,lit,rim)*lerp(1,.60,_IslandCloud);
                    clouds+=pow(saturate(sun),31)*_IslandCloudLight.rgb*(day*.34);
                    float fade=smoothstep(.02,.19,dir.y);
                    c=lerp(c,lerp(base,lit,.74),high*fade);
                    c=lerp(c,clouds,mass*fade*.92);
                }
                c=lerp(c,_IslandFog.rgb,1-smoothstep(-.026,.101,dir.y)*exp(-_IslandFogDensity*79));
                return half4(c,1);
            }
            ENDHLSL
        }
    }
}

