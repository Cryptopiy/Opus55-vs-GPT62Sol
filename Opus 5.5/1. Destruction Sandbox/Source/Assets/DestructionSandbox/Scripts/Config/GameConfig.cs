using UnityEngine;

namespace DestructionSandbox
{
    /// <summary>
    /// The ONE central config file. Every number from the specification (D-xx) and every tuning number chosen
    /// during development lives here, grouped by system. Spec values are marked with their requirement ID.
    /// </summary>
    public static class GameConfig
    {
        public static class Layers
        {
            public const int Default = 0;
            public const int Block = 8;
            public const int Shard = 9;
            public const int Projectile = 10;
            public const int Player = 11;
            public const int Environment = 12;

            public const int BlockMask = 1 << Block;
            public const int ShardMask = 1 << Shard;
            public const int ProjectileMask = 1 << Projectile;
            public const int EnvironmentMask = 1 << Environment;
            /// <summary>Everything explosions / gravity orbs can push.</summary>
            public const int DynamicMask = BlockMask | ShardMask | ProjectileMask;
            /// <summary>Everything the aim ray can hit.</summary>
            public const int AimMask = BlockMask | ShardMask | EnvironmentMask | (1 << Default);
        }

        public static class Timing
        {
            public const float FixedTimestep = 1f / 60f;          // D-26
            public const float MaximumDeltaTime = 1f / 30f;       // at most 2 physics steps per frame: overload slows the
                                                                  // simulation slightly instead of stalling frames
            public const int VSyncCount = 1;                      // GPU safety rule: VSync on by default
            public const float SlowMotionTimeScale = 0.25f;       // D-20
            public const float SlowMotionAudioPitch = 0.5f;       // D-20 "audio pitch lowered" (one octave down)
        }

        public static class World
        {
            public const float GroundSize = 80f;                  // D-01
            public const float GroundColliderThickness = 4f;      // thick slab under y=0 so nothing tunnels through
            public const float GridMinorCell = 1f;                // D-01
            public const float GridMajorCell = 10f;               // D-01
            public const float GridMinorLineWidth = 0.025f;       // metres
            public const float GridMajorLineWidth = 0.07f;        // metres
            public const float GridMinorLineDarkness = 0.28f;
            public const float GridMajorLineDarkness = 0.5f;
            public const float BorderHeight = 1f;                 // D-01
            public const float BorderThickness = 0.5f;
            public const float InvisibleWallHeight = 400f;        // higher than anything can fly at 80 m/s (v^2/2g = 326 m)
            public const float InvisibleWallThickness = 4f;
            public const float KillY = -20f;                      // safety net: bodies below this are recycled
            /// <summary>A light shard squeezed into the ground under a heavy pile (PhysX mass-ratio limit) is
            /// removed as soon as its centre is this far below the surface, so nothing visibly tunnels.</summary>
            public const float BelowGroundLimit = 0.3f;
            public static readonly Vector3 StructureOrigin = Vector3.zero;
        }

        public static class Lighting
        {
            public static readonly Vector3 SunEuler = new Vector3(42f, 38f, 0f);
            public static readonly Color SunColor = new Color(1.0f, 0.9f, 0.76f);   // D-02 warm
            public const float SunIntensity = 1.9f;
            public const float ShadowStrength = 0.92f;
            public const float ShadowDistance = 80f;              // D-02
            public static readonly Color SkyZenith = new Color(0.20f, 0.42f, 0.78f);
            public static readonly Color SkyHorizon = new Color(0.78f, 0.84f, 0.90f);
            public static readonly Color SkyGround = new Color(0.33f, 0.31f, 0.29f);
            public const float SkySunSize = 0.9995f;              // cosine of the sun disc radius
            public const float SkyExposure = 1.0f;
            public const float AmbientIntensity = 1.0f;
            public const int AmbientSampleCount = 512;            // directions used to project the sky into SH
            public const int ReflectionCubeSize = 64;
            public const float FogDensity = 0.0035f;
            public const float BloomIntensity = 0.6f;             // D-02
            public const float BloomThreshold = 1.0f;
            public const float BloomScatter = 0.65f;
            public const float VignetteIntensity = 0.2f;          // D-02
        }

