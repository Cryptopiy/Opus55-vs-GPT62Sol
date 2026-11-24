using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Static access point to the systems that <see cref="GameBootstrap"/> builds on every scene load.
    /// </summary>
    public static class Game
    {
        public static GameManager Manager;
        public static PlayerRig Player;
        public static WaveManager Waves;
        public static EnemyManager Enemies;
        public static AudioManager Audio;
        public static FxManager Fx;
        public static ProjectilePool Projectiles;
        public static PickupManager Pickups;
        public static Hud Hud;
        public static GameStats Stats;
        public static ArenaInfo Arena;
        public static AiDebugView AiDebug;
        public static bool GodMode;

        public static bool IsPlaying => Manager != null && Manager.State == GameState.Playing;

        public static void Clear()
        {
            Manager = null; Player = null; Waves = null; Enemies = null; Audio = null; Fx = null;
            Projectiles = null; Pickups = null; Hud = null; Stats = null; Arena = null; AiDebug = null;
            GodMode = false;
            EnemyBase.DamageHook = null;
            EnemyBase.EventHook = null;
        }

        // The project disables domain reload on entering Play Mode, so statics must be reset explicitly.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Clear();
    }
}
