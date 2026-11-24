using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Fx = DestructionSandbox.GameConfig.Effects;
using Wpn = DestructionSandbox.GameConfig.Weapons;

namespace DestructionSandbox
{
    /// <summary>
    /// Pools of short-lived effect objects: flash point lights (D-18: 0.15 s), expanding fresnel shockwave
    /// spheres and ground scorch decals that fade over 20 s. When a pool is full the oldest entry is reused.
    /// </summary>
    public sealed class TransientEffects : MonoBehaviour
    {
        private struct FlashSlot { public Light Light; public float Age; public float Duration; public float Intensity; }
        private struct WaveSlot { public Transform T; public Material Mat; public float Age; public float Duration; public float Radius; }
        private struct ScorchSlot { public Transform T; public Material Mat; public float Age; public float Strength; }

        private FlashSlot[] flashes;
        private WaveSlot[] waves;
        private ScorchSlot[] scorches;
        private int nextFlash, nextWave, nextScorch;

        private static readonly int FadeId = Shader.PropertyToID("_Fade");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmbersId = Shader.PropertyToID("_Embers");
        private static readonly int SeedId = Shader.PropertyToID("_Seed");

        public void Init(MaterialLibrary materials, MeshLibrary meshes)
        {
            flashes = new FlashSlot[Fx.FlashLightPool];
            for (int i = 0; i < flashes.Length; i++)
            {
                var go = new GameObject("FlashLight" + i);
                go.transform.SetParent(transform, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.enabled = false;
                go.AddComponent<UniversalAdditionalLightData>();
                flashes[i] = new FlashSlot { Light = light, Age = 1f, Duration = 0f };
            }

            waves = new WaveSlot[Fx.ShockwavePool];
            for (int i = 0; i < waves.Length; i++)
            {
                var go = new GameObject("Shockwave" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes.Sphere;
                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(materials.ShockwaveShader) { name = "Shockwave" + i };
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.SetActive(false);
                waves[i] = new WaveSlot { T = go.transform, Mat = mat, Age = 1f, Duration = 0f };
            }

            scorches = new ScorchSlot[Wpn.ScorchPool];
            for (int i = 0; i < scorches.Length; i++)
            {
                var go = new GameObject("Scorch" + i);
                go.transform.SetParent(transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = meshes.DecalQuad;
                var mr = go.AddComponent<MeshRenderer>();
                var mat = new Material(materials.ScorchShader) { name = "Scorch" + i };
                mat.SetFloat(SeedId, i * 3.17f);
                mr.sharedMaterial = mat;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                mr.receiveShadows = false;
                go.SetActive(false);
                scorches[i] = new ScorchSlot { T = go.transform, Mat = mat, Age = Wpn.ScorchFadeTime };
            }
        }

        public void Flash(Vector3 position, Color color, float intensity, float range, float duration)
        {
            ref FlashSlot f = ref flashes[nextFlash];
            nextFlash = (nextFlash + 1) % flashes.Length;
            f.Light.transform.position = position;
            f.Light.color = color;
            f.Light.range = range;
            f.Light.intensity = intensity;
            f.Light.enabled = true;
            f.Age = 0f;
            f.Duration = duration;
            f.Intensity = intensity;
        }

        public void Shockwave(Vector3 position, float radius, float duration, Color color)
        {
            ref WaveSlot w = ref waves[nextWave];
            nextWave = (nextWave + 1) % waves.Length;
            w.T.position = position;
            w.T.localScale = Vector3.one * 0.1f;
            w.Mat.SetColor(ColorId, color);
            w.Mat.SetFloat(FadeId, 1f);
            w.Age = 0f;
            w.Duration = duration;
            w.Radius = radius;
            w.T.gameObject.SetActive(true);
        }

        public void Scorch(Vector3 groundPoint, float size, float strength)
        {
            ref ScorchSlot s = ref scorches[nextScorch];
            nextScorch = (nextScorch + 1) % scorches.Length;
            s.T.SetPositionAndRotation(groundPoint + Vector3.up * 0.015f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            s.T.localScale = new Vector3(size, 1f, size);
            s.Age = 0f;
            s.Strength = strength;
            s.Mat.SetFloat(FadeId, strength);
            s.Mat.SetFloat(EmbersId, 1f);
            s.T.gameObject.SetActive(true);
        }

        public void ClearAll()
        {
            for (int i = 0; i < flashes.Length; i++) { flashes[i].Light.enabled = false; flashes[i].Age = flashes[i].Duration; }
            for (int i = 0; i < waves.Length; i++) { waves[i].T.gameObject.SetActive(false); waves[i].Age = waves[i].Duration; }
            for (int i = 0; i < scorches.Length; i++) { scorches[i].T.gameObject.SetActive(false); scorches[i].Age = Wpn.ScorchFadeTime; }
        }

        public int ActiveScorches()
        {
            int n = 0;
            for (int i = 0; i < scorches.Length; i++) if (scorches[i].T.gameObject.activeSelf) n++;
            return n;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < flashes.Length; i++)
            {
                ref FlashSlot f = ref flashes[i];
                if (!f.Light.enabled) continue;
                f.Age += dt;
                if (f.Age >= f.Duration) f.Light.enabled = false;
                else f.Light.intensity = f.Intensity * (1f - f.Age / f.Duration);
            }

            for (int i = 0; i < waves.Length; i++)
            {
                ref WaveSlot w = ref waves[i];
                if (!w.T.gameObject.activeSelf) continue;
                w.Age += dt;
                float t = w.Age / w.Duration;
                if (t >= 1f) { w.T.gameObject.SetActive(false); continue; }
                float ease = 1f - (1f - t) * (1f - t) * (1f - t);
                w.T.localScale = Vector3.one * Mathf.Max(0.1f, w.Radius * 2f * ease);
                w.Mat.SetFloat(FadeId, 1f - t);
            }

            for (int i = 0; i < scorches.Length; i++)
            {
                ref ScorchSlot s = ref scorches[i];
                if (!s.T.gameObject.activeSelf) continue;
                s.Age += dt;
                float t = s.Age / Wpn.ScorchFadeTime;          // D-18: fades over 20 s
                if (t >= 1f) { s.T.gameObject.SetActive(false); continue; }
                s.Mat.SetFloat(FadeId, s.Strength * (1f - t));
                s.Mat.SetFloat(EmbersId, Mathf.Clamp01(1f - s.Age / 3f));
            }
        }
    }
}
