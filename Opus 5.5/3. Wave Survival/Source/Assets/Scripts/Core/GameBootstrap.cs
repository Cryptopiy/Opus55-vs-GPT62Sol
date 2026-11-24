using UnityEngine;
using UnityEngine.Rendering;

namespace WaveSurvival
{
    /// <summary>
    /// The only object in the main scene. Builds the whole game from code on load (section 4: "starts directly into
    /// playable state with no manual setup"): assets, arena, lighting, NavMesh, player, systems, HUD and debug tools.
    /// </summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        VolumeProfile postProfile;

        void Awake()
        {
            QualitySettings.vSyncCount = 1;          // GPU safety: VSync on by default
            Application.targetFrameRate = -1;
            Physics.simulationMode = SimulationMode.FixedUpdate;
            ConfigureCollisionMatrix();
            Game.Clear();

            GameAssets.Ensure();
            SoundLibrary.Ensure();

            var root = new GameObject("Game").transform;
            var arenaRoot = new GameObject("Arena");
            arenaRoot.transform.SetParent(root, false);
            Game.Arena = ArenaBuilder.Build(arenaRoot.transform);
            postProfile = LightingSetup.Apply(root);
            Physics.SyncTransforms();
            ArenaNavMesh.Build(arenaRoot, Game.Arena);

            Game.Stats = new GameStats();
            Game.Manager = GameManager.Create(root);
            Game.Audio = AudioManager.Create(root);
            Game.Fx = FxManager.Create(root);
            Game.Projectiles = ProjectilePool.Create(root);
            Game.Pickups = PickupManager.Create(root);
            Game.Enemies = EnemyManager.Create(root);
            Game.Waves = WaveManager.Create(root);
            root.gameObject.AddComponent<InputRouter>();

            Vector3 spawn = GameConfig.Arena.PlayerSpawn;
            Game.Player = PlayerRig.Create(spawn, 0f);
            Game.Hud = Hud.Create(root);
            Game.AiDebug = AiDebugView.Create(root);
            UiVisibility.Create(root);
            DevTools.Create(root);
            AutoTest.TryStart(root);

            Game.Manager.StartGame();
        }

        /// <summary>Layer collision rules: shells only hit the world, hitboxes never push the player, etc.</summary>
        static void ConfigureCollisionMatrix()
        {
            int[] all = { GameConfig.Layers.Default, GameConfig.Layers.Player, GameConfig.Layers.Enemy, GameConfig.Layers.Hitbox,
                          GameConfig.Layers.Viewmodel, GameConfig.Layers.Pickup, GameConfig.Layers.Debris };
            foreach (int a in all)
            foreach (int b in all)
                Physics.IgnoreLayerCollision(a, b, false);
            Physics.IgnoreLayerCollision(GameConfig.Layers.Player, GameConfig.Layers.Hitbox, true);
            Physics.IgnoreLayerCollision(GameConfig.Layers.Player, GameConfig.Layers.Pickup, true);
            Physics.IgnoreLayerCollision(GameConfig.Layers.Player, GameConfig.Layers.Debris, true);
            Physics.IgnoreLayerCollision(GameConfig.Layers.Player, GameConfig.Layers.Viewmodel, true);
            foreach (int l in all)
            {
                if (l != GameConfig.Layers.Default) Physics.IgnoreLayerCollision(GameConfig.Layers.Debris, l, true);
                if (l != GameConfig.Layers.Player) Physics.IgnoreLayerCollision(GameConfig.Layers.Enemy, l, true);
                Physics.IgnoreLayerCollision(GameConfig.Layers.Hitbox, l, true); // hitboxes are query-only (raycasts)
                Physics.IgnoreLayerCollision(GameConfig.Layers.Viewmodel, l, true);
                Physics.IgnoreLayerCollision(GameConfig.Layers.Pickup, l, true);
            }
        }

        void OnDestroy()
        {
            // Statics are reset by the next bootstrap's Awake (destruction order across a scene reload is not relied on).
            if (postProfile != null) Destroy(postProfile);
        }
    }
}
