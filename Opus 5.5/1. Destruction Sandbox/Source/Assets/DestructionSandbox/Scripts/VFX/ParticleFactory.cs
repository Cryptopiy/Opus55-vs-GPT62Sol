using UnityEngine;
using UnityEngine.Rendering;

namespace DestructionSandbox
{
    /// <summary>
    /// Builds the world-space particle systems used as particle "pools": each system never emits by itself,
    /// effects call Emit() into it, so a burst costs no allocation and maxParticles is a hard cap.
    /// </summary>
    public static class ParticleFactory
    {
        public struct Spec
        {
            public string Name;
            public Material Material;
            public int MaxParticles;
            public Vector2 Lifetime;
            public Vector2 Speed;
            public Vector2 Size;
            public Color ColorA;
            public Color ColorB;
            public float Gravity;
            public float ShapeRadius;
            public ParticleSystemRenderMode RenderMode;
            public Mesh Mesh;
            public float StretchVelocityScale;
            public float SizeGrowth;          // size multiplier reached at the end of life (1 = constant)
            public float FadeIn;              // fraction of life used to fade in
            public float Drag;
            public float Noise;
            public bool CollideWithGround;
            public float Bounce;
            public bool Rotate3D;
            public float AngularSpeed;        // degrees per second (random +-)
            public bool Twinkle;
            public float MaxScreenSize;       // largest on-screen size (fraction of the view); 0 = default 2
        }

        public static ParticleSystem Create(Transform parent, Spec s, Transform ground)
        {
            var go = new GameObject(s.Name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.maxParticles = s.MaxParticles;
            main.startLifetime = new ParticleSystem.MinMaxCurve(s.Lifetime.x, s.Lifetime.y);
            main.startSpeed = new ParticleSystem.MinMaxCurve(s.Speed.x, s.Speed.y);
            main.startSize = new ParticleSystem.MinMaxCurve(s.Size.x, s.Size.y);
            main.startColor = new ParticleSystem.MinMaxGradient(s.ColorA, s.ColorB);
            main.gravityModifier = s.Gravity;
            if (s.Rotate3D)
            {
                main.startRotation3D = true;
                main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }
            else
            {
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            }

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = Mathf.Max(0.01f, s.ShapeRadius);
            shape.radiusThickness = 1f;

            ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
            col.enabled = true;
            var gradient = new Gradient();
            float fadeIn = Mathf.Clamp(s.FadeIn, 0.001f, 0.5f);
            if (s.Twinkle)
            {
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.25f, 0.15f), new GradientAlphaKey(1f, 0.3f),
                            new GradientAlphaKey(0.3f, 0.5f), new GradientAlphaKey(0.9f, 0.7f), new GradientAlphaKey(0f, 1f) });
            }
            else
            {
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, fadeIn), new GradientAlphaKey(0.7f, 0.6f), new GradientAlphaKey(0f, 1f) });
            }
            col.color = new ParticleSystem.MinMaxGradient(gradient);

            if (!Mathf.Approximately(s.SizeGrowth, 1f))
            {
                ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var curve = new AnimationCurve(new Keyframe(0f, 1f / Mathf.Max(1f, s.SizeGrowth)), new Keyframe(1f, 1f));
                if (s.SizeGrowth < 1f) curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, s.SizeGrowth));
                sol.size = new ParticleSystem.MinMaxCurve(1f, curve);
            }

            if (s.Drag > 0f)
            {
                ParticleSystem.LimitVelocityOverLifetimeModule lv = ps.limitVelocityOverLifetime;
                lv.enabled = true;
                lv.limit = 1000f;
                lv.drag = s.Drag;
                lv.multiplyDragByParticleSize = false;
                lv.multiplyDragByParticleVelocity = false;
            }

            if (s.Noise > 0f)
            {
                ParticleSystem.NoiseModule noise = ps.noise;
                noise.enabled = true;
                noise.strength = s.Noise;
                noise.frequency = 0.6f;
                noise.scrollSpeed = 0.4f;
                noise.quality = ParticleSystemNoiseQuality.Low;
            }

            if (s.AngularSpeed > 0f)
            {
                ParticleSystem.RotationOverLifetimeModule rol = ps.rotationOverLifetime;
                rol.enabled = true;
                float w = s.AngularSpeed * Mathf.Deg2Rad;
                if (s.Rotate3D)
                {
                    rol.separateAxes = true;
                    rol.x = new ParticleSystem.MinMaxCurve(-w, w);
                    rol.y = new ParticleSystem.MinMaxCurve(-w, w);
                    rol.z = new ParticleSystem.MinMaxCurve(-w, w);
                }
                else rol.z = new ParticleSystem.MinMaxCurve(-w, w);
            }

            if (s.CollideWithGround && ground != null)
            {
                ParticleSystem.CollisionModule collision = ps.collision;
                collision.enabled = true;
                collision.type = ParticleSystemCollisionType.Planes;
                collision.SetPlane(0, ground);
                collision.bounce = s.Bounce;
                collision.dampen = 0.45f;
                collision.lifetimeLoss = 0f;
                collision.radiusScale = 0.5f;
                collision.enableDynamicColliders = false;
            }

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = s.Material;
            r.renderMode = s.RenderMode;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.maxParticleSize = s.MaxScreenSize > 0f ? s.MaxScreenSize : 2f;
            if (s.RenderMode == ParticleSystemRenderMode.Stretch)
            {
                r.velocityScale = s.StretchVelocityScale;
                r.lengthScale = 1f;
            }
            if (s.RenderMode == ParticleSystemRenderMode.Mesh && s.Mesh != null)
            {
                r.mesh = s.Mesh;
                r.alignment = ParticleSystemRenderSpace.World;
            }

            ps.Play();
            return ps;
        }
    }
}
