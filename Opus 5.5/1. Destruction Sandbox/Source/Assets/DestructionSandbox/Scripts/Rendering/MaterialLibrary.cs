using UnityEngine;
using UnityEngine.Rendering;
using P = DestructionSandbox.GameConfig.Palette;
using L = DestructionSandbox.GameConfig.Lighting;
using W = DestructionSandbox.GameConfig.World;
using MatCfg = DestructionSandbox.GameConfig.Materials;

namespace DestructionSandbox
{
    /// <summary>Creates every render material (from the project's own shaders) and every physics material.</summary>
    public sealed class MaterialLibrary
    {
        public Material Stone { get; }
        public Material Wood { get; }
        public Material Glass { get; }
        public Material Concrete { get; }
        public Material Metal { get; }
        public Material ShellPaint { get; }
        public Material Ground { get; }
        public Material Sky { get; }

        public Material DustAlpha { get; }
        public Material SmokeAlpha { get; }
        public Material FireAdditive { get; }
        public Material SparkAdditive { get; }
        public Material GlassGlint { get; }
        public Material SplinterSolid { get; }
        public Material TrailAdditive { get; }
        public Material FlashAdditive { get; }
        public Material OrbParticles { get; }

        public Material OrbFlying { get; }
        public Shader ShockwaveShader { get; }
        public Shader ScorchShader { get; }
        public Shader OrbShader { get; }

        public PhysicsMaterial StonePhysics { get; }
        public PhysicsMaterial WoodPhysics { get; }
        public PhysicsMaterial GlassPhysics { get; }
        public PhysicsMaterial GroundPhysics { get; }
        public PhysicsMaterial ProjectilePhysics { get; }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        private static readonly int PatternId = Shader.PropertyToID("_Pattern");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MetallicId = Shader.PropertyToID("_Metallic");
        private static readonly int VariationId = Shader.PropertyToID("_Variation");
        private static readonly int EdgeColorId = Shader.PropertyToID("_EdgeColor");
        private static readonly int SkyZenithId = Shader.PropertyToID("_SkyZenith");
        private static readonly int SkyHorizonId = Shader.PropertyToID("_SkyHorizon");
        private static readonly int SkyGroundId = Shader.PropertyToID("_SkyGround");
        private static readonly int MinorCellId = Shader.PropertyToID("_MinorCell");
        private static readonly int MajorCellId = Shader.PropertyToID("_MajorCell");
        private static readonly int MinorWidthId = Shader.PropertyToID("_MinorWidth");
        private static readonly int MajorWidthId = Shader.PropertyToID("_MajorWidth");
        private static readonly int MinorDarkId = Shader.PropertyToID("_MinorDark");
        private static readonly int MajorDarkId = Shader.PropertyToID("_MajorDark");
        private static readonly int TintId = Shader.PropertyToID("_TintColor");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ShapeId = Shader.PropertyToID("_Shape");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int SoftId = Shader.PropertyToID("_SoftDistance");
        private static readonly int SunDirId = Shader.PropertyToID("_SunDirection");
        private static readonly int SunColorId = Shader.PropertyToID("_SunColor");
        private static readonly int SunSizeId = Shader.PropertyToID("_SunSize");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        public MaterialLibrary(GameBootstrap root)
        {
            Shader block = Resolve(root.BlockShader, "DestructionSandbox/Block");
            Shader glass = Resolve(root.GlassShader, "DestructionSandbox/Glass");
            Shader ground = Resolve(root.GroundShader, "DestructionSandbox/Ground");
            Shader sky = Resolve(root.SkyShader, "DestructionSandbox/SkyGradient");
            Shader particle = Resolve(root.ParticleShader, "DestructionSandbox/Particle");
            ShockwaveShader = Resolve(root.ShockwaveShader, "DestructionSandbox/Shockwave");
            ScorchShader = Resolve(root.ScorchShader, "DestructionSandbox/Scorch");
            OrbShader = Resolve(root.OrbShader, "DestructionSandbox/GravityOrb");

            Stone = BlockMat(block, "Stone", P.StoneA, P.StoneB, 0f, P.StoneSmoothness, 0f);
            Wood = BlockMat(block, "Wood", P.WoodA, P.WoodB, 1f, P.WoodSmoothness, 0f);
            Concrete = BlockMat(block, "Concrete", P.ConcreteA, P.ConcreteB, 2f, P.StoneSmoothness, 0f);
            Metal = BlockMat(block, "Metal", P.MetalA, P.MetalB, 3f, P.MetalSmoothness, P.MetalMetallic);
            ShellPaint = BlockMat(block, "ShellPaint", P.ShellA, P.ShellB, 3f, P.MetalSmoothness, 0.3f);
            Concrete.SetFloat(VariationId, 0f);
            Metal.SetFloat(VariationId, 0f);
            ShellPaint.SetFloat(VariationId, 0f);

            Glass = new Material(glass) { name = "Glass" };
            Glass.SetColor(BaseColorId, P.GlassTint);
            Glass.SetColor(EdgeColorId, P.GlassEdge);
            Glass.SetFloat(VariationId, GameConfig.Structures.ColorVariation);
            SetSkyColors(Glass);

            Ground = new Material(ground) { name = "Ground" };
            Ground.SetColor(BaseColorId, P.GroundA);
            Ground.SetColor(ColorBId, P.GroundB);
            Ground.SetFloat(SmoothnessId, P.GroundSmoothness);
            Ground.SetFloat(MinorCellId, W.GridMinorCell);
            Ground.SetFloat(MajorCellId, W.GridMajorCell);
            Ground.SetFloat(MinorWidthId, W.GridMinorLineWidth);
            Ground.SetFloat(MajorWidthId, W.GridMajorLineWidth);
            Ground.SetFloat(MinorDarkId, W.GridMinorLineDarkness);
            Ground.SetFloat(MajorDarkId, W.GridMajorLineDarkness);

            Sky = new Material(sky) { name = "Sky" };
            SetSkyColors(Sky);
            Sky.SetColor(SunColorId, L.SunColor);
            Sky.SetFloat(SunSizeId, L.SkySunSize);
            Sky.SetFloat(ExposureId, L.SkyExposure);

            DustAlpha = ParticleMat(particle, "Dust", false, 0f, 1f, 0.6f);
            SmokeAlpha = ParticleMat(particle, "Smoke", false, 0f, 1f, 1.0f);
            FireAdditive = ParticleMat(particle, "Fire", true, 0f, 2.2f, 0.5f);
            SparkAdditive = ParticleMat(particle, "Spark", true, 1f, 3.5f, 0.05f);
            GlassGlint = ParticleMat(particle, "GlassGlint", true, 2f, 1.6f, 0.05f);
            SplinterSolid = ParticleMat(particle, "Splinter", false, 4f, 1f, 0.05f);
            TrailAdditive = ParticleMat(particle, "Trail", true, 3f, 1.6f, 0.05f);
            FlashAdditive = ParticleMat(particle, "Flash", true, 0f, 6f, 0.05f);
            OrbParticles = ParticleMat(particle, "OrbParticles", true, 0f, 4f, 0.2f);
            OrbFlying = new Material(OrbShader) { name = "OrbFlying" };

            StonePhysics = PhysMat("Stone", MatCfg.StoneFriction, MatCfg.StoneBounciness);
            WoodPhysics = PhysMat("Wood", MatCfg.WoodFriction, MatCfg.WoodBounciness);
            GlassPhysics = PhysMat("Glass", MatCfg.GlassFriction, MatCfg.GlassBounciness);
            GroundPhysics = PhysMat("Ground", MatCfg.GroundFriction, MatCfg.GroundBounciness);
            ProjectilePhysics = PhysMat("Projectile", MatCfg.ProjectileFriction, MatCfg.ProjectileBounciness);
        }

