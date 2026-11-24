using UnityEngine;
using UnityEngine.Rendering;
using C = LivingIsland.GameConfig;

namespace LivingIsland
{
    /// <summary>
    /// Procedural sky (I-14) and environment reflections (I-16):
    /// - owns the skybox material (gradient, sun, moon, stars, two cloud layers in Sky.shader);
    /// - builds the 1500-star lookup texture (one star per equal-area sky cell, upper hemisphere only);
    /// - accumulates wind-driven cloud scrolling;
    /// - re-renders a 128^2 HDR environment cubemap from the same sky shader every 0.5 s
    ///   (procedural substitute for a reflection probe that follows the time of day and weather).
    /// </summary>
    public sealed class SkyController : MonoBehaviour
    {
        public Material SkyMaterial { get; private set; }
        public RenderTexture EnvCube { get; private set; }
        public int StarCount { get; private set; }

        const int StarCellsU = 128, StarCellsV = 48;
        TimeOfDay tod;
        WeatherSystem weather;
        CommandBuffer cmd;
        float refreshTimer;
        Vector4 cloudOffsets;

        Material cubeMaterial;

        public void Init(Shader skyShader, Shader skyCubeShader, TimeOfDay timeOfDay, WeatherSystem weatherSystem)
        {
            tod = timeOfDay;
            weather = weatherSystem;
            SkyMaterial = new Material(skyShader) { name = "IslandSky" };
            cubeMaterial = new Material(skyCubeShader) { name = "IslandSkyCube" };
            RenderSettings.skybox = SkyMaterial;
            Shader.SetGlobalTexture(ShaderIds.StarTex, BuildStarTexture());

            EnvCube = new RenderTexture(C.EnvCubemapSize, C.EnvCubemapSize, 0, RenderTextureFormat.ARGBHalf)
            {
                name = "IslandEnvCube",
                dimension = TextureDimension.Cube,
                useMipMap = true,
                autoGenerateMips = false,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            EnvCube.Create();
            Shader.SetGlobalTexture(ShaderIds.EnvCube, EnvCube);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = EnvCube;
            cmd = new CommandBuffer { name = "Island Env Cubemap" };
        }

        Texture2D BuildStarTexture()
        {
            // Equal-area cells: u = azimuth, v = sin(elevation). Rows 3..46 keep stars between ~3.6 and ~78 deg,
            // above the horizon haze and away from the pole where cells get too narrow.
            var tex = new Texture2D(StarCellsU, StarCellsV, TextureFormat.RGBA32, false, true)
            {
                name = "StarCells", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat
            };
            var pixels = new Color32[StarCellsU * StarCellsV];
            var rng = new DetRandom(C.DefaultSeed, 4242);
            int placed = 0, guard = 0;
            while (placed < C.StarCount && guard++ < 200000)
            {
                int cu = rng.Range(0, StarCellsU);
                int cv = rng.Range(3, StarCellsV - 1);
                int idx = cv * StarCellsU + cu;
                if (pixels[idx].b != 0) continue;
                float bright = 0.12f + 0.88f * Mathf.Pow(rng.Value, 5f); // few bright, many faint
                pixels[idx] = new Color32((byte)rng.Range(64, 192), (byte)rng.Range(64, 192),
                                          (byte)Mathf.Clamp(Mathf.RoundToInt(bright * 255f), 1, 255), (byte)rng.Range(0, 256));
                placed++;
            }
            StarCount = placed;
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return tex;
        }

        void LateUpdate()
        {
            if (tod == null) return;
            float dt = Time.deltaTime;
            WeatherParams w = weather.Current;
            Vector2 wind = C.WindDirection.normalized;
            float speed = 1f + w.Wind * 0.8f;
            // Offsets accumulate so changing wind speed never makes the clouds jump.
            cloudOffsets.x += wind.x * speed * dt * 9f;
            cloudOffsets.y += wind.y * speed * dt * 9f;
            cloudOffsets.z += (wind.x * 0.6f - wind.y * 0.4f) * speed * dt * 16f;
            cloudOffsets.w += (wind.y * 0.6f + wind.x * 0.4f) * speed * dt * 16f;
            Shader.SetGlobalVector(ShaderIds.CloudOffsets, cloudOffsets);
            // Stars fade in with the night and disappear behind thick fog.
            float thickFog = Mathf.Clamp01(w.FogDensity / (3.912f / 300f));
            Shader.SetGlobalVector(ShaderIds.StarParams, new Vector4(tod.NightFactor * (1f - thickFog), tod.Hour * 0.0436f, StarCellsU, StarCellsV));

            refreshTimer -= dt;
            if (refreshTimer <= 0f)
            {
                refreshTimer = C.EnvCubemapRefreshSeconds;
                RenderEnvironment();
            }
        }

        /// <summary>Renders all six faces with the sky shader's cube pass, then builds the roughness mips.</summary>
        public void RenderEnvironment()
        {
            cmd.Clear();
            for (int face = 0; face < 6; face++)
            {
                cmd.SetRenderTarget(EnvCube, 0, (CubemapFace)face, 0);
                cmd.SetGlobalFloat(ShaderIds.CubeFace, face);
                cmd.DrawProcedural(Matrix4x4.identity, cubeMaterial, 0, MeshTopology.Triangles, 3, 1);
            }
            cmd.GenerateMips(EnvCube);
            Graphics.ExecuteCommandBuffer(cmd);
        }

        void OnDestroy()
        {
            if (EnvCube != null) EnvCube.Release();
            cmd?.Release();
        }
    }
}
