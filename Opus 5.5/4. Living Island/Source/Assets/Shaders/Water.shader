// Ocean surface (I-04 .. I-08, I-26 foam ring).
// Vertex: sum of 4 Gerstner waves (direction/amplitude/wavelength from GameConfig) with analytic normals,
//         damped in shallow water (same formula as WaveModel.cs) and faded where the grid is too coarse.
// Fragment: depth-based colour #2fd6c9 -> #06304f from the camera depth texture, refraction of the opaque
//           texture, fresnel reflection of the procedural sky cubemap, sun glints, crest subsurface tint,
//           animated shoreline foam below 0.6 m, storm crest foam, swimmer foam ring, rain ripple rings,
//           and a separate underwater (back-face) look with Snell's window.
Shader "Island/Water"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-100" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Water"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite On
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Include/IslandCommon.hlsl"
            #include "Include/TerrainCommon.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
            float _WaterPad;
            CBUFFER_END

            float4 _GerstnerWaves[4];     // xy = direction, z = amplitude, w = wavenumber k
            float4 _GerstnerQ;
            float4 _GerstnerOmega;
            float _WaveAmplitudeScale;
            float _WaterRoughness;
            float _WaterQuality;
            float4 _WaterParams;          // x = foam depth, y = shallow->deep depth, z = refraction, w = shore damping depth
            float4 _WaterShallowColor;
            float4 _WaterDeepColor;

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float waveHeight : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 basePos = TransformObjectToWorld(v.positionOS.xyz);
                float2 x0 = basePos.xz;
                float depth = _WaterParams2.x - IslandHeight(x0);
                float t = saturate(depth / _WaterParams.w);
                float damp = t * t * (3.0 - 2.0 * t) * _WaveAmplitudeScale;
                float spacing = v.uv.x;

                float3 disp = 0.0;
                float nx = 0.0, nz = 0.0, ny = 1.0;
                [unroll] for (int i = 0; i < 4; i++)
                {
                    float4 w = _GerstnerWaves[i];
                    float wavelength = 6.2831853 / w.w;
                    // Waves shorter than ~4 grid spacings would alias on the coarse far grid: fade them out.
                    float lod = saturate(wavelength / (spacing * 4.0) - 0.5);
                    float a = w.z * damp * lod;
                    float theta = w.w * dot(w.xy, x0) - _GerstnerOmega[i] * _IslandTime;
                    float c = cos(theta), s = sin(theta);
                    float q = _GerstnerQ[i];
                    disp.x += q * a * w.x * c;
                    disp.z += q * a * w.y * c;
                    disp.y += a * s;
                    float wa = w.w * a;
                    nx -= w.x * wa * c;
                    nz -= w.y * wa * c;
                    ny -= q * wa * s;
                }
                o.positionWS = float3(basePos.x + disp.x, _WaterParams2.x + disp.y, basePos.z + disp.z);
                o.normalWS = normalize(float3(nx, ny, nz));
                o.waveHeight = disp.y;
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            // Expanding rain rings, one per cell and layer (only evaluated while it rains).
            float2 RainRipples(float2 xz, float t)
            {
                float2 total = 0.0;
                [unroll] for (int layer = 0; layer < 2; layer++)
                {
                    float2 p = xz * 1.7 + layer * 0.37;
                    float2 cell = floor(p);
                    float2 f = frac(p);
                    float2 h = IslandHash22(cell + layer * 17.0);
                    float2 centre = 0.25 + 0.5 * h;
                    float phase = frac(t * 0.85 + h.x * 7.0 + layer * 0.5);
                    float2 d = f - centre;
                    float r = length(d);
                    float radius = phase * 0.42;
                    float ring = sin((r - radius) * 45.0) * smoothstep(0.07, 0.0, abs(r - radius)) * (1.0 - phase);
                    total += d / max(r, 1e-3) * ring;
                }
                return total * 0.6;
            }

            half4 Frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                float3 p = i.positionWS;
                float3 toCam = _WorldSpaceCameraPos - p;
                float dist = length(toCam);
                float3 V = toCam / dist;
                float2 screenUV = GetNormalizedScreenSpaceUV(i.positionCS);
                float t = _IslandTime;
                float detailFade = saturate(1.0 - dist / 260.0);

                // ---------------- detail normal: scrolling derivative noise + rain rings
                float2 wind = _IslandWind.xy;
                float rough = lerp(0.35, 1.25, _WaterRoughness);
                float3 d1 = IslandFbmD(p.xz * 0.32 + wind * t * 0.45, 3);
                float2 slope = d1.yz * 0.32;
                if (_WaterQuality > 0.25)
                {
                    float3 d2 = IslandFbmD(p.xz * 0.95 - float2(wind.y, -wind.x) * t * 0.6 + 31.0, 2);
                    slope += d2.yz * 0.22;
                }
                slope *= rough * detailFade;
                if (_RainIntensity > 0.01) slope += RainRipples(p.xz, t) * _RainIntensity * saturate(1.0 - dist / 60.0);
                float3 N = normalize(i.normalWS + float3(-slope.x, 0.0, -slope.y));

                Light ml = GetMainLight(TransformWorldToShadowCoord(p), p, half4(1, 1, 1, 1));
                float shadow = ml.shadowAttenuation;

                // ---------------- underwater view of the surface (camera below the water)
                if (!isFront)
                {
                    float3 Nb = -N;
                    float cosT = saturate(dot(Nb, V));
                    // Snell's window: beyond the critical angle (~48.6 deg) the surface reflects the depths.
                    float window = smoothstep(0.6, 0.74, cosT);
                    float3 above = SampleSceneColor(saturate(screenUV + Nb.xz * 0.04));
                    float3 col = lerp(_UnderwaterColor.rgb * 0.9, above * float3(0.75, 0.97, 0.97), window);
                    col += _UnderwaterColor.rgb * pow(cosT, 8.0) * 0.5;
                    return half4(IslandApplyFog(col, p), 1.0);
                }

                // ---------------- depth through the water (camera depth texture)
                float surfEye = -TransformWorldToView(p).z;
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float3 rayPerEye = -toCam / surfEye;
                float3 scenePos = _WorldSpaceCameraPos + rayPerEye * sceneEye;
                float waterDepth = max(p.y - scenePos.y, 0.0);
                float thickness = max(sceneEye - surfEye, 0.0);

                // ---------------- refraction (rejects samples of objects in front of the water)
                float2 refrUV = screenUV;
                float refrDepth = waterDepth;
                if (_WaterQuality > 0.25)
                {
                    float2 offset = N.xz * (_WaterParams.z * 0.05) * saturate(thickness * 0.5) / max(1.0, dist * 0.05);
                    float2 uv2 = saturate(screenUV + offset);
                    float eye2 = LinearEyeDepth(SampleSceneDepth(uv2), _ZBufferParams);
                    if (eye2 > surfEye)
                    {
                        refrUV = uv2;
                        refrDepth = max(p.y - (_WorldSpaceCameraPos + rayPerEye * eye2).y, 0.0);
                    }
                }
                float3 sceneColor = SampleSceneColor(refrUV);

                // ---------------- water body colour by depth (#2fd6c9 shallow -> #06304f deep)
                float depthT = smoothstep(0.0, 1.0, saturate(refrDepth / _WaterParams.y));
                float3 bodyLight = IslandAmbient(half3(0, 1, 0)) * 1.1 + ml.color * (saturate(ml.direction.y) * 0.45 * shadow + 0.05);
                float3 waterCol = lerp(_WaterShallowColor.rgb, _WaterDeepColor.rgb, depthT) * bodyLight;
                float opacity = 1.0 - exp(-refrDepth * 0.42 - thickness * 0.03);
                float3 transmitted = sceneColor * lerp(float3(1, 1, 1), _WaterShallowColor.rgb * 1.15, saturate(refrDepth * 0.6));
                float3 body = lerp(transmitted, waterCol, opacity);

                // ---------------- reflection + sun
                float3 R = reflect(-V, N);
                R.y = max(abs(R.y), 0.015);
                float3 sky = SAMPLE_TEXTURECUBE_LOD(_IslandEnvCube, sampler_IslandEnvCube, R, 0).rgb;
                float fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(N, V)), 5.0);
                float3 H = normalize(V + ml.direction);
                float nh = saturate(dot(N, H));
                float sparkle = smoothstep(0.6, 1.0, IslandValueNoise(p.xz * 6.0 + t * 1.3)) * detailFade;
                float spec = pow(nh, 1400.0) * (30.0 + 60.0 * sparkle) + pow(nh, 180.0) * 0.9;
                float3 sunSpec = ml.color * spec * shadow * saturate(ml.direction.y * 6.0);

                // Subsurface light through thin wave crests (seen against the light).
                float crest = saturate(i.waveHeight / (0.45 * max(_WaveAmplitudeScale, 0.5)) + 0.25);
                float sss = pow(saturate(dot(V, -ml.direction + N * 0.55)), 4.0) * crest;
                float3 sssCol = _WaterShallowColor.rgb * ml.color * sss * (0.55 * shadow) * (_WaterQuality > 0.25 ? 1.0 : 0.0);

                // ---------------- foam
                float fnoise = IslandFbm(p.xz * 1.25 + float2(t * 0.18, t * 0.11), 3);
                float shore = saturate(1.0 - waterDepth / _WaterParams.x);
                float bands = 0.5 + 0.5 * sin(waterDepth * 14.0 - t * 2.4 + fnoise * 4.0);
                // Lacy foam: strongest at the waterline, breaking up into noisy bands towards 0.6 m depth.
                float shoreFoam = pow(shore, 1.6) * smoothstep(0.48, 0.78, fnoise * 0.7 + bands * 0.42 + shore * 0.3);
                float crestFoam = smoothstep(0.62, 0.95, crest + fnoise * 0.35 - 0.15) * saturate((_WaveAmplitudeScale - 1.05) * 1.4);
                float ring = 0.0;
                if (_PlayerPosition.w > 0.5)
                {
                    float pd = distance(p.xz, _PlayerPosition.xz);
                    ring = smoothstep(0.3, 0.55, pd) * smoothstep(1.35, 0.7, pd) * smoothstep(0.25, 0.65, fnoise + 0.2 * sin(pd * 9.0 - t * 4.0));
                }
                float foam = saturate(shoreFoam + crestFoam + ring);
                float3 foamCol = IslandAmbient(half3(0, 1, 0)) * 1.2 + ml.color * (saturate(dot(i.normalWS, ml.direction)) * shadow);

                float3 color = lerp(body, sky, fresnel) + sunSpec + sssCol;
                color = lerp(color, foamCol * 0.92, foam);
                color = IslandApplyFog(color, p);

                // Soft contact with the beach: fade the last few centimetres instead of a hard seam.
                float edge = saturate(waterDepth / 0.07);
                return half4(color, edge);
            }
            ENDHLSL
        }
    }
}
