using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed partial class Arena
    {
        readonly Renderer[] gateSignals = new Renderer[GateCount];
        readonly Light[] ceilingLights = new Light[LightCount];
        readonly MaterialPropertyBlock gateTint = new();
        readonly Material[] gateIdMaterials = new Material[GateCount];
        static readonly int Emissive = Shader.PropertyToID("_EmissionColor");

        partial void DecorateGate(Transform frame, int index)
        {
            Material signal = Geometry.Lit("Gate beacon", Geometry.Hex(0xff9853), .3f, .2f, 1);
            gateSignals[index] = Geometry.Part(frame, "Spawn warning lamp", new(0, 3.26f, .49f),
                new(2.2f, .095f, .08f), signal).GetComponent<Renderer>();
            for (int sign = -1; sign < 2; sign += 2)
            {
                Geometry.Part(frame, "Jamb accent", new(sign * 1.85f, 1.63f, .46f), new(.05f, 2.65f, .045f), Cyan);
                Geometry.Part(frame, "Hydraulic channel", new(sign * .95f, 1.38f, .13f), new(.075f, 2.45f, .08f), Metal);
                var brace = Geometry.Part(frame, "Diagonal door brace", new(sign * .48f, 1.52f, .145f),
                    new(.13f, 1.60f, .085f), Metal);
                brace.localRotation = Quaternion.Euler(0, 0, sign * 33);
            }
            gateIdMaterials[index] = Stencil("GATE 0" + (index + 1), Geometry.Hex(0x15262b), Geometry.Hex(0x91c5bf));
            Geometry.Part(frame, "Gate identification", new(0, 2.46f, .22f), new(1.18f, .31f, 1), gateIdMaterials[index], PrimitiveType.Quad)
                .localRotation = Quaternion.Euler(0, 180, 0);
        }

        partial void DecorateCover(Transform cover, bool round)
        {
            if (round)
            {
                for (int s = -1; s <= 1; s += 2)
                    Geometry.Part(cover, "Column collar", new(0, s * .85f, 0), new(1.09f, .046f, 1.09f), Dark, PrimitiveType.Cylinder);
                return;
            }
            bool crate = cover.localScale.z > 1;
            for (int side = -1; side <= 1; side += 2)
            {
                Geometry.Part(cover, "Reinforced lip", new(0, side * .43f, 0), new(1.02f, .108f, 1.04f), Dark);
                for (int panel = -1; panel <= 1; panel += 2)
                    Geometry.Part(cover, "Inset load rail", new(panel * .34f, 0, side * .514f),
                        new(.055f, .72f, .027f), crate ? Dark : Metal);
                Geometry.Part(cover, "Safety marker", new(0, .22f, side * .521f),
                    new(crate ? .32f : .51f, .055f, .041f), crate ? Cyan : Amber);
            }
        }

        partial void Decorate()
        {
            var concreteTex = ConcreteTexture();
            Concrete.SetTexture("_BaseMap", concreteTex);
            Metal.SetTexture("_BaseMap", PanelTexture());
            var seam = Geometry.Lit("Concrete shadow reveal", Geometry.Hex(0x202a2a));
            var trim = Geometry.Lit("Zinc fasteners", Geometry.Hex(0x66787c), .5f, .4f);
            var road = Geometry.Lit("Safety paint", Geometry.Hex(0xb17c34), .46f, .05f, .19f);
            var stripe = Geometry.Lit("Lane paint", Geometry.Hex(0x6b8b86), .29f, .04f, .12f);
            var ceiling = Geometry.Lit("Ceiling cladding", Geometry.Hex(0x2c383a), .3f, .3f);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Geometry.Hex(0x708c94);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = .017f;
            RenderSettings.fogColor = Geometry.Hex(0x283b44);

            Geometry.Part(Root, "Roof cladding", new(0, WallHeight + .19f, 0), new(ArenaSize, .4f, ArenaSize), ceiling);
            for (int i = -2; i <= 2; i++)
            {
                Geometry.Part(Root, "Roof crossbeam", new(0, 5.71f, i * 10), new(ArenaSize, .35f, .68f), Dark);
                Geometry.Part(Root, "Overhead conduit", new(i * 9.25f, 5.77f, 0), new(.27f, .29f, ArenaSize), Metal);
                for (int j = -2; j <= 2; j++)
                {
                    Geometry.Part(Root, "Deck expansion joint", new(i * 10, .003f, 0), new(.057f, .007f, ArenaSize), seam);
                    Geometry.Part(Root, "Deck alignment mark", new(i * 10, .009f, j * 10), new(.20f, .01f, .20f), trim);
                }
                Geometry.Part(Root, "Deck expansion joint", new(0, .003f, i * 10), new(ArenaSize, .007f, .057f), seam);
            }
            for (int k = 0; k < 4; k++)
            {
                Transform wall = Geometry.Root("Wall details", Root);
                wall.localRotation = Quaternion.Euler(0, k * 90, 0);
                Geometry.Part(wall, "Kick plate", new(0, .26f, Half - .07f), new(ArenaSize, .52f, .15f), Dark);
                Geometry.Part(wall, "Utility rail", new(0, 4.22f, Half - .14f), new(ArenaSize, .2f, .28f), Metal);
                Geometry.Part(wall, "Utility rail tracer", new(0, 4.21f, Half - .291f), new(ArenaSize, .032f, .016f), Cyan);
                for (int panel = -4; panel <= 4; panel++)
                {
                    float x = panel * 5.5f;
                    Geometry.Part(wall, "Precast vertical seam", new(x, 3, Half - .011f), new(.055f, 6, .029f), seam);
                }
                Geometry.Part(wall, "Precast horizontal seam", new(0, 3.58f, Half - .011f), new(ArenaSize, .057f, .029f), seam);
                Material wallMark = Stencil("BASTION 06", Geometry.Hex(0x213338), Geometry.Hex(0x79a7a7));
                Geometry.Part(wall, "Sector designation", new(k % 2 == 0 ? 0 : 11, 4.91f, Half - .04f),
                    new(4.5f, .72f, 1), wallMark, PrimitiveType.Quad);
                for (int j = 0; j < 9; j++)
                {
                    float d = -7.2f + j * 1.8f;
                    var hazard = Geometry.Part(wall, "Deck safety chevron", new(d, .013f, 9.66f), new(.42f, .012f, .135f), road);
                    hazard.localRotation = Quaternion.Euler(0, -35, 0);
                }
                Geometry.Part(wall, "Containment lane", new(-10, .011f, 9.4f), new(.065f, .01f, 16), stripe);
            }
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -5.02f : 5.02f;
                float z = i < 2 ? -3.72f : 3.72f;
                Geometry.Part(Root, "Platform corner marker", new(x, 1.452f, z), new(.07f, .09f, 2.1f), Amber);
            }
            for (int sign = -1; sign <= 1; sign += 2)
            {
                for (int i = 0; i < 4; i++)
                {
                    float u = (i + .5f) / 4;
                    Vector3 position = new(0, .015f + u * PlatformHeight, sign * (PlatformSize * .5f + (1 - u) * RampRun));
                    var rung = Geometry.Part(Root, "Ramp anti-slip stripe", position, new(3.39f, .035f, .09f), i == 3 ? road : seam);
                    rung.localRotation = Quaternion.Euler(sign * Mathf.Atan2(PlatformHeight, RampRun) * Mathf.Rad2Deg, 0, 0);
                }
            }

            for (int i = 0; i < LightCount; i++)
            {
                float sign = i % 2 == 0 ? -1 : 1;
                Vector3 point = new(sign * (i < 4 ? 7 : 18), 5.32f, (i / 2 % 2 == 0 ? -1 : 1) * (i < 4 ? 8 : 16));
                var fixture = Geometry.Root("Industrial point fixture", Root);
                fixture.position = point;
                Geometry.Part(fixture, "Pendant housing", new(0, .15f, 0), new(2.7f, .35f, .47f), Dark);
                Material diffuser = i < 4 ? Cyan : Amber;
                Geometry.Part(fixture, "Light diffuser", new(0, -.05f, 0), new(2.5f, .08f, .29f), diffuser);
                var lamp = fixture.gameObject.AddComponent<Light>();
                lamp.type = LightType.Point;
                lamp.color = i < 4 ? Geometry.Hex(0xa2f3ff) : Geometry.Hex(0xffc084);
                lamp.intensity = i < 4 ? 12.5f : 9.2f;
                lamp.range = i < 4 ? 24 : 19;
                lamp.shadows = LightShadows.None;
                ceilingLights[i] = lamp;
            }
            var post = Geometry.Root("Color grade and optics", Root).gameObject.AddComponent<Volume>();
            post.isGlobal = true;
            post.priority = 1;
            var profile = Geometry.Own(ScriptableObject.CreateInstance<VolumeProfile>());
            post.sharedProfile = profile;
            var bloom = Geometry.Own(profile.Add<Bloom>());
            bloom.threshold.Override(1.1f);
            bloom.intensity.Override(.36f);
            bloom.scatter.Override(.52f);
            var vignette = Geometry.Own(profile.Add<Vignette>());
            vignette.intensity.Override(.22f);
            vignette.smoothness.Override(.68f);
            var tone = Geometry.Own(profile.Add<Tonemapping>());
            tone.mode.Override(TonemappingMode.ACES);
            var grade = Geometry.Own(profile.Add<ColorAdjustments>());
            grade.postExposure.Override(.23f);
            grade.contrast.Override(10);
            grade.saturation.Override(-8);
        }

        public void UpdateLighting(int activeGate, float countdown)
        {
            for (int g = 0; g < gateSignals.Length; g++)
            {
                float pulse = g == activeGate ? 2.4f + Mathf.Sin((1 - countdown / GateWarning) * 35) * 2.1f : .28f;
                gateTint.SetColor(Emissive, Geometry.Hex(0xff833a) * pulse);
                gateSignals[g].SetPropertyBlock(gateTint);
            }
            for (int i = 0; i < FlickerCount; i++)
            {
                float signal = Mathf.PerlinNoise(Time.time * (3.8f + i), 12.1f + i);
                ceilingLights[LightCount - 1 - i].intensity = 9.2f * (signal > .42f ? 1 : Mathf.Lerp(.09f, .8f, signal));
            }
        }

        static Texture2D ConcreteTexture()
        {
            const int size = 256;
            var texture = Geometry.Own(new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Procedural aggregate" });
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float p = Mathf.PerlinNoise(x * .035f, y * .048f);
                    float stains = Mathf.PerlinNoise(x * .014f + 80, y * .016f + 4);
                    int grain = ((x * 3533 ^ y * 2753 ^ (x + y) * (x - y) * 97) & 31) - 15;
                    int level = Mathf.Clamp((int)(169 + p * 38 + stains * 47 + grain), 0, 255);
                    pixels[y * size + x] = new Color32((byte)level, (byte)level, (byte)level, 255);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            texture.anisoLevel = 4;
            return texture;
        }

        static Texture2D PanelTexture()
        {
            const int size = 96;
            var texture = Geometry.Own(new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Procedural steel panel" });
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int edge = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    int level = edge < 2 ? 76 : edge < 4 ? 133 : 204 + ((y * 13 ^ x * 51) & 23);
                    if (x % 16 < 2 && y % 16 < 2) level = 137;
                    pixels[y * size + x] = new Color32((byte)level, (byte)level, (byte)level, 255);
                }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            return texture;
        }

        static Material Stencil(string label, Color bg, Color fg)
        {
            const string letters = "ABEGINOST0123456789";
            string[] shapes =
            {
                "01110100011000111111100011000110001", "11110100011000111110100011000111110",
                "11111100001000011110100001000011111", "01110100011000010111100011000101110",
                "11111001000010000100001000010011111", "10001110011010110011100011000110001",
                "01110100011000110001100011000101110", "01111100001000001110000010000111110",
                "11111001000010000100001000010000100", "01110100011001110101110011000101110",
                "00100011000010000100001000010001110", "01110100010000100010001000100011111",
                "11110000010000101110000010000111110", "00010001100101010010111110001000010",
                "11111100001000011110000010000111110", "01110100001000011110100011000101110",
                "11111000010001000100010000100001000", "01110100011000101110100011000101110",
                "01110100011000101111000010000101110"
            };
            const int width = 160, height = 36, scale = 2;
            var texture = Geometry.Own(new Texture2D(width, height, TextureFormat.RGBA32, true) { name = label, filterMode = FilterMode.Point });
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = bg;
            int x0 = (width - label.Length * 6 * scale) / 2;
            for (int i = 0; i < label.Length; i++)
            {
                int at = letters.IndexOf(label[i]);
                if (at < 0) continue;
                for (int y = 0; y < 7 * scale; y++)
                    for (int x = 0; x < 5 * scale; x++)
                        if (shapes[at][y / scale * 5 + x / scale] == '1')
                            pixels[(24 - y) * width + x0 + i * 6 * scale + x] = fg;
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var material = Geometry.Lit(label, Color.white, .2f, .05f);
            material.SetTexture("_BaseMap", texture);
            return material;
        }
    }
}




