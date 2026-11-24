using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>
    /// S-23 boss "Warden": HP 3000, x2.2 scale, three phases by health (100-66 / 66-33 / below 33 %).
    /// Phase 1: walks at 2 m/s, 5-round bursts every 2 s, summons 4 runners every 15 s.
    /// Phase 2: + jump-slam every 8 s (1 s floor telegraph, 35 damage + knockback within 6 m), 3 m/s.
    /// Phase 3: enrage (red glow, 4.5 m/s), 5-way volley every 1.5 s, summons 2 shooters every 20 s (slams continue).
    /// Head takes x1.5. Uses its own NavMesh agent type (radius 1.1) so the big body never clips into geometry.
    /// </summary>
    public sealed class WardenBoss : EnemyBase
    {
        static readonly Color EnrageGlow = new Color(3f, 0.15f, 0.05f);

        public int Phase { get; private set; } = 1;
        public float BurstTimer, VolleyTimer, RunnerSummonTimer, ShooterSummonTimer, SlamTimer;
        int burstLeft;
        float burstGap;
        float summonPose;

        protected override float Stride => 1.6f;
        protected override float Lean => 6f;
        protected override SoundId Growl => SoundId.TankGrowl;
        protected override SoundId DeathSound => SoundId.BossRoar;
        protected override float BlockerRadius => 1.1f;
        protected override float BlockerHeight => 4.1f;
        public override NavMeshQueryFilter Filter => Game.Arena.BossFilter;
        public float HealthFraction => Health / MaxHealth;

        protected override EnemyBody BuildBody()
        {
            Type = EnemyType.Warden;
            return EnemyBodies.Warden(this);
        }

        protected override void ConfigureAgent(NavMeshAgent a)
        {
            int bossType = ArenaNavMesh.FindAgentType(GameConfig.Nav.BossAgentType);
            a.agentTypeID = bossType >= 0 ? bossType : 0;
            a.radius = 1.1f;
            a.height = 4f;
            a.speed = GameConfig.Boss.Speed1;
            a.acceleration = 8f;
            a.angularSpeed = 160f;
            a.stoppingDistance = GameConfig.Boss.StopDistance;
            a.avoidancePriority = 10;
            a.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        }

        protected override void BuildStates()
        {
            Fsm.Add(new BossIntroState(this));
            Fsm.Add(new BossChaseState(this));
            Fsm.Add(new BossAttackState(this));
            Fsm.Add(new BossSlamState(this));
            Fsm.Add(new DeadState(this));
        }

        protected override void OnSpawned()
        {
            MaxHealth = GameConfig.Boss.Hp;
            Phase = 1;
            Agent.speed = GameConfig.Boss.Speed1;
            BurstTimer = GameConfig.Boss.BurstInterval;
            RunnerSummonTimer = GameConfig.Boss.SummonRunnersEvery;
            ShooterSummonTimer = GameConfig.Boss.SummonShootersEvery;
            VolleyTimer = GameConfig.Boss.VolleyInterval;
            SlamTimer = GameConfig.Boss.SlamEvery;
            burstLeft = 0;
            Game.Audio.PlayAt(SoundId.BossRoar, EyePosition, 1f);
        }

        protected override float DamageMultiplier(in DamageInfo info, Hitbox hb) =>
            hb.Zone == HitZone.Head ? GameConfig.Boss.HeadMultiplier : 1f;

        protected override void OnDamaged(in DamageInfo info, float dealt)
        {
            float f = HealthFraction;
            int phase = f > GameConfig.Boss.Phase2 ? 1 : f > GameConfig.Boss.Phase3 ? 2 : 3;
            if (phase != Phase && Health > 0f) EnterPhase(phase);
        }

        void EnterPhase(int phase)
        {
            Phase = phase;
            Agent.speed = phase == 1 ? GameConfig.Boss.Speed1 : phase == 2 ? GameConfig.Boss.Speed2 : GameConfig.Boss.Speed3;
            Game.Audio.PlayAt(SoundId.BossRoar, EyePosition, 1f, phase == 3 ? 0.85f : 1f);
            Game.Player.View.Shake(0.6f);
            if (phase == 2) SlamTimer = GameConfig.Boss.SlamEvery;
            if (phase == 3)
            {
                VolleyTimer = GameConfig.Boss.VolleyInterval;
                ShooterSummonTimer = GameConfig.Boss.SummonShootersEvery;
            }
            EventHook?.Invoke(this, phase == 2 ? "phase2" : "phase3");
        }

        protected override void OnDied()
        {
            Visuals.SetTint(EnrageGlow, 0f);
            Game.Manager.OnBossDefeated(this);
        }

        /// <summary>
        /// Summon and slam timers run continuously once the fight started (also during a slam), so the intervals are
        /// exactly 15 s / 20 s / 8 s start-to-start.
        /// </summary>
        public void TickTimers(float dt)
        {
            if (Phase == 3) Visuals.SetTint(EnrageGlow, 0.75f + 0.25f * Mathf.Sin(Time.time * 6f));
            if (Phase >= 2) SlamTimer -= dt;
            if (Phase < 3)
            {
                RunnerSummonTimer -= dt;
                if (RunnerSummonTimer <= 0f)
                {
                    RunnerSummonTimer = GameConfig.Boss.SummonRunnersEvery;
                    Summon(EnemyType.Runner, GameConfig.Boss.SummonRunners);
                }
            }
            else
            {
                ShooterSummonTimer -= dt;
                if (ShooterSummonTimer <= 0f)
                {
                    ShooterSummonTimer = GameConfig.Boss.SummonShootersEvery;
                    Summon(EnemyType.Shooter, GameConfig.Boss.SummonShooters);
                }
            }
        }

        /// <summary>Weapons (bursts / volley); runs while the boss walks or stands, not during the slam.</summary>
        public void TickCombat(float dt, bool canFire)
        {
            summonPose = Mathf.Max(0f, summonPose - dt);
            if (summonPose > 0f) Anim.SetPose(Pose.Summon, 1f);

            if (Phase < 3)
            {
                if (burstLeft > 0)
                {
                    burstGap -= dt;
                    if (burstGap <= 0f)
                    {
                        burstGap = GameConfig.Boss.BurstRoundGap;
                        burstLeft--;
                        Vector3 dir = MathUtil.RandomInCone((Game.Player.ChestPosition - Body.Muzzle.position).normalized, GameConfig.Boss.BurstSpread);
                        FireRound(dir);
                    }
                }
                else
                {
                    BurstTimer -= dt;
                    if (BurstTimer <= 0f && canFire)
                    {
                        BurstTimer = GameConfig.Boss.BurstInterval;
                        burstLeft = GameConfig.Boss.BurstRounds;
                        burstGap = 0f;
                        EventHook?.Invoke(this, "burst");
                    }
                }
            }
            else
            {
                VolleyTimer -= dt;
                if (VolleyTimer <= 0f && canFire)
                {
                    VolleyTimer = GameConfig.Boss.VolleyInterval;
                    Vector3 aim = (Game.Player.ChestPosition - Body.Muzzle.position).normalized;
                    int n = GameConfig.Boss.VolleyCount;
                    for (int i = 0; i < n; i++)
                        FireRound(MathUtil.RotateY(aim, (i - (n - 1) * 0.5f) * GameConfig.Boss.VolleyStep));
                    EventHook?.Invoke(this, "volley");
                }
            }
            if (canFire && burstLeft > 0) Anim.SetPose(Pose.Cannon, 1f);
        }

        /// <summary>True when a jump-slam is due (phase 2 and 3).</summary>
        public bool SlamReady => Phase >= 2 && SlamTimer <= 0f;

        void FireRound(Vector3 dir)
        {
            Vector3 origin = Body.Muzzle.position;
            Game.Projectiles.Fire(origin, dir, GameConfig.Boss.ProjectileSpeed, GameConfig.Boss.ProjectileDamage, true, transform.position);
            Game.Audio.PlayAt(SoundId.BossShot, origin, 0.9f, Random.Range(0.95f, 1.05f));
        }

        void Summon(EnemyType type, int count)
        {
            int spawned = 0;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.value * 0.5f) * Mathf.PI * 2f / count;
                float r = Random.Range(GameConfig.Boss.SummonRadiusMin, GameConfig.Boss.SummonRadiusMax);
                Vector3 p = transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * r;
                if (Game.Enemies.Spawn(type, p, transform.eulerAngles.y) != null) spawned++;
            }
            if (spawned > 0)
            {
                summonPose = 0.8f;
                Game.Audio.PlayAt(SoundId.Summon, EyePosition, 1f);
                EventHook?.Invoke(this, type == EnemyType.Runner ? "summon_runners" : "summon_shooters");
            }
        }
    }
}
