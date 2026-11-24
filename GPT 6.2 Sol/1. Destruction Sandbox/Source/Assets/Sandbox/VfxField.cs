using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    internal sealed class VfxField
    {
        readonly ParticleSystem dust, sparks, chips, fire, smoke, motes;
        readonly Transient[] waves, scorches, orbs;
        readonly Light[] flashes = new Light[4];
        readonly float[] lightTime = new float[4];
        readonly Transform muzzle;
        readonly Color[] shotColors = {
            new(4.8f, 2.05f, .51f), new(5.2f, 1.46f, .44f), new(1.7f, .70f, 4.9f)
        };
        float muzzleTime;
        int waveIndex, scorchIndex, orbIndex, lightIndex;
        uint random = 0x32af891u;

        public int ActiveParticles => dust.particleCount + sparks.particleCount + chips.particleCount +
            fire.particleCount + smoke.particleCount + motes.particleCount;
        public int ActiveWaves
        {
            get
            {
                int count = 0;
                foreach (var wave in waves) if (wave.Time > 0) count++;
                return count;
            }
        }

        public VfxField(Sandbox game)
        {
            var particleMaterial = game.EffectMaterial("Soft particles", 0, Color.white);
            dust = ParticlePool(game, "Dust", 820, particleMaterial, -.025f, .85f);
            sparks = ParticlePool(game, "Sparks", 640, particleMaterial, .79f, .64f, true);
            chips = ParticlePool(game, "Splinters and glass flecks", 510, particleMaterial, .48f, .8f, true);
            fire = ParticlePool(game, "Fire", 260, particleMaterial, -.55f, .68f);
            smoke = ParticlePool(game, "Smoke", 320, particleMaterial, -.22f, 1f);
            motes = ParticlePool(game, "Gravity motes", 520, particleMaterial, 0, 1f, true);
            waves = CreateTransients(game, "Shockwave", 8, PrimitiveType.Sphere, 1);
            scorches = CreateTransients(game, "Scorch", 24, PrimitiveType.Quad, 3);
            orbs = CreateTransients(game, "Gravity distortion", ProjectileCapacity, PrimitiveType.Sphere, 2);
            for (int i = 0; i < flashes.Length; i++)
            {
                var light = game.Node("Explosion flash").AddComponent<Light>();
                light.type = LightType.Point;
                light.range = BlastRadius * 2.75f;
                light.color = new Color(1, .58f, .22f);
                light.shadows = LightShadows.None;
                light.enabled = false;
                flashes[i] = light;
            }

            Material glow = game.EffectMaterial("Muzzle glow", 1, Color.white);
            muzzle = game.Shape("Muzzle flash", game.Player.Muzzle, Vector3.zero, Vector3.one, glow, PrimitiveType.Sphere).transform;
            muzzle.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            muzzle.gameObject.SetActive(false);
        }

        static Transient[] CreateTransients(Sandbox game, string name, int capacity, PrimitiveType mesh, float mode)
        {
            var set = new Transient[capacity];
            for (int i = 0; i < set.Length; i++)
            {
                var material = game.EffectMaterial(name, mode, Color.white);
                var go = game.Shape(name, game.transform, Vector3.zero, Vector3.one, material, mesh);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
                go.SetActive(false);
                set[i] = new Transient(go.transform, material);
            }
            return set;
        }

        static ParticleSystem ParticlePool(Sandbox game, string name, int capacity, Material material,
            float gravity, float shrink, bool stretched = false)
        {
            var pool = game.Node(name).AddComponent<ParticleSystem>();
            pool.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = pool.main;
            main.playOnAwake = false;
            main.loop = true;
            main.maxParticles = capacity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = pool.emission;
            emission.enabled = false;
            var shape = pool.shape;
            shape.enabled = false;
            var fade = pool.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(.88f, .30f), new GradientAlphaKey(0, 1) });
            fade.color = gradient;
            var size = pool.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 1, 1, shrink));
            var renderer = pool.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = stretched ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
            renderer.lengthScale = .78f;
            renderer.velocityScale = stretched ? .075f : 0;
            renderer.maxParticleSize = .34f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.sharedMaterial = material;
            renderer.SetActiveVertexStreams(new List<ParticleSystemVertexStream>
            {
                ParticleSystemVertexStream.Position, ParticleSystemVertexStream.Normal,
                ParticleSystemVertexStream.Color, ParticleSystemVertexStream.UV
            });
            pool.Play();
            return pool;
        }

        public void Muzzle(Weapon weapon)
        {
            muzzleTime = .085f;
            muzzle.localScale = new Vector3(.21f, .19f, .43f);
            muzzle.gameObject.SetActive(true);
            muzzle.GetComponent<MeshRenderer>().sharedMaterial.SetColor("_BaseColor", shotColors[(int)weapon]);
            Emit(sparks, muzzle.position, 12, shotColors[(int)weapon], .055f, 9f, .19f, .35f);
        }

        public void Break(Matter matter, Vector3 p)
        {
            switch (matter)
            {
                case Matter.Glass:
                    Emit(chips, p, 9, new Color(.65f, 1.8f, 1.9f, .92f), .075f, 5.1f, .55f, .10f);
                    Emit(sparks, p, 6, new Color(1.0f, 2.7f, 2.9f, .96f), .038f, 6.9f, .52f, .10f);
                    break;
                case Matter.Wood:
                    Emit(chips, p, 7, new Color(.75f, .36f, .10f, .92f), .095f, 5.7f, .52f, .10f);
                    Emit(dust, p, 3, new Color(.53f, .33f, .15f, .38f), .51f, 2f, .82f, .12f);
                    break;
                default:
                    Emit(dust, p, 6, new Color(.34f, .35f, .33f, .45f), .68f, 2.4f, 1.15f, .20f);
                    Emit(sparks, p, 3, new Color(2.3f, 1.05f, .3f, .82f), .046f, 5.3f, .42f, .16f);
                    break;
            }
        }

        public void HeavyImpact(Vector3 p, float impulse)
        {
            if (impulse < 35f) return;
            Emit(sparks, p, 12, new Color(2.5f, 1.31f, .46f), .06f, Mathf.Min(8f, impulse / 42f), .53f, .42f);
        }

        public void Blast(Vector3 p)
        {
            int light = lightIndex++ % flashes.Length;
            flashes[light].transform.position = p;
            flashes[light].intensity = 270f;
            flashes[light].enabled = true;
            lightTime[light] = BlastFlash;
            Wave(p, BlastRadius, new Color(2.5f, 1.68f, .77f), .68f);
            var scorch = scorches[scorchIndex++ % scorches.Length];
            scorch.Start(new Vector3(p.x, .013f, p.z), ScorchLife, new Color(.058f, .043f, .033f, .86f));
            scorch.Transform.localRotation = Quaternion.Euler(90, 0, 0);
            scorch.Transform.localScale = new Vector3(BlastRadius * 1.18f, BlastRadius * 1.18f, 1);
            Emit(fire, p, 85, new Color(4f, 1.28f, .225f, .93f), 1.8f, 11.2f, .62f, .20f);
            Emit(smoke, p, 64, new Color(.235f, .214f, .198f, .50f), 2.7f, 5.4f, 2.85f, .62f);
            Emit(sparks, p, 115, new Color(2.8f, 1.36f, .32f, 1), .15f, 15f, 1.13f, .46f);
        }

        public void Anchor(Vector3 p)
        {
            var orb = orbs[orbIndex++ % orbs.Length];
            orb.Start(p, OrbTime, new Color(1.28f, .60f, 3.4f));
            orb.Transform.localScale = Vector3.one * 1.2f;
            orb.Remainder = 0;
        }

        public void Release(Vector3 p)
        {
            Wave(p, OrbRadius, new Color(1.18f, .56f, 3.2f), .63f);
            Emit(sparks, p, 68, new Color(1.3f, .74f, 3.2f), .095f, 12f, .9f, .2f);
        }

        void Wave(Vector3 p, float radius, Color tint, float time)
        {
            var wave = waves[waveIndex++ % waves.Length];
            wave.Start(p, time, tint);
            wave.Radius = radius;
            wave.Transform.localScale = Vector3.one * .62f;
        }

        public void Tick(float dt)
        {
            if (dt <= 0) return;
            if (muzzleTime > 0)
            {
                muzzleTime -= dt;
                if (muzzleTime <= 0) muzzle.gameObject.SetActive(false);
            }

            for (int i = 0; i < flashes.Length; i++)
            {
                if (lightTime[i] <= 0) continue;
                lightTime[i] -= dt;
                flashes[i].intensity = 270f * Mathf.Clamp01(lightTime[i] / BlastFlash);
                if (lightTime[i] <= 0) flashes[i].enabled = false;
            }
            foreach (var wave in waves)
                if (wave.Tick(dt))
                    wave.Transform.localScale = Vector3.one * (2 * Mathf.Lerp(.31f, wave.Radius, 1 - Mathf.Pow(1 - wave.Phase, 1.8f)));
            foreach (var scorch in scorches) scorch.Tick(dt);
            foreach (var orb in orbs)
            {
                if (!orb.Tick(dt)) continue;
                orb.Transform.localScale = Vector3.one * (1.23f + Mathf.Sin(Time.time * 11) * .13f);
                orb.Remainder += dt * 92;
                int count = Mathf.FloorToInt(orb.Remainder);
                orb.Remainder -= count;
                for (int i = 0; i < count; i++)
                {
                    Vector3 direction = Direction();
                    float radius = Mathf.Lerp(1.8f, OrbRadius, Random01());
                    float life = Mathf.Lerp(.21f, .63f, Random01());
                    var options = new ParticleSystem.EmitParams
                    {
                        position = orb.Transform.position + direction * radius,
                        velocity = -direction * (radius / life),
                        startLifetime = life, startSize = .048f + Random01() * .10f,
                        startColor = new Color(.59f, .28f, 1, .92f)
                    };
                    motes.Emit(options, 1);
                }
            }
        }

        void Emit(ParticleSystem pool, Vector3 p, int count, Color color, float size, float speed, float life, float upward)
        {
            count = Mathf.Min(count, pool.main.maxParticles - pool.particleCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 dir = Direction();
                var options = new ParticleSystem.EmitParams
                {
                    position = p + dir * (size * Random01() * .55f),
                    velocity = (dir + Vector3.up * upward) * (speed * (.35f + Random01() * .65f)),
                    startColor = color,
                    startSize = size * (.5f + Random01() * .95f),
                    startLifetime = life * (.48f + Random01() * .72f),
                    rotation = Random01() * 360
                };
                pool.Emit(options, 1);
            }
        }

        Vector3 Direction()
        {
            float y = Random01() * 2 - 1;
            float angle = Random01() * 2 * Mathf.PI;
            float r = Mathf.Sqrt(1 - y * y);
            return new Vector3(Mathf.Cos(angle) * r, y, Mathf.Sin(angle) * r);
        }

        float Random01()
        {
            random ^= random << 13; random ^= random >> 17; random ^= random << 5;
            return (random & 0x00ffffffu) / 16777215f;
        }

        public void ClearDebris()
        {
            dust.Clear(); sparks.Clear(); chips.Clear(); fire.Clear(); smoke.Clear();
            foreach (var scorch in scorches) scorch.End();
        }

        public void Reset()
        {
            ClearDebris();
            motes.Clear();
            muzzleTime = 0;
            muzzle.gameObject.SetActive(false);
            foreach (var wave in waves) wave.End();
            foreach (var orb in orbs) orb.End();
            for (int i = 0; i < flashes.Length; i++)
            {
                lightTime[i] = 0;
                flashes[i].enabled = false;
            }
        }

        sealed class Transient
        {
            public readonly Transform Transform;
            readonly Material material;
            public float Time, Duration, Radius, Remainder;
            public float Phase => Mathf.Clamp01(1 - Time / Duration);

            public Transient(Transform transform, Material material)
            {
                Transform = transform;
                this.material = material;
            }

            public void Start(Vector3 p, float time, Color tint)
            {
                Transform.position = p;
                Transform.gameObject.SetActive(true);
                material.SetColor("_BaseColor", tint);
                material.SetFloat("_Phase", 0);
                Time = Duration = time;
            }

            public bool Tick(float dt)
            {
                if (Time <= 0) return false;
                Time -= dt;
                if (Time <= 0)
                {
                    End();
                    return false;
                }
                material.SetFloat("_Phase", Phase);
                return true;
            }

            public void End()
            {
                Time = 0;
                Transform.gameObject.SetActive(false);
            }
        }
    }
}
