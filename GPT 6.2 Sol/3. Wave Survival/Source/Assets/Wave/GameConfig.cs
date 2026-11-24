using UnityEngine;

namespace WaveSurvival
{
    internal enum EnemyKind { Runner, Shooter, Tank, Warden }
    internal enum EnemyState { Idle, Chase, Attack, TakeCover, Peek, Charge, Stunned, Dead }
    internal enum WeaponKind { Pistol, Rifle, Shotgun }
    internal enum SurfaceKind { Concrete, Flesh, Metal }

    internal static class GameConfig
    {
        public const float ArenaSize = 50, WallHeight = 6;
        public const float PlatformSize = 10, PlatformHeight = 1.5f, RampAngle = 25, RampRun = 4;
        public static readonly Vector3 Crate = new(2, 1.5f, 2), Barrier = new(4, 1.2f, .6f);
        public const float PillarRadius = .6f, PillarHeight = 4;
        public const int CoverCount = 12, GateCount = 6, LightCount = 8, FlickerCount = 2;
        public const float AgentRadius = .5f, AgentHeight = 1.8f;
        public const int WorldLayer = 0, PlayerLayer = 8, EnemyLayer = 9, ArtLayer = 10;
        public const int WorldMask = 1 << WorldLayer, HurtMask = 1 << EnemyLayer;
        public const float Health = 100, Stamina = 100, Walk = 5, Sprint = 8, Crouch = 2.5f;
        public const float StaminaDrain = 25, StaminaGain = 15, StaminaDelay = 1;
        public const float CrouchHeight = 1, StandHeight = 1.8f, Jump = 1, Gravity = 24;
        public const float HipFov = 75, AimFov = 45, AimTime = .12f, AimSensitivity = .58f;
        public const float AimSpread = .5f, PistolAimSpread = .2f, ModelSwitch = .35f;
        public const float RifleBloomMax = 4, RifleBloomRecovery = .5f;
        public const int RifleClimbShots = 10, RifleReserveMax = 240;
        public const int Pellets = 8;
        public const float FalloffDistance = 20, FalloffFloor = .3f, HeadBonus = 2, BossHeadBonus = 1.5f;
        public const float FlashTime = .05f, HitFlashTime = .08f, CorpseTime = 5;
        public const float RunnerRange = 1.6f, RunnerDamage = 15, RunnerInterval = 1, RunnerWindup = .3f;
        public const float CoverNear = 12, CoverFar = 18, CoverRadius = 20, RetreatRange = 6;
        public const int CoverSamples = 48, CoverSearchAttempts = 112, ShooterBurst = 3;
        public const float ShooterProjectileSpeed = 40, ShooterDamage = 8, MissChance = .25f, MissAngle = 2;
        public const float CoverWaitMin = 1.5f, CoverWaitMax = 3;
        public const float TankFrontAngle = 70, TankRearAngle = 45, TankArmor = .4f, TankRear = 2, TankSide = 1;
        public const float ChargeRange = 12, ChargeWarning = 1, ChargeSpeed = 10, ChargeMax = 2;
        public const float ChargeDamage = 40, ChargeKnockback = 8, Stun = 1.5f, StunMultiplier = 1.5f;
        public const float StuckDistance = .5f, StuckTime = 3;
        public const int StartWave = 1, RunnerBase = 4, RunnerGrowth = 2, TankWaveStep = 3, AliveCap = 25;
        public const float ShooterGrowth = 1.5f;
        public const int GroupMin = 2, GroupMax = 4, BossWave = 5;
        public const float GateDistance = 20, GroupInterval = 1.5f, GateWarning = 1, WaveBreak = 8;
        public const int WaveRifle = 25, WaveShells = 6, DropRifle = 30, DropShells = 6;
        public const float DropHeal = 25, HealthChance = .3f, AmmoChance = .4f, DropLife = 20, DropBlink = 5;
        public const float BossScale = 2.2f, BossPhase2 = .66f, BossPhase3 = .33f;
        public const float BossBurstInterval = 2, BossDamage = 10, BossProjectileSpeed = 35;
        public const float BossRunnerInterval = 15, BossSlamInterval = 8, BossSlamWarning = 1, BossSlamRadius = 6, BossAirTime = .48f;
        public const float BossSlamDamage = 35, BossSlamImpulse = 8, BossVolleyInterval = 1.5f, BossShooterInterval = 20;
        public const int BossBurst = 5, BossRunners = 4, BossVolley = 5, BossShooters = 2;
        public const float BossPhase2Speed = 3, BossPhase3Speed = 4.5f;
        public const int VSync = 1, FullWidth = 1920, FullHeight = 1080;
        public const int EnemyPoolSize = 64, ProjectilePoolSize = 160, TracerPoolSize = 80, DecalPoolSize = 140;
        public const int PickupPoolSize = 60, ShellPoolSize = 48, ParticlePoolSize = 36, VoicePoolSize = 24;
        public const float RankSAccuracy = .65f, RankAAccuracy = .5f, RankBAccuracy = .35f;
        public const float RankSDamage = 100, RankADamage = 225, RankBDamage = 400;
        public const float RankSTime = 420, RankATime = 660, RankBTime = 960;

        public readonly struct Weapon
        {
            public readonly string Name;
            public readonly float Damage, Interval, Reload, Spread;
            public readonly int Magazine, Reserve;
            public Weapon(string name, float damage, float interval, int magazine, int reserve, float reload, float spread)
            {
                Name = name; Damage = damage; Interval = interval; Magazine = magazine;
                Reserve = reserve; Reload = reload; Spread = spread;
            }
        }

        public static readonly Weapon[] Weapons =
        {
            new("SERVICE PISTOL", 20, .25f, 12, -1, 1.2f, .5f),
            new("ASSAULT RIFLE", 12, .1f, 30, 180, 2, .8f),
            new("PUMP SHOTGUN", 10, .9f, 6, 36, .5f, 6)
        };

        public static readonly float[] EnemyHealth = { 40, 60, 300, 3000 };
        public static readonly float[] EnemySpeed = { 6, 3.5f, 2.5f, 2 };
    }
}

