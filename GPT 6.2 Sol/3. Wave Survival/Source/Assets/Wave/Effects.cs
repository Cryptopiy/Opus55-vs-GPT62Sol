using System;
using UnityEngine;
using UnityEngine.Rendering;
using static WaveSurvival.GameConfig;
using Random = UnityEngine.Random;

namespace WaveSurvival
{
    internal sealed class Effects
    {
        struct Timed<T> where T : Component
        {
            public T Item;
            public float Until;
        }

        struct Casing
        {
            public Transform Mesh;
            public Vector3 Velocity, Spin;
            public float Until;
        }

        readonly Timed<LineRenderer>[] tracers = new Timed<LineRenderer>[TracerPoolSize];
        readonly Timed<Transform>[] marks = new Timed<Transform>[DecalPoolSize];
        readonly ParticleSystem[] impacts = new ParticleSystem[ParticlePoolSize];
        readonly Casing[] casings = new Casing[ShellPoolSize];
        readonly Material[] impactMarks = new Material[3];
        readonly Color[] surfaceColor = { Geometry.Hex(0xb1c3b9), Geometry.Hex(0xd44642), Geometry.Hex(0xffad56) };
        readonly Transform root;
        readonly LineRenderer ring;
        float ringUntil;
        int tracerIndex, markIndex, impactIndex, caseIndex;
        public int Particles { get; private set; }

        public Effects(Transform parent)
        {
            root = Geometry.Root("Combat effect pools", parent);
            var tracerMaterial = Ink(new Color(3.21f, 1.45f, .38f, .9f));
            var particleMaterial = Ink(Color.white, Mask("Particle falloff", 16, (x, y) => Mathf.Clamp01(1 - (x * x + y * y))));
            particleMaterial.SetFloat("_VertexTint", 1);
            var darkMark = Mask("Radial impact erosion", 32, (x, y) =>
            {
                float grain = Mathf.PerlinNoise(x * 14 + 51, y * 14 + 18);
                return Mathf.Clamp01((1 - Mathf.Sqrt(x * x + y * y) * 1.65f) * (grain * 1.3f + .3f));
            });
            impactMarks[0] = Ink(new(.12f, .15f, .16f, .89f), darkMark);
            impactMarks[1] = Ink(new(.40f, .055f, .06f, .81f), darkMark);
            impactMarks[2] = Ink(new(.97f, .56f, .22f, .61f), darkMark);
            Material brass = Geometry.Lit("Spent brass", Geometry.Hex(0xc59b5e), .43f, .58f);
            for (int i = 0; i < tracers.Length; i++)
            {
                var line = Line("Hitscan trail", tracerMaterial);
                line.positionCount = 2;
                line.enabled = false;
                tracers[i].Item = line;
            }
            for (int i = 0; i < marks.Length; i++)
            {
                Transform mark = Geometry.Part(root, "Surface impact mark", Vector3.zero, Vector3.one, impactMarks[0], PrimitiveType.Quad);
                mark.gameObject.SetActive(false);
                marks[i].Item = mark;
            }
            for (int i = 0; i < impacts.Length; i++)
            {
                var container = Geometry.Root("Impact fragments", root);
                container.gameObject.SetActive(false);
                var system = container.gameObject.AddComponent<ParticleSystem>();
                var main = system.main;
                main.playOnAwake = false;
                main.loop = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startSize = new ParticleSystem.MinMaxCurve(.033f, .092f);
                main.startLifetime = new ParticleSystem.MinMaxCurve(.11f, .44f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(2, 6);
                main.gravityModifier = .73f;
                main.maxParticles = 32;
                var emission = system.emission;
                emission.enabled = false;
                var shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 31;
                shape.radius = .035f;
                system.GetComponent<ParticleSystemRenderer>().sharedMaterial = particleMaterial;
                container.gameObject.SetActive(true);
                impacts[i] = system;
            }
            for (int i = 0; i < casings.Length; i++)
            {
                var mesh = Geometry.Part(root, "Spent casing", Vector3.zero, new(.029f, .031f, .029f), brass, PrimitiveType.Cylinder);
                mesh.gameObject.SetActive(false);
                casings[i].Mesh = mesh;
            }
            ring = Line("Warden slam radius", Ink(new Color(3, .19f, .08f, .9f)));
            ring.loop = true;
            ring.positionCount = 72;
            ring.enabled = false;
        }

        public static Texture2D Mask(string name, int size, Func<float, float, float> alpha)
        {
            var texture = Geometry.Own(new Texture2D(size, size, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Clamp });
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float a = alpha(x * 2f / (size - 1) - 1, y * 2f / (size - 1) - 1);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a) * 255));
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        public static Material Ink(Color color, Texture2D texture = null)
        {
            var material = Geometry.Own(new Material(Resources.Load<Shader>("WaveInk")));
            material.SetColor("_BaseColor", color);
            if (texture != null) material.SetTexture("_MainTex", texture);
            return material;
        }

