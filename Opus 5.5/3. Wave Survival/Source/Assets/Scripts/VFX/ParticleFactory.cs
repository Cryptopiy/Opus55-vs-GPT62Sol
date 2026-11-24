using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// Creates the shared world-space particle systems. Effects are emitted with ParticleSystem.Emit(EmitParams), so
    /// every impact/burst reuses the same pre-allocated particle buffers (no GameObjects are instantiated at runtime).
    /// </summary>
    public static class ParticleFactory
    {
        public static ParticleSystem Create(Transform parent, string name, Material mat, bool stretched, float gravity,
            float growTo, bool collide, float drag)
        {
            var go = new GameObject(name);
            go.SetActive(false); // configure before Awake so it never plays the default emitter
            go.transform.SetParent(parent, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = ps.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 1f;
            main.maxParticles = GameConfig.Fx.MaxParticlesPerSystem;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.startLifetime = 1f;
            main.startSpeed = 0f;
            main.startSize = 0.1f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = false;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.5f), new GradientAlphaKey(0f, 1f) });
            col.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, growTo));

            if (drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule lim = ps.limitVelocityOverLifetime;
                lim.enabled = true;
                lim.drag = drag;
                lim.limit = 100f;
            }

            if (collide)
            {
                ParticleSystem.CollisionModule c = ps.collision;
                c.enabled = true;
                c.type = ParticleSystemCollisionType.World;
                c.mode = ParticleSystemCollisionMode.Collision3D;
                c.collidesWith = GameConfig.Layers.EnvironmentMask;
                c.quality = ParticleSystemCollisionQuality.Medium;
                c.bounce = 0.25f;
                c.dampen = 0.5f;
                c.lifetimeLoss = 0.35f;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = mat;
            r.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            if (stretched)
            {
                r.velocityScale = 0.045f;
                r.lengthScale = 1.6f;
            }
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.minParticleSize = 0f;
            r.maxParticleSize = 2f;
            go.SetActive(true);
            return ps;
        }
    }
}
