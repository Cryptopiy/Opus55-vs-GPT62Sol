using UnityEngine;

namespace LivingIsland
{
    /// <summary>
    /// Swimming splashes (I-26): droplets on every arm stroke and a burst when entering deep water.
    /// The foam ring around the body is drawn by the water shader from _PlayerPosition (w = swimming).
    /// </summary>
    public sealed class SwimEffects : MonoBehaviour
    {
        public int ActiveParticles => ps != null ? ps.particleCount : 0;

        ParticleSystem ps;
        PlayerController player;
        ParticleSystem.EmitParams emit;
        DetRandom rng = new DetRandom(4242, 9);
        bool wasSwimming;

        public void Init(Material material, PlayerController controller, PlayerAvatar avatar)
        {
            player = controller;
            ps = ParticleFactory.Create("SwimSplash", transform, material, 400);
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            main.gravityModifier = 1f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            var col = ps.colorOverLifetime;
            col.enabled = true;
            col.color = ParticleFactory.Gradient((0f, new Color(1f, 1f, 1f, 0.9f)), (1f, new Color(1f, 1f, 1f, 0f)));
            var em = ps.emission;
            em.rateOverTime = 0f;
            ps.Play();
            emit = new ParticleSystem.EmitParams { applyShapeToPosition = false };
            avatar.OnSwimStroke += Stroke;
        }

        void Burst(Vector3 centre, int count, float spread, float up)
        {
            for (int i = 0; i < count; i++)
            {
                emit.position = centre + new Vector3(rng.Range(-spread, spread), 0.02f, rng.Range(-spread, spread));
                emit.velocity = new Vector3(rng.Range(-1f, 1f), rng.Range(up * 0.5f, up), rng.Range(-1f, 1f)) + player.Velocity * 0.4f;
                ps.Emit(emit, 1);
            }
        }

        void Stroke(int side)
        {
            if (player == null || player.HorizontalSpeed < 0.3f) return;
            Transform t = player.transform;
            Vector3 hand = t.position + t.forward * 0.7f + t.right * (side == 0 ? -0.3f : 0.3f);
            hand.y = player.WaterSurfaceHere;
            Burst(hand, 9, 0.15f, 2.2f);
        }

        void Update()
        {
            if (player == null) return;
            if (player.IsSwimming && !wasSwimming)
            {
                Vector3 p = player.transform.position;
                p.y = player.WaterSurfaceHere;
                Burst(p, 30, 0.45f, 3f);
            }
            wasSwimming = player.IsSwimming;
        }
    }
}
