using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static DestructionRange.SandboxConfig;

namespace DestructionRange
{
    public sealed partial class Sandbox
    {
        partial void GroundStyle(Material ground, Material border)
        {
            ground.SetTexture("_BaseMap", SurfaceTexture("Arena grid", 512, Tile.Grid));
            ground.SetTextureScale("_BaseMap", Vector2.one * (ArenaSize / GridMajor));
            border.SetTexture("_BaseMap", SurfaceTexture("Concrete", 128, Tile.Concrete));
            border.SetTextureScale("_BaseMap", new Vector2(18, 1));
        }

        partial void SkyStyle()
        {
            var sky = new Material(Shader.Find("Destruction Range/Gradient Sky")) { name = "Air above the range" };
            Color upper = new(.16f, .32f, .45f);
            Color horizon = new(.64f, .76f, .80f);
            Color lower = new(.18f, .23f, .25f);
            sky.SetColor("_Zenith", upper);
            sky.SetColor("_Horizon", horizon);
            sky.SetColor("_Nadir", lower);
            Vector3 sun = -RenderSettings.sun.transform.forward;
            sky.SetVector("_SunDirection", sun);
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            var probe = new SphericalHarmonicsL2();
            // Low-order irradiance from the same gradient as the skybox, with no runtime cubemap bake.
            probe.AddAmbientLight(horizon.linear * .33f);
            probe.AddDirectionalLight(Vector3.up, upper.linear, 1.4f);
            probe.AddDirectionalLight(Vector3.down, lower.linear, .68f);
            RenderSettings.ambientProbe = probe;

            var volume = Node("Range post-processing").AddComponent<Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.Add<Bloom>().intensity.Override(SandboxConfig.Bloom);
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.ACES);
            profile.Add<Vignette>().intensity.Override(SandboxConfig.Vignette);
            volume.sharedProfile = profile;
        }

        partial void SurfaceStyle(Material[,] materials)
        {
            var stone = SurfaceTexture("Stone speckle", 160, Tile.Stone);
            var wood = SurfaceTexture("Wood grain", 160, Tile.Wood);
            var glass = Shader.Find("Destruction Range/Edge Glass");
            for (int i = 0; i < materials.GetLength(1); i++)
            {
                materials[(int)Matter.Stone, i].SetTexture("_BaseMap", stone);
                materials[(int)Matter.Wood, i].SetTexture("_BaseMap", wood);
                var pane = materials[(int)Matter.Glass, i];
                Color tint = pane.color;
                pane.shader = glass;
                pane.SetColor("_BaseColor", tint);
            }
        }

        internal Material EffectMaterial(string name, float mode, Color color)
        {
            var material = new Material(Shader.Find("Destruction Range/Effects")) { name = name };
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Mode", mode);
            return material;
        }

        enum Tile { Grid, Concrete, Stone, Wood }

        static Texture2D SurfaceTexture(string name, int size, Tile type)
        {
            var pixels = new Color32[size * size];
            uint noise = 0x918be315u;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                noise ^= noise << 13;
                noise ^= noise >> 17;
                noise ^= noise << 5;
                float random = (noise & 255u) / 255f;
                float u = x / (float)size, v = y / (float)size;
                float stone = Mathf.PerlinNoise(u * 14 + 11, v * 14);
                float fine = Mathf.PerlinNoise(u * 47, v * 47 + 12);
                float low = Mathf.PerlinNoise(u * 4, v * 4 + 17);
                float bevel = Mathf.Clamp01(Mathf.Min(Mathf.Min(u, v), Mathf.Min(1 - u, 1 - v)) * 51);
                float value;
                if (type == Tile.Grid)
                {
                    float unit = GridMajor / GridCell;
                    float a = Mathf.Abs(u * unit - Mathf.Round(u * unit));
                    float b = Mathf.Abs(v * unit - Mathf.Round(v * unit));
                    bool heavy = Mathf.Min(Mathf.Min(u, v), Mathf.Min(1 - u, 1 - v)) < .0048f;
                    bool line = Mathf.Min(a, b) < .023f;
                    value = heavy ? .45f : line ? .68f : .89f + low * .08f + random * .035f;
                }
                else if (type == Tile.Wood)
                {
                    float bow = Mathf.PerlinNoise(u * 3.0f + 2, v * 5f);
                    float grain = .5f + .5f * Mathf.Sin(115 * v + 9 * bow + 4 * low);
                    value = .57f + .24f * grain + .105f * fine + .055f * random;
                    value *= .69f + bevel * .31f;
                }
                else
                {
                    float pit = random > .981f ? -.21f : 0;
                    value = .62f + low * .19f + stone * .17f + fine * .10f + pit;
                    if (type == Tile.Stone) value *= .69f + bevel * .31f;
                }
                byte grey = (byte)(Mathf.Clamp01(value) * 255);
                pixels[y * size + x] = new Color32(grey, grey, grey, 255);
            }
            var map = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                name = name, wrapMode = TextureWrapMode.Repeat, anisoLevel = 8, filterMode = FilterMode.Trilinear
            };
            map.SetPixels32(pixels);
            map.Apply(true, true);
            return map;
        }
    }
}
