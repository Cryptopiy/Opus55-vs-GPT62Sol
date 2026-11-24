using UnityEngine;
using UnityEngine.AI;
using static WaveSurvival.GameConfig;

namespace WaveSurvival
{
    internal sealed partial class Enemy
    {
        public readonly Transform Root;
        public readonly NavMeshAgent Agent;
        public readonly EnemyKind Kind;
        public EnemyState State { get; private set; }
        public bool Active => Root.gameObject.activeSelf;
        public bool Alive => Active && State != EnemyState.Dead;
        public float HP { get; private set; }
        public float Maximum => EnemyHealth[(int)Kind];
        public Vector3 Position => Root.position;
        public Vector3 Eyes => head.position;
        public Vector3 CoverPoint => cover;
        public bool HasCover => coverValid;
        public int Phase { get; private set; }
        public int RecoveryCount { get; private set; }
        public float StationaryAge => trackingPath ? Time.time - progressAt : 0;
        public Vector3 Goal { get; private set; }
        public bool HasGoal { get; private set; }

        readonly CoreGame game;
        readonly NavMeshPath path = new();
        readonly NavMeshQueryFilter filter = new() { agentTypeID = 0, areaMask = NavMesh.AllAreas };
        Vector3 progressPosition, cover, peek, chargeDirection, slamTarget, leapOrigin;
        float progressAt, moveAt, until, nextAttack, shotAt, nextSummon, nextSlam, corpseAt, flashUntil;
        int burstLeft, attackStep, flank;
        bool trackingPath, coverValid, hasPeek, chargingHit;
        readonly Transform figure, head, rightArm, leftArm, rightLeg, leftLeg;
        readonly Collider[] hitboxes;
        float gait, stance, visualDamage;
        Vector3 slump;
        readonly Vector3[] leapCorners;
        int leapCornerCount;
        float leapLength;

        public Enemy(CoreGame game, EnemyKind kind, int index)
        {
            this.game = game;
            Kind = kind;
            if (kind == EnemyKind.Warden) leapCorners = new Vector3[64];
            Root = Geometry.Root(kind.ToString(), game.transform);
            Root.gameObject.SetActive(false);
            Agent = Root.gameObject.AddComponent<NavMeshAgent>();
            Agent.radius = AgentRadius;
            Agent.height = AgentHeight;
            Agent.angularSpeed = 720;
            Agent.acceleration = 26;
            Agent.stoppingDistance = .18f;
            Agent.updateRotation = false;
            Agent.avoidancePriority = 20 + index % 60;
            Agent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
            figure = Geometry.Root("Primitive humanoid", Root);
            figure.localScale = Vector3.one * (kind == EnemyKind.Warden ? BossScale : 1);
            (head, rightArm, leftArm, rightLeg, leftLeg, hitboxes) = BuildModel();
            flank = index % 2 == 0 ? -1 : 1;
        }

        public bool Spawn(Vector3 point)
        {
            Root.position = point;
            Agent.enabled = true;
            Root.gameObject.SetActive(true);
            if (!Agent.isOnNavMesh)
            {
                Despawn();
                return false;
            }
            Agent.ResetPath();
            Agent.speed = EnemySpeed[(int)Kind];
            Agent.isStopped = false;
            HP = Maximum;
            Phase = 1;
            State = EnemyState.Idle;
            HasGoal = trackingPath = coverValid = hasPeek = false;
            RecoveryCount = attackStep = burstLeft = 0;
            moveAt = shotAt = until = flashUntil = 0;
            nextAttack = Time.time + .65f;
            nextSummon = Time.time + BossRunnerInterval;
            nextSlam = Time.time + BossSlamInterval;
            stance = visualDamage = gait = 0;
            figure.localPosition = Vector3.zero;
            figure.localRotation = Quaternion.identity;
            SetGlow(0);
            foreach (Collider shape in hitboxes) shape.enabled = true;
            Face(game.Player.Position - Position, 1);
            return true;
        }

        public void Despawn()
        {
            HasGoal = false;
            Root.gameObject.SetActive(false);
        }

        public void Tick(float dt)
        {
            if (State == EnemyState.Dead)
            {
                AnimateDeath();
                return;
            }
            switch (Kind)
            {
                case EnemyKind.Runner: RunBrain(dt); break;
                case EnemyKind.Shooter: ShooterBrain(dt); break;
                case EnemyKind.Tank: TankBrain(dt); break;
                case EnemyKind.Warden: BossBrain(dt); break;
            }
            CheckProgress();
            Animate(dt);
        }

        public float Armor(Vector3 from)
        {
            if (Kind != EnemyKind.Tank) return 1;
            if (State == EnemyState.Stunned) return StunMultiplier;
            Vector3 direction = Vector3.ProjectOnPlane(from, Vector3.up);
            float angle = Vector3.Angle(Root.forward, direction);
            return angle <= TankFrontAngle ? TankArmor : angle >= 180 - TankRearAngle ? TankRear : TankSide;
        }

        public bool Hurt(float amount, Vector3 source, bool headshot)
        {
            if (!Alive) return false;
            HP = Mathf.Max(0, HP - amount * Armor(source - Position) *
                (headshot ? Kind == EnemyKind.Warden ? BossHeadBonus : HeadBonus : 1));
            flashUntil = Time.time + HitFlashTime;
            if (HP > 0) return false;
            Stop();
            Agent.enabled = false;
            foreach (Collider shape in hitboxes) shape.enabled = false;
            State = EnemyState.Dead;
            corpseAt = Time.time;
            slump = new Vector3(92, 15 * flank, 18 * flank);
            game.EnemyDied(this);
            return true;
        }

