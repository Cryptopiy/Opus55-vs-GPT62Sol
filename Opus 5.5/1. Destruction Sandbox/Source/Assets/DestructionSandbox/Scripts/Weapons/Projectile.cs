using UnityEngine;
using Wpn = DestructionSandbox.GameConfig.Weapons;

namespace DestructionSandbox
{
    public enum WeaponType { Cannonball = 0, Shell = 1, Orb = 2 }

    /// <summary>
    /// Pooled projectile. All three fly straight (no gravity) so they land exactly under the crosshair;
    /// a cannonball gets gravity after its first impact and then rolls like a real ball.
    /// Continuous dynamic collision detection is used only here (D-27).
    /// </summary>
    public sealed class Projectile : MonoBehaviour
    {
        public WeaponType Type { get; private set; }
        public Rigidbody Body { get; private set; }
        public bool InUse { get; private set; }
        public Vector3 PreStepVelocity { get; private set; }
        public float Age { get; private set; }
        public bool HasImpacted { get; private set; }
        public int PassStamp { get; set; } = -1;
        public Vector3 PassVelocity { get; set; }

        private SphereCollider sphere;
        private TrailRenderer trail;

        public Collider Collider => sphere;

        public static Projectile Create(Transform parent, WeaponType type, int index, MaterialLibrary materials, MeshLibrary meshes)
        {
            var go = new GameObject(type + "_" + index);
            go.SetActive(false);
            go.layer = GameConfig.Layers.Projectile;
            go.transform.SetParent(parent, false);
            var p = go.AddComponent<Projectile>();
            p.Type = type;

            float radius = type == WeaponType.Cannonball ? Wpn.CannonballRadius : type == WeaponType.Shell ? Wpn.ShellRadius : Wpn.OrbRadius;
            float mass = type == WeaponType.Cannonball ? Wpn.CannonballMass : type == WeaponType.Shell ? Wpn.ShellMass : Wpn.OrbMass;
            go.transform.localScale = Vector3.one * (radius * 2f);
            go.AddComponent<MeshFilter>().sharedMesh = meshes.Sphere;
            var mr = go.AddComponent<MeshRenderer>();
            if (type == WeaponType.Orb)
            {
                mr.sharedMaterial = materials.OrbFlying;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            else mr.sharedMaterial = type == WeaponType.Cannonball ? materials.Metal : materials.ShellPaint;

            p.sphere = go.AddComponent<SphereCollider>();
            p.sphere.radius = 0.5f;   // unit sphere scaled by the transform
            p.sphere.sharedMaterial = materials.ProjectilePhysics;
            p.Body = go.AddComponent<Rigidbody>();
            p.Body.mass = mass;
            p.Body.interpolation = RigidbodyInterpolation.Interpolate;
            p.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            p.Body.maxLinearVelocity = GameConfig.Physics.MaxLinearVelocity;
            p.Body.linearDamping = 0f;
            p.Body.angularDamping = 0.2f;

            p.trail = go.AddComponent<TrailRenderer>();
            p.trail.sharedMaterial = materials.TrailAdditive;
            p.trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            p.trail.minVertexDistance = 0.25f;
            p.trail.numCapVertices = 2;
            Color tc = type == WeaponType.Shell ? GameConfig.Palette.ShellTrailColor
                : type == WeaponType.Orb ? GameConfig.Palette.OrbParticle : GameConfig.Palette.TrailColor;
            p.trail.time = type == WeaponType.Cannonball ? 0.35f : 0.25f;
            p.trail.widthMultiplier = radius * 1.6f;
            var widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            p.trail.widthCurve = widthCurve;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(tc, 0f), new GradientColorKey(tc, 1f) },
                new[] { new GradientAlphaKey(tc.a, 0f), new GradientAlphaKey(0f, 1f) });
            p.trail.colorGradient = gradient;
            p.trail.emitting = false;
            return p;
        }

        public void Launch(Vector3 position, Vector3 velocity)
        {
            transform.SetPositionAndRotation(position, Quaternion.LookRotation(velocity));
            Age = 0f;
            HasImpacted = false;
            PassStamp = -1;
            InUse = true;
            Body.useGravity = false;
            Body.isKinematic = false;
            gameObject.SetActive(true);
            Body.maxLinearVelocity = GameConfig.Physics.MaxLinearVelocity;
            Body.linearVelocity = velocity;
            Body.angularVelocity = Vector3.zero;
            PreStepVelocity = velocity;
            trail.Clear();
            trail.emitting = true;
        }

        public void Deactivate()
        {
            InUse = false;
            trail.emitting = false;
            trail.Clear();
            gameObject.SetActive(false);
        }

        private void FixedUpdate()
        {
            if (!InUse) return;
            PreStepVelocity = Body.linearVelocity;
            Age += Time.fixedDeltaTime;
            switch (Type)
            {
                case WeaponType.Shell:
                    if (Age >= Wpn.ShellFuse) Detonate(Body.position);
                    break;
                case WeaponType.Cannonball:
                    if (Age >= Wpn.CannonballLifetime) Game.Projectiles.Recycle(this);
                    break;
                case WeaponType.Orb:
                    if (Age >= Wpn.OrbMaxFlightTime) Game.Projectiles.Recycle(this);
                    break;
            }
        }

        private void Detonate(Vector3 at)
        {
            Game.Explosions.Queue(at);
            Game.Projectiles.Recycle(this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!InUse) return;
            ContactPoint contact = collision.GetContact(0);
            switch (Type)
            {
                case WeaponType.Shell:
                    Detonate(Body.position);            // D-18: explodes on first contact
                    break;
                case WeaponType.Orb:
                    Game.Orbs.Activate(Body.position);  // D-19: becomes a stationary orb where it touched
                    Game.Projectiles.Recycle(this);
                    break;
                default:
                    if (!HasImpacted)
                    {
                        HasImpacted = true;
                        Body.useGravity = true;
                        float speed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
                        if (speed >= Wpn.CannonballHeavyImpactSpeed)
                        {
                            Game.Audio.PlayHeavyImpact(contact.point, speed / Wpn.CannonballSpeed);
                            Game.Effects.ImpactDust(contact.point, contact.normal);
                        }
                    }
                    break;
            }
        }
    }
}
