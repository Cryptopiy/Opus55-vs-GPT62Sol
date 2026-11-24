using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Beach campfire (I-22): Blender campfire logs inside a ring of rocks, three driftwood log seats,
    /// fire + sparks + smoke particle systems and a warm point light with noise-driven flicker that casts
    /// soft dynamic shadows.
    /// </summary>
    public sealed class Campfire : MonoBehaviour
    {
        public Light FireLight { get; private set; }
        public int ActiveParticles => fire.particleCount + sparks.particleCount + smoke.particleCount;

        ParticleSystem fire, sparks, smoke;
        Vector3 lightBase;
        float seed;

        public static Campfire Create(IslandAssets assets, MaterialLibrary mats, IslandData d, PoiPlan plan, Transform parent, PlacementValidator validator)
        {
            var root = new GameObject("Campfire");
            root.transform.SetParent(parent, false);
            root.transform.position = plan.Campfire;
            var cf = root.AddComponent<Campfire>();
            cf.Build(assets, mats, d, plan, validator);
            return cf;
        }

        void Build(IslandAssets assets, MaterialLibrary mats, IslandData d, PoiPlan plan, PlacementValidator validator)
        {
            Vector3 centre = plan.Campfire;
            var rng = new DetRandom(d.Seed, 777);
            var logs = PropPlacer.Spawn(assets.campfireLogs, new Vector3(centre.x, d.HeightAt(centre.x, centre.z) - 0.03f, centre.z),
                                        Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), 1f, transform);
            if (logs != null) { PropPlacer.AddMeshCollider(logs, true); validator.Check(logs.transform, PropPlacer.FirstMesh(logs), true); }

            // Ring of small rocks around the fire pit.
            for (int i = 0; i < C.CampfireRingRocks; i++)
            {
                float a = i / (float)C.CampfireRingRocks * Mathf.PI * 2f + rng.Range(-0.1f, 0.1f);
                float x = centre.x + Mathf.Cos(a) * 0.66f, z = centre.z + Mathf.Sin(a) * 0.66f;
                float s = rng.Range(0.2f, 0.28f);
                var rock = PropPlacer.Spawn(assets.rocks[rng.Range(0, 5)], new Vector3(x, d.HeightAt(x, z) - 0.02f, z),
                                            Quaternion.Euler(0f, rng.Range(0f, 360f), 0f), s, transform);
                if (rock != null) validator.Check(rock.transform, PropPlacer.FirstMesh(rock), true);
            }

            // Three driftwood seats facing the fire (their length runs tangentially to the circle).
            for (int i = 0; i < C.CampfireSeatCount; i++)
            {
                float a = i / (float)C.CampfireSeatCount * Mathf.PI * 2f + rng.Range(-0.25f, 0.25f) + 0.4f;
                float x = centre.x + Mathf.Cos(a) * 2.3f, z = centre.z + Mathf.Sin(a) * 2.3f;
                Vector3 tangent = new Vector3(-Mathf.Sin(a), 0f, Mathf.Cos(a));
                // The driftwood log lies along its local X axis.
                Quaternion rot = Quaternion.FromToRotation(Vector3.right, tangent);
                var seat = PropPlacer.Spawn(assets.driftwood, new Vector3(x, d.HeightAt(x, z) - 0.04f, z), rot, rng.Range(0.85f, 1f), transform);
                if (seat != null) { PropPlacer.AddMeshCollider(seat, true); validator.Check(seat.transform, PropPlacer.FirstMesh(seat), true); }
            }

            // Fire light: warm, flickering, soft shadows.
            var lightGo = new GameObject("FireLight");
            lightGo.transform.SetParent(transform, false);
            lightBase = centre + Vector3.up * 0.7f;
            lightGo.transform.position = lightBase;
            FireLight = lightGo.AddComponent<Light>();
            FireLight.type = LightType.Point;
            FireLight.range = C.CampfireLightRange;
            FireLight.color = new Color(1f, 0.58f, 0.28f);
            FireLight.intensity = C.CampfireLightIntensity;
            FireLight.shadows = LightShadows.Soft;
            FireLight.shadowNearPlane = 0.25f;
            FireLight.shadowBias = 0.05f;
            FireLight.shadowNormalBias = 0.4f;
            var data = lightGo.AddComponent<UniversalAdditionalLightData>();
            data.softShadowQuality = SoftShadowQuality.Medium;
            data.usePipelineSettings = false;

            BuildParticles(mats, centre);
            seed = rng.Range(0f, 100f);
        }

        void BuildParticles(MaterialLibrary mats, Vector3 centre)
        {
            Transform pt = new GameObject("FireFX").transform;
            pt.SetParent(transform, false);
            pt.position = centre + Vector3.up * 0.12f;

            fire = ParticleFactory.Create("Fire", pt, mats.Fire, 160);
            var m = fire.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.0f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.3f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.6f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new Color(1f, 0.75f, 0.4f, 1f);
            var e = fire.emission; e.rateOverTime = 70f;
            var s = fire.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 8f; s.radius = 0.22f;
            s.rotation = new Vector3(-90f, 0f, 0f);
            var col = fire.colorOverLifetime; col.enabled = true;
            col.color = ParticleFactory.Gradient((0f, new Color(1f, 0.9f, 0.55f, 0f)), (0.12f, new Color(1f, 0.75f, 0.35f, 1f)),
                                                 (0.55f, new Color(1f, 0.38f, 0.1f, 0.8f)), (1f, new Color(0.6f, 0.1f, 0.02f, 0f)));
            var sz = fire.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, ParticleFactory.Curve((0f, 0.7f), (0.3f, 1f), (1f, 0.25f)));
            var n = fire.noise; n.enabled = true; n.strength = 0.35f; n.frequency = 1.6f; n.scrollSpeed = 1.2f;
            var rot = fire.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-1.2f, 1.2f);
            fire.Play();

            sparks = ParticleFactory.Create("Sparks", pt, mats.Spark, 80);
            m = sparks.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.6f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(1.5f, 3.4f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.05f);
            m.startColor = new Color(1f, 0.7f, 0.3f, 1f);
            m.gravityModifier = -0.05f;
            e = sparks.emission; e.rateOverTime = 14f;
            s = sparks.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 18f; s.radius = 0.15f;
            s.rotation = new Vector3(-90f, 0f, 0f);
            col = sparks.colorOverLifetime; col.enabled = true;
            col.color = ParticleFactory.Gradient((0f, new Color(1f, 0.85f, 0.5f, 1f)), (0.6f, new Color(1f, 0.45f, 0.1f, 1f)), (1f, new Color(0.8f, 0.2f, 0.05f, 0f)));
            n = sparks.noise; n.enabled = true; n.strength = 1.1f; n.frequency = 0.9f; n.scrollSpeed = 0.8f;
            var sr = sparks.GetComponent<ParticleSystemRenderer>();
            sr.renderMode = ParticleSystemRenderMode.Stretch; sr.velocityScale = 0.06f; sr.lengthScale = 1.5f;
            sparks.Play();

            smoke = ParticleFactory.Create("Smoke", pt, mats.Smoke, 90);
            m = smoke.main;
            m.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            m.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startSize = new ParticleSystem.MinMaxCurve(0.5f, 0.9f);
            m.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            m.startColor = new Color(0.35f, 0.34f, 0.33f, 0.5f);
            e = smoke.emission; e.rateOverTime = 9f;
            s = smoke.shape; s.enabled = true; s.shapeType = ParticleSystemShapeType.Cone; s.angle = 10f; s.radius = 0.25f;
            s.rotation = new Vector3(-90f, 0f, 0f); s.position = new Vector3(0f, 0.5f, 0f);
            col = smoke.colorOverLifetime; col.enabled = true;
            col.color = ParticleFactory.Gradient((0f, new Color(0.3f, 0.3f, 0.3f, 0f)), (0.15f, new Color(0.34f, 0.33f, 0.32f, 0.42f)), (1f, new Color(0.5f, 0.5f, 0.5f, 0f)));
            sz = smoke.sizeOverLifetime; sz.enabled = true;
            sz.size = new ParticleSystem.MinMaxCurve(1f, ParticleFactory.Curve((0f, 0.6f), (1f, 4.5f)));
            var force = smoke.forceOverLifetime; force.enabled = true; force.space = ParticleSystemSimulationSpace.World;
            Vector2 wind = C.WindDirection.normalized * 0.35f;
            force.x = wind.x; force.y = 0.05f; force.z = wind.y;
            rot = smoke.rotationOverLifetime; rot.enabled = true; rot.z = new ParticleSystem.MinMaxCurve(-0.3f, 0.3f);
            n = smoke.noise; n.enabled = true; n.strength = 0.25f; n.frequency = 0.3f;
            smoke.Play();
        }

        void Update()
        {
            if (FireLight == null) return;
            float t = Time.time * 1.0f + seed;
            // Two octaves of noise: slow breathing of the flames plus fast flicker.
            float flicker = 0.72f + 0.2f * Mathf.PerlinNoise(t * 1.3f, 0.3f) + 0.16f * Mathf.PerlinNoise(t * 9f, 7.1f);
            float rainDamp = Game.Weather != null ? Mathf.Lerp(1f, 0.75f, Game.Weather.Current.Rain) : 1f;
            FireLight.intensity = C.CampfireLightIntensity * flicker * rainDamp;
            FireLight.transform.position = lightBase + new Vector3(Mathf.PerlinNoise(t * 3f, 1f) - 0.5f, Mathf.PerlinNoise(t * 2.5f, 3f) * 0.3f, Mathf.PerlinNoise(t * 3f, 5f) - 0.5f) * 0.12f;
        }
    }
}
