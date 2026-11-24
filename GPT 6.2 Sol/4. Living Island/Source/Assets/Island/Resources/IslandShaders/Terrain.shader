Shader "Living Island/Terrain"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
        HLSLINCLUDE
        #include "IslandLighting.hlsl"
        float4 _IslandBeach;
        float3 _LightDirection, _LightPosition;
        struct A { float4 pos:POSITION; float3 normal:NORMAL; };
        struct V { float4 pos:SV_POSITION; float3 world:TEXCOORD0; float3 normal:TEXCOORD1; };
        V Vert(A i) {
            V o; o.world=TransformObjectToWorld(i.pos.xyz); o.pos=TransformWorldToHClip(o.world);
            o.normal=TransformObjectToWorldNormal(i.normal); return o;
        }
        V ShadowVert(A i) {
            V o=Vert(i);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
            float3 d=normalize(_LightPosition-o.world);
            #else
            float3 d=_LightDirection;
            #endif
            o.pos=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.world,normalize(o.normal),d)));
            return o;
        }
        float Stone(float2 p)
        {
            float bands=sin(p.y*2.1+Fractal(p*.72)*4+sin(p.x*.38)*1.8);
            return .35*Fractal(p*1.18)+.40*Noise2(p*5.7)+.25*smoothstep(-.6,.68,bands);
        }
        half4 Frag(V i):SV_Target
        {
            float3 p=i.world, n=normalize(i.normal);
            float gentle=sqrt(saturate(1-n.y*n.y));
            float coarse=Fractal(p.xz*.105);
            float fine=Noise2(p.xz*12.7);
            float2 rippled=p.xz+float2(Noise2(p.xz*.37),Noise2(p.zx*.41+14))*.92;
            float ripple=sin(dot(rippled,float2(9.4,4.9))+1.8*sin(p.z*.67));
            float exposure=p.y+(coarse-.5)*1.08;
            float wet=1-smoothstep(-.17,.63,exposure);
            float meadow=smoothstep(_IslandBeach.x*.72,_IslandBeach.x*1.75,exposure)*smoothstep(.97,.995,n.y);
            meadow=max(meadow,smoothstep(_IslandBeach.x*1.28,_IslandBeach.x*2.43,exposure)*(1-smoothstep(.47,.85,gentle)));
            float cliff=smoothstep(.48,.84,gentle+(coarse-.49)*.30)*smoothstep(-.11,1.84,p.y);
            cliff=max(cliff,smoothstep(29,42,p.y+(coarse-.46)*16)*smoothstep(.22,.52,gentle));
            float dirt=smoothstep(.47,.78,coarse)*smoothstep(.14,.60,gentle)*(1-cliff);
            float3 sand=lerp(float3(.55,.403,.235),float3(.79,.65,.425),coarse)*(.91+.12*fine+.036*ripple);
            float3 deepSand=lerp(float3(.216,.153,.076),float3(.52,.388,.221),saturate((p.y+9)*.105))*(.83+.24*coarse+.057*ripple);
            float3 grass=lerp(float3(.105,.184,.048),float3(.258,.328,.088),smoothstep(.21,.75,coarse))*(.82+.29*fine);
            float3 weights=pow(abs(n),5); weights/=max(.01,dot(weights,1));
            float rockGrain=0;
            if(cliff>.005) rockGrain=dot(weights,float3(Stone(p.zy),Stone(p.xz),Stone(p.xy+10)));
            float3 stone=lerp(float3(.15,.137,.111),float3(.46,.417,.332),smoothstep(.05,.98,rockGrain));
            stone*=lerp(float3(.83,.91,.73),float3(1.10,1.02,.90),coarse);
            float3 albedo=lerp(lerp(deepSand,sand,1-wet),grass,meadow);
            albedo=lerp(albedo,float3(.24,.144,.072)*(.7+coarse*.65),dirt*.75);
            albedo=lerp(albedo,stone,cliff)*(1-.35*_IslandWet);
            float bump=lerp(fine*.014+ripple*.003,rockGrain*.19,cliff);
            // Screen derivatives reorient the procedural micro-height along the actual ground surface.
            float3 px=ddx(p), py=ddy(p);
            float3 r1=cross(py,n),r2=cross(n,px);
            float det=dot(px,r1);
            n=normalize(n*max(1e-7,abs(det))-sign(det)*(ddx(bump)*r1+ddy(bump)*r2));
            float3 rgb=IslandLighting(albedo,n,p,i.pos,saturate(.12+wet*.41+_IslandWet*.48),0);
            if(p.y<0) rgb+=float3(.23,.43,.30)*Caustic(p)*lerp(.63,1,_IslandUnderwater);
            return half4(FogIsland(rgb,p),1);
        }
        half4 Depth(V i):SV_Target { return 0; }
        ENDHLSL
        Pass
        {
            Name "Island strata"
            Tags { "LightMode"="UniversalForwardOnly" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
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
            Name "Island shadow"
            Tags { "LightMode"="ShadowCaster" }
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment Depth
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }
        Pass
        {
            Name "Island depth"
            Tags { "LightMode"="DepthOnly" }
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Depth
            ENDHLSL
        }
    }
}

