using UnityEngine;
using UnityEngine.Rendering;

namespace LivingIsland
{
    /// <summary>Helpers that configure Unity ParticleSystems from code (pooled internally by Unity).</summary>
    public static class ParticleFactory
    {
        public static ParticleSystem Create(string name, Transform parent, Material material, int maxParticles,
                                            ParticleSystemSimulationSpace space = ParticleSystemSimulationSpace.World)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.maxParticles = maxParticles;
            main.simulationSpace = space;
            main.playOnAwake = false;
            main.loop = true;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            var shape = ps.shape;
            shape.enabled = false;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            r.sortMode = ParticleSystemSortMode.Distance;
            r.lightProbeUsage = LightProbeUsage.Off;
            r.reflectionProbeUsage = ReflectionProbeUsage.Off;
            r.minParticleSize = 0f;
            r.maxParticleSize = 4f;
            return ps;
        }

        public static Gradient Gradient(params (float t, Color c)[] keys)
        {
            var g = new Gradient();
            var ck = new GradientColorKey[keys.Length];
            var ak = new GradientAlphaKey[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                ck[i] = new GradientColorKey(keys[i].c, keys[i].t);
                ak[i] = new GradientAlphaKey(keys[i].c.a, keys[i].t);
            }
            g.SetKeys(ck, ak);
            return g;
        }

        public static AnimationCurve Curve(params (float t, float v)[] keys)
        {
            var frames = new Keyframe[keys.Length];
            for (int i = 0; i < keys.Length; i++) frames[i] = new Keyframe(keys[i].t, keys[i].v);
            var curve = new AnimationCurve(frames);
            for (int i = 0; i < frames.Length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }
    }
}
