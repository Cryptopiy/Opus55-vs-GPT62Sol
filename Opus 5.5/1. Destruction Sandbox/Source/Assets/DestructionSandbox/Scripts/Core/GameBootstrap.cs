using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// The only object in the main scene. Builds the whole game from code at start-up: settings, materials,
    /// meshes, world, player, pools (blocks, shards, projectiles, effects, audio voices, UI pop-ups) and UI,
    /// then spawns the default structure (Tower, F5). Shader references are assigned by the editor scene builder
    /// so the shaders are always included in player builds.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        public Shader BlockShader;
        public Shader GlassShader;
        public Shader GroundShader;
        public Shader SkyShader;
        public Shader ParticleShader;
        public Shader ShockwaveShader;
        public Shader OrbShader;
        public Shader ScorchShader;

        private void Awake()
        {
            Game.Root = this;
            RuntimeSettings.Apply();

            Game.Meshes = new MeshLibrary(GameConfig.World.GroundSize);
            Game.Materials = new MaterialLibrary(this);
            MaterialLibrary mats = Game.Materials;
            MeshLibrary meshes = Game.Meshes;

            gameObject.AddComponent<FrameProbe>();
            gameObject.AddComponent<PhysicsStepTimer>();
            Game.Input = gameObject.AddComponent<GameInput>();
            WorldBuilder.Build(transform, mats, meshes);

            Game.Player = PlayerController.Create(transform);
            Game.Audio = CreateSystem<AudioManager>("Audio");
            Game.Audio.Init(Game.MainCamera.transform);

            Game.Effects = CreateSystem<EffectsManager>("Effects");
            Game.Effects.Init(mats, meshes, Game.GroundTransform);

            Game.Shards = CreateSystem<ShardManager>("Shards");
            Game.Shards.Init(mats, meshes);
            Game.Projectiles = CreateSystem<ProjectileManager>("Projectiles");
            Game.Projectiles.Init(mats, meshes);
            Game.Explosions = CreateSystem<ExplosionSystem>("Explosions");
            Game.Orbs = CreateSystem<GravityOrbSystem>("GravityOrbs");
            Game.Orbs.Init(mats, meshes);
            Game.Fracture = CreateSystem<FractureSystem>("Fracture");
            Game.Score = CreateSystem<ScoreSystem>("Score");
            Game.SlowMo = CreateSystem<SlowMotion>("SlowMotion");
            Game.Structures = CreateSystem<StructureManager>("Structure");
            Game.Structures.Init(mats, meshes);

            CannonView cannon = CannonView.Create(Game.MainCamera.transform, mats, meshes);
            Game.Weapons = CreateSystem<WeaponController>("Weapons");
            Game.Weapons.Init(cannon);

            var ui = new GameObject("UI").transform;
            ui.SetParent(transform, false);
            Game.Ui = ui.gameObject.AddComponent<UiVisibility>();   // before any canvas: they register with it
            Game.Hud = ui.gameObject.AddComponent<Hud>();
            Game.Hud.Build(ui);
            Game.Popups = ui.gameObject.AddComponent<FloatingTextPool>();
            Game.Popups.Build(ui);
            Game.Overlay = ui.gameObject.AddComponent<DebugOverlay>();
            Game.Overlay.Build(ui);
            Game.Pause = ui.gameObject.AddComponent<PauseMenu>();
            Game.Pause.Build(ui);

            Game.Controller = gameObject.AddComponent<GameController>();
            Game.Player.Spawn();
            Game.Structures.Select(0);          // D-04 default structure: Tower (F5)
            GameController.LockCursor();

            if (AutoTest.Requested(out string scenario))
                gameObject.AddComponent<AutoTest>().Begin(scenario);
        }

        private T CreateSystem<T>(string name) where T : Component
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            return go.AddComponent<T>();
        }
    }
}
