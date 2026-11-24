// GPU-instanced grass blades drawn with Graphics.RenderMeshIndirect (I-10, I-11).
// Instances come from GrassPlacement.compute. Each blade curves under a natural lean, gusts of scrolling
// noise along the global wind and bends away from the player within 1.5 m (_PlayerPosition / _PlayerParams).
Shader "Island/Grass"
{
    Properties { }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Cull Off

        HLSLINCLUDE
        #include "Include/IslandCommon.hlsl"

        struct GrassInstance { float4 posYaw; float4 shape; };
        StructuredBuffer<GrassInstance> _GrassInstances;
        float4 _TerrainParams2;   // w = blade height (also used by the compute shader)
        float _GrassBladeWidth;

        CBUFFER_START(UnityPerMaterial)
        float _GrassPad;
        CBUFFER_END

        struct Attributes { float3 positionOS : POSITION; float2 uv : TEXCOORD0; uint instanceID : SV_InstanceID; };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float3 positionWS : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            float3 data : TEXCOORD2;      // x = height fraction, y = colour variation, z = ground tint
        };

        Varyings GrassVert(Attributes v)
        {
            GrassInstance g = _GrassInstances[v.instanceID];
            float3 root = g.posYaw.xyz;
            float yaw = g.posYaw.w;
            float height = g.shape.x;
            float width = g.shape.y * _GrassBladeWidth;
            float hFrac = v.uv.y;

            float2 facing = float2(sin(yaw), cos(yaw));
            float3 right = float3(facing.y, 0.0, -facing.x);
            float3 fwd = float3(facing.x, 0.0, facing.y);

            // Wind: gust noise scrolling along the wind plus a per-blade flutter.
            float gust = IslandGust(root.xz);
            float strength = _IslandWind.z;
            float flutter = sin(_IslandTime * (2.2 + strength * 0.8) + g.shape.w * 6.2831 + dot(root.xz, _IslandWind.xy) * 0.35);
            float2 windBend = _IslandWind.xy * (gust * _IslandWind.w * strength * 0.7 + flutter * 0.08 * min(strength, 2.0));

            // Player interaction: bend away radially inside the bend radius.
            float2 away = root.xz - _PlayerPosition.xz;
            float pd = length(away);
            float push = saturate(1.0 - pd / _PlayerParams.x) * step(abs(root.y - _PlayerPosition.y), 1.6);
            float2 pushDir = away / max(pd, 1e-3);

            float lean = 0.18 + 0.25 * frac(g.shape.w * 7.31);
            float2 bend = fwd.xz * lean + windBend + pushDir * push * 1.5;
            float bendAmt = saturate(length(bend));
            // Quadratic curve; shorten the stem as it bends so the blade length stays roughly constant.
            float2 offset = bend * (height * hFrac * hFrac * 0.85);
            float up = height * hFrac * (1.0 - 0.38 * bendAmt * hFrac) * (1.0 - 0.45 * push);
            float3 pos = root + right * (v.positionOS.x * width) + float3(offset.x, up, offset.y);

            float3 tangentUp = normalize(float3(bend.x * 1.7 * hFrac, 1.0, bend.y * 1.7 * hFrac));
            float3 n = normalize(cross(right, tangentUp));
            Varyings o;
            o.positionWS = pos;
            // Blend towards up: grass reads as a soft lit mass instead of dark-sided cards.
            o.normalWS = normalize(lerp(n, float3(0, 1, 0), 0.45));
            o.data = float3(hFrac, g.shape.z, IslandValueNoise(root.xz * 0.011 + 5.3));
            o.positionCS = TransformWorldToHClip(pos);
            return o;
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVert
            #pragma fragment Frag
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION

            half4 Frag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.positionWS);
                float3 n = normalize(i.normalWS);
                if (!isFront) n = normalize(float3(-n.x, n.y, -n.z));
                float h = i.data.x;
                float tint = smoothstep(0.35, 0.65, i.data.z);
                half3 baseC = half3(0.10, 0.19, 0.04);
                half3 tipLush = half3(0.32, 0.52, 0.12), tipDry = half3(0.56, 0.54, 0.22);
                half3 tip = lerp(tipLush, tipDry, saturate(tint * 0.8 + (i.data.y - 0.5) * 0.5));
                half3 albedo = lerp(baseC, tip, smoothstep(0.0, 1.0, h)) * (0.85 + 0.3 * i.data.y);
                half smoothV = 0.25;
                IslandApplyWetness(albedo, smoothV, half3(0, 1, 0), 0.6);
                IslandSurface s = IslandDefaultSurface(albedo, n);
                s.smoothness = smoothV;
                s.translucency = 0.55;
                s.occlusion = lerp(0.35, 1.0, h);
                half3 color = IslandLighting(s, i.positionWS, i.positionCS, viewDir);
                return half4(IslandApplyFog(color, i.positionWS), 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings i) : SV_Target { return i.positionCS.z; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVert
            #pragma fragment NormalsFrag
            half4 NormalsFrag(Varyings i, bool isFront : SV_IsFrontFace) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                return half4(isFront ? n : float3(-n.x, n.y, -n.z), 0.0);
            }
            ENDHLSL
        }
    }
}