        public static class Player
        {
            public static readonly Vector3 SpawnEyePosition = new Vector3(0f, 1.8f, -28f);   // D-03
            public static readonly Vector3 SpawnLookAt = new Vector3(0f, 6f, 0f);            // D-03
            public const float WalkSpeed = 6f;                    // D-15
            public const float SprintSpeed = 10f;                 // D-15
            public const float JumpHeight = 1.2f;                 // D-15
            public const float ControllerHeight = 2.0f;
            public const float ControllerRadius = 0.4f;
            public const float ControllerSkinWidth = 0.04f;
            public const float ControllerStepOffset = 0.35f;
            public const float ControllerSlopeLimit = 50f;
            public const float EyeHeight = 1.8f;                  // camera height above the feet (spawn eye y = 1.8)
            public const float GroundedStickVelocity = -2f;
            public const float MaxPitch = 88f;
            public const float DefaultSensitivity = 0.12f;        // degrees per mouse pixel
            public const float MinSensitivity = 0.02f;
            public const float MaxSensitivity = 0.6f;
            public const float CameraNearClip = 0.05f;
            public const float CameraFarClip = 600f;
            public const float CameraFieldOfView = 70f;
            public const float MaxFrameDelta = 0.1f;              // clamp for unscaled movement after hitches
        }

        public static class Structures
        {
            // D-04 Tower
            public const int TowerFootprint = 4;
            public const int TowerFloors = 24;
            public static readonly Vector3 TowerBlockSize = new Vector3(1.0f, 0.5f, 1.0f);
            public const int TowerStoneLastFloor = 8;             // floors 1-8 stone
            public const int TowerWoodLastFloor = 18;             // floors 9-18 wood, 19-24 glass
            public const float TowerOddFloorYaw = 90f;            // odd floors rotated 90 degrees

            // D-05 Wall with arch
            public const int WallBricksWide = 24;
            public const int WallRows = 12;
            public static readonly Vector3 BrickSize = new Vector3(1.0f, 0.5f, 0.5f);
            public const float ArchOpeningWidth = 6f;             // inner radius = 3 m
            public const int ArchSpringingRow = 3;                // rows 1-3 are the jambs, arch springs at y = 1.5 m
            public const float ArchCrownTopY = 5.0f;              // keystone top meets the top of row 10
            public const float ArchOuterRadius = 3.52f;           // ring thickness ~0.52 m (one brick course)
            public const float ArchMaxVoussoirSpanDegrees = 13.5f; // long outer edges are split into extra voussoirs
            public const int WindowFirstRow = 8;                  // glass windows on rows 8-9
            public const int WindowLastRow = 9;
            public static readonly Vector2[] WindowSpans =        // x ranges of the glass windows (metres)
            {
                new Vector2(-11f, -9f), new Vector2(-7f, -5f), new Vector2(5f, 7f), new Vector2(9f, 11f)
            };
            public const float MinCutPieceArea = 0.1f;            // smaller cut slivers are merged into a neighbour

            // D-06 Pyramid
            public const int PyramidBaseSize = 10;
            public const float PyramidCubeSize = 1f;
            public const int PyramidGlassCapLayers = 3;           // "glass cap (3 blocks)" = top three layers

            public const float ColorVariation = 0.06f;            // D-09 +-6% brightness
        }

        public static class Materials
        {
            // D-07 densities (kg/m^3)
            public const float WoodDensity = 600f;
            public const float StoneDensity = 2400f;
            public const float GlassDensity = 2500f;
            // D-07 friction / bounciness
            public const float WoodFriction = 0.6f, WoodBounciness = 0.1f;
            public const float StoneFriction = 0.8f, StoneBounciness = 0.05f;
            public const float GlassFriction = 0.3f, GlassBounciness = 0.15f;
            public const float GroundFriction = 0.8f, GroundBounciness = 0.05f;
            public const float ProjectileFriction = 0.5f, ProjectileBounciness = 0.1f;
            // D-10 break thresholds (collision impulse, N*s)
            public const float GlassBreakImpulse = 6f;
            public const float WoodBreakImpulse = 40f;
            public const float StoneBreakImpulse = 180f;
        }

