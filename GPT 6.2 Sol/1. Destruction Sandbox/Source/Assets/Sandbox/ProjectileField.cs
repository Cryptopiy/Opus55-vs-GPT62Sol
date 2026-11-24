using UnityEngine;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal sealed class ProjectileField
    {
        readonly Sandbox game;
        readonly ProjectileBody[] shots = new ProjectileBody[ProjectileCapacity];
        readonly Material[] colors;
        readonly float[] ready = new float[Weapons.Length];
        int shellAmmo;
        public Weapon Selected { get; private set; }
        public int Ammo => Selected == Weapon.Shell ? shellAmmo : Weapons[(int)Selected].Ammo;
        public float Remaining => Mathf.Max(0, ready[(int)Selected] - Time.time);
        public float Charge => 1 - Mathf.Clamp01(Remaining / Weapons[(int)Selected].Cooldown);
        public int Detonations { get; private set; }
        public int Anchors { get; private set; }
        public int Releases { get; private set; }
        public int Fired { get; private set; }

        public ProjectileField(Sandbox game)
        {
            this.game = game;
            colors = new[] {
                game.Paint("Forged steel", new Color(.15f, .19f, .21f), .66f),
                game.Paint("Shell copper", new Color(1, .43f, .15f), .55f),
                game.EffectMaterial("Orb energy", 2, new Color(1.35f, .59f, 3.28f))
            };
            Material trailMaterial = game.EffectMaterial("Shot trail", 0, new Color(1.75f, 1.32f, .84f));
            for (int i = 0; i < shots.Length; i++)
            {
                GameObject go = game.Shape("Projectile", game.transform, Vector3.zero, Vector3.one, colors[0], PrimitiveType.Sphere);
                go.layer = ShotLayer;
                go.SetActive(false);
                var shot = go.AddComponent<ProjectileBody>();
                shot.Field = this;
                shot.Mesh = go.GetComponent<MeshRenderer>();
                shot.Collider = go.AddComponent<SphereCollider>();
                shot.Trail = go.AddComponent<TrailRenderer>();
                shot.Trail.sharedMaterial = trailMaterial;
                shot.Trail.time = .25f;
                shot.Trail.minVertexDistance = .16f;
                shot.Trail.startWidth = .15f;
                shot.Trail.endWidth = .015f;
                shot.Trail.startColor = new Color(1, .88f, .57f);
                shot.Trail.endColor = new Color(1, .45f, .13f, 0);
                shot.Body = go.AddComponent<Rigidbody>();
                shot.Body.maxLinearVelocity = TopSpeed;
                shot.Body.interpolation = RigidbodyInterpolation.Interpolate;
                shots[i] = shot;
            }
            Reset();
        }

        public int Active
        {
            get
            {
                int count = 0;
                foreach (var shot in shots) if (shot.isActiveAndEnabled) count++;
                return count;
            }
        }

        public void Select(Weapon weapon)
        {
            if (weapon == Selected) return;
            Selected = weapon;
            game.SelectionChanged();
        }

        public bool Fire()
        {
            int kind = (int)Selected;
            if (Time.time < ready[kind] || Ammo == 0) return false;
            foreach (var shot in shots)
            {
                if (shot.gameObject.activeSelf) continue;
                var spec = Weapons[kind];
                var camera = game.Player.Camera.transform;
                shot.transform.SetPositionAndRotation(camera.position + camera.forward * (spec.Radius + .52f), camera.rotation);
                shot.transform.localScale = Vector3.one * (spec.Radius * 2);
                shot.Mesh.sharedMaterial = colors[kind];
                shot.Mesh.enabled = true;
                shot.Collider.enabled = true;
                shot.Kind = Selected;
                shot.Anchored = false;
                shot.Spent = false;
                shot.Life = Selected == Weapon.Shell ? ShellFuse : ProjectileLife;
                shot.gameObject.SetActive(true);
                shot.Trail.Clear();
                shot.Trail.emitting = Selected == Weapon.Cannon;
                shot.Trail.enabled = Selected == Weapon.Cannon;
                shot.Body.isKinematic = false;
                shot.Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                shot.Body.useGravity = Selected == Weapon.Cannon;
                shot.Body.mass = spec.Mass;
                shot.Body.linearVelocity = camera.forward * spec.Speed;
                shot.Body.angularVelocity = Vector3.zero;
                ready[kind] = Time.time + spec.Cooldown;
                if (Selected == Weapon.Shell) shellAmmo--;
                Fired++;
                game.ShotFired(Selected);
                return true;
            }
            return false;
        }

        public void Contact(ProjectileBody shot, Vector3 point, float impulse)
        {
            switch (shot.Kind)
            {
                case Weapon.Shell:
                    Detonate(shot, point);
                    break;
                case Weapon.Orb:
                    shot.Anchored = true;
                    shot.Life = OrbTime;
                    shot.Body.linearVelocity = Vector3.zero;
                    shot.Body.angularVelocity = Vector3.zero;
                    shot.Body.collisionDetectionMode = CollisionDetectionMode.Discrete;
                    shot.Body.isKinematic = true;
                    shot.Collider.enabled = false;
                    shot.Mesh.enabled = false;
                    shot.Body.position = point;
                    Anchors++;
                    game.OrbStarted(point);
                    break;
                default:
                    shot.Spent = true;
                    game.CannonImpact(point, impulse);
                    break;
            }
        }

        void Detonate(ProjectileBody shot, Vector3 point)
        {
            shot.gameObject.SetActive(false);
            Detonations++;
            game.Detonate(point);
        }

        public void Step(float dt)
        {
            foreach (var shot in shots)
            {
                if (!shot.isActiveAndEnabled) continue;
                shot.Life -= dt;
                if (shot.Anchored && shot.Life > 0)
                    game.OrbPull(shot.Body.position);
                if (shot.Life > 0) continue;
                if (shot.Kind == Weapon.Shell)
                    Detonate(shot, shot.Body.position);
                else
                {
                    shot.gameObject.SetActive(false);
                    if (shot.Anchored)
                    {
                        Releases++;
                        game.OrbReleased(shot.Body.position);
                    }
                }
            }
        }

        public void AffectShots(Vector3 center, float radius, float strength, float lift, bool pull)
        {
            foreach (var shot in shots)
            {
                if (!shot.isActiveAndEnabled || shot.Anchored) continue;
                Rigidbody body = shot.Body;
                Vector3 away = body.position - center;
                float distance = away.magnitude;
                if (distance >= radius) continue;
                if (pull)
                    body.AddForce(-away.normalized * Mathf.Min(strength * body.mass, OrbMaxForce));
                else
                    body.AddForce((away + Vector3.up * lift).normalized * (strength * (1 - distance / radius)), ForceMode.Impulse);
            }
        }

        public void Reset()
        {
            foreach (var shot in shots) shot.gameObject.SetActive(false);
            System.Array.Clear(ready, 0, ready.Length);
            shellAmmo = Weapons[(int)Weapon.Shell].Ammo;
            Detonations = Anchors = Releases = Fired = 0;
        }

        public void ClearSpent()
        {
            foreach (var shot in shots)
                if (shot.gameObject.activeSelf && shot.Spent) shot.gameObject.SetActive(false);
        }
    }
}
