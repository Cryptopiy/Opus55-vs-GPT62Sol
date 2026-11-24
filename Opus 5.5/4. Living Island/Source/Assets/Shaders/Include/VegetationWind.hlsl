#ifndef ISLAND_VEGETATION_WIND_INCLUDED
#define ISLAND_VEGETATION_WIND_INCLUDED
// Vertex wind for palms and bushes (I-11). Vertex colour channels (authored in Blender for the palms,
// generated for bushes): R = trunk bend weight (0 at the base, 1 at the crown), G = frond flutter weight
// (0 at the attachment, 1 at the tip), B = per-frond random phase.
#include "IslandCommon.hlsl"

float3 VegetationWind(float3 positionWS, float4 vcol, float windScale)
{
    float3 pivot = float3(UNITY_MATRIX_M._m03, UNITY_MATRIX_M._m13, UNITY_MATRIX_M._m23);
    float treeScale = length(float3(UNITY_MATRIX_M._m01, UNITY_MATRIX_M._m11, UNITY_MATRIX_M._m21));
    float strength = _IslandWind.z * windScale;
    float3 windDir = float3(_IslandWind.x, 0.0, _IslandWind.y);
    float gust = IslandGust(pivot.xz);
    float treePhase = dot(pivot.xz, float2(0.131, 0.173));

    // Trunk: slow sway plus a gust-driven lean, growing with the square of the height (vertex colour R).
    float sway = sin(_IslandTime * (0.8 + 0.12 * _IslandWind.z) + treePhase) * 0.4 + gust * 1.1;
    float bend = (0.05 + 0.11 * strength) * sway * treeScale * vcol.r;
    float3 offset = windDir * bend;
    offset.y -= abs(bend) * 0.12;                         // arc, not shear: the crown drops a little

    // Fronds/leaves: fast flutter (up/down + along the wind) weighted by vertex colour G.
    float phase = vcol.b * 6.2831853 + treePhase;
    float flutter = sin(_IslandTime * (3.5 + 1.5 * _IslandWind.z) + phase + positionWS.y * 1.2) * 0.6
                  + sin(_IslandTime * 6.7 + phase * 1.9) * 0.3;
    offset.y += flutter * vcol.g * 0.05 * (0.5 + strength);
    offset += windDir * (vcol.g * (0.08 + 0.22 * gust) * strength * 0.35 * treeScale);
    return positionWS + offset;
}

#endif
