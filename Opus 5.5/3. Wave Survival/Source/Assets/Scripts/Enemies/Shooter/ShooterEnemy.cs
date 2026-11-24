using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>
    /// S-15 Shooter: HP 60, 3.5 m/s. Keeps 12-18 m from the player using cover (see <see cref="CoverFinder"/>), peeks
    /// out to fire 3-round bursts (40 m/s projectiles, 8 damage, 25% deliberate 2 deg misses) and retreats inside 6 m.
    /// </summary>
    public sealed class ShooterEnemy : EnemyBase
    {
        public Vector3 CoverPoint;
        public bool HasCover;
        public Vector3 PeekPoint;
        public bool ReturningFromPeek;
        public bool Retreating;
        public readonly CoverFinder.Candidate[] Candidates = new CoverFinder.Candidate[CoverFinder.Max];
        public int CandidateCount;
        public float CandidateTime = -100f;

        protected override float Stride => 1.4f;
        protected override float Lean => 6f;
        protected override SoundId Growl => SoundId.ShooterGrowl;
        protected override SoundId DeathSound => SoundId.ShooterDeath;
        protected override float BlockerRadius => 0.4f;
        protected override float BlockerHeight => 1.85f;

        public bool PlayerTooClose => DistanceToPlayer < GameConfig.Enemy.ShooterRetreatDist;

        protected override EnemyBody BuildBody()
        {
            Type = EnemyType.Shooter;
            return EnemyBodies.Shooter(this);
        }

        protected override void ConfigureAgent(NavMeshAgent a)
        {
            a.agentTypeID = 0;
            a.radius = GameConfig.Nav.AgentRadius;
            a.height = GameConfig.Nav.AgentHeight;
            a.speed = GameConfig.Enemy.ShooterSpeed;
            a.acceleration = 14f;
            a.angularSpeed = 600f;
            a.stoppingDistance = 0.15f;
            a.avoidancePriority = 40;
            a.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        }

        protected override void BuildStates()
        {
            Fsm.Add(new IdleState(this, EnemyState.TakeCover));
            Fsm.Add(new ShooterTakeCoverState(this));
            Fsm.Add(new ShooterPeekState(this));
            Fsm.Add(new ShooterAttackState(this));
            Fsm.Add(new ShooterChaseState(this));
            Fsm.Add(new DeadState(this));
        }

        protected override void OnSpawned()
        {
            MaxHealth = GameConfig.Enemy.ShooterHp;
            HasCover = false;
            ReturningFromPeek = false;
            Retreating = false;
            CandidateCount = 0;
        }

        /// <summary>True if another living shooter already claimed a cover point near <paramref name="p"/>.</summary>
        public static bool IsReservedByOther(ShooterEnemy self, Vector3 p)
        {
            var list = Game.Enemies.Active;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is ShooterEnemy s && s != self && s.IsAlive && s.HasCover &&
                    MathUtil.FlatDistance(s.CoverPoint, p) < GameConfig.Enemy.CoverReserveRadius) return true;
            }
            return false;
        }

        public bool SearchCover(bool retreat)
        {
            bool found = CoverFinder.Find(this, retreat, Candidates, out CandidateCount, out Vector3 best);
            CandidateTime = Time.time;
            HasCover = found;
            if (found) CoverPoint = best;
            return found;
        }

        public void RequestRetreat()
        {
            Retreating = true;
            ReturningFromPeek = false;
            EventHook?.Invoke(this, "retreat");
            Fsm.Change(EnemyState.TakeCover);
        }

        /// <summary>One burst round: projectile at the player's chest, 25% of rounds deliberately offset by 2 degrees.</summary>
        public void FireAtPlayer()
        {
            Vector3 origin = Body.Muzzle.position;
            Vector3 dir = (Game.Player.ChestPosition - origin).normalized;
            bool miss = Random.value < GameConfig.Enemy.ShooterMissChance;
            if (miss) dir = MathUtil.OffsetByAngle(dir, GameConfig.Enemy.ShooterMissAngle);
            Game.Projectiles.Fire(origin, dir, GameConfig.Enemy.ShooterProjectileSpeed, GameConfig.Enemy.ShooterDamage, false, transform.position);
            Game.Audio.PlayAt(SoundId.EnemyShot, origin, 0.8f, Random.Range(0.95f, 1.05f));
            Game.Fx.ProjectileImpact(origin, dir, new Color(0.3f, 1.4f, 1.8f, 1f));
            EventHook?.Invoke(this, miss ? "shot_miss" : "shot");
        }
    }
}
