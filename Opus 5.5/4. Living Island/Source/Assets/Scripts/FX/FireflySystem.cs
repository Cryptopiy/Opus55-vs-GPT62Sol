using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Night fireflies (I-21): up to 200 additive, pulsing particles drifting among the vegetation within
    /// 40 m of the player. They are spawned on grassy ground near the player at night and culled as soon
    /// as they drift farther than 40 m. Particle data uses a preallocated array (no GC).
    /// </summary>
    public sealed class FireflySystem : MonoBehaviour
    {
        public int Count => ps != null ? ps.particleCount : 0;

        ParticleSystem ps;
        Transform player;
        readonly ParticleSystem.Particle[] buffer = new ParticleSystem.Particle[C.FireflyCount];
        ParticleSystem.EmitParams emit;
        DetRandom rng = new DetRandom(C.DefaultSeed, 808);
        float cullTimer;

        public void Init(Material material, Transform playerTransform)
        {
            player = playerTransform;
            ps = ParticleFactory.Create("Fireflies", transform, material, C.FireflyCount);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(9f, 15f);
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
            main.startColor = new Color(0.8f, 1f, 0.4f, 1f);
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.55f;
            noise.frequency = 0.25f;
            noise.scrollSpeed = 0.15f;
            noise.damping = true;
            // Several slow pulses per life; births are staggered so the field twinkles out of phase.
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = ParticleFactory.Gradient((0f, new Color(1f, 1f, 1f, 0f)), (0.12f, new Color(1f, 1f, 1f, 1f)),
                (0.25f, new Color(1f, 1f, 1f, 0.15f)), (0.4f, new Color(1f, 1f, 1f, 1f)), (0.55f, new Color(1f, 1f, 1f, 0.1f)),
                (0.72f, new Color(1f, 1f, 1f, 1f)), (1f, new Color(1f, 1f, 1f, 0f)));
            var em = ps.emission;
            em.rateOverTime = 0f;
            ps.Play();
            emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        }

        void Update()
        {
            if (player == null || Game.TimeOfDay == null || Game.World == null) return;
            float night = Game.TimeOfDay.NightFactor;
            float rain = Game.Weather != null ? Game.Weather.Current.Rain : 0f;
            bool active = night > 0.55f && rain < 0.3f;
            var island = Game.World.Data;

            if (active && ps.particleCount < C.FireflyCount)
            {
                int budget = Mathf.Min(8, C.FireflyCount - ps.particleCount);
                for (int i = 0; i < budget; i++)
                {
                    float a = rng.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(rng.Value) * (C.FireflyRadius - 2f);
                    float x = player.position.x + Mathf.Cos(a) * r, z = player.position.z + Mathf.Sin(a) * r;
                    Vector3 ground = new Vector3(x, island.HeightAt(x, z), z);
                    SurfaceType s = island.SurfaceAt(ground);
                    if (s == SurfaceType.Water || s == SurfaceType.Rock) continue;   // among vegetation only
                    emit.position = ground + Vector3.up * rng.Range(0.4f, 2.6f);
                    ps.Emit(emit, 1);
                }
            }

            cullTimer -= Time.deltaTime;
            if (cullTimer <= 0f)
            {
                cullTimer = 0.2f;
                int n = ps.GetParticles(buffer);
                float r2 = C.FireflyRadius * C.FireflyRadius;
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    Vector3 d = buffer[i].position - player.position;
                    d.y = 0f;
                    bool kill = d.sqrMagnitude > r2 || (!active && buffer[i].remainingLifetime > 1.5f);
                    if (kill) { buffer[i].remainingLifetime = d.sqrMagnitude > r2 ? 0f : 1.5f; changed = true; }
                }
                if (changed) ps.SetParticles(buffer, n);
            }
        }
    }
}
