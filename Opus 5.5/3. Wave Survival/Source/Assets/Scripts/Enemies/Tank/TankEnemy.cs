using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>
    /// S-16 Tank: HP 300, 2.5 m/s. Frontal armour (x0.4 within +-70 deg of its forward), x1.0 from the sides, x2.0 on the
    /// glowing back weak point (rear +-45 deg). Charges when within 12 m with line of sight; stunned 1.5 s after a
    /// charge, taking x1.5 damage.
    /// </summary>
    public sealed class TankEnemy : EnemyBase
    {
        public float LastArmorMultiplier { get; private set; } = 1f;

        protected override float Stride => 1.5f;
        protected override float Lean => 10f;
        protected override SoundId Growl => SoundId.TankGrowl;
        protected override SoundId DeathSound => SoundId.TankDeath;
        protected override float BlockerRadius => 0.5f;
        protected override float BlockerHeight => 2f;

        protected override EnemyBody BuildBody()
        {
            Type = EnemyType.Tank;
            return EnemyBodies.Tank(this);
        }

        protected override void ConfigureAgent(NavMeshAgent a)
        {
            a.agentTypeID = 0;
            a.radius = GameConfig.Nav.AgentRadius;
            a.height = GameConfig.Nav.AgentHeight;
            a.speed = GameConfig.Enemy.TankSpeed;
            a.acceleration = 10f;
            a.angularSpeed = 200f;
            a.stoppingDistance = 1.4f;
            a.avoidancePriority = 30;
            a.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        }

        protected override void BuildStates()
        {
            Fsm.Add(new IdleState(this, EnemyState.Chase));
            Fsm.Add(new TankChaseState(this));
            Fsm.Add(new TankChargeState(this));
            Fsm.Add(new StunnedState(this, EnemyState.Chase) { Duration = GameConfig.Enemy.TankStun });
            Fsm.Add(new DeadState(this));
        }

        protected override void OnSpawned() => MaxHealth = GameConfig.Enemy.TankHp;

        /// <summary>
        /// Armour from the direction the shot came from (relative to the tank's facing). The x2 rear bonus applies when
        /// the glowing back panel itself is hit from the rear arc; stun (x1.5) and headshots (x2) stack on top.
        /// </summary>
        protected override float DamageMultiplier(in DamageInfo info, Hitbox hb)
        {
            Vector3 toAttacker = MathUtil.FlatDirection(transform.position, info.SourcePosition);
            float angle = Vector3.Angle(MathUtil.Flat(transform.forward).normalized, toAttacker);
            float armor;
            if (angle <= GameConfig.Enemy.TankFrontArc) armor = GameConfig.Enemy.TankFrontMult;
            else if (angle >= 180f - GameConfig.Enemy.TankRearArc && hb.Zone == HitZone.WeakPoint) armor = GameConfig.Enemy.TankRearMult;
            else armor = GameConfig.Enemy.TankSideMult;
            LastArmorMultiplier = armor;
            float m = armor;
            if (hb.Zone == HitZone.Head) m *= GameConfig.Weapons.HeadshotMultiplier;
            if (Fsm.CurrentId == EnemyState.Stunned) m *= GameConfig.Enemy.TankStunMult;
            return m;
        }
    }
}
