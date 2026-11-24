#ifndef DS_COMMON_INCLUDED
#define DS_COMMON_INCLUDED

// Small, branch-free procedural helpers shared by the Destruction Sandbox shaders.

float DSHash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float DSHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Trilinear value noise in [0,1].
float DSValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    float3 u = f * f * (3.0 - 2.0 * f);
    float n000 = DSHash13(i);
    float n100 = DSHash13(i + float3(1, 0, 0));
    float n010 = DSHash13(i + float3(0, 1, 0));
    float n110 = DSHash13(i + float3(1, 1, 0));
    float n001 = DSHash13(i + float3(0, 0, 1));
    float n101 = DSHash13(i + float3(1, 0, 1));
    float n011 = DSHash13(i + float3(0, 1, 1));
    float n111 = DSHash13(i + float3(1, 1, 1));
    float nx00 = lerp(n000, n100, u.x);
    float nx10 = lerp(n010, n110, u.x);
    float nx01 = lerp(n001, n101, u.x);
    float nx11 = lerp(n011, n111, u.x);
    return lerp(lerp(nx00, nx10, u.y), lerp(nx01, nx11, u.y), u.z);
}

float DSValueNoise2(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = DSHash12(i);
    float b = DSHash12(i + float2(1, 0));
    float c = DSHash12(i + float2(0, 1));
    float d = DSHash12(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// Three-octave fBm in [0,1] (fixed loop count, unrolled by the compiler).
float DSFbm(float3 p)
{
    float sum = 0.0;
    float amp = 0.5;
    [unroll] for (int o = 0; o < 3; o++)
    {
        sum += DSValueNoise(p) * amp;
        p = p * 2.03 + 17.1;
        amp *= 0.5;
    }
    return sum / 0.875;
}

// The renderer user value packs: bits 0-7 brightness variation, bits 8-25 pattern seed.
float DSBrightness(uint userValue, float variation)
{
    float t = (float)(userValue & 255u) / 255.0;
    return 1.0 + (t * 2.0 - 1.0) * variation;
}

float3 DSSeedOffset(uint userValue)
{
    uint s = userValue >> 8;
    return float3((float)(s & 63u), (float)((s >> 6) & 63u), (float)((s >> 12) & 63u)) * 1.37;
}

// Gradient sky used by the skybox and by the glass reflections, so both always agree.
float3 DSSkyColor(float3 dir, float3 zenith, float3 horizon, float3 ground)
{
    float y = dir.y;
    float up = saturate(y);
    float3 sky = lerp(horizon, zenith, pow(up, 0.55));
    float3 below = lerp(horizon, ground, saturate(-y * 4.0));
    return y >= 0.0 ? sky : below;
}

#endif
