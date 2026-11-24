Shader "Wave Survival/Atmospheric Ink"
{
    Properties
    {
        [MainTexture] _MainTex("Mask", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        _VertexTint("Vertex tint", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _MainTex_ST;
                float _VertexTint;
            CBUFFER_END
            struct Varyings { float4 position : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(float4 position : POSITION, float2 uv : TEXCOORD0, half4 color : COLOR)
            {
                Varyings output;
                output.position = TransformObjectToHClip(position.xyz);
                output.uv = TRANSFORM_TEX(uv, _MainTex);
                output.color = color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _BaseColor * lerp(1, input.color, _VertexTint);
            }
            ENDHLSL
        }
    }
}


