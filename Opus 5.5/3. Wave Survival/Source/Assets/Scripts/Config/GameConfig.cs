using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// The ONE central config file: every number used by the game lives here.
    /// Values taken from the specification are tagged with their requirement ID (S-xx).
    /// </summary>
    public static class GameConfig
    {
        public static class Layers
        {
            public const int Default = 0;
            public const int Player = 8;
            public const int Enemy = 9;      // body blocker capsules (collide with the player only)
            public const int Hitbox = 10;    // enemy damage colliders (head / body / limbs / weak point)
            public const int Viewmodel = 11; // first-person weapon (no colliders)
            public const int Pickup = 12;
            public const int Debris = 13;    // shell casings (collide with the environment only)
            public const int EnvironmentMask = 1 << Default;
            public const int PlayerShotMask = (1 << Default) | (1 << Hitbox);
            public const int EnemyShotMask = (1 << Default) | (1 << Player);
        }

        public static class Arena
        {
            public const float Size = 50f;                        // S-01 50 x 50 m
            public const float Half = Size * 0.5f;
            public const float WallHeight = 6f;                   // S-01
            public const float WallThickness = 1f;
            public const float CeilingThickness = 0.5f;
            public const float WallPanelWidth = 4f;               // S-01 panel lines: one texture tile = one 4 x 3 m panel
            public const float WallPanelHeight = 3f;
            public const float FloorTile = 4f;                    // floor slab joints every 4 m
            public static readonly Vector3 CrateSize = new Vector3(2f, 1.5f, 2f);     // S-02
            public static readonly Vector3 BarrierSize = new Vector3(4f, 1.2f, 0.6f); // S-02 (length, height, thickness)
            public const float BarrierTopWidth = 0.28f;           // jersey-barrier profile (base = BarrierSize.z)
            public const float PillarRadius = 0.6f;               // S-02
            public const float PillarHeight = 4f;                 // S-02
            public const int CoverCount = 12;                     // S-02
            public const float PlatformSize = 10f;                // S-02 10 x 10 m
            public const float PlatformHeight = 1.5f;             // S-02
            public const float RampRun = 4f;                      // slope = atan(1.5 / 4) = 20.6 deg
            public const float RampWidth = 3f;
            public const float RampMaxSlope = 25f;                // S-02 limit, asserted at build time
            public const int GateCount = 6;                       // S-02
            public const float GateDoorWidth = 3f;
            public const float GateDoorHeight = 3.6f;
            public const float GatePostWidth = 0.5f;
            public const float GateDepth = 0.6f;
            public const float GateSpawnOffset = 2.2f;            // spawn point distance in front of the door
            public const float GateSpawnSpread = 1.3f;            // lateral spacing of a spawn group
            public static readonly Vector3 PlayerSpawn = new Vector3(0f, 0f, -14f);
            public const float NavVolumeHeight = 4f;              // NavMesh collection volume (floor .. 3.5 m), excludes the ceiling

            // Cover layout: x, z, yaw (deg), kind (0 crate, 1 barrier, 2 pillar).
            public static readonly Vector4[] Cover =
            {
                new Vector4(-14f, -2f, 15f, 0), new Vector4(14f, 2f, -10f, 0), new Vector4(-4f, 17f, 30f, 0), new Vector4(4f, -17.5f, 5f, 0),
                new Vector4(-8f, 9.5f, 0f, 1), new Vector4(8f, -9.5f, 0f, 1), new Vector4(-19f, -13f, 90f, 1), new Vector4(19f, 13f, 90f, 1),
                new Vector4(-9.5f, -8.5f, 0f, 2), new Vector4(9.5f, 8.5f, 0f, 2), new Vector4(-16f, 16f, 0f, 2), new Vector4(16f, -16f, 0f, 2),
            };

            // Spawn gates: x, z on the inner wall face, yaw facing into the arena.
            public static readonly Vector3[] Gates =
            {
                new Vector3(-12f, 25f, 180f), new Vector3(12f, 25f, 180f), new Vector3(25f, 0f, 270f),
                new Vector3(12f, -25f, 0f), new Vector3(-12f, -25f, 0f), new Vector3(-25f, 0f, 90f),
            };
        }

        public static class Lighting
        {
            public const int LampCount = 8;                       // S-03
            public static readonly Vector2[] LampXZ =
            {
                new Vector2(-18f, -12f), new Vector2(-6f, -12f), new Vector2(6f, -12f), new Vector2(18f, -12f),
                new Vector2(-18f, 12f), new Vector2(-6f, 12f), new Vector2(6f, 12f), new Vector2(18f, 12f),
            };
            public static readonly int[] FlickerLamps = { 2, 4 }; // S-03 flicker on 2 of them
            public const float LampHeight = 4.9f;
            public const float LampRange = 20f;
            public const float LampIntensity = 26f;
            public static readonly Color LampColor = new Color(1f, 0.83f, 0.62f);
            public const float FlickerSpeed = 11f;
            public const float FlickerDropChance = 0.07f;          // chance per flicker step to cut out briefly
            public static readonly Color AmbientSky = new Color(0.15f, 0.16f, 0.19f);
            public static readonly Color AmbientEquator = new Color(0.1f, 0.1f, 0.11f);
            public static readonly Color AmbientGround = new Color(0.06f, 0.06f, 0.065f);
            public const float FogDensity = 0.016f;               // S-03 light exp2 fog
            public static readonly Color FogColor = new Color(0.07f, 0.075f, 0.085f);
            public const float BloomThreshold = 0.95f;            // S-03 bloom
            public const float BloomIntensity = 0.75f;
            public const float BloomScatter = 0.65f;
            public const float VignetteIntensity = 0.33f;         // S-03 vignette
            public const float VignetteSmoothness = 0.45f;
            public const float PostExposure = 0.6f;
            public const float Contrast = 14f;
            public const float Saturation = -12f;
            public const float GateLightIdle = 1.5f;
            public const float GateLightPulse = 24f;
            public const float GateLightRange = 7f;
            public static readonly Color GateColor = new Color(1f, 0.32f, 0.12f);
            public const float GatePulseHz = 3f;
        }

        public static class Nav
        {
            public const float AgentRadius = 0.5f;                // S-04
            public const float AgentHeight = 1.8f;                // S-04
            public const string BossAgentType = "Boss";
            public const float VoxelSize = 0.1f;
            public const float BossVoxelSize = 0.25f;
            public const float SampleRadius = 2.5f;
        }

        public static class Player
        {
            public const float WalkSpeed = 5f;                    // S-05
            public const float SprintSpeed = 8f;                  // S-05
            public const float CrouchSpeed = 2.5f;                // S-05
            public const float StaminaMax = 100f;                 // S-05
            public const float StaminaDrain = 25f;                // S-05 per second
            public const float StaminaRegen = 15f;                // S-05 per second
            public const float StaminaRegenDelay = 1f;            // S-05
            public const float StandHeight = 1.8f;
            public const float CrouchHeight = 1.0f;               // S-05
            public const float Radius = 0.35f;
            public const float EyeStand = 1.62f;
            public const float EyeCrouch = 0.82f;
            public const float CrouchLerpSpeed = 10f;
            public const float JumpHeight = 1.0f;                 // S-05
            public const float Gravity = 20f;
            public const float GroundAccel = 55f;
            public const float AirAccel = 12f;
            public const float KnockbackDamping = 4.5f;
            public const float StepOffset = 0.35f;
            public const float MaxHealth = 100f;                  // S-06
            public const float MouseSensitivity = 0.075f;         // degrees per mouse count
            public const float AdsSensitivityScale = 0.55f;       // S-11 sensitivity scaled down in ADS
            public const float PitchLimit = 88f;
            public const float BobFrequency = 1.85f;              // bob cycles per metre travelled * 2pi / 2 steps
            public const float BobAmplitude = 0.045f;
            public const float BobSideAmplitude = 0.025f;
            public const float FootstepVolume = 0.55f;
            public const float FlinchDegrees = 3.2f;              // S-06 camera flinch
            public const float VignettePulse = 0.75f;             // S-06 vignette alpha per hit
            public const float VignetteFade = 1.6f;
            public const float DeathCameraTime = 1.4f;
            public static readonly Vector3 StartEuler = new Vector3(0f, 0f, 0f);
        }

        public static class View
        {
            public const float HipFov = 75f;                      // S-11
            public const float AdsFov = 45f;                      // S-11
            public const float AdsTime = 0.12f;                   // S-11
            public const float Near = 0.01f;
            public const float Far = 160f;
            public const float ViewmodelScale = 0.1f;             // viewmodel is shrunk toward the eye so it never clips walls
            public const float KickStiffness = 260f;
            public const float KickDamping = 24f;
            public const float RecoilRecovery = 7f;               // per second, exponential return of the recoil offset
            public const float ShakeDecay = 3.5f;
            public static readonly Vector3 ViewmodelFillOffset = new Vector3(0.35f, 0.55f, 0.1f); // small light that keeps the gun readable
            public const float ViewmodelFillIntensity = 0.35f;
            public const float ViewmodelFillRange = 3f;
        }

        public static class Weapons
        {
            public const float Range = 200f;
            public const float SwitchTime = 0.35f;                // S-13
            public const float HeadshotMultiplier = 2f;           // S-12
            public const float AdsSpreadMultiplier = 0.5f;        // S-11 (not for the shotgun)
            public const float FireBufferTime = 0.12f;            // click slightly early still fires (crisp semi-auto)
            public const float HitMarkerTime = 0.16f;
            public const float KillMarkerTime = 0.32f;

            // S-08 Pistol
            public const float PistolDamage = 20f, PistolInterval = 0.25f, PistolReload = 1.2f;
            public const int PistolMag = 12;
            public const float PistolSpread = 0.5f, PistolSpreadAds = 0.2f;
            public const float PistolKick = 1.1f, PistolViewRecoil = 0.9f;

            // S-09 Assault rifle
            public const float RifleDamage = 12f, RifleInterval = 0.1f, RifleReload = 2.0f;
            public const int RifleMag = 30, RifleReserve = 180, RifleReserveMax = 240;
            public const float RifleSpreadMin = 0.8f, RifleSpreadMax = 4f, RifleBloomPerShot = 0.4f, RifleBloomRecover = 0.5f;
            public const int RifleClimbShots = 10;                // first 10 shots climb
            public const float RifleClimbPerShot = 0.55f;         // degrees of pitch per shot during the climb
            public const float RifleWobblePitch = 0.12f;          // after the climb: small pitch...
            public const float RifleWobbleYaw = 0.32f;            // ...and slight horizontal wobble
            public const float RifleKick = 0.45f;
            public const float RifleResetTime = 0.3f;             // pattern restarts after the trigger is released this long

            // S-10 Shotgun
            public const int ShotgunPellets = 8;
            public const float ShotgunCone = 6f, ShotgunDamage = 10f, ShotgunFalloffEnd = 20f, ShotgunFalloffMin = 0.3f;
            public const int ShotgunMag = 6, ShotgunReserve = 36, ShotgunReserveMax = 36;
            public const float ShotgunPump = 0.9f, ShotgunShellTime = 0.5f;
            public const float ShotgunKick = 3.4f, ShotgunViewRecoil = 2.4f;
        }

        public static class Fx
        {
            public const float MuzzleFlashTime = 0.05f;           // S-12
            public const float MuzzleLightIntensity = 9f;
            public const float MuzzleLightRange = 9f;
            public const int TracerPool = 48;
            public const float TracerSpeed = 320f;
            public const float TracerLength = 3.5f;
            public const float TracerWidth = 0.025f;
            public const int DecalPool = 180;
            public const float DecalSize = 0.13f;
            public const float BloodDecalSize = 0.22f;
            public const int ShellPool = 40;
            public const float ShellLife = 1.6f;
            public const int MaxParticlesPerSystem = 600;
            public const float HitFlashTime = 0.08f;              // S-enemies hit flash
            public const float HitFlashIntensity = 2.2f;
        }

        public static class Enemy
        {
            public const float DeathFadeDelay = 5f;               // S-17 fade after 5 s
            public const float DeathFadeTime = 1f;
            public const float HealthBarTime = 4f;                // floating bar stays this long after the last hit
            public const float StuckWindow = 3f;                  // S-18
            public const float StuckMinProgress = 0.5f;           // S-18
            public const float ValidityCheckInterval = 0.5f;
            public const float RepathInterval = 0.2f;
            public const float SpawnMaterializeTime = 0.45f;
            public const float GrowlMin = 3.5f, GrowlMax = 8f;
            public const float EyeHeight = 1.55f;

            // S-14 Runner
            public const float RunnerHp = 40f, RunnerSpeed = 6f, RunnerRange = 1.6f, RunnerDamage = 15f;
            public const float RunnerAttackInterval = 1f, RunnerWindup = 0.3f;
            public const float RunnerNeighborRadius = 5f;         // "near each other"
            public const float RunnerSeparationRadius = 1.9f;
            public const float RunnerSeparationStrength = 3.2f;
            public const float RunnerFlankAngle = 70f;            // degrees off the direct line per flank slot
            public const float RunnerFlankRadius = 5.5f;          // flank approach point distance from the player
            public const float RunnerDirectDistance = 6.5f;       // closer than this: go straight for the player

            // S-15 Shooter
            public const float ShooterHp = 60f, ShooterSpeed = 3.5f;
            public const float ShooterMinDist = 12f, ShooterMaxDist = 18f, ShooterSearchRadius = 20f, ShooterRetreatDist = 6f;
            public const int ShooterCandidates = 16;              // >= 12 candidate points
            public const int ShooterBurst = 3;
            public const float ShooterBurstInterval = 0.22f;
            public const float ShooterProjectileSpeed = 40f, ShooterDamage = 8f, ShooterMissChance = 0.25f, ShooterMissAngle = 2f;
            public const float ShooterWaitMin = 1.5f, ShooterWaitMax = 3f;
            public const float ShooterPeekStep = 1.6f;
            public const float ShooterAimTime = 0.25f;
            public const float CoverReserveRadius = 2f;

            // S-16 Tank
            public const float TankHp = 300f, TankSpeed = 2.5f;
            public const float TankFrontArc = 70f, TankFrontMult = 0.4f, TankSideMult = 1f, TankRearArc = 45f, TankRearMult = 2f;
            public const float TankChargeRange = 12f, TankTelegraph = 1f, TankChargeSpeed = 10f, TankChargeMaxTime = 2f;
            public const float TankChargeDamage = 40f, TankKnockback = 8f, TankStun = 1.5f, TankStunMult = 1.5f;
            public const float TankHitReach = 1.25f;              // centre-to-centre distance that counts as a charge hit
        }

        public static class Waves
        {
            public const int RunnerBase = 4, RunnerPerWave = 2;   // S-19 runners = 4 + 2N
            public const float ShooterFactor = 1.5f;              // S-19 shooters = floor(1.5 (N-1))
            public const int TankDivisor = 3;                     // S-19 tanks = floor(N / 3)
            public const int MaxAlive = 25;                       // S-19
            public const float GateMinDistance = 20f;             // S-20
            public const int GroupMin = 2, GroupMax = 4;          // S-20
            public const float GroupInterval = 1.5f;              // S-20
            public const float GatePulse = 1f;                    // S-20
            public const float BreakTime = 8f;                    // S-21
            public const float FirstWaveDelay = 5f;
            public const int BreakRifleAmmo = 25, BreakShotgunShells = 6; // S-21
            public const int BossWave = 5;                        // S-23
            public const float VictoryDelay = 2.5f;
        }

        public static class Pickups
        {
            public const float HealthAmount = 25f;                // S-22
            public const float HealthChance = 0.3f;               // S-22 (guaranteed from tanks)
            public const float AmmoChance = 0.4f;                 // S-22
            public const int AmmoRifle = 30, AmmoShells = 6;      // S-22
            public const float Lifetime = 20f, BlinkTime = 5f;    // S-22
            public const float CollectRadius = 1.3f;
            public const float SpinSpeed = 90f, BobHeight = 0.12f, BobSpeed = 2.2f, HoverHeight = 0.45f;
            public const int Pool = 40;
        }

        public static class Boss
        {
            public const float Hp = 3000f, Scale = 2.2f;          // S-23
            public const float Phase2 = 0.66f, Phase3 = 0.33f;    // S-23 thresholds
            public const float Speed1 = 2f, Speed2 = 3f, Speed3 = 4.5f; // S-23
            public const float BurstInterval = 2f;                // S-23 phase 1
            public const int BurstRounds = 5;
            public const float BurstRoundGap = 0.12f;
            public const float ProjectileDamage = 10f, ProjectileSpeed = 35f, BurstSpread = 0.75f;
            public const float SummonRunnersEvery = 15f;          // S-23
            public const int SummonRunners = 4;
            public const float SlamEvery = 8f, SlamTelegraph = 1f, SlamDamage = 35f, SlamRadius = 6f, SlamKnockback = 9f; // S-23 phase 2
            public const float SlamCrouch = 0.3f, SlamRecover = 0.6f, SlamArcHeight = 5f;
            public const float VolleyInterval = 1.5f;             // S-23 phase 3
            public const int VolleyCount = 5;
            public const float VolleyStep = 10f;
            public const float SummonShootersEvery = 20f;         // S-23
            public const int SummonShooters = 2;
            public const float HeadMultiplier = 1.5f;             // S-23
            public const float StopDistance = 7f;
            public const float IntroTime = 1.6f;
            public const float SummonRadiusMin = 3.5f, SummonRadiusMax = 6.5f;
        }

        public static class Score
        {
            public const int Runner = 100, Shooter = 150, Tank = 400, Warden = 5000, HeadshotKillBonus = 50, WaveClearPerWave = 250;
        }

        public static class Rank
        {
            // S-24 explicit thresholds: rank needs ALL conditions of its row.
            public const float SAccuracy = 0.45f, SDamageTaken = 250f, STime = 660f;
            public const float AAccuracy = 0.33f, ADamageTaken = 500f, ATime = 900f;
            public const float BAccuracy = 0.20f, BDamageTaken = 900f;
        }

        public static class Audio
        {
            public const int SampleRate = 44100;
            public const int Pool3D = 40, Pool2D = 12;
            public const float MinDistance = 2f, MaxDistance = 45f;
            public const float MasterVolume = 0.85f, AmbientVolume = 0.32f;
        }

        public static class Pools
        {
            public const int Runners = 40, Shooters = 32, Tanks = 16;
            public const int RunnersWarm = 26, ShootersWarm = 12, TanksWarm = 6;
            public const int Projectiles = 120;
            public const int HealthBars = 30;
            public const int DamageIndicators = 8;
        }

        public static class Hud
        {
            public const float ReferenceWidth = 1920f, ReferenceHeight = 1080f;
            public const float IndicatorTime = 1.6f, IndicatorRadius = 190f;
            public const float CrosshairMinGap = 6f, CrosshairLength = 12f, CrosshairThickness = 2f;
            public const float FpsUpdateInterval = 0.5f;
            public const float HealthBarWidth = 70f, HealthBarHeight = 7f, HealthBarMaxDistance = 45f;
        }
    }
}