        LineRenderer Line(string name, Material material)
        {
            var line = Geometry.Root(name, root).gameObject.AddComponent<LineRenderer>();
            line.sharedMaterial = material;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            return line;
        }

        public void Tracer(Vector3 from, Vector3 to, bool pellet)
        {
            ref var item = ref tracers[tracerIndex];
            tracerIndex = (tracerIndex + 1) % tracers.Length;
            item.Item.startWidth = pellet ? .012f : .024f;
            item.Item.endWidth = .003f;
            item.Item.SetPosition(0, from);
            item.Item.SetPosition(1, to);
            item.Until = Time.time + (pellet ? .055f : .09f);
            item.Item.enabled = true;
        }

        public void Impact(Vector3 point, Vector3 normal, SurfaceKind type)
        {
            Burst(point + normal * .022f, normal, surfaceColor[(int)type], type == SurfaceKind.Flesh ? 7 : 11);
            ref var item = ref marks[markIndex];
            markIndex = (markIndex + 1) % marks.Length;
            item.Item.gameObject.SetActive(true);
            item.Item.GetComponent<Renderer>().sharedMaterial = impactMarks[(int)type];
            item.Item.localScale = Vector3.one * (type == SurfaceKind.Flesh ? .19f : .15f);
            item.Item.SetPositionAndRotation(point + normal * .009f,
                Quaternion.LookRotation(normal) * Quaternion.Euler(0, 0, Random.Range(0, 360)));
            item.Until = Time.time + (type == SurfaceKind.Flesh ? .42f : 12);
        }

        public void Burst(Vector3 point, Vector3 normal, Color color, int count)
        {
            ParticleSystem system = impacts[impactIndex];
            impactIndex = (impactIndex + 1) % impacts.Length;
            system.transform.SetPositionAndRotation(point, Quaternion.LookRotation(normal));
            var parameters = new ParticleSystem.EmitParams { startColor = color };
            system.Emit(parameters, count);
        }

        public void Shell(Vector3 point, Transform view)
        {
            ref Casing casing = ref casings[caseIndex];
            caseIndex = (caseIndex + 1) % casings.Length;
            casing.Mesh.SetPositionAndRotation(point, view.rotation);
            casing.Velocity = view.right * Random.Range(2.15f, 3.25f) + Vector3.up * 2 + view.forward * .35f;
            casing.Spin = new(960, 440, 700);
            casing.Until = Time.time + .76f;
            casing.Mesh.gameObject.SetActive(true);
        }

        public void WarnSlam(Vector3 center)
        {
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * (Mathf.PI * 2 / ring.positionCount);
                Vector3 point = center + new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle)) * BossSlamRadius;
                if (Physics.Raycast(point + Vector3.up * 3, Vector3.down, out RaycastHit hit, 6, WorldMask))
                    point.y = hit.point.y;
                ring.SetPosition(i, point + Vector3.up * .065f);
            }
            ring.enabled = true;
            ringUntil = Time.time + BossSlamWarning + BossAirTime;
        }

        public void Update(float dt)
        {
            float now = Time.time;
            foreach (var tracer in tracers)
                if (tracer.Item.enabled && now >= tracer.Until) tracer.Item.enabled = false;
            foreach (var mark in marks)
                if (mark.Item.gameObject.activeSelf && now >= mark.Until) mark.Item.gameObject.SetActive(false);
            foreach (ref Casing casing in casings.AsSpan())
            {
                if (!casing.Mesh.gameObject.activeSelf) continue;
                if (now >= casing.Until)
                {
                    casing.Mesh.gameObject.SetActive(false);
                    continue;
                }
                casing.Velocity += Vector3.down * (9.81f * dt);
                Vector3 move = casing.Velocity * dt;
                if (move.sqrMagnitude > .000001f && Physics.Raycast(casing.Mesh.position, move.normalized, out RaycastHit hit, move.magnitude, WorldMask))
                {
                    casing.Mesh.position = hit.point + hit.normal * .021f;
                    casing.Velocity = Vector3.Reflect(casing.Velocity, hit.normal) * .36f;
                }
                else casing.Mesh.position += move;
                casing.Mesh.Rotate(casing.Spin * dt);
            }
            int count = 0;
            foreach (ParticleSystem system in impacts) count += system.particleCount;
            Particles = count;
            if (ring.enabled)
            {
                ring.widthMultiplier = .06f + Mathf.Pow(Mathf.Sin(now * 17), 2) * .10f;
                if (now >= ringUntil) ring.enabled = false;
            }
        }
    }
}




