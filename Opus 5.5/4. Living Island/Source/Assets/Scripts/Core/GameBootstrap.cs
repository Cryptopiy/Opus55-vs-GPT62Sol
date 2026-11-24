using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace LivingIsland
{
    public static class Layers
    {
        public const int Water = 4;
        public const int Terrain = 8;
        public const int Player = 9;
        public const int Props = 10;
        public const int CameraCollisionMask = (1 << Terrain) | (1 << Props);
    }

    /// <summary>
    /// The only object in Island.unity. Builds the complete game by code on Awake (no manual editor
    /// setup), routes the global hotkeys (F1, F8, F12, Esc, Tab, P, T) and drives the shared clock.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public int CurrentSeed { get; private set; } = GameConfig.DefaultSeed;
        public float StartupSeconds { get; private set; }

        void Awake()
        {
            float t0 = Time.realtimeSinceStartup;
            Game.Clear();
            // The automated test hooks the log first so warnings raised during construction are counted too.
            if (AutoTest.Requested) gameObject.AddComponent<AutoTest>();
            QualitySettings.vSyncCount = 1;            // GPU safety + smooth pacing by default
            Application.targetFrameRate = -1;
            Application.runInBackground = true;
            Time.fixedDeltaTime = 1f / 60f;
            Time.timeScale = 1f;

            IslandAssets assets = Game.Assets = IslandAssets.Load();
            Game.Materials = new MaterialLibrary(assets);
            ShaderGlobals.PushStatic();

            // ------------------------------------------------------------ camera + light
            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = GameConfig.DefaultFov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 4000f;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            var camData = camGo.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.renderShadows = true;
            camData.dithering = true;
            camData.stopNaN = true;
            camGo.AddComponent<AudioListener>();
            Game.MainCamera = cam;

            var sunGo = new GameObject("Sun-Moon Light");
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowBias = 0.35f;
            sun.shadowNormalBias = 0.6f;
            var lightData = sunGo.AddComponent<UniversalAdditionalLightData>();
            lightData.softShadowQuality = SoftShadowQuality.High;
            lightData.usePipelineSettings = false;

            // ------------------------------------------------------------ sky, time, weather
            var systems = new GameObject("Systems");
            Game.TimeOfDay = systems.AddComponent<TimeOfDay>();
            Game.TimeOfDay.ComputeDirections();
            Game.Weather = systems.AddComponent<WeatherSystem>();
            Game.Lightning = new GameObject("Lightning").AddComponent<LightningSystem>();
            Game.Lightning.Init(Game.Weather, camGo.transform, Game.Materials.Lightning);
            Game.Atmosphere = systems.AddComponent<Atmosphere>();
            Game.Atmosphere.Init(Game.TimeOfDay, Game.Weather, Game.Lightning, sun, camGo.transform);
            Game.Sky = systems.AddComponent<SkyController>();
            Game.Sky.Init(assets.skyShader, assets.skyCubeShader, Game.TimeOfDay, Game.Weather);

            // ------------------------------------------------------------ island + ocean
            Game.World = new GameObject("Island").AddComponent<IslandWorld>();
            Game.World.Init(assets, Game.Materials);
            Game.World.Generate(CurrentSeed);
            var ocean = new GameObject("Ocean") { layer = Layers.Water }.AddComponent<WaterSurface>();
            ocean.Init(Game.Materials.Water, camGo.transform);

            // ------------------------------------------------------------ player + camera
            var playerGo = new GameObject("Player") { layer = Layers.Player };
            playerGo.AddComponent<CharacterController>();
            Game.Player = playerGo.AddComponent<PlayerController>();
            Game.Player.Init(Game.World.Data);
            Game.Player.CameraTransform = camGo.transform;
            Game.Avatar = playerGo.AddComponent<PlayerAvatar>();
            Game.Avatar.Build(Game.Player, Game.Materials.Skin, Game.Materials.Shirt, Game.Materials.Shorts, Game.Materials.Hair);
            Game.CameraRig = camGo.AddComponent<ThirdPersonCamera>();
            Game.CameraRig.Init(playerGo.transform, Game.World.Data, Layers.CameraCollisionMask);
            RespawnPlayer();

            // ------------------------------------------------------------ vegetation, FX, audio
            Game.Grass = new GameObject("Grass").AddComponent<GrassRenderer>();
            Game.Grass.Init(assets.grassCompute, Game.Materials.Grass, cam);
            Game.Rain = new GameObject("Rain").AddComponent<RainSystem>();
            Game.Rain.Init(Game.Materials, camGo.transform);
            Game.Fireflies = new GameObject("Fireflies").AddComponent<FireflySystem>();
            Game.Fireflies.Init(Game.Materials.Firefly, playerGo.transform);
            Game.SwimFx = new GameObject("Swim FX").AddComponent<SwimEffects>();
            Game.SwimFx.Init(Game.Materials.SwimSplash, Game.Player, Game.Avatar);
            Game.Audio = new GameObject("Audio").AddComponent<AudioSystem>();
            Game.Audio.Init(camGo.transform);

            // ------------------------------------------------------------ rendering features + UI
            Game.Post = new GameObject("Post FX Volume").AddComponent<PostFX>();
            Game.Post.Init();
            Game.Quality = systems.AddComponent<QualityManager>();
            Game.Quality.Apply(QualityPreset.High);
            Game.Photo = systems.AddComponent<PhotoMode>();
            Game.UI = new GameObject("UI").AddComponent<UIRoot>();
            Game.UI.Init();

            StartupSeconds = Time.realtimeSinceStartup - t0;
            Debug.Log("[LI] Startup complete in " + StartupSeconds.ToString("F2") + " s");
        }

        /// <summary>
        /// Shader warm-up behind a black fade-in (I-34): storm, rain, lightning bolt and depth of field are
        /// rendered for a few frames so their GPU programs exist before play, then the island fades in at
        /// clear noon.
        /// </summary>
        System.Collections.IEnumerator Start()
        {
            Game.UI.SetFade(1f);
            Game.Weather.SnapTo(WeatherState.Storm);
            Game.Lightning.WarmupBolt();
            Game.Post.DofEnabled = true;
            for (int i = 0; i < 3; i++) yield return null;
            Game.Post.DofEnabled = false;
            Game.Weather.SnapTo(WeatherState.Clear);
            Game.Rain.ClearAll();
            Game.Sky.RenderEnvironment();
            yield return null;
            const float fadeSeconds = 1.2f;
            for (float t = 0f; t < fadeSeconds; t += Time.unscaledDeltaTime)
            {
                Game.UI.SetFade(1f - t / fadeSeconds);
                yield return null;
            }
            Game.UI.SetFade(0f);
        }

        /// <summary>Places the player at the island's spawn point on the beach, looking at the bay.</summary>
        public void RespawnPlayer()
        {
            Vector3 p = Game.World.SpawnPoint;
            Game.Player.Teleport(p, Game.World.SpawnYaw);
            Game.CameraRig.Yaw = Game.World.SpawnYaw;
            Game.CameraRig.Pitch = 10f;
            Game.CameraRig.Snap();
        }

        /// <summary>Regenerates the island from a new seed (settings panel); returns the measured seconds.</summary>
        public float Regenerate(int seed)
        {
            CurrentSeed = seed;
            float seconds = Game.World.Generate(seed);
            Game.Player.SetIsland(Game.World.Data);
            Game.CameraRig.SetIsland(Game.World.Data);
            RespawnPlayer();
            return seconds;
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        void Update()
        {
            WaveModel.Time = Time.time;
            if (Game.Player != null)
                ShaderGlobals.PushFrame(Time.time, Game.Player.transform.position, Game.Player.IsSwimming);
            HandleHotkeys();
        }

        void HandleHotkeys()
        {
            if (Game.UI == null) return;
            // F8 hides / shows all interface (UIVisibility); read first, so it also works while paused or typing.
            if (GameInput.DownAlways(Key.F8)) UIVisibility.Toggle();
            if (GameInput.Down(Key.F1)) Game.UI.ToggleDebugOverlay();
            if (GameInput.Down(Key.F12))
            {
                if (Game.Photo.Active) Game.Photo.CaptureHighRes();
                else ScreenshotService.CaptureWindow();
            }
            if (GameInput.Down(Key.Escape))
            {
                if (Game.UI.SettingsOpen) Game.UI.SetSettingsOpen(false);
                else if (Game.Photo.Active) Game.Photo.SetActive(false);
                else Game.UI.SetPaused(!Game.Paused);
            }
            if (Game.Paused) return;
            if (GameInput.Down(Key.Tab) && !Game.Photo.Active) Game.UI.SetSettingsOpen(!Game.UI.SettingsOpen);
            if (GameInput.Down(Key.P)) Game.Photo.SetActive(!Game.Photo.Active);
            if (GameInput.Down(Key.T)) Game.TimeOfDay.Paused = !Game.TimeOfDay.Paused;
        }
    }
}
