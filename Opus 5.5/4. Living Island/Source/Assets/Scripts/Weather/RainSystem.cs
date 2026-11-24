using UnityEngine;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Rain around the camera (I-18): a world-space streak particle system emitting from a box that follows
    /// the camera (4500 alive in Rain, ~6500 in Storm) plus ground splashes emitted on the terrain around the
    /// camera with preallocated EmitParams (no GC). Water ripples are drawn by the water shader.
    /// </summary>
    public sealed class RainSystem : MonoBehaviour
    {
        public int ActiveParticles => rain.particleCount + splashes.particleCount;
        public int RainParticles => rain.particleCount;

        ParticleSystem rain, splashes;
        Transform cam;
        DetRandom rng = new DetRandom(C.DefaultSeed, 555);
        float splashAccumulator;
        ParticleSystem.EmitParams splashParams;
        const float Lifetime = 1.7f;

        public void Init(MaterialLibrary mats, Transform cameraTransform)
        {
            cam = cameraTransform;
            rain = ParticleFactory.Create("RainStreaks", transform, mats.Rain, 7000);
            var main = rain.main;
            main.startLifetime = Lifetime;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.startColor = new Color(0.8f, 0.85f, 0.92f, 0.65f);
            var shape = rain.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(C.RainAreaSize, 1f, C.RainAreaSize);
            var vel = rain.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.World;
            vel.y = new ParticleSystem.MinMaxCurve(-C.RainFallSpeed * 1.08f, -C.RainFallSpeed * 0.92f);
            vel.x = new ParticleSystem.MinMaxCurve(0f, 0f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var r = rain.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Stretch;
            r.velocityScale = 0.05f;
            r.lengthScale = 1f;
            r.sortMode = ParticleSystemSortMode.None;
            var em = rain.emission;
            em.rateOverTime = 0f;
            rain.Play();

            splashes = ParticleFactory.Create("RainSplashes", transform, mats.Splash, 1500);
            main = splashes.main;
            main.startLifetime = 0.28f;
            main.startSpeed = 0f;
            main.loop = true;
            var col = splashes.colorOverLifetime;
            col.enabled = true;
            col.color = ParticleFactory.Gradient((0f, new Color(1f, 1f, 1f, 0.8f)), (1f, new Color(1f, 1f, 1f, 0f)));
            var sz = splashes.sizeOverLifetime;
            sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, ParticleFactory.Curve((0f, 0.3f), (1f, 1f)));
            em = splashes.emission;
            em.rateOverTime = 0f;
            splashes.GetComponent<ParticleSystemRenderer>().sortMode = ParticleSystemSortMode.None;
            splashes.Play();
            splashParams = new ParticleSystem.EmitParams { applyShapeToPosition = false };
        }

        /// <summary>Removes every live drop (after the startup shader warm-up).</summary>
        public void ClearAll()
        {
            rain.Clear();
            splashes.Clear();
        }

        void LateUpdate()
        {
            if (cam == null || Game.Weather == null) return;
            float intensity = Game.Weather.Current.Rain;
            // Keep the emitter box above the camera, slightly upwind so streaks fall into view.
            Vector2 windDir = C.WindDirection.normalized;
            float wind = C.WindStrength * Game.Weather.Current.Wind;
            Vector3 p = cam.position + cam.forward * 4f;
            rain.transform.position = new Vector3(p.x - windDir.x * wind * 1.5f, cam.position.y + 16f, p.z - windDir.y * wind * 1.5f);

            // Alive particles = rate x lifetime: 4500 at Rain intensity, ~6500 in a Storm.
            float alive = C.RainParticles * Mathf.Clamp01(intensity / 0.6f) + 2000f * Mathf.Clamp01((intensity - 0.6f) / 0.4f);
            if (Game.Atmosphere != null && Game.Atmosphere.CameraUnderwater) alive = 0f;
            var em = rain.emission;
            em.rateOverTime = alive / Lifetime;
            // All three velocity axes must stay in the same curve mode (TwoConstants) or Unity warns.
            var vel = rain.velocityOverLifetime;
            float vx = windDir.x * wind * 1.3f, vz = windDir.y * wind * 1.3f;
            vel.x = new ParticleSystem.MinMaxCurve(vx * 0.9f, vx * 1.1f);
            vel.z = new ParticleSystem.MinMaxCurve(vz * 0.9f, vz * 1.1f);

            EmitSplashes(intensity);
        }

        void EmitSplashes(float intensity)
        {
            var island = Game.World != null ? Game.World.Data : null;
            if (island == null || intensity < 0.02f) return;
            splashAccumulator += C.RainSplashesPerSecond * intensity * Time.deltaTime;
            int count = Mathf.Min((int)splashAccumulator, 64);
            splashAccumulator -= count;
            Vector3 c = cam.position;
            for (int i = 0; i < count; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f), r = Mathf.Sqrt(rng.Value) * 14f;
                float x = c.x + Mathf.Cos(a) * r, z = c.z + Mathf.Sin(a) * r;
                float h = island.HeightAt(x, z);
                if (h < C.SeaLevel + 0.05f) continue;              // on water the shader draws ripple rings
                splashParams.position = new Vector3(x, h + 0.03f, z);
                splashParams.startSize = rng.Range(0.08f, 0.16f);
                splashParams.velocity = new Vector3(0f, rng.Range(0.2f, 0.6f), 0f);
                splashes.Emit(splashParams, 1);
            }
        }
    }
}
