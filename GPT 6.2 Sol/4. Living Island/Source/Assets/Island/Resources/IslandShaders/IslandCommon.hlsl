#ifndef ISLAND_COMMON
#define ISLAND_COMMON
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
float4 _IslandSun, _IslandMoon, _IslandHorizon, _IslandZenith, _IslandCloudLight, _IslandFog, _IslandUnderwaterColor;
float4 _IslandWind, _IslandPlayer;
float _IslandDay, _IslandFlash, _IslandCloud, _IslandWet, _IslandUnderwater, _IslandFogDensity, _IslandUnderVisibility;
float _IslandWeatherTime;

float Hash21(float2 p)
{
    p = frac(p*float2(.1031,.1030));
    p += dot(p,p.yx+33.33);
    return frac((p.x+p.y)*p.x);
}
float Noise2(float2 p)
{
    float2 i=floor(p), f=frac(p); f=f*f*(3.0-2.0*f);
    return lerp(lerp(Hash21(i),Hash21(i+float2(1,0)),f.x),lerp(Hash21(i+float2(0,1)),Hash21(i+1),f.x),f.y);
}
float Fractal(float2 p)
{
    float v=.57*Noise2(p);
    p=mul(float2x2(1.66,-1.22,1.22,1.66),p)+13.73;
    return v+.28*Noise2(p)+.15*Noise2(p*2.07-27.13);
}
float3 SkyBase(float3 direction)
{
    float y=max(direction.y,0);
    float3 sky=lerp(_IslandHorizon.rgb,_IslandZenith.rgb,pow(saturate(y),.46));
    float sun=saturate(dot(direction,_IslandSun.xyz));
    float moon=saturate(dot(direction,_IslandMoon.xyz));
    sky+=pow(sun,19)*_IslandCloudLight.rgb*.23*_IslandDay;
    sky+=pow(moon,64)*float3(.075,.17,.30)*(1-_IslandDay);
    return sky + _IslandFlash*float3(.30,.43,.70);
}
float3 FogIsland(float3 color,float3 position)
{
    float3 cam=GetCameraPositionWS();
    float distanceToPoint=length(position-cam);
    float heightAtCamera=max(cam.y,0);
    float heightDelta=(max(position.y,0)-heightAtCamera)*.038;
    float integral=abs(heightDelta)>.01?(1-exp(-heightDelta))/heightDelta:1;
    float air=distanceToPoint*_IslandFogDensity*(.24+.76*exp(-heightAtCamera*.038)*integral);
    float under=distanceToPoint*3.912/max(_IslandUnderVisibility,1);
    return lerp(color,lerp(_IslandFog.rgb,_IslandUnderwaterColor.rgb,_IslandUnderwater),
        1-exp(-lerp(air,under,_IslandUnderwater)));
}
float Caustic(float3 p)
{
    float2 uv=(p.xz-p.y*_IslandSun.xz/max(.24,_IslandSun.y))*.67;
    float a=Fractal(uv+float2(.28,.11)*_IslandWeatherTime);
    float b=Fractal(float2(uv.y,-uv.x)*1.31+float2(-.14,.22)*_IslandWeatherTime);
    return pow(saturate(1-4*abs(a-b)),8)*exp(p.y*.12)*_IslandDay;
}
#endif

