using UnityEngine;
using Fx = DestructionSandbox.GameConfig.Effects;
using Wpn = DestructionSandbox.GameConfig.Weapons;
using P = DestructionSandbox.GameConfig.Palette;

namespace DestructionSandbox
{
    /// <summary>
    /// Effect API used by gameplay code (D-11 fracture effects, D-16 muzzle, D-18 explosion, D-19 orb).
    /// Particles are emitted into long-lived world-space systems; lights, shockwaves and scorch decals come
    /// from <see cref="TransientEffects"/> pools. Nothing is instantiated after start-up.
    /// </summary>
    public sealed class EffectsManager : MonoBehaviour
    {
        private ParticleSystem stoneDust, woodDust, sparks, splinters, glass, fire, smoke, flash, orbParticles;
        private ParticleSystem[] all;
        private TransientEffects transients;

        public void Init(MaterialLibrary m, MeshLibrary meshes, Transform ground)
        {
            stoneDust = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "StoneDust", Material = m.DustAlpha, MaxParticles = Fx.DustMaxParticles,
                Lifetime = new Vector2(1.6f, 2.8f), Speed = new Vector2(0.4f, 2.2f), Size = new Vector2(0.5f, 1.3f),
                ColorA = P.StoneDust, ColorB = P.StoneDust * 0.82f, Gravity = -0.02f, ShapeRadius = 0.5f,
                RenderMode = ParticleSystemRenderMode.Billboard, SizeGrowth = 2.4f, FadeIn = 0.08f, Drag = 1.4f, Noise = 0.25f
            }, ground);
            woodDust = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "WoodDust", Material = m.DustAlpha, MaxParticles = Fx.DustMaxParticles,
                Lifetime = new Vector2(1.0f, 1.9f), Speed = new Vector2(0.4f, 1.8f), Size = new Vector2(0.35f, 0.9f),
                ColorA = P.WoodDust, ColorB = P.WoodDust * 0.8f, Gravity = -0.01f, ShapeRadius = 0.4f,
                RenderMode = ParticleSystemRenderMode.Billboard, SizeGrowth = 2.0f, FadeIn = 0.08f, Drag = 1.6f, Noise = 0.2f
            }, ground);
            sparks = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "Sparks", Material = m.SparkAdditive, MaxParticles = Fx.SparkMaxParticles,
                Lifetime = new Vector2(0.25f, 0.75f), Speed = new Vector2(4f, 11f), Size = new Vector2(0.03f, 0.07f),
                ColorA = P.Spark, ColorB = new Color(1f, 0.95f, 0.7f), Gravity = 1f, ShapeRadius = 0.2f,
                RenderMode = ParticleSystemRenderMode.Stretch, StretchVelocityScale = 0.045f, SizeGrowth = 0.4f, FadeIn = 0.01f,
                CollideWithGround = true, Bounce = 0.35f, MaxScreenSize = 0.12f
            }, ground);
            splinters = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "Splinters", Material = m.SplinterSolid, MaxParticles = Fx.SplinterMaxParticles,
                Lifetime = new Vector2(1.2f, 2.4f), Speed = new Vector2(2f, 6.5f), Size = new Vector2(0.05f, 0.14f),
                ColorA = P.Splinter, ColorB = P.Splinter * 0.7f, Gravity = 1f, ShapeRadius = 0.35f,
                RenderMode = ParticleSystemRenderMode.Mesh, Mesh = meshes.UnitCube, SizeGrowth = 1f, FadeIn = 0.01f,
                CollideWithGround = true, Bounce = 0.2f, Rotate3D = true, AngularSpeed = 540f
            }, ground);
            glass = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "GlassFragments", Material = m.GlassGlint, MaxParticles = Fx.GlassMaxParticles,
                Lifetime = new Vector2(0.7f, 1.6f), Speed = new Vector2(1.5f, 7f), Size = new Vector2(0.04f, 0.11f),
                ColorA = P.GlassGlint, ColorB = new Color(0.6f, 0.85f, 1f, 1f), Gravity = 1f, ShapeRadius = 0.4f,
                RenderMode = ParticleSystemRenderMode.Billboard, SizeGrowth = 1f, Twinkle = true,
                CollideWithGround = true, Bounce = 0.3f, AngularSpeed = 720f
            }, ground);
            fire = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "Fire", Material = m.FireAdditive, MaxParticles = Fx.FireMaxParticles,
                Lifetime = new Vector2(0.35f, 0.85f), Speed = new Vector2(2f, 8f), Size = new Vector2(1.0f, 2.4f),
                ColorA = P.Fire, ColorB = new Color(1f, 0.85f, 0.4f), Gravity = -0.25f, ShapeRadius = 0.8f,
                RenderMode = ParticleSystemRenderMode.Billboard, SizeGrowth = 1.8f, FadeIn = 0.02f, Drag = 2.5f, Noise = 0.5f
            }, ground);
            smoke = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "Smoke", Material = m.SmokeAlpha, MaxParticles = Fx.SmokeMaxParticles,
                Lifetime = new Vector2(2.5f, 4.5f), Speed = new Vector2(1f, 4.5f), Size = new Vector2(1.4f, 3.0f),
                ColorA = P.Smoke, ColorB = P.Smoke * 1.4f, Gravity = -0.06f, ShapeRadius = 1.0f,
                RenderMode = ParticleSystemRenderMode.Billboard, SizeGrowth = 2.6f, FadeIn = 0.1f, Drag = 1.2f, Noise = 0.35f, MaxScreenSize = 0.8f
            }, ground);
            flash = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "Flash", Material = m.FlashAdditive, MaxParticles = Fx.MuzzleMaxParticles,
                Lifetime = new Vector2(0.05f, 0.09f), Speed = new Vector2(0f, 0.2f), Size = new Vector2(0.5f, 0.85f),
                ColorA = P.MuzzleFlash, ColorB = new Color(1f, 0.95f, 0.8f), Gravity = 0f, ShapeRadius = 0.05f,
                RenderMode = ParticleSystemRenderMode.Billboard, SizeGrowth = 1.6f, FadeIn = 0.01f, MaxScreenSize = 0.35f
            }, ground);
            orbParticles = ParticleFactory.Create(transform, new ParticleFactory.Spec
            {
                Name = "OrbParticles", Material = m.OrbParticles, MaxParticles = Fx.OrbMaxParticles,
                Lifetime = new Vector2(0.6f, 1.0f), Speed = new Vector2(0f, 0f), Size = new Vector2(0.12f, 0.3f),
                ColorA = P.OrbParticle, ColorB = new Color(0.45f, 0.7f, 1f, 1f), Gravity = 0f, ShapeRadius = 0.1f,
                RenderMode = ParticleSystemRenderMode.Billboard, SizeGrowth = 0.5f, FadeIn = 0.2f
            }, ground);

            all = new[] { stoneDust, woodDust, sparks, splinters, glass, fire, smoke, flash, orbParticles };
            transients = gameObject.AddComponent<TransientEffects>();
            transients.Init(m, meshes);
        }

        /// <summary>Total live particles across all systems (debug overlay).</summary>
        public int ParticleCount()
        {
            int n = 0;
            for (int i = 0; i < all.Length; i++) n += all[i].particleCount;
            return n;
        }

        public int ActiveScorchDecals() => transients.ActiveScorches();

        public void ClearAll()
        {
            for (int i = 0; i < all.Length; i++) all[i].Clear();
            transients.ClearAll();
        }

        private static void Burst(ParticleSystem ps, Vector3 position, int count, float radius, float speedScale, float sizeScale)
        {
            if (count <= 0) return;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.radius = Mathf.Max(0.01f, radius);
            ParticleSystem.MainModule main = ps.main;
            main.startSpeedMultiplier = speedScale;
            main.startSizeMultiplier = sizeScale;
            var ep = new ParticleSystem.EmitParams { position = position, applyShapeToPosition = true };
            ps.Emit(ep, count);
        }

        /// <summary>
        /// D-11 material-specific break effect. glass: many sparkling fragments; wood: splinters + dust puff;
        /// stone: grey dust cloud + sparks. 'size' is the broken piece's largest dimension.
        /// </summary>
        public void FractureBurst(BlockMaterialType material, Vector3 position, float size, float intensity)
        {
            float s = Mathf.Clamp(size, 0.1f, 1.2f);
            float k = Mathf.Clamp01(intensity) * s;
            switch (material)
            {
                case BlockMaterialType.Glass:
                    Burst(glass, position, Mathf.CeilToInt(26f * k) + 2, s * 0.5f, 1f, 0.8f);
                    Burst(sparks, position, Mathf.CeilToInt(6f * k), s * 0.4f, 0.6f, 0.8f);
                    break;
                case BlockMaterialType.Wood:
                    Burst(splinters, position, Mathf.CeilToInt(26f * k), s * 0.45f, 1f, 1f);
                    Burst(woodDust, position, Mathf.CeilToInt(9f * k) + 1, s * 0.4f, 1f, Mathf.Lerp(0.6f, 1.2f, s));
                    break;
                default:
                    Burst(stoneDust, position, Mathf.CeilToInt(12f * k) + 1, s * 0.5f, 1f, Mathf.Lerp(0.6f, 1.4f, s));
                    Burst(sparks, position, Mathf.CeilToInt(18f * k) + 2, s * 0.4f, 1f, 1f);
                    break;
            }
        }

        public void ImpactDust(Vector3 point, Vector3 normal)
        {
            Burst(stoneDust, point + normal * 0.2f, 5, 0.3f, 0.8f, 0.7f);
            Burst(sparks, point, 8, 0.15f, 0.8f, 0.8f);
        }

        /// <summary>Muzzle flash sprites, sparks and a little smoke, all emitted forward (never toward the camera).</summary>
        public void MuzzleFlash(Vector3 position, Vector3 forward)
        {
            for (int i = 0; i < 2; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = position + forward * (0.18f + 0.12f * i),
                    velocity = forward * 2f,
                    startSize = Random.Range(0.22f, 0.36f),
                    startLifetime = Random.Range(0.04f, 0.07f),
                    applyShapeToPosition = false
                };
                flash.Emit(ep, 1);
            }
            for (int i = 0; i < 7; i++)
            {
                Vector3 dir = (forward + Random.insideUnitSphere * 0.35f).normalized;
                var ep = new ParticleSystem.EmitParams
                {
                    position = position + forward * 0.15f,
                    velocity = dir * Random.Range(6f, 13f),
                    startSize = Random.Range(0.025f, 0.045f),
                    startLifetime = Random.Range(0.12f, 0.3f),
                    applyShapeToPosition = false
                };
                sparks.Emit(ep, 1);
            }
            for (int i = 0; i < 3; i++)
            {
                var ep = new ParticleSystem.EmitParams
                {
                    position = position + forward * (0.45f + 0.2f * i),
                    velocity = forward * Random.Range(1.2f, 2.5f) + Vector3.up * 0.3f,
                    startSize = Random.Range(0.25f, 0.45f),
                    startLifetime = Random.Range(0.9f, 1.4f),
                    applyShapeToPosition = false
                };
                smoke.Emit(ep, 1);
            }
        }

        /// <summary>D-18 explosion visuals: flash light, shockwave, fire, smoke, sparks, scorch decal.</summary>
        public void Explosion(Vector3 center)
        {
            transients.Flash(center, P.ExplosionLight, Wpn.ExplosionFlashIntensity, Wpn.ExplosionFlashRange, Wpn.ExplosionFlashDuration);
            transients.Shockwave(center, Wpn.ExplosionRadius, Wpn.ShockwaveDuration, new Color(2.5f, 1.7f, 1.0f));
            Burst(flash, center, 4, 0.4f, 0f, 4.5f);
            Burst(fire, center, 46, 0.9f, 1f, 1f);
            Burst(smoke, center, 26, 1.2f, 1f, 1f);
            Burst(sparks, center, 90, 0.5f, 1.6f, 1.2f);
            Burst(stoneDust, center, 10, 1.5f, 1.5f, 1.6f);

            // Ground scorch under the blast (fainter and smaller the higher the explosion was).
            if (Physics.Raycast(center + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, Wpn.ScorchMaxHeight + 0.5f,
                    GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore))
            {
                float height = Mathf.Max(0f, center.y - hit.point.y);
                float strength = 1f - Mathf.Clamp01(height / Wpn.ScorchMaxHeight) * 0.6f;
                transients.Scorch(hit.point, Wpn.ScorchSize * Mathf.Lerp(0.6f, 1f, strength), strength);
            }
        }

        public void OrbSuck(Vector3 center, int count)
        {
            float radius = Wpn.OrbFieldRadius;
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                float r = radius * Random.Range(0.55f, 1f);
                float life = Random.Range(0.6f, 1.0f);
                var ep = new ParticleSystem.EmitParams
                {
                    position = center + dir * r,
                    velocity = -dir * (r / life),
                    startLifetime = life,
                    startSize = Random.Range(0.12f, 0.3f),
                    applyShapeToPosition = false
                };
                orbParticles.Emit(ep, 1);
            }
        }

        public void OrbPulse(Vector3 center)
        {
            transients.Flash(center, P.OrbLight, Wpn.ExplosionFlashIntensity * 0.6f, Wpn.OrbFieldRadius * 1.5f, Wpn.ExplosionFlashDuration * 1.5f);
            transients.Shockwave(center, Wpn.OrbFieldRadius, Wpn.ShockwaveDuration, new Color(1.2f, 0.8f, 2.6f));
            for (int i = 0; i < 70; i++)
            {
                Vector3 dir = Random.onUnitSphere;
                var ep = new ParticleSystem.EmitParams
                {
                    position = center + dir * 0.3f,
                    velocity = dir * Random.Range(8f, 16f),
                    startLifetime = Random.Range(0.4f, 0.8f),
                    startSize = Random.Range(0.12f, 0.3f),
                    applyShapeToPosition = false
                };
                orbParticles.Emit(ep, 1);
            }
        }
    }
}
