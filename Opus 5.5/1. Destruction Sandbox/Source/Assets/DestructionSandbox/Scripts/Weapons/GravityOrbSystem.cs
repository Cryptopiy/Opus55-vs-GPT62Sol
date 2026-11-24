using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Wpn = DestructionSandbox.GameConfig.Weapons;
using Layers = DestructionSandbox.GameConfig.Layers;

namespace DestructionSandbox
{
    /// <summary>
    /// D-19 stationary gravity orbs. For 4 s every rigidbody within 8 m is pulled toward the centre with
    /// F = 60 x mass (applied as a 60 m/s^2 acceleration). The pull is clamped twice: it ramps to zero inside a
    /// small core (no violent overshoot through the centre) and stops once a body already falls inward faster
    /// than OrbMaxPullSpeed; inside a small radius the debris is also damped so it gathers into a clump.
    /// Then a radial pulse of 600 N*s with linear falloff to 8 m throws everything out.
    /// </summary>
    public sealed class GravityOrbSystem : MonoBehaviour
    {
        private sealed class Orb
        {
            public bool Active;
            public Vector3 Center;
            public float Elapsed;
            public Transform Visual;
            public Material Material;
            public Light Light;
            public int HumVoice = -1;
            public float SuckAccumulator;
            public int Order;
        }

        private Orb[] orbs;
        private readonly Collider[] hits = new Collider[2048];
        private int order;
        private static readonly int ActivityId = Shader.PropertyToID("_Activity");

        public int ActiveCount { get; private set; }
        public int PulsesFired { get; private set; }
        public Vector3 LastCenter { get; private set; }
        public int LastPulledBodies { get; private set; }

        public void Init(MaterialLibrary materials, MeshLibrary meshes)
        {
            orbs = new Orb[Wpn.OrbPool];
            for (int i = 0; i < orbs.Length; i++)
            {
                var go = new GameObject("GravityOrb" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes.Sphere;
                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(materials.OrbShader) { name = "GravityOrb" + i };
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;

                var lightGo = new GameObject("OrbLight");
                lightGo.transform.SetParent(go.transform, false);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = GameConfig.Palette.OrbLight;
                light.range = Wpn.OrbFieldRadius;
                light.intensity = 0f;
                light.shadows = LightShadows.None;
                lightGo.AddComponent<UniversalAdditionalLightData>();

                go.SetActive(false);
                orbs[i] = new Orb { Visual = go.transform, Material = mat, Light = light };
            }
        }

        public void Activate(Vector3 center)
        {
            Orb orb = null;
            foreach (Orb o in orbs) if (!o.Active) { orb = o; break; }
            if (orb == null)
            {
                orb = orbs[0];
                foreach (Orb o in orbs) if (o.Order < orb.Order) orb = o;
                Deactivate(orb);
            }
            orb.Active = true;
            orb.Center = center;
            LastCenter = center;
            orb.Elapsed = 0f;
            orb.SuckAccumulator = 0f;
            orb.Order = ++order;
            orb.Visual.position = center;
            orb.Visual.localScale = Vector3.one * (Wpn.OrbRadius * 2f);
            orb.Visual.gameObject.SetActive(true);
            orb.HumVoice = Game.Audio.PlayOrbHum(center);
            ActiveCount++;
        }

        public void ClearAll()
        {
            if (orbs == null) return;
            foreach (Orb o in orbs) if (o.Active) Deactivate(o);
        }

        private void Deactivate(Orb orb)
        {
            orb.Active = false;
            orb.Visual.gameObject.SetActive(false);
            Game.Audio.StopVoice(orb.HumVoice);
            orb.HumVoice = -1;
            ActiveCount--;
        }

        private void FixedUpdate()
        {
            if (orbs == null) return;
            float dt = Time.fixedDeltaTime;
            foreach (Orb orb in orbs)
            {
                if (!orb.Active) continue;
                Pull(orb.Center);
                orb.Elapsed += dt;
                if (orb.Elapsed >= Wpn.OrbActiveTime)
                {
                    Pulse(orb.Center);
                    Deactivate(orb);
                }
            }
        }

        private void Pull(Vector3 center)
        {
            int n = Physics.OverlapSphereNonAlloc(center, Wpn.OrbFieldRadius, hits, Layers.DynamicMask, QueryTriggerInteraction.Ignore);
            int pulled = 0;
            for (int i = 0; i < n; i++)
            {
                Rigidbody rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                Vector3 to = center - rb.worldCenterOfMass;
                float dist = to.magnitude;
                if (dist < 1e-3f) continue;
                Vector3 dir = to / dist;
                Vector3 velocity = rb.linearVelocity;
                // Near the centre the debris is calmed so it gathers into a clump instead of oscillating through it.
                if (dist < Wpn.OrbDampingRadius) rb.AddForce(-velocity * Wpn.OrbDamping, ForceMode.Acceleration);
                if (Vector3.Dot(velocity, dir) >= Wpn.OrbMaxPullSpeed) continue;            // speed clamp
                float ramp = Mathf.Clamp01(dist / Wpn.OrbCoreRadius);                         // core clamp
                rb.AddForce(dir * (Wpn.OrbPullAcceleration * ramp), ForceMode.Acceleration); // = 60 * mass N
                pulled++;
            }
            LastPulledBodies = pulled;
        }

        private void Pulse(Vector3 center)
        {
            int n = Physics.OverlapSphereNonAlloc(center, Wpn.OrbFieldRadius, hits, Layers.DynamicMask, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Rigidbody rb = hits[i].attachedRigidbody;
                if (rb == null || rb.isKinematic) continue;
                RadialImpulse.Apply(rb, center, Wpn.OrbPulseImpulse, Wpn.OrbFieldRadius, 0f, GameConfig.Physics.MaxLinearVelocity);
            }
            PulsesFired++;
            Game.Effects.OrbPulse(center);
            Game.Audio.PlayOrbImplosion(center);
            Game.Shake.ShakeFromExplosion(center, Wpn.ExplosionShakeTrauma * 0.5f);
        }

        private void Update()
        {
            if (orbs == null) return;
            float dt = Time.deltaTime;
            foreach (Orb orb in orbs)
            {
                if (!orb.Active) continue;
                float t = orb.Elapsed / Wpn.OrbActiveTime;
                // Grows quickly, breathes while active and collapses just before the pulse.
                float grow = Mathf.Clamp01(orb.Elapsed / 0.25f);
                float collapse = t > 0.9f ? Mathf.Clamp01((1f - t) / 0.1f) : 1f;
                float breathe = 1f + 0.08f * Mathf.Sin(Time.time * 12f);
                float diameter = Mathf.Lerp(Wpn.OrbRadius * 2f, Wpn.OrbVisualDiameter, grow) * breathe * Mathf.Lerp(0.35f, 1f, collapse);
                orb.Visual.localScale = Vector3.one * diameter;
                orb.Material.SetFloat(ActivityId, grow);
                orb.Light.intensity = 6f * grow * collapse * breathe;

                orb.SuckAccumulator += GameConfig.Effects.OrbSuckParticlesPerSecond * dt;
                int count = Mathf.FloorToInt(orb.SuckAccumulator);
                if (count > 0)
                {
                    orb.SuckAccumulator -= count;
                    Game.Effects.OrbSuck(orb.Center, count);
                }
            }
        }
    }
}
