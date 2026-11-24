using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>
    /// S-14 Runner: HP 40, 6 m/s, melee 1.6 m for 15 damage once per second after a 0.3 s wind-up. When two or more
    /// runners are near each other they separate and take different flank slots around the player.
    /// </summary>
    public sealed class RunnerEnemy : EnemyBase
    {
        static readonly float[] FlankSlots = { -1f, 1f, 0f, -0.55f, 0.55f };

        /// <summary>-1 = flank left, +1 = flank right, 0 = straight in. Assigned round-robin so a group spreads out.</summary>
        public float FlankSide { get; private set; }

        protected override float Stride => 1.7f;
        protected override float Lean => 22f;
        protected override SoundId Growl => SoundId.RunnerGrowl;
        protected override SoundId DeathSound => SoundId.RunnerDeath;
        protected override float BlockerRadius => 0.38f;
        protected override float BlockerHeight => 1.7f;

        protected override EnemyBody BuildBody()
        {
            Type = EnemyType.Runner;
            return EnemyBodies.Runner(this);
        }

        protected override void ConfigureAgent(NavMeshAgent a)
        {
            a.agentTypeID = 0;
            a.radius = GameConfig.Nav.AgentRadius;
            a.height = GameConfig.Nav.AgentHeight;
            a.speed = GameConfig.Enemy.RunnerSpeed;
            a.acceleration = 40f;
            a.angularSpeed = 900f;
            a.stoppingDistance = 0.9f;
            a.avoidancePriority = 50;
            a.obstacleAvoidanceType = ObstacleAvoidanceType.HighQualityObstacleAvoidance;
        }

        protected override void BuildStates()
        {
            Fsm.Add(new IdleState(this, EnemyState.Chase));
            Fsm.Add(new RunnerChaseState(this));
            Fsm.Add(new RunnerAttackState(this));
            Fsm.Add(new DeadState(this));
        }

        protected override void OnSpawned()
        {
            MaxHealth = GameConfig.Enemy.RunnerHp;
            FlankSide = FlankSlots[Serial % FlankSlots.Length];
        }

        /// <summary>
        /// Counts runners within the "near each other" radius and returns a separation push away from the ones that
        /// are too close (classic boids separation weighted by proximity).
        /// </summary>
        public int Neighbours(out Vector3 separation)
        {
            separation = Vector3.zero;
            int count = 0;
            Vector3 me = transform.position;
            var list = Game.Enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                EnemyBase o = list[i];
                if (o == this || !o.IsAlive || o.Type != EnemyType.Runner) continue;
                Vector3 d = me - o.transform.position;
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > GameConfig.Enemy.RunnerNeighborRadius) continue;
                count++;
                if (dist < GameConfig.Enemy.RunnerSeparationRadius && dist > 1e-3f)
                    separation += d / dist * (1f - dist / GameConfig.Enemy.RunnerSeparationRadius);
            }
            return count;
        }
    }
}
