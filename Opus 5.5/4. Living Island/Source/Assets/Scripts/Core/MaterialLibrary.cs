using UnityEngine;
using UnityEngine.Rendering;

namespace LivingIsland
{
    /// <summary>All runtime-created materials (every texture/pattern is procedural inside the shaders).</summary>
    public sealed class MaterialLibrary
    {
        public readonly Material Terrain, Water, Grass, Bush, Flower;
        public readonly Material Rain, Splash, Fire, Spark, Smoke, Firefly, Lightning, Beam, SwimSplash, Ember;
        public readonly Material Skin, Shirt, Shorts, Hair;
        readonly IslandAssets assets;

        public MaterialLibrary(IslandAssets a)
        {
            assets = a;
            Terrain = New(a.terrainShader, "Terrain");
            Water = New(a.waterShader, "Water");
            Water.renderQueue = (int)RenderQueue.Transparent - 100; // before every other transparent (rain, particles)
            Grass = New(a.grassShader, "Grass");
            Bush = New(a.foliageShader, "Bush");
            Bush.SetFloat("_LeafPattern", 1f);
            Bush.SetColor(ShaderIds.BaseColor, new Color(0.26f, 0.46f, 0.15f));
            Flower = New(a.flowerShader, "Flowers");

            Rain = New(a.rainShader, "Rain");
            Splash = Particle(a.particleAlphaShader, "RainSplash", new Color(0.8f, 0.85f, 0.9f, 0.55f), 0f);
            Fire = Particle(a.particleAdditiveShader, "Fire", new Color(1f, 0.55f, 0.2f, 1f), 1f);
            Ember = Particle(a.particleAdditiveShader, "Ember", new Color(1f, 0.5f, 0.15f, 1f), 0f);
            Spark = Particle(a.particleAdditiveShader, "Spark", new Color(1f, 0.7f, 0.3f, 1f), 0f);
            Smoke = Particle(a.particleAlphaShader, "Smoke", new Color(0.32f, 0.31f, 0.30f, 0.45f), 1f);
            Firefly = Particle(a.particleAdditiveShader, "Firefly", new Color(0.75f, 1f, 0.35f, 1f), 0f);
            SwimSplash = Particle(a.particleAlphaShader, "SwimSplash", new Color(0.9f, 0.95f, 1f, 0.7f), 0f);
            Lightning = New(a.lightningShader, "LightningBolt");
            Beam = New(a.beamShader, "LighthouseBeam");

            Skin = Prop(new Color(0.78f, 0.55f, 0.42f), 0.35f);
            Shirt = Prop(new Color(0.08f, 0.45f, 0.52f), 0.25f);
            Shorts = Prop(new Color(0.82f, 0.72f, 0.52f), 0.2f);
            Hair = Prop(new Color(0.12f, 0.08f, 0.05f), 0.4f);
        }

        static Material New(Shader s, string name)
        {
            if (s == null) { Debug.LogError("[LI] Missing shader for material " + name); s = Shader.Find("Hidden/InternalErrorShader"); }
            return new Material(s) { name = name, enableInstancing = true };
        }

        /// <summary>_Shape: 0 = soft round sprite, 1 = flame/puff shape with animated noise.</summary>
        static Material Particle(Shader s, string name, Color tint, float shape)
        {
            var m = New(s, name);
            m.SetColor(ShaderIds.BaseColor, tint);
            m.SetFloat("_Shape", shape);
            return m;
        }

        public Material Prop(Color color, float smoothness, float metallic = 0f)
        {
            var m = New(assets.propShader, "Prop");
            m.SetColor(ShaderIds.BaseColor, color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", metallic);
            return m;
        }
    }
}
