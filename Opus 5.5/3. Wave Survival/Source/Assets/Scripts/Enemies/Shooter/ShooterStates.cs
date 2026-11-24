using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// TakeCover: search the best hidden point (or retreat point), move there, and wait. First arrival -> short settle
    /// then Peek. Coming back from a peek -> wait 1.5-3 s, then re-evaluate the cover (a fresh search).
    /// </summary>
    public sealed class ShooterTakeCoverState : StateOf<ShooterEnemy>
    {
        enum Phase { Moving, Waiting }
        Phase phase;
        bool peekNext;
        float waitUntil, checkTimer, moveTimer;

        public ShooterTakeCoverState(ShooterEnemy owner) : base(owner) { }
        public override EnemyState Id => EnemyState.TakeCover;
        public override string Detail => E.Retreating ? "retreat" : phase == Phase.Moving ? (E.ReturningFromPeek ? "back to cover" : "moving to cover") : "in cover";

        public override void Enter()
        {
            moveTimer = 0f;
            checkTimer = 0f;
            if (E.ReturningFromPeek && E.HasCover)
            {
                peekNext = false;
            }
            else
            {
                if (!E.SearchCover(E.Retreating))
                {
                    E.Retreating = false;
                    E.Fsm.Change(EnemyState.Attack);
                    return;
                }
                peekNext = true;
                EnemyBase.EventHook?.Invoke(E, E.Retreating ? "retreat_point" : "cover_chosen");
            }
            phase = Phase.Moving;
            E.MoveTo(E.CoverPoint, true);
        }

        public override void Tick(float dt)
        {
            if (!E.Retreating && E.PlayerTooClose)
            {
                E.RequestRetreat();
                return;
            }
            PlayerRig player = Game.Player;
            if (phase == Phase.Moving)
            {
                E.Anim.ClearPose();
                E.MoveTo(E.CoverPoint);
                moveTimer += dt;
                if (E.Arrived(0.25f))
                {
                    E.StopMoving();
                    phase = Phase.Waiting;
                    waitUntil = Time.time + (peekNext ? 0.35f : Random.Range(GameConfig.Enemy.ShooterWaitMin, GameConfig.Enemy.ShooterWaitMax));
                    E.ReturningFromPeek = false;
                    E.Retreating = false;
                }
                else if (moveTimer > 9f)
                {
                    E.ReturningFromPeek = false;
                    E.Fsm.Change(EnemyState.TakeCover); // taking too long: search again
                }
                return;
            }

            E.FaceTowards(player.Position, 240f, dt);
            E.Anim.SetPose(Pose.Crouch, 1f, true);
            checkTimer -= dt;
            if (checkTimer <= 0f)
            {
                checkTimer = 0.5f;
                if (!CoverFinder.IsHidden(E.CoverPoint, player.EyePosition))
                {
                    E.ReturningFromPeek = false;
                    E.Fsm.Change(EnemyState.TakeCover); // the player flanked the cover: re-evaluate now
                    return;
                }
            }
            if (Time.time >= waitUntil)
            {
                if (peekNext) E.Fsm.Change(EnemyState.Peek);
                else
                {
                    E.ReturningFromPeek = false;
                    E.Fsm.Change(EnemyState.TakeCover); // S-15 "re-evaluate cover"
                }
            }
        }

        public override void Exit() => E.Anim.ClearPose();
    }

    /// <summary>Peek: step out sideways until the player is visible, aim, fire a 3-round burst, return to cover.</summary>
    public sealed class ShooterPeekState : StateOf<ShooterEnemy>
    {
        enum Phase { Step, Aim, Fire, Done }
        static readonly string[] PhaseNames = { "step out", "aim", "burst", "back" };
        static readonly string[] BurstNames = { "burst 0/3", "burst 1/3", "burst 2/3", "burst 3/3" };
        Phase phase;
        float timer, nextShot;
        int shots;

        public ShooterPeekState(ShooterEnemy owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Peek;
        public override string Detail => phase == Phase.Fire ? BurstNames[Mathf.Min(shots, 3)] : PhaseNames[(int)phase];

        public override void Enter()
        {
            if (!CoverFinder.FindPeek(E, E.CoverPoint, out E.PeekPoint))
            {
                // No firing line from this cover: get closer and look for a better spot.
                E.HasCover = false;
                E.Fsm.Change(EnemyState.Chase);
                return;
            }
            phase = Phase.Step;
            timer = 0f;
            shots = 0;
            E.MoveTo(E.PeekPoint, true);
            EnemyBase.EventHook?.Invoke(E, "peek");
        }

        public override void Tick(float dt)
        {
            if (E.PlayerTooClose)
            {
                E.RequestRetreat();
                return;
            }
            PlayerRig player = Game.Player;
            timer += dt;
            switch (phase)
            {
                case Phase.Step:
                    E.MoveTo(E.PeekPoint);
                    if (E.Arrived(0.2f) || timer > 1.6f)
                    {
                        E.StopMoving();
                        phase = Phase.Aim;
                        timer = 0f;
                    }
                    break;
                case Phase.Aim:
                    E.FaceTowards(player.Position, 540f, dt);
                    E.Anim.SetPose(Pose.Aim, 1f);
                    if (timer >= GameConfig.Enemy.ShooterAimTime)
                    {
                        phase = Phase.Fire;
                        timer = 0f;
                        nextShot = 0f;
                    }
                    break;
                case Phase.Fire:
                    E.FaceTowards(player.Position, 540f, dt);
                    E.Anim.SetPose(Pose.Aim, 1f);
                    if (timer >= nextShot)
                    {
                        E.FireAtPlayer();
                        shots++;
                        nextShot += GameConfig.Enemy.ShooterBurstInterval;
                        if (shots >= GameConfig.Enemy.ShooterBurst)
                        {
                            phase = Phase.Done;
                            timer = 0f;
                        }
                    }
                    break;
                default:
                    E.Anim.SetPose(Pose.Aim, 1f);
                    if (timer >= 0.25f)
                    {
                        E.ReturningFromPeek = true;
                        E.Fsm.Change(EnemyState.TakeCover);
                    }
                    break;
            }
        }

        public override void Exit() => E.Anim.ClearPose();
    }

    /// <summary>Attack: fallback when no hidden point exists – stand, face the player and fire bursts; keep looking for cover.</summary>
    public sealed class ShooterAttackState : StateOf<ShooterEnemy>
    {
        float burstTimer, shotTimer, searchTimer, noLos;
        int shots;
        bool bursting;

        public ShooterAttackState(ShooterEnemy owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Attack;
        public override string Detail => "no cover";

        public override void Enter()
        {
            E.StopMoving();
            burstTimer = Random.Range(0.3f, 1f);
            searchTimer = 2f;
            noLos = 0f;
            bursting = false;
        }

        public override void Tick(float dt)
        {
            if (E.PlayerTooClose)
            {
                E.RequestRetreat();
                return;
            }
            PlayerRig player = Game.Player;
            E.FaceTowards(player.Position, 360f, dt);
            E.Anim.SetPose(Pose.Aim, 1f);
            bool los = E.CanSeePlayer();
            noLos = los ? 0f : noLos + dt;
            if (noLos > 1.5f || E.DistanceToPlayer > GameConfig.Enemy.ShooterMaxDist + 2f)
            {
                E.Fsm.Change(EnemyState.Chase);
                return;
            }
            searchTimer -= dt;
            if (searchTimer <= 0f)
            {
                searchTimer = 2f;
                if (E.SearchCover(false))
                {
                    E.Fsm.Change(EnemyState.TakeCover);
                    return;
                }
            }
            if (bursting)
            {
                shotTimer -= dt;
                if (shotTimer <= 0f)
                {
                    E.FireAtPlayer();
                    shots++;
                    shotTimer = GameConfig.Enemy.ShooterBurstInterval;
                    if (shots >= GameConfig.Enemy.ShooterBurst)
                    {
                        bursting = false;
                        burstTimer = Random.Range(GameConfig.Enemy.ShooterWaitMin, GameConfig.Enemy.ShooterWaitMax);
                    }
                }
            }
            else
            {
                burstTimer -= dt;
                if (burstTimer <= 0f && los)
                {
                    bursting = true;
                    shots = 0;
                    shotTimer = 0f;
                }
            }
        }

        public override void Exit() => E.Anim.ClearPose();
    }

    /// <summary>Chase: close in to the 12-18 m band (or regain line of sight), then look for cover again.</summary>
    public sealed class ShooterChaseState : StateOf<ShooterEnemy>
    {
        float retryTimer;
        public ShooterChaseState(ShooterEnemy owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Chase;
        public override string Detail => "reposition";

        public override void Enter() => retryTimer = 1f;

        public override void Tick(float dt)
        {
            if (E.PlayerTooClose)
            {
                E.RequestRetreat();
                return;
            }
            PlayerRig player = Game.Player;
            Vector3 p = player.Position;
            Vector3 goal = p + MathUtil.FlatDirection(p, E.transform.position) * 14f;
            if (Game.Arena.SampleHumanoid(goal, 3f, out Vector3 s)) goal = s;
            E.MoveTo(goal);
            E.Anim.ClearPose();
            retryTimer -= dt;
            if (retryTimer <= 0f)
            {
                retryTimer = 1f;
                if (E.DistanceToPlayer <= GameConfig.Enemy.ShooterMaxDist) E.Fsm.Change(EnemyState.TakeCover);
            }
        }
    }
}
