using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// Static access point to the running systems. Filled by <see cref="GameBootstrap"/>; cleared on subsystem
    /// registration so stale references never survive a play-mode restart without domain reload.
    /// </summary>
    public static class Game
    {
        public static GameBootstrap Root;
        public static MaterialLibrary Materials;
        public static MeshLibrary Meshes;
        public static AudioManager Audio;
        public static EffectsManager Effects;
        public static ShardManager Shards;
        public static StructureManager Structures;
        public static FractureSystem Fracture;
        public static ProjectileManager Projectiles;
        public static ExplosionSystem Explosions;
        public static GravityOrbSystem Orbs;
        public static WeaponController Weapons;
        public static PlayerController Player;
        public static CameraShake Shake;
        public static ScoreSystem Score;
        public static SlowMotion SlowMo;
        public static GameInput Input;
        public static Hud Hud;
        public static PauseMenu Pause;
        public static DebugOverlay Overlay;
        public static FloatingTextPool Popups;
        public static UiVisibility Ui;
        public static GameController Controller;
        public static Camera MainCamera;
        public static Transform GroundTransform;

        /// <summary>Session start in unscaled time (pause time is subtracted by the controller).</summary>
        public static float SessionTime;

        public static bool IsPaused => Pause != null && Pause.IsOpen;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Root = null; Materials = null; Meshes = null; Audio = null; Effects = null; Shards = null;
            Structures = null; Fracture = null; Projectiles = null; Explosions = null; Orbs = null;
            Weapons = null; Player = null; Shake = null; Score = null; SlowMo = null; Input = null;
            Hud = null; Pause = null; Overlay = null; Popups = null; Ui = null; Controller = null; MainCamera = null;
            GroundTransform = null; SessionTime = 0f;
        }
    }
}
