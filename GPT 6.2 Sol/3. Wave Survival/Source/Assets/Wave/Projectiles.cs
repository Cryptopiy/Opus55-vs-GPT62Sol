using System;
using UnityEngine;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed class Projectiles
    {
        struct Bolt
        {
            public Transform Mesh;
            public Vector3 Speed;
            public float Damage, Expires;
        }
        readonly Bolt[] bolts = new Bolt[ProjectilePoolSize];
        readonly CoreGame game;
        int next;
        public int Active { get; private set; }

        public Projectiles(CoreGame game)
        {
            this.game = game;
            var carrier = Geometry.Root("Pooled enemy projectiles", game.transform);
            var material = Geometry.Lit("Hostile plasma", Geometry.Hex(0xff522e), .35f, .1f, 3.7f);
            for (int i = 0; i < bolts.Length; i++)
            {
                bolts[i].Mesh = Geometry.Part(carrier, "Tracer round", Vector3.zero, new(.075f, .075f, .72f),
                    material, PrimitiveType.Sphere);
                bolts[i].Mesh.gameObject.SetActive(false);
            }
        }

        public void Fire(Vector3 position, Vector3 direction, float speed, float damage)
        {
            ref Bolt bolt = ref bolts[next];
            next = (next + 1) % bolts.Length;
            bolt.Expires = Time.time + 2.9f;
            bolt.Speed = direction * speed;
            bolt.Damage = damage;
            bolt.Mesh.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
            bolt.Mesh.gameObject.SetActive(true);
        }

        public void Tick(float dt)
        {
            int count = 0;
            foreach (ref Bolt bolt in bolts.AsSpan())
            {
                if (!bolt.Mesh.gameObject.activeSelf) continue;
                if (Time.time >= bolt.Expires)
                {
                    bolt.Mesh.gameObject.SetActive(false);
                    continue;
                }
                Vector3 from = bolt.Mesh.position;
                Vector3 step = bolt.Speed * dt;
                if (Physics.Raycast(from, bolt.Speed.normalized, out RaycastHit hit, step.magnitude,
                    WorldMask | (1 << PlayerLayer), QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.gameObject.layer == PlayerLayer) game.DamagePlayer(bolt.Damage, from);
                    else game.Impact(hit.point, hit.normal, game.Arena.IsMetal(hit.collider.transform) ? SurfaceKind.Metal : SurfaceKind.Concrete);
                    bolt.Mesh.gameObject.SetActive(false);
                    continue;
                }
                bolt.Mesh.position += step;
                count++;
            }
            Active = count;
        }

        public void Clear()
        {
            foreach (Bolt bolt in bolts) bolt.Mesh.gameObject.SetActive(false);
            Active = 0;
        }
    }
}

