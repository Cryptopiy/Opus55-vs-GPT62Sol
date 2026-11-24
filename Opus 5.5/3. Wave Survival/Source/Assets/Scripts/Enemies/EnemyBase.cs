using System;
using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>
    /// Shared enemy behaviour: NavMeshAgent movement helpers, FSM host, damage pipeline (zones, multipliers, hit flash,
    /// floating health bar), death, growls, the S-18 stuck fix and the "never outside the arena" validity check.
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour
    {
        /// <summary>Debug/test hooks (AutoTest, logs): (enemy, zone, multiplier, damage) and (enemy, event name).</summary>
        public static Action<EnemyBase, HitZone, float, float> DamageHook;
        public static Action<EnemyBase, string> EventHook;
        static int serialCounter;

        public EnemyType Type { get; protected set; }
        public NavMeshAgent Agent { get; private set; }
        public EnemyBody Body { get; private set; }
        public EnemyAnimator Anim { get; private set; }
        public EnemyVisuals Visuals { get; private set; }
        public readonly EnemyStateMachine Fsm = new EnemyStateMachine();
        public readonly StuckDetector Stuck = new StuckDetector();
        public float Health { get; protected set; }
        public float MaxHealth { get; protected set; }
        public bool IsAlive { get; private set; }
        public int Serial { get; private set; }
        public float LastDamageTime { get; private set; } = -100f;
        public bool HealthBarVisible => IsAlive && Time.time - LastDamageTime < GameConfig.Enemy.HealthBarTime;
        public Vector3 DebugTarget;
        public bool HasDebugTarget;

        protected CapsuleCollider Blocker;
        float growlTimer, validityTimer, nextRepath;
        Vector3 lastDest;
        bool hasDest;

        protected abstract EnemyBody BuildBody();
        protected abstract void BuildStates();
        protected abstract void ConfigureAgent(NavMeshAgent agent);
        protected abstract float Stride { get; }
        protected abstract float Lean { get; }
        protected abstract SoundId Growl { get; }
        protected abstract SoundId DeathSound { get; }
        protected abstract float BlockerRadius { get; }
        protected abstract float BlockerHeight { get; }
        protected virtual void OnSpawned() { }
        protected virtual void OnDied() { }
        protected virtual void OnDamaged(in DamageInfo info, float dealt) { }

        public virtual NavMeshQueryFilter Filter => Game.Arena.HumanoidFilter;
        public Color GlowColor => Body.Mats.Glow.GetColor(MaterialFactory.BaseColor);
        public float HeadHeight => Body.HeadHeight * Body.Scale;
        public Vector3 EyePosition => transform.position + Vector3.up * HeadHeight;
        public Vector3 CenterPosition => transform.position + Vector3.up * HeadHeight * 0.6f;
        public Vector3 HealthBarAnchor => transform.position + Vector3.up * (HeadHeight + 0.45f * Body.Scale);
        public bool AgentReady => Agent.enabled && Agent.isOnNavMesh;
        public float DistanceToPlayer => MathUtil.FlatDistance(transform.position, Game.Player.Position);
        public string StateDetail => Fsm.Current != null ? Fsm.Current.Detail : null;

        /// <summary>Called once by the pool after the GameObject (inactive) received its agent/body components.</summary>
        public void Init()
        {
            Agent = GetComponent<NavMeshAgent>();
            Blocker = GetComponent<CapsuleCollider>();
            Blocker.radius = BlockerRadius;
            Blocker.height = BlockerHeight;
            Blocker.center = new Vector3(0f, BlockerHeight * 0.5f, 0f);
            ConfigureAgent(Agent);
            Agent.autoRepath = true;
            Agent.autoBraking = true;
            Body = BuildBody();
            Anim = new EnemyAnimator(Body, Stride, Lean);
            Visuals = new EnemyVisuals(Body);
            BuildStates();
        }

        /// <summary>Activates the pooled enemy at a NavMesh position (the caller sampled it with this enemy's filter).</summary>
        public void Spawn(Vector3 position, float yaw)
        {
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            IsAlive = true;
            Serial = ++serialCounter;
            LastDamageTime = -100f;
            hasDest = false;
            HasDebugTarget = false;
            Anim.Reset();
            Visuals.Reset();
            Body.SetHitboxesEnabled(true);
            Blocker.enabled = true;
            Agent.enabled = true;
            gameObject.SetActive(true); // the agent binds to the NavMesh here
            if (AgentReady) Agent.isStopped = false;
            Stuck.Reset(position);
            growlTimer = UnityEngine.Random.Range(0.5f, GameConfig.Enemy.GrowlMax);
            validityTimer = GameConfig.Enemy.ValidityCheckInterval;
            OnSpawned();
            Health = MaxHealth;
            Fsm.Change(EnemyState.Idle);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            // The player died: everybody stands down (Idle waits until the player is alive again).
            if (IsAlive && Fsm.CurrentId != EnemyState.Idle && (Game.Player == null || !Game.Player.IsAlive))
                Fsm.Change(EnemyState.Idle);
            Fsm.Tick(dt);
            if (IsAlive)
            {
                Stuck.Tick(this, dt);
                validityTimer -= dt;
                if (validityTimer <= 0f)
                {
                    validityTimer = GameConfig.Enemy.ValidityCheckInterval;
                    EnsureValidPosition();
                }
                growlTimer -= dt;
                if (growlTimer <= 0f)
                {
                    growlTimer = UnityEngine.Random.Range(GameConfig.Enemy.GrowlMin, GameConfig.Enemy.GrowlMax);
                    Game.Audio.PlayAt(Growl, EyePosition, 0.75f, UnityEngine.Random.Range(0.9f, 1.1f));
                }
                Anim.Update(AgentReady ? Agent.velocity.magnitude : 0f, dt);
            }
            else
            {
                Anim.Update(0f, dt);
            }
            Visuals.Update(dt);
        }

        // ---------------- damage ----------------

        /// <summary>Default zone multiplier: x2 headshots (S-12). Overridden by the tank (armour) and boss (head x1.5).</summary>
        protected virtual float DamageMultiplier(in DamageInfo info, Hitbox hb) =>
            hb.Zone == HitZone.Head ? GameConfig.Weapons.HeadshotMultiplier : 1f;

        public DamageResult ApplyHit(ref DamageInfo info, Hitbox hb)
        {
            var r = new DamageResult();
            if (!IsAlive) return r;
            float mult = DamageMultiplier(info, hb);
            float dmg = info.Amount * mult;
            Health -= dmg;
            LastDamageTime = Time.time;
            Visuals.Flash();
            r.Dealt = dmg;
            r.Multiplier = mult;
            r.Headshot = hb.Zone == HitZone.Head;
            Game.Stats.DamageDealt += dmg;
            DamageHook?.Invoke(this, hb.Zone, mult, dmg);
            OnDamaged(info, dmg);
            if (Health <= 0f)
            {
                r.Killed = true;
                Die(info.Direction, r.Headshot, true);
            }
            return r;
        }

        /// <summary>Kills the enemy. <paramref name="credit"/> = false for scripted kills (boss defeat clean-up).</summary>
        public void Die(Vector3 hitDirection, bool headshot, bool credit)
        {
            if (!IsAlive) return;
            IsAlive = false;
            Health = 0f;
            StopMoving();
            Agent.enabled = false;
            Blocker.enabled = false;
            Body.SetHitboxesEnabled(false);
            Visuals.SetTint(Color.black, 0f);
            Anim.BeginDeath(transform.InverseTransformDirection(hitDirection));
            Game.Fx.DeathBurst(CenterPosition, GlowColor);
            Game.Audio.PlayAt(DeathSound, EyePosition, 1f, UnityEngine.Random.Range(0.92f, 1.08f));
            if (credit)
            {
                Game.Stats.AddKill(Type, headshot);
                Game.Pickups.RollDrops(this);
            }
            Game.Enemies.OnEnemyDied(this);
            EventHook?.Invoke(this, "died");
            OnDied();
            Fsm.Change(EnemyState.Dead);
        }

        /// <summary>Returns the enemy to its pool (end of the death fade, or cheat/clean-up).</summary>
        public void Despawn()
        {
            if (IsAlive)
            {
                IsAlive = false;
                Game.Enemies.OnEnemyDied(this);
            }
            Game.Fx.ReleaseDecalsUnder(transform);
            Fsm.Clear();
            Agent.enabled = false;
            Game.Enemies.Release(this);
        }

        // ---------------- movement helpers ----------------

        public void MoveTo(Vector3 destination, bool force = false)
        {
            DebugTarget = destination;
            HasDebugTarget = true;
            if (!AgentReady) return;
            if (Agent.isStopped) Agent.isStopped = false;
            // Re-path only when the goal moved noticeably or periodically, to keep 25 agents cheap.
            if (force || !hasDest || (destination - lastDest).sqrMagnitude > 0.36f || Time.time >= nextRepath)
            {
                Agent.SetDestination(destination);
                lastDest = destination;
                hasDest = true;
                nextRepath = Time.time + GameConfig.Enemy.RepathInterval;
            }
        }

        public void StopMoving()
        {
            hasDest = false;
            HasDebugTarget = false;
            if (!AgentReady) return;
            Agent.isStopped = true;
            Agent.ResetPath();
        }

        public bool WantsToMove => IsAlive && AgentReady && hasDest && !Agent.isStopped &&
                                   (Agent.pathPending || Agent.remainingDistance > Agent.stoppingDistance + 0.3f);

        public bool Arrived(float tolerance = 0.3f) =>
            AgentReady && !Agent.pathPending && Agent.remainingDistance <= Agent.stoppingDistance + tolerance;

        public void FaceTowards(Vector3 target, float degreesPerSecond, float dt)
        {
            Vector3 d = target - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < 1e-4f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(d), degreesPerSecond * dt);
        }

        public bool CanSeePlayer()
        {
            PlayerRig p = Game.Player;
            return p != null && !Physics.Linecast(EyePosition, p.ChestPosition, GameConfig.Layers.EnvironmentMask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>S-18 fix: recompute the path and snap to the nearest valid NavMesh point; repeated strikes jump ahead.</summary>
        public void Unstick(int strike)
        {
            EventHook?.Invoke(this, "stuck_fix");
            if (!Agent.enabled) return;
            Vector3 pos = transform.position;
            Vector3 dest = hasDest ? lastDest : pos;
            // Second strike: hop 1.5 m along the route, angled randomly so a crowd fans out instead of stacking.
            Vector3 probe = strike >= 2 ? pos + MathUtil.RotateY(MathUtil.FlatDirection(pos, dest), UnityEngine.Random.Range(-60f, 60f)) * 1.5f : pos;
            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 3f, Filter) && ArenaInfo.Inside(hit.position, 0.3f))
                Agent.Warp(hit.position);
            if (AgentReady)
            {
                Agent.ResetPath();
                if (hasDest) Agent.SetDestination(dest);
            }
        }

        /// <summary>Guarantees the enemy is on the NavMesh and inside the walls (never in geometry or outside the arena).</summary>
        void EnsureValidPosition()
        {
            if (!Agent.enabled) return;
            Vector3 p = transform.position;
            if (Agent.isOnNavMesh && ArenaInfo.Inside(p, 0.2f)) return;
            Vector3 probe = new Vector3(Mathf.Clamp(p.x, -20f, 20f), Mathf.Max(0f, p.y), Mathf.Clamp(p.z, -20f, 20f));
            if (NavMesh.SamplePosition(probe, out NavMeshHit hit, 6f, Filter)) Agent.Warp(hit.position);
            EventHook?.Invoke(this, "invalid_fix");
        }
    }
}
