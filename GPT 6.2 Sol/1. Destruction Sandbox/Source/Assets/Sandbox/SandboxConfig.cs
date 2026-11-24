using UnityEngine;

namespace DestructionRange
{
    internal enum Matter { Stone, Wood, Glass }
    internal enum Layout { Tower, Arch, Pyramid }
    internal enum Weapon { Cannon, Shell, Orb }

    internal static class SandboxConfig
    {
        public const int FrameWidth = 1920, FrameHeight = 1080;
        public const int Solver = 12, VelocitySolver = 4, VSync = 1;
        public const int DebrisSolver = 6, DebrisVelocitySolver = 2, AcceptanceCannonShots = 5;
        public const float FixedStep = 1f / 60f, SlowScale = .25f, TopSpeed = 80f;
        public const float SleepSpeed = .05f, ContactOffset = .004f;
        public const float ArenaSize = 80f, BorderHeight = 1f, InvisibleWallHeight = 360f, ShadowDistance = 80f;
        public const float GridCell = 1f, GridMajor = 10f, Bloom = .6f, Vignette = .2f;
        public static readonly Vector3 Spawn = new(0, 1.8f, -28), Focus = new(0, 6, 0);
        public const int TowerWidth = 4, TowerFloors = 24, StoneFloors = 8, WoodEndFloor = 18;
        public static readonly Vector3 TowerBlock = new(1, .5f, 1);
        public const float TowerBond = .16f;
        public const int WallWidth = 24, WallRows = 12, WindowsStart = 8, WindowsEnd = 9;
        public static readonly Vector3 WallBrick = new(1, .5f, .5f);
        public const float ArchWidth = 6f, ArchSpring = 1.5f, ArchStoneDepth = .5f;
        public const int ArchStones = 13, PyramidWidth = 10, PyramidGlass = 3;
        public static readonly Vector3 PyramidBlock = Vector3.one;
        public const int BlockCapacity = 385, ShardCapacity = 1500, AxialCuts = 2;
        public const int ShardsPerBreak = AxialCuts * AxialCuts * AxialCuts;
        public const float SmallestShard = .25f, ShardWait = 12f, ShardShrink = 1.5f;
        public const float BrightnessVariation = .06f;
        public const float Move = 6f, Sprint = 10f, Jump = 1.2f;
        public const float BaseSensitivity = .13f, Recoil = .15f, RecoilTime = .2f;
        public const float CharacterHeight = 1.8f;
        public const float Displacement = 3f, ComboWindow = 2f;
        public const int ComboStep = 10, ComboMax = 5, PopupCap = 20, VoiceCap = 16;
        public const int ProjectileCapacity = 24;
        public const float ShellFuse = 3f, BlastRadius = 6f, BlastImpulse = 1500f;
        public const float BlastLift = .5f, BlastFlash = .15f, ScorchLife = 20f;
        public const float ShakeNear = 5f, ShakeFar = 40f;
        public const float OrbTime = 4f, OrbRadius = 8f, OrbPull = 60f, OrbPulse = 600f;
        public const float OrbMaxForce = 180000f, ProjectileLife = 12f;
        public const float RebuildTolerance = .002f;
        public const int BlockLayer = 8, ShardLayer = 9, ShotLayer = 10, PlayerLayer = 11;
        public const int BenchResets = 20;
        public const float StandTime = 60f, SettleLimit = 3f, StutterLimitMs = 100f, EnduranceTime = 110f;

        public static readonly MatterSpec[] MatterSpecs =
        {
            new(2400, .8f, .05f, 180, 10), new(600, .6f, .1f, 40, 6), new(2500, .3f, .15f, 6, 3)
        };

        public static readonly WeaponSpec[] Weapons =
        {
            new("CANNONBALL", .4f, 20, 40, .6f, -1),
            new("EXPLOSIVE SHELL", .25f, 8, 30, 2, 10),
            new("GRAVITY ORB", .32f, 5, 18, 5, -1)
        };

        public static readonly string[] LayoutNames = { "TOWER", "ARCH WALL", "PYRAMID" };

        internal readonly struct MatterSpec
        {
            public readonly float Density, Friction, Bounce, Threshold;
            public readonly int Points;
            public MatterSpec(float density, float friction, float bounce, float threshold, int points)
            {
                Density = density; Friction = friction; Bounce = bounce; Threshold = threshold; Points = points;
            }
        }

        internal readonly struct WeaponSpec
        {
            public readonly string Name;
            public readonly float Radius, Mass, Speed, Cooldown;
            public readonly int Ammo;
            public WeaponSpec(string name, float radius, float mass, float speed, float cooldown, int ammo)
            {
                Name = name; Radius = radius; Mass = mass; Speed = speed; Cooldown = cooldown; Ammo = ammo;
            }
        }
    }
}
