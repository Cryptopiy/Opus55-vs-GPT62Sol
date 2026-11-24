using UnityEngine;

namespace LivingIsland
{
    /// <summary>Registry of the running systems (filled by <see cref="GameBootstrap"/>).</summary>
    public static class Game
    {
        public static IslandAssets Assets;
        public static Camera MainCamera;
        public static IslandWorld World;
        public static TimeOfDay TimeOfDay;
        public static WeatherSystem Weather;
        public static LightningSystem Lightning;
        public static Atmosphere Atmosphere;
        public static SkyController Sky;
        public static PlayerController Player;
        public static PlayerAvatar Avatar;
        public static ThirdPersonCamera CameraRig;
        public static GrassRenderer Grass;
        public static RainSystem Rain;
        public static FireflySystem Fireflies;
        public static SwimEffects SwimFx;
        public static AudioSystem Audio;
        public static PostFX Post;
        public static QualityManager Quality;
        public static PhotoMode Photo;
        public static UIRoot UI;
        public static MaterialLibrary Materials;

        /// <summary>True while the pause menu is open (time scale 0).</summary>
        public static bool Paused;

        public static void Clear()
        {
            Assets = null; MainCamera = null; World = null; TimeOfDay = null; Weather = null; Lightning = null;
            Atmosphere = null; Sky = null; Player = null; Avatar = null; CameraRig = null; Grass = null; Rain = null;
            Fireflies = null; SwimFx = null; Audio = null; Post = null; Quality = null; Photo = null; UI = null;
            Materials = null; Paused = false;
            GameInput.TextEntryActive = false;
            GameInput.ScriptedMove = Vector2.zero;
            GameInput.ScriptedRun = false;
            GameInput.ScriptedJump = false;
        }
    }
}
