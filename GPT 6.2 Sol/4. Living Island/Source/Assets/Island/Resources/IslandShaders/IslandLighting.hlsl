#ifndef ISLAND_LIGHTING
#define ISLAND_LIGHTING
#include "IslandCommon.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

float3 IslandLighting(float3 albedo,float3 normal,float3 ws,float4 cs,float shine,float translucency)
{
    float3 view=SafeNormalize(GetCameraPositionWS()-ws);
    float2 screenUV=GetNormalizedScreenSpaceUV(cs);
    AmbientOcclusionFactor ao=GetScreenSpaceAmbientOcclusion(screenUV);
    Light sun=GetMainLight(TransformWorldToShadowCoord(ws));
    float nl=dot(normal,sun.direction);
    float diffuse=saturate(nl)+saturate(-nl)*translucency;
    float3 halfWay=SafeNormalize(view+sun.direction);
    float spec=pow(saturate(dot(normal,halfWay)),lerp(21,108,shine))*shine*.19;
    float up=normal.y*.5+.5;
    float3 ambient=lerp(_IslandFog.rgb*.33,_IslandHorizon.rgb*.49,up)+float3(.016,.026,.042);
    float3 result=albedo*ambient*ao.indirectAmbientOcclusion;
    result+=(albedo*diffuse+spec)*sun.color*(sun.shadowAttenuation*sun.distanceAttenuation*ao.directAmbientOcclusion);
    #if defined(_ADDITIONAL_LIGHTS)
        InputData inputData=(InputData)0;
        inputData.positionWS=ws;
        inputData.normalizedScreenSpaceUV=screenUV;
        #if USE_CLUSTER_LIGHT_LOOP
        UNITY_LOOP for(uint lightIndex=0; lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS); lightIndex++)
        {
            Light extra=GetAdditionalLight(lightIndex,ws,half4(1,1,1,1));
            result+=albedo*extra.color*saturate(dot(normal,extra.direction))*extra.shadowAttenuation*extra.distanceAttenuation;
        }
        #endif
        uint count=GetAdditionalLightsCount();
        LIGHT_LOOP_BEGIN(count)
            Light extra=GetAdditionalLight(lightIndex,ws,half4(1,1,1,1));
            result+=albedo*extra.color*(saturate(dot(normal,extra.direction))+.09)*extra.shadowAttenuation*extra.distanceAttenuation;
        LIGHT_LOOP_END
    #endif
    return result;
}
#endif

