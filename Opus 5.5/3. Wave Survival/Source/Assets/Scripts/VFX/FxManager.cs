using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// All transient visual effects (S-12, S-17, S-20, S-23): shared particle emitters, a ring buffer of decal quads,
    /// pooled tracer line renderers, pooled Rigidbody shell casings and the boss slam telegraph.
    /// </summary>
    public sealed class FxManager : MonoBehaviour
    {
        struct DecalEntry
        {
            public Transform T;
            public MeshRenderer R;
            public Transform Parent;
            public bool Active;
        }

        struct TracerEntry
        {
            public LineRenderer L;
            public Vector3 A, Dir;
            public float Length, Travel;
            public bool Active;
        }

        ParticleSystem smoke, debris, sparks, glow;
        ParticleSystem.EmitParams ep;
        DecalEntry[] decals;
        int decalNext;
        TracerEntry[] tracers;
        int tracerNext;
        ComponentPool<ShellCasing> shells;
        TelegraphRing telegraph;

        public int ActiveDecals { get; private set; }
        public int ActiveTracers { get; private set; }
        public int ActiveShells => shells.Active;
        public int ParticleCount => smoke.particleCount + debris.particleCount + sparks.particleCount + glow.particleCount;

        public static FxManager Create(Transform parent)
        {
            var go = new GameObject("FX");
            go.transform.SetParent(parent, false);
            FxManager fx = go.AddComponent<FxManager>();
            fx.Build();
            return fx;
        }

        void Build()
        {
            smoke = ParticleFactory.Create(transform, "Smoke", GameAssets.SmokeAlpha, false, -0.02f, 2.6f, false, 1.6f);
            debris = ParticleFactory.Create(transform, "Debris", GameAssets.DotAlpha, false, 1f, 0.8f, true, 0.4f);
            sparks = ParticleFactory.Create(transform, "Sparks", GameAssets.DotAdditive, true, 1.1f, 0.5f, true, 0.6f);
            glow = ParticleFactory.Create(transform, "Glow", GameAssets.DotAdditive, false, 0f, 0.2f, false, 0f);

            decals = new DecalEntry[GameConfig.Fx.DecalPool];
            for (int i = 0; i < decals.Length; i++)
            {
                var d = new GameObject("Decal");
                d.transform.SetParent(transform, false);
                d.AddComponent<MeshFilter>().sharedMesh = GameAssets.Quad;
                var mr = d.AddComponent<MeshRenderer>();
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.sharedMaterial = GameAssets.DecalConcrete;
                d.SetActive(false);
                decals[i] = new DecalEntry { T = d.transform, R = mr };
            }

            tracers = new TracerEntry[GameConfig.Fx.TracerPool];
            for (int i = 0; i < tracers.Length; i++)
            {
                var t = new GameObject("Tracer");
                t.transform.SetParent(transform, false);
                LineRenderer l = t.AddComponent<LineRenderer>();
                l.useWorldSpace = true;
                l.positionCount = 2;
                l.sharedMaterial = GameAssets.Tracer;
                l.startWidth = GameConfig.Fx.TracerWidth;
                l.endWidth = GameConfig.Fx.TracerWidth * 0.35f;
                l.textureMode = LineTextureMode.Stretch;
                l.shadowCastingMode = ShadowCastingMode.Off;
                l.receiveShadows = false;
                l.startColor = new Color(1f, 0.85f, 0.55f, 1f);
                l.endColor = new Color(1f, 0.7f, 0.35f, 0.6f);
                t.SetActive(false);
                tracers[i] = new TracerEntry { L = l };
            }

            shells = new ComponentPool<ShellCasing>(() => ShellCasing.Create(transform), GameConfig.Fx.ShellPool, GameConfig.Fx.ShellPool);
            telegraph = TelegraphRing.Create(transform);
        }

        void Emit(ParticleSystem ps, Vector3 pos, Vector3 vel, float size, float life, Color color)
        {
            ep.position = pos;
            ep.velocity = vel;
            ep.startSize = size;
            ep.startLifetime = life;
            ep.startColor = color;
            ep.rotation = Random.Range(0f, 360f);
            ps.Emit(ep, 1);
        }

        // ---------------- impacts (S-12: concrete vs flesh vs metal) ----------------

        public void Impact(SurfaceKind kind, Vector3 p, Vector3 n, Vector3 dir)
        {
            Vector3 refl = Vector3.Reflect(dir, n);
            switch (kind)
            {
                case SurfaceKind.Concrete:
                    for (int i = 0; i < 5; i++)
                        Emit(smoke, p + n * 0.05f, n * Random.Range(0.5f, 1.4f) + Random.insideUnitSphere * 0.4f, Random.Range(0.12f, 0.3f), Random.Range(0.5f, 0.9f), new Color(0.55f, 0.53f, 0.5f, 0.55f));
                    for (int i = 0; i < 7; i++)
                        Emit(debris, p + n * 0.02f, (refl + Random.insideUnitSphere * 0.6f) * Random.Range(2f, 4.5f), Random.Range(0.015f, 0.035f), Random.Range(0.5f, 0.9f), new Color(0.22f, 0.21f, 0.2f, 1f));
                    break;
                case SurfaceKind.Metal:
                    for (int i = 0; i < 12; i++)
                        Emit(sparks, p + n * 0.02f, (refl + Random.insideUnitSphere * 0.7f).normalized * Random.Range(3.5f, 9f), Random.Range(0.018f, 0.03f), Random.Range(0.15f, 0.4f), new Color(2.4f, 1.6f, 0.7f, 1f));
                    Emit(glow, p + n * 0.03f, Vector3.zero, 0.3f, 0.06f, new Color(2f, 1.5f, 0.8f, 1f));
                    for (int i = 0; i < 2; i++)
                        Emit(smoke, p + n * 0.05f, n * 0.6f + Random.insideUnitSphere * 0.3f, 0.12f, 0.6f, new Color(0.4f, 0.4f, 0.42f, 0.35f));
                    break;
                default:
                    for (int i = 0; i < 10; i++)
                        Emit(debris, p, (n * 0.8f + dir * 1.4f + Random.insideUnitSphere * 0.9f) * Random.Range(1.2f, 2.8f), Random.Range(0.025f, 0.055f), Random.Range(0.45f, 0.8f), new Color(0.36f, 0.015f, 0.02f, 1f));
                    for (int i = 0; i < 3; i++)
                        Emit(smoke, p, dir * 0.8f + Random.insideUnitSphere * 0.4f, Random.Range(0.18f, 0.32f), 0.35f, new Color(0.45f, 0.03f, 0.03f, 0.6f));
                    break;
            }
        }

        /// <summary>Small spark burst used by enemy projectile impacts.</summary>
        public void ProjectileImpact(Vector3 p, Vector3 n, Color c)
        {
            for (int i = 0; i < 8; i++)
                Emit(sparks, p + n * 0.03f, (n + Random.insideUnitSphere * 0.8f).normalized * Random.Range(2f, 5f), 0.025f, Random.Range(0.12f, 0.3f), c);
            Emit(glow, p + n * 0.05f, Vector3.zero, 0.45f, 0.08f, c);
        }

        public void MuzzleSmoke(Vector3 p, Vector3 dir)
        {
            for (int i = 0; i < 2; i++)
                Emit(smoke, p, dir * Random.Range(0.5f, 1.2f) + Vector3.up * 0.3f + Random.insideUnitSphere * 0.15f, Random.Range(0.08f, 0.14f), Random.Range(0.4f, 0.7f), new Color(0.65f, 0.63f, 0.6f, 0.22f));
        }

        /// <summary>S-17 small particle burst on death, tinted with the enemy's glow colour.</summary>
        public void DeathBurst(Vector3 p, Color c)
        {
            for (int i = 0; i < 16; i++)
                Emit(sparks, p + Random.insideUnitSphere * 0.3f, Random.onUnitSphere * Random.Range(1f, 3f) + Vector3.up * 1.5f, Random.Range(0.03f, 0.06f), Random.Range(0.5f, 1.1f), c * 2.5f);
            for (int i = 0; i < 6; i++)
                Emit(smoke, p + Random.insideUnitSphere * 0.4f, Vector3.up * Random.Range(0.3f, 0.9f) + Random.insideUnitSphere * 0.4f, Random.Range(0.4f, 0.75f), Random.Range(1f, 1.6f), new Color(0.12f, 0.11f, 0.11f, 0.55f));
        }

        public void SpawnBurst(Vector3 p, Color c)
        {
            for (int i = 0; i < 18; i++)
                Emit(sparks, p + new Vector3(Random.Range(-0.5f, 0.5f), Random.Range(0f, 1.8f), Random.Range(-0.5f, 0.5f)), Vector3.up * Random.Range(1f, 3.5f) + Random.insideUnitSphere, 0.04f, Random.Range(0.4f, 0.9f), c * 2f);
            for (int i = 0; i < 4; i++)
                Emit(smoke, p + Vector3.up * Random.Range(0.2f, 1.4f), Random.insideUnitSphere * 0.6f, Random.Range(0.6f, 1f), 1.1f, new Color(0.15f, 0.08f, 0.06f, 0.5f));
            Emit(glow, p + Vector3.up, Vector3.zero, 2.4f, 0.25f, c * 1.5f);
        }

        public void SlamBurst(Vector3 p, float radius)
        {
            for (int i = 0; i < 40; i++)
            {
                float a = i * Mathf.PI * 2f / 40f;
                Vector3 d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Emit(smoke, p + d * 0.8f + Vector3.up * 0.2f, d * Random.Range(6f, 9f) + Vector3.up * 0.4f, Random.Range(0.6f, 1.1f), Random.Range(0.7f, 1f), new Color(0.45f, 0.42f, 0.38f, 0.6f));
            }
            for (int i = 0; i < 24; i++)
                Emit(debris, p + Random.insideUnitSphere * radius * 0.3f, Vector3.up * Random.Range(3f, 7f) + Random.insideUnitSphere * 3f, Random.Range(0.04f, 0.09f), Random.Range(0.8f, 1.4f), new Color(0.25f, 0.24f, 0.22f, 1f));
            Emit(glow, p + Vector3.up * 0.3f, Vector3.zero, radius * 2f, 0.2f, new Color(1.5f, 0.5f, 0.2f, 1f));
        }

        public void PickupBurst(Vector3 p, Color c)
        {
            for (int i = 0; i < 16; i++)
                Emit(glow, p + Random.insideUnitSphere * 0.3f, Vector3.up * Random.Range(1f, 2.5f) + Random.insideUnitSphere * 0.6f, Random.Range(0.05f, 0.1f), Random.Range(0.4f, 0.8f), c * 2f);
        }

        public void DustPuff(Vector3 p, float size)
        {
            Emit(smoke, p + Random.insideUnitSphere * 0.2f, Random.insideUnitSphere * 0.6f + Vector3.up * 0.3f, size, 0.8f, new Color(0.4f, 0.38f, 0.35f, 0.35f));
        }

        // ---------------- decals ----------------

        public void Decal(SurfaceKind kind, Vector3 p, Vector3 n, Transform parent)
        {
            ref DecalEntry d = ref decals[decalNext];
            decalNext = (decalNext + 1) % decals.Length;
            if (!d.Active) ActiveDecals++;
            d.Active = true;
            d.Parent = parent;
            Material mat = kind == SurfaceKind.Metal ? GameAssets.DecalMetal : kind == SurfaceKind.Flesh ? GameAssets.DecalBlood : GameAssets.DecalConcrete;
            float size = (kind == SurfaceKind.Flesh ? GameConfig.Fx.BloodDecalSize : GameConfig.Fx.DecalSize) * Random.Range(0.75f, 1.25f);
            d.R.sharedMaterial = mat;
            d.T.SetParent(parent != null ? parent : transform, false);
            d.T.SetPositionAndRotation(p + n * 0.006f, Quaternion.LookRotation(-n) * Quaternion.Euler(0f, 0f, Random.Range(0f, 360f)));
            Vector3 ls = Vector3.one * size;
            if (parent != null)
            {
                // Compensate the parent's scale so the decal keeps its world size.
                Vector3 s = parent.lossyScale;
                ls = new Vector3(size / Mathf.Max(0.001f, s.x), size / Mathf.Max(0.001f, s.y), size / Mathf.Max(0.001f, s.z));
            }
            d.T.localScale = ls;
            d.T.gameObject.SetActive(true);
        }

        /// <summary>Removes the decals attached to a body that is about to fade out or be pooled.</summary>
        public void ReleaseDecalsUnder(Transform root)
        {
            for (int i = 0; i < decals.Length; i++)
            {
                ref DecalEntry d = ref decals[i];
                if (d.Active && d.Parent != null && d.Parent.IsChildOf(root)) Deactivate(ref d);
            }
        }

        void Deactivate(ref DecalEntry d)
        {
            d.Active = false;
            d.Parent = null;
            ActiveDecals--;
            d.T.gameObject.SetActive(false);
            d.T.SetParent(transform, false);
        }

        // ---------------- tracers ----------------

        public void Tracer(Vector3 a, Vector3 b)
        {
            ref TracerEntry t = ref tracers[tracerNext];
            tracerNext = (tracerNext + 1) % tracers.Length;
            if (!t.Active) ActiveTracers++;
            t.Active = true;
            t.A = a;
            Vector3 d = b - a;
            t.Length = d.magnitude;
            t.Dir = t.Length > 0.001f ? d / t.Length : Vector3.forward;
            t.Travel = 0f;
            t.L.SetPosition(0, a);
            t.L.SetPosition(1, a);
            t.L.gameObject.SetActive(true);
        }

        public void EjectShell(Vector3 p, Vector3 v, bool pistol)
        {
            ShellCasing s = shells.Get();
            if (s == null) return;
            s.Launch(p, v, pistol, shells);
        }

        public void ShowTelegraph(Vector3 p, float radius, float duration) => telegraph.Show(p, radius, duration);

        public void HideTelegraph() => telegraph.Hide();

        void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < tracers.Length; i++)
            {
                ref TracerEntry t = ref tracers[i];
                if (!t.Active) continue;
                // A short bright segment races from the muzzle to the impact point.
                t.Travel += GameConfig.Fx.TracerSpeed * dt;
                float head = Mathf.Min(t.Length, t.Travel);
                float tail = Mathf.Max(0f, t.Travel - GameConfig.Fx.TracerLength);
                if (tail >= t.Length)
                {
                    t.Active = false;
                    ActiveTracers--;
                    t.L.gameObject.SetActive(false);
                    continue;
                }
                t.L.SetPosition(0, t.A + t.Dir * tail);
                t.L.SetPosition(1, t.A + t.Dir * head);
            }
            for (int i = 0; i < decals.Length; i++)
            {
                ref DecalEntry d = ref decals[i];
                if (d.Active && d.Parent != null && !d.Parent.gameObject.activeInHierarchy) Deactivate(ref d);
            }
        }
    }
}