        public Material ForBlock(BlockMaterialType t)
        {
            switch (t)
            {
                case BlockMaterialType.Wood: return Wood;
                case BlockMaterialType.Glass: return Glass;
                default: return Stone;
            }
        }

        public PhysicsMaterial PhysicsFor(BlockMaterialType t)
        {
            switch (t)
            {
                case BlockMaterialType.Wood: return WoodPhysics;
                case BlockMaterialType.Glass: return GlassPhysics;
                default: return StonePhysics;
            }
        }

        public void SetSunDirection(Vector3 directionToSun)
        {
            Sky.SetVector(SunDirId, directionToSun.normalized);
        }

        private static Shader Resolve(Shader assigned, string name)
        {
            if (assigned != null) return assigned;
            Shader found = Shader.Find(name);
            if (found == null) throw new MissingReferenceException("Shader not found: " + name);
            return found;
        }

        private static Material BlockMat(Shader s, string name, Color a, Color b, float pattern, float smoothness, float metallic)
        {
            var m = new Material(s) { name = name };
            m.SetColor(BaseColorId, a);
            m.SetColor(ColorBId, b);
            m.SetFloat(PatternId, pattern);
            m.SetFloat(SmoothnessId, smoothness);
            m.SetFloat(MetallicId, metallic);
            m.SetFloat(VariationId, GameConfig.Structures.ColorVariation);
            m.enableInstancing = false;
            return m;
        }

        private static void SetSkyColors(Material m)
        {
            m.SetColor(SkyZenithId, L.SkyZenith);
            m.SetColor(SkyHorizonId, L.SkyHorizon);
            m.SetColor(SkyGroundId, L.SkyGround);
        }

        private static Material ParticleMat(Shader s, string name, bool additive, float shape, float intensity, float softDistance)
        {
            var m = new Material(s) { name = name };
            m.SetColor(TintId, Color.white);
            m.SetFloat(IntensityId, intensity);
            m.SetFloat(ShapeId, shape);
            m.SetFloat(SoftId, softDistance);
            m.SetFloat(SrcBlendId, (float)BlendMode.One);
            m.SetFloat(DstBlendId, additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            return m;
        }

        private static PhysicsMaterial PhysMat(string name, float friction, float bounciness)
        {
            return new PhysicsMaterial(name)
            {
                staticFriction = friction,
                dynamicFriction = friction,
                bounciness = bounciness,
                frictionCombine = PhysicsMaterialCombine.Average,
                bounceCombine = PhysicsMaterialCombine.Average
            };
        }
    }
}
