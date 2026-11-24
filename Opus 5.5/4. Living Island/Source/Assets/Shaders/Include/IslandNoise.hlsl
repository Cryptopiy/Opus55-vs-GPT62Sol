#ifndef ISLAND_NOISE_INCLUDED
#define ISLAND_NOISE_INCLUDED
// Plain-HLSL procedural noise shared by shaders and compute shaders (no textures: everything is
// computed, so nothing tiles).

float IslandHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float2 IslandHash22(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.xx + p3.yz) * p3.zy);
}

uint IslandHashU(uint x)
{
    x ^= x >> 16; x *= 0x7feb352du; x ^= x >> 15; x *= 0x846ca68bu; x ^= x >> 16;
    return x;
}

float IslandHashCell(int2 c, uint seed)
{
    uint h = IslandHashU((uint)c.x * 1597334677u ^ (uint)c.y * 3812015801u ^ seed * 2654435769u);
    return (h >> 8) * (1.0 / 16777216.0);
}

// Value noise in [0,1] with quintic interpolation.
float IslandValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float a = IslandHash12(i);
    float b = IslandHash12(i + float2(1, 0));
    float c = IslandHash12(i + float2(0, 1));
    float d = IslandHash12(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Value noise with analytic derivatives: x = value, yz = d/dp (used for procedural normals).
float3 IslandValueNoiseD(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    float2 du = 30.0 * f * f * (f * (f - 2.0) + 1.0);
    float a = IslandHash12(i);
    float b = IslandHash12(i + float2(1, 0));
    float c = IslandHash12(i + float2(0, 1));
    float d = IslandHash12(i + float2(1, 1));
    float k1 = b - a, k2 = c - a, k4 = a - b - c + d;
    return float3(a + k1 * u.x + k2 * u.y + k4 * u.x * u.y,
                  du * float2(k1 + k4 * u.y, k2 + k4 * u.x));
}

float IslandFbm(float2 p, int octaves)
{
    float sum = 0.0, amp = 0.5, norm = 0.0;
    [loop] for (int o = 0; o < octaves; o++)
    {
        sum += amp * IslandValueNoise(p);
        norm += amp;
        p = mul(float2x2(1.6, 1.2, -1.2, 1.6), p) + 3.17; // rotate + scale: hides lattice alignment
        amp *= 0.5;
    }
    return sum / norm;
}

// fBm of derivative noise: x = value (0..1), yz = gradient.
float3 IslandFbmD(float2 p, int octaves)
{
    float3 sum = 0.0;
    float amp = 0.5, norm = 0.0;
    float2x2 m = float2x2(1.6, 1.2, -1.2, 1.6);
    float2x2 acc = float2x2(1, 0, 0, 1);
    [loop] for (int o = 0; o < octaves; o++)
    {
        float3 n = IslandValueNoiseD(p);
        sum.x += amp * n.x;
        sum.yz += amp * mul(n.yz, acc); // chain rule through the accumulated rotation/scale
        norm += amp;
        p = mul(m, p) + 3.17;
        acc = mul(m, acc);
        amp *= 0.5;
    }
    return float3(sum.x / norm, sum.yz / norm);
}

// Animated Voronoi: x = F1, y = F2 distance.
float2 IslandVoronoi(float2 p, float time)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float f1 = 8.0, f2 = 8.0;
    [unroll] for (int y = -1; y <= 1; y++)
    [unroll] for (int x = -1; x <= 1; x++)
    {
        float2 g = float2(x, y);
        float2 o = IslandHash22(i + g);
        o = 0.5 + 0.42 * sin(time + 6.2831 * o);
        float d = length(g + o - f);
        if (d < f1) { f2 = f1; f1 = d; }
        else if (d < f2) { f2 = d; }
    }
    return float2(f1, f2);
}

#endif
