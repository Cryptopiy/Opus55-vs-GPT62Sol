using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// Creates URP Lit / Particles materials from code with exactly the keywords and render states URP's own material
    /// editor would set. The editor setup script uses the same functions to write "shader keeper" materials into
    /// Resources so that player builds keep the required shader variants.
    /// </summary>
    public static class MaterialFactory
    {
        public const string LitName = "Universal Render Pipeline/Lit";
        public const string ParticleName = "Universal Render Pipeline/Particles/Unlit";

        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        public static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        public static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
        static readonly int Metallic = Shader.PropertyToID("_Metallic");
        static readonly int Smoothness = Shader.PropertyToID("_Smoothness");
        static readonly int BumpMap = Shader.PropertyToID("_BumpMap");
        static readonly int BumpScale = Shader.PropertyToID("_BumpScale");
        static readonly int Surface = Shader.PropertyToID("_Surface");
        static readonly int Blend = Shader.PropertyToID("_Blend");
        static readonly int SrcBlend = Shader.PropertyToID("_SrcBlend");
        static readonly int DstBlend = Shader.PropertyToID("_DstBlend");
        static readonly int SrcBlendAlpha = Shader.PropertyToID("_SrcBlendAlpha");
        static readonly int DstBlendAlpha = Shader.PropertyToID("_DstBlendAlpha");
        static readonly int ZWrite = Shader.PropertyToID("_ZWrite");
        static readonly int Cull = Shader.PropertyToID("_Cull");
        static readonly int PreserveSpecular = Shader.PropertyToID("_BlendModePreserveSpecular");

        static Shader lit, particle;
        public static Shader LitShader => lit != null ? lit : (lit = Shader.Find(LitName));
        public static Shader ParticleShader => particle != null ? particle : (particle = Shader.Find(ParticleName));

        static Material New(Shader shader, string name) =>
            AssetTracker.Track(new Material(shader) { name = name });

        public static Material Lit(string name, Color color, float metallic, float smoothness,
            Texture albedo = null, Texture normal = null, float normalScale = 1f)
        {
            Material m = New(LitShader, name);
            ConfigureLit(m, color, metallic, smoothness, albedo, normal, normalScale, false, Color.black);
            return m;
        }

        /// <summary>Lit material with the _EMISSION keyword on (needed for glows, gate pulses and enemy hit flashes).</summary>
        public static Material LitEmissive(string name, Color color, Color emission, float metallic, float smoothness,
            Texture albedo = null, Texture normal = null, float normalScale = 1f)
        {
            Material m = New(LitShader, name);
            ConfigureLit(m, color, metallic, smoothness, albedo, normal, normalScale, true, emission);
            return m;
        }

        public static void ConfigureLit(Material m, Color color, float metallic, float smoothness,
            Texture albedo, Texture normal, float normalScale, bool emissive, Color emission)
        {
            m.SetColor(BaseColor, color);
            m.SetFloat(Metallic, metallic);
            m.SetFloat(Smoothness, smoothness);
            if (albedo != null) m.SetTexture(BaseMap, albedo);
            if (normal != null)
            {
                m.SetTexture(BumpMap, normal);
                m.SetFloat(BumpScale, normalScale);
                m.EnableKeyword("_NORMALMAP");
            }
            if (emissive)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor(EmissionColor, emission);
                // URP's material validation keeps _EMISSION only with an emissive GI flag (no GI is baked here).
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
        }

        /// <summary>Switches a Lit material to alpha-blended transparency (URP "Alpha" mode without preserve-specular).</summary>
        public static void MakeTransparent(Material m)
        {
            m.SetFloat(Surface, 1f);
            m.SetFloat(Blend, 0f);
            m.SetFloat(PreserveSpecular, 0f);
            m.SetFloat(SrcBlend, (float)BlendMode.SrcAlpha);
            m.SetFloat(DstBlend, (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(SrcBlendAlpha, (float)BlendMode.One);
            m.SetFloat(DstBlendAlpha, (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(ZWrite, 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("ShadowCaster", false);
        }

        public static Material Particle(string name, Texture tex, Color color, bool additive)
        {
            Material m = New(ParticleShader, name);
            ConfigureParticle(m, tex, color, additive);
            return m;
        }

        public static void ConfigureParticle(Material m, Texture tex, Color color, bool additive)
        {
            if (tex != null) m.SetTexture(BaseMap, tex);
            m.SetColor(BaseColor, color);
            m.SetFloat(Surface, 1f);
            m.SetFloat(Blend, additive ? 2f : 0f);
            m.SetFloat(SrcBlend, (float)BlendMode.SrcAlpha);
            m.SetFloat(DstBlend, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(SrcBlendAlpha, (float)BlendMode.One);
            m.SetFloat(DstBlendAlpha, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat(ZWrite, 0f);
            m.SetFloat(Cull, 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}
