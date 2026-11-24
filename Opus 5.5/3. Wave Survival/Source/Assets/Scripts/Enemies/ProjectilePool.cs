using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// Pooled enemy projectiles (S-15 shooter rounds, S-23 boss rounds). Each moves with a swept sphere cast every
    /// frame, so fast rounds never tunnel through the player or thin cover.
    /// </summary>
    public sealed class ProjectilePool : MonoBehaviour
    {
        const float Radius = 0.07f;
        const float MaxLife = 3f;

        struct Round
        {
            public GameObject Go;
            public Transform T;
            public TrailRenderer Trail;
            public MeshRenderer Mesh;
            public Vector3 Vel;
            public Vector3 Source;
            public float Damage, Life;
            public bool Boss, Active;
        }

        Round[] rounds;
        int next;
        public int ActiveCount { get; private set; }

        public static ProjectilePool Create(Transform parent)
        {
            var go = new GameObject("Projectiles");
            go.transform.SetParent(parent, false);
            ProjectilePool p = go.AddComponent<ProjectilePool>();
            p.Build();
            return p;
        }

        void Build()
        {
            rounds = new Round[GameConfig.Pools.Projectiles];
            var widths = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
            for (int i = 0; i < rounds.Length; i++)
            {
                var go = new GameObject("Round");
                go.transform.SetParent(transform, false);
                go.transform.localScale = Vector3.one * 0.13f;
                go.AddComponent<MeshFilter>().sharedMesh = GameAssets.Sphere;
                var mr = go.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                var trail = go.AddComponent<TrailRenderer>();
                trail.time = 0.12f;
                trail.minVertexDistance = 0.2f;
                trail.widthMultiplier = 0.09f;
                trail.widthCurve = widths;
                trail.shadowCastingMode = ShadowCastingMode.Off;
                trail.receiveShadows = false;
                go.SetActive(false);
                rounds[i] = new Round { Go = go, T = go.transform, Trail = trail, Mesh = mr };
            }
        }

        public void Fire(Vector3 origin, Vector3 dir, float speed, float damage, bool boss, Vector3 source)
        {
            // Find a free slot, otherwise recycle the oldest round (hard cap, S-28 pooling).
            int idx = -1;
            for (int k = 0; k < rounds.Length; k++)
            {
                int i = (next + k) % rounds.Length;
                if (!rounds[i].Active) { idx = i; break; }
            }
            if (idx < 0) { idx = next; Deactivate(ref rounds[idx]); }
            next = (idx + 1) % rounds.Length;
            ref Round r = ref rounds[idx];
            r.Vel = dir * speed;
            r.Source = source;
            r.Damage = damage;
            r.Life = MaxLife;
            r.Boss = boss;
            r.Active = true;
            ActiveCount++;
            r.Mesh.sharedMaterial = boss ? GameAssets.ProjectileBoss : GameAssets.ProjectileShooter;
            r.Trail.sharedMaterial = boss ? GameAssets.TrailBoss : GameAssets.TrailShooter;
            r.T.position = origin;
            r.T.localScale = Vector3.one * (boss ? 0.2f : 0.13f);
            r.Go.SetActive(true);
            r.Trail.Clear();
        }

        void Deactivate(ref Round r)
        {
            if (!r.Active) return;
            r.Active = false;
            ActiveCount--;
            r.Go.SetActive(false);
        }

        public void ClearAll()
        {
            for (int i = 0; i < rounds.Length; i++) Deactivate(ref rounds[i]);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < rounds.Length; i++)
            {
                ref Round r = ref rounds[i];
                if (!r.Active) continue;
                Vector3 pos = r.T.position;
                Vector3 step = r.Vel * dt;
                float len = step.magnitude;
                if (Physics.SphereCast(pos, Radius, step / len, out RaycastHit hit, len, GameConfig.Layers.EnemyShotMask, QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider.gameObject.layer == GameConfig.Layers.Player && Game.Player != null)
                        Game.Player.Health.Damage(r.Damage, r.Source);
                    Color c = r.Boss ? new Color(2.5f, 0.9f, 0.3f, 1f) : new Color(0.4f, 1.6f, 2f, 1f);
                    Game.Fx.ProjectileImpact(hit.point, hit.normal, c);
                    Deactivate(ref r);
                    continue;
                }
                pos += step;
                r.T.position = pos;
                r.Life -= dt;
                if (r.Life <= 0f || !ArenaInfo.Inside(pos, -1f)) Deactivate(ref r);
            }
        }
    }
}