        public static class Physics
        {
            public const int SolverIterations = 12;               // D-08
            public const int SolverVelocityIterations = 4;        // D-08
            public const float SleepThreshold = 0.01f;            // mass-normalised kinetic energy (~0.14 m/s)
            public const float ContactOffset = 0.02f;
            public const float MaxDepenetrationVelocity = 3f;     // gentle separation of freshly spawned shards
            public const float BounceThreshold = 2f;
            public const float MaxAngularSpeed = 50f;
            public const float MaxLinearVelocity = 80f;           // D-27
            public const float BlockAngularDamping = 0.05f;
            /// <summary>Blocks/shards faster than this switch to cheap speculative CCD (no tunnelling into the
            /// ground at up to 80 m/s); they return to discrete detection below FastBodyReleaseSpeed.</summary>
            public const float FastBodySpeed = 30f;
            public const float FastBodyReleaseSpeed = 15f;
            // Debris (shards, and blocks once they are collapsing) does not need stacking accuracy: a lighter
            // solver and earlier sleep keep a 1500-shard pile cheap. Standing structure blocks keep 12 / 4.
            public const int DebrisSolverIterations = 4;
            public const int DebrisSolverVelocityIterations = 1;
            public const float DebrisSpeed = 0.75f;               // an awake block faster than this is collapsing debris
            public const float ShardSleepThreshold = 0.05f;
            public const float ShardContactOffset = 0.01f;
            public const float ShardLinearDamping = 0.05f;
            public const float ShardAngularDamping = 0.25f;
            public const float SettleCheckTime = 3f;              // D-08 "within 3 seconds"
            public const float SettleSpeedLimit = 0.05f;          // D-08 "< 0.05 m/s"
            public const float StabilityWatchTime = 60f;          // D-08 "at least 60 s"
        }

        public static class Fracture
        {
            public const int ShardsPerBreak = 8;                  // D-11 2x2x2
            public const float MinSubdivideSize = 0.25f;          // D-12 (pieces smaller than this in EVERY axis stay)
            public const int ShardBudget = 1500;                  // D-13
            public const float ShardSettleTime = 12f;             // D-13
            public const float ShardShrinkTime = 1.5f;            // D-13
            public const float ShardSettledSpeed = 0.1f;          // below this a shard counts as settled
            public const float ShardSettledAngularSpeed = 0.2f;
            public const float SettleCheckInterval = 0.2f;
            /// <summary>
            /// Minimum normal impact speed for a contact to be treated as an impact. Resting contacts in a stack
            /// carry the static load (m*g*dt per step, e.g. 204 N*s for a glass block) which exceeds every
            /// D-10 threshold, so only contacts that actually closed at this speed may fracture.
            /// </summary>
            public const float ImpactSpeedGate = 2.5f;
            /// <summary>Crush rule: a heavy body landing on a piece also breaks it when the contact closes at
            /// CrushSpeedGate or more and the impulse is at least CrushImpulseFactor x the threshold (a column
            /// dropping onto rubble). Resting loads close at ~0 m/s and still never count.</summary>
            public const float CrushSpeedGate = 0.6f;
            public const float CrushImpulseFactor = 20f;
            /// <summary>Each of the 8 sub-boxes is drawn/collides at this fraction of its cell (mass share stays
            /// exact): the missing volume is the dust cloud, and the structure above settles onto the rubble.</summary>
            public const float ShardInset = 0.85f;
            /// <summary>Shard spawns allowed per physics step; further breaks wait for the next step (keeps
            /// chain reactions from stalling a frame). Block breaks always wait; queued shard sub-fractures
            /// older than MaxShardBreakDelay are dropped (the impact is over, the shard simply stays whole).</summary>
            public const int MaxShardSpawnsPerStep = 64;
            public const float MaxShardBreakDelay = 0.15f;
            public const float ScatterImpulseFraction = 1.0f;     // share of the impulse above threshold turned into outward scatter
            public const float MaxScatterSpeed = 4f;
            public const float MinScatterSpeed = 1.5f;            // shattered pieces burst apart: rubble cannot keep
                                                                  // carrying the column above as if it were intact
            public const float ScatterAngularSpeed = 3f;
            public const float MinShardVolume = 1e-5f;
        }