        void Change(EnemyState state, float duration = 0)
        {
            State = state;
            until = Time.time + duration;
        }

        void Stop()
        {
            HasGoal = trackingPath = false;
            if (!Agent.isOnNavMesh) return;
            Agent.ResetPath();
            Agent.isStopped = true;
        }

        bool Go(Vector3 point, float sampleDistance = 3)
        {
            if (!Agent.isOnNavMesh || !Arena.Sample(point, out Vector3 destination, sampleDistance)) return false;
            Agent.isStopped = false;
            if (HasGoal && (Goal - destination).sqrMagnitude < .09f && Agent.hasPath) return true;
            if (!Agent.SetDestination(destination)) return false;
            Goal = destination;
            HasGoal = true;
            return true;
        }

        void Follow(Vector3 point, float cadence = .19f)
        {
            if (Time.time < moveAt) return;
            moveAt = Time.time + cadence;
            Go(point);
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0;
            if (direction.sqrMagnitude < .005f) return;
            Root.rotation = Quaternion.RotateTowards(Root.rotation, Quaternion.LookRotation(direction), 700 * dt);
        }

        bool CanSeePlayer() => Arena.Clear(Eyes, game.Player.Eyes);
        bool Arrived(float radius = .63f) => HasGoal && (Position - Goal).sqrMagnitude <= radius * radius;

        void CheckProgress()
        {
            if (!Agent.isOnNavMesh)
            {
                if (Arena.Sample(Position, out Vector3 safe, 5)) Agent.Warp(safe);
                HasGoal = trackingPath = false;
                return;
            }
            if (!HasGoal || Arrived())
            {
                trackingPath = false;
                return;
            }
            if (!trackingPath || (Position - progressPosition).sqrMagnitude >= StuckDistance * StuckDistance)
            {
                trackingPath = true;
                progressAt = Time.time;
                progressPosition = Position;
                return;
            }
            if (Time.time - progressAt < StuckTime) return;
            RecoveryCount++;
            progressAt = Time.time;
            progressPosition = Position;
            // A valid path can still be blocked by a crowd. Advance on its next reachable corner.
            if (NavMesh.CalculatePath(Position, Goal, filter, path) &&
                path.status == NavMeshPathStatus.PathComplete && path.GetCornersNonAlloc(game.PathCorners) > 1)
            {
                Vector3 step = Vector3.MoveTowards(Position, game.PathCorners[1], 1.3f);
                if (Arena.Sample(step, out Vector3 safe, .75f) && game.SlotClear(safe, this)) Agent.Warp(safe);
            }
            else if (Arena.Sample(Position, out Vector3 nearest, 3))
                Agent.Warp(nearest);
            Agent.SetDestination(Goal);
        }

        void Animate(float dt)
        {
            float stride = Mathf.Clamp01(Agent.velocity.magnitude / 3);
            gait += dt * 11 * stride;
            stance = Mathf.MoveTowards(stance, Kind == EnemyKind.Shooter &&
                (State == EnemyState.TakeCover || State == EnemyState.Idle && coverValid) ? 1 : 0, dt * 5);
            Vector3 offset = Vector3.up * (Mathf.Abs(Mathf.Sin(gait)) * stride * .055f - stance * .52f);
            if (Kind == EnemyKind.Warden && State == EnemyState.Charge && attackStep == 2)
            {
                float progress = Mathf.Clamp01(1 - (until - Time.time) / BossAirTime);
                offset += Root.InverseTransformVector(Vector3.Lerp(leapOrigin, slamTarget, progress) - Position);
                offset.y += Mathf.Sin(progress * Mathf.PI) * 2.3f;
            }
            figure.localPosition = offset;
            float swing = Mathf.Sin(gait) * stride * 35;
            rightLeg.localRotation = Quaternion.Euler(swing + stance * 28, 0, 0);
            leftLeg.localRotation = Quaternion.Euler(-swing + stance * 28, 0, 0);
            float raised = State == EnemyState.Attack || State == EnemyState.Peek ? -72 : 0;
            rightArm.localRotation = Quaternion.Euler(raised - swing * .7f, 0, -8);
            leftArm.localRotation = Quaternion.Euler(swing * .7f - (Kind == EnemyKind.Warden ? 34 : 0), 0, 8);
            float signal = Time.time < flashUntil ? 1 : 0;
            if (State == EnemyState.Charge || Kind == EnemyKind.Tank && State == EnemyState.Attack || Kind == EnemyKind.Warden && Phase == 3)
                signal = Mathf.Max(signal, .45f + Mathf.Sin(Time.time * 21) * .16f);
            if (!Mathf.Approximately(visualDamage, signal)) SetGlow(visualDamage = signal);
        }

        void AnimateDeath()
        {
            float elapsed = Time.time - corpseAt;
            if (elapsed >= CorpseTime)
            {
                Despawn();
                return;
            }
            float t = Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / .42f));
            figure.localRotation = Quaternion.Euler(slump * t);
            Vector3 offset = figure.localPosition;
            offset.y = Mathf.Lerp(.06f, .23f, t);
            figure.localPosition = offset;
            if (elapsed >= CorpseTime - 1) SetCorpseVisibility(Mathf.InverseLerp(CorpseTime - 1, CorpseTime, elapsed));
        }
    }

    internal sealed class HitZone : MonoBehaviour
    {
        [System.NonSerialized] public Enemy Enemy;
        public bool Head;
    }
}