        public static class Weapons
        {
            public const int WeaponCount = 3;
            // D-17 cannonball
            public const float CannonballRadius = 0.4f;
            public const float CannonballMass = 20f;
            public const float CannonballSpeed = 40f;
            public const float CannonballCooldown = 0.6f;
            public const float CannonballLifetime = 10f;
            public const int CannonballPool = 12;
            public const float CannonballHeavyImpactSpeed = 4f;   // first impact faster than this plays the heavy sound
            // D-18 explosive shell
            public const float ShellRadius = 0.25f;
            public const float ShellMass = 8f;
            public const float ShellSpeed = 30f;
            public const float ShellFuse = 3f;
            public const float ShellCooldown = 2.0f;
            public const int ShellAmmo = 10;
            public const int ShellPool = 6;
            public const float ExplosionRadius = 6f;
            public const float ExplosionPeakImpulse = 1500f;
            public const float ExplosionUpwardModifier = 0.5f;
            public const int MaxBlastBreaks = 32;                 // cap of pieces the blast itself may shatter
            public const float ExplosionFlashDuration = 0.15f;
            public const float ExplosionFlashIntensity = 60f;
            public const float ExplosionFlashRange = 18f;
            public const float ShockwaveDuration = 0.45f;
            public const float ScorchFadeTime = 20f;
            public const float ScorchSize = 5.5f;
            public const float ScorchMaxHeight = 12f;             // no ground scorch for blasts higher than this
            public const int ScorchPool = 12;
            public const float ShakeFullDistance = 5f;
            public const float ShakeNoneDistance = 40f;
            public const float ExplosionShakeTrauma = 1f;
            // D-19 gravity orb
            public const float OrbRadius = 0.35f;
            public const float OrbMass = 2f;
            public const float OrbSpeed = 18f;
            public const float OrbActiveTime = 4f;
            public const float OrbFieldRadius = 8f;
            public const float OrbPullAcceleration = 60f;         // force = 60 * mass N
            public const float OrbCoreRadius = 1.2f;              // clamp: pull ramps to 0 inside the core (no overshoot)
            public const float OrbMaxPullSpeed = 14f;             // clamp: no extra pull above this inward speed
            public const float OrbDampingRadius = 2.2f;           // inside this radius debris is calmed into a clump
            public const float OrbDamping = 4f;                   // 1/s velocity damping inside OrbDampingRadius
            public const float OrbPulseImpulse = 600f;
            public const float OrbCooldown = 5f;
            public const float OrbMaxFlightTime = 8f;
            public const float OrbVisualDiameter = 2.8f;
            public const int OrbPool = 3;
            // D-16 view model
            public const float RecoilDistance = 0.15f;
            public const float RecoilRecoverTime = 0.2f;
            public const float MuzzleFlashTime = 0.06f;
            public const float MuzzleLightIntensity = 12f;
            public const float MuzzleLightRange = 7f;
            public const float AimMaxDistance = 250f;
            public static readonly Vector3 CannonViewOffset = new Vector3(0.36f, -0.3f, 0.66f);
            public const float CannonViewScale = 0.8f;
        }

        public static class Score
        {
            public const int StonePoints = 10;                    // D-22
            public const int WoodPoints = 6;
            public const int GlassPoints = 3;
            public const int MaxCombo = 5;
            public const int BlocksPerComboStep = 10;
            public const float ComboWindow = 2f;
            public const float ComboDecayTime = 2f;
            public const float DisplacementForDestroyed = 3f;     // D-21
            public const int MaxPopups = 20;                      // D-22
            public const float PopupLifetime = 1.1f;
            public const float PopupRiseSpeed = 1.2f;
            public const int DestructionHistory = 256;
        }

        public static class Audio
        {
            public const int MaxVoices = 16;                      // D-25
            public const int SampleRate = 48000;
            public const float SpatialMinDistance = 6f;
            public const float SpatialMaxDistance = 160f;
            public const int MaxImpactSoundsPerStep = 3;
            public const float ImpactSoundMinSpeed = 1.2f;
            public const float ImpactMinImpulse = 5f;             // impulse mapped to the quietest impact sound
            public const float ImpactMaxImpulse = 4000f;          // impulse mapped to the loudest impact sound
            public const float ImpactMinVolume = 0.12f;
            public const float ImpactMaxVolume = 1f;
            public const float ImpactPitchSoft = 1.2f;            // small impulse -> brighter
            public const float ImpactPitchHard = 0.78f;           // big impulse -> deeper
            public const float PitchJitter = 0.06f;
            public const float DefaultMasterVolume = 0.8f;
            public const int SynthSeed = 1337;
        }

        public static class Effects
        {
            public const int FlashLightPool = 8;
            public const int ShockwavePool = 4;
            public const int DustMaxParticles = 2500;
            public const int SparkMaxParticles = 2500;
            public const int SplinterMaxParticles = 1500;
            public const int GlassMaxParticles = 3000;
            public const int FireMaxParticles = 900;
            public const int SmokeMaxParticles = 700;
            public const int OrbMaxParticles = 900;
            public const int MuzzleMaxParticles = 300;
            public const int OrbSuckParticlesPerSecond = 240;
            public const float CameraShakeDecay = 1.6f;
            public const float CameraShakeMaxOffset = 0.18f;
            public const float CameraShakeMaxAngle = 3.5f;
            public const float CameraShakeFrequency = 22f;
        }

        /// <summary>Material colours and shading parameters of the procedural materials.</summary>
        public static class Palette
        {
            public static readonly Color StoneA = new Color(0.60f, 0.585f, 0.56f);
            public static readonly Color StoneB = new Color(0.43f, 0.42f, 0.405f);
            public const float StoneSmoothness = 0.12f;
            public static readonly Color WoodA = new Color(0.64f, 0.43f, 0.24f);
            public static readonly Color WoodB = new Color(0.42f, 0.25f, 0.12f);
            public const float WoodSmoothness = 0.22f;
            public static readonly Color GlassTint = new Color(0.62f, 0.86f, 0.95f, 0.22f);
            public static readonly Color GlassEdge = new Color(0.86f, 0.97f, 1.0f, 0.7f);
            public static readonly Color ConcreteA = new Color(0.66f, 0.65f, 0.62f);
            public static readonly Color ConcreteB = new Color(0.55f, 0.54f, 0.52f);
            public static readonly Color MetalA = new Color(0.36f, 0.36f, 0.38f);
            public static readonly Color MetalB = new Color(0.24f, 0.24f, 0.26f);
            public const float MetalSmoothness = 0.55f;
            public const float MetalMetallic = 0.6f;
            public static readonly Color ShellA = new Color(0.75f, 0.22f, 0.08f);
            public static readonly Color ShellB = new Color(0.45f, 0.1f, 0.04f);
            public static readonly Color GroundA = new Color(0.47f, 0.47f, 0.45f);
            public static readonly Color GroundB = new Color(0.41f, 0.41f, 0.395f);
            public const float GroundSmoothness = 0.12f;
            public static readonly Color StoneDust = new Color(0.52f, 0.51f, 0.49f, 0.5f);
            public static readonly Color WoodDust = new Color(0.5f, 0.4f, 0.28f, 0.45f);
            public static readonly Color Splinter = new Color(0.55f, 0.36f, 0.18f, 1f);
            public static readonly Color Spark = new Color(1f, 0.72f, 0.35f, 1f);
            public static readonly Color GlassGlint = new Color(0.8f, 0.95f, 1f, 1f);
            public static readonly Color Fire = new Color(1f, 0.55f, 0.18f, 1f);
            public static readonly Color Smoke = new Color(0.3f, 0.29f, 0.28f, 0.55f);
            public static readonly Color OrbParticle = new Color(0.65f, 0.45f, 1f, 1f);
            public static readonly Color MuzzleFlash = new Color(1f, 0.78f, 0.45f, 1f);
            public static readonly Color ExplosionLight = new Color(1f, 0.62f, 0.3f);
            public static readonly Color OrbLight = new Color(0.6f, 0.4f, 1f);
            public static readonly Color TrailColor = new Color(1f, 0.85f, 0.6f, 0.55f);
            public static readonly Color ShellTrailColor = new Color(1f, 0.5f, 0.2f, 0.7f);
        }

        public static class UI
        {
            public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
            public const float MatchWidthOrHeight = 0.5f;
            public const float HudRefreshInterval = 0.1f;
            public const float OverlayRefreshInterval = 0.25f;
            public const int FontSizeSmall = 20;
            public const int FontSizeMedium = 26;
            public const int FontSizeLarge = 38;
            public const int FontSizeTitle = 56;
        }
    }
}
