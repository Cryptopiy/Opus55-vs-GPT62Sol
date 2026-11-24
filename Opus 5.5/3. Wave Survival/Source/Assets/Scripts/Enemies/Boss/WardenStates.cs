using UnityEngine;

namespace WaveSurvival
{
    /// <summary>Boss Idle: entrance roar after stepping out of the gate.</summary>
    public sealed class BossIntroState : StateOf<WardenBoss>
    {
        public BossIntroState(WardenBoss owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Idle;
        public override string Detail => "intro";

        public override void Enter() => E.StopMoving();

        public override void Tick(float dt)
        {
            if (Game.Player == null || !Game.Player.IsAlive) return;
            E.FaceTowards(Game.Player.Position, 90f, dt);
            E.Anim.SetPose(Pose.Summon, Mathf.Clamp01(E.Fsm.TimeInState * 2f));
            if (E.Fsm.TimeInState >= GameConfig.Boss.IntroTime)
            {
                E.Anim.ClearPose();
                E.Fsm.Change(EnemyState.Chase);
            }
        }
    }

    /// <summary>Boss Chase: slow walk toward the player while the weapon/summon timers run.</summary>
    public sealed class BossChaseState : StateOf<WardenBoss>
    {
        public BossChaseState(WardenBoss owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Chase;
        public override string Detail => E.Phase == 1 ? "phase 1" : E.Phase == 2 ? "phase 2" : "phase 3 enraged";

        public override void Tick(float dt)
        {
            E.Anim.ClearPose();
            E.TickTimers(dt);
            if (E.SlamReady)
            {
                E.Fsm.Change(EnemyState.Charge);
                return;
            }
            E.MoveTo(Game.Player.Position);
            E.TickCombat(dt, E.CanSeePlayer());
            if (E.DistanceToPlayer <= GameConfig.Boss.StopDistance) E.Fsm.Change(EnemyState.Attack);
        }
    }

    /// <summary>Boss Attack: in range, plant and keep firing while turning to face the player.</summary>
    public sealed class BossAttackState : StateOf<WardenBoss>
    {
        public BossAttackState(WardenBoss owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Attack;
        public override string Detail => "firing";

        public override void Enter() => E.StopMoving();

        public override void Tick(float dt)
        {
            E.Anim.ClearPose();
            E.TickTimers(dt);
            if (E.SlamReady)
            {
                E.Fsm.Change(EnemyState.Charge);
                return;
            }
            E.FaceTowards(Game.Player.Position, 120f, dt);
            E.TickCombat(dt, E.CanSeePlayer());
            if (E.DistanceToPlayer > GameConfig.Boss.StopDistance + 2f) E.Fsm.Change(EnemyState.Chase);
        }
    }

    /// <summary>
    /// Boss Charge = jump-slam (S-23 phase 2+): the floor ring appears at the player's position, the boss crouches,
    /// leaps on a parabola and lands exactly 1 s after the ring appeared: 35 damage + knockback within 6 m.
    /// </summary>
    public sealed class BossSlamState : StateOf<WardenBoss>
    {
        enum Phase { Crouch, Air, Recover }
        Phase phase;
        float timer;
        Vector3 start, target;

        public BossSlamState(WardenBoss owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Charge;
        public override string Detail => phase == Phase.Air ? "jump-slam (air)" : "jump-slam";

        public override void Enter()
        {
            E.StopMoving();
            E.SlamTimer = GameConfig.Boss.SlamEvery;
            Vector3 pp = Game.Player.Position;
            if (!Game.Arena.SampleBoss(pp, 4f, out target)) target = E.transform.position;
            start = E.transform.position;
            phase = Phase.Crouch;
            timer = 0f;
            Game.Fx.ShowTelegraph(target, GameConfig.Boss.SlamRadius, GameConfig.Boss.SlamTelegraph);
            EnemyBase.EventHook?.Invoke(E, "slam_telegraph");
        }

        public override void Tick(float dt)
        {
            timer += dt;
            E.TickTimers(dt);
            switch (phase)
            {
                case Phase.Crouch:
                    E.FaceTowards(target, 360f, dt);
                    E.Anim.SetPose(Pose.SlamCrouch, 1f, true);
                    if (timer >= GameConfig.Boss.SlamCrouch)
                    {
                        phase = Phase.Air;
                        // Move the transform manually on the arc; the agent is re-synced with Warp on landing.
                        E.Agent.updatePosition = false;
                        Game.Audio.PlayAt(SoundId.Whoosh, E.EyePosition, 1f, 0.7f);
                    }
                    break;
                case Phase.Air:
                    float airTime = GameConfig.Boss.SlamTelegraph - GameConfig.Boss.SlamCrouch;
                    float u = Mathf.Clamp01((timer - GameConfig.Boss.SlamCrouch) / airTime);
                    Vector3 p = Vector3.Lerp(start, target, u) + Vector3.up * (4f * GameConfig.Boss.SlamArcHeight * u * (1f - u));
                    E.transform.position = p;
                    E.Anim.SetPose(Pose.Air, 1f, true);
                    if (u >= 1f) Land();
                    break;
                default:
                    E.Anim.SetPose(Pose.SlamCrouch, 1f - Mathf.Clamp01(timer / GameConfig.Boss.SlamRecover), true);
                    if (timer >= GameConfig.Boss.SlamRecover) E.Fsm.Change(EnemyState.Chase);
                    break;
            }
        }

        void Land()
        {
            E.transform.position = target;
            E.Agent.Warp(target);
            E.Agent.updatePosition = true;
            phase = Phase.Recover;
            timer = 0f;
            Game.Fx.SlamBurst(target, GameConfig.Boss.SlamRadius);
            Game.Fx.HideTelegraph();
            Game.Audio.PlayAt(SoundId.Slam, target, 1f);
            PlayerRig player = Game.Player;
            float d = MathUtil.FlatDistance(player.Position, target);
            player.View.Shake(Mathf.Lerp(1.6f, 0.4f, Mathf.Clamp01(d / 20f)));
            if (d <= GameConfig.Boss.SlamRadius && Mathf.Abs(player.Position.y - target.y) < 2.5f)
            {
                player.Health.Damage(GameConfig.Boss.SlamDamage, target);
                Vector3 push = MathUtil.FlatDirection(target, player.Position);
                player.Motor.AddImpulse(push * GameConfig.Boss.SlamKnockback + Vector3.up * 4f);
                EnemyBase.EventHook?.Invoke(E, "slam_hit");
            }
            EnemyBase.EventHook?.Invoke(E, "slam");
        }

        public override void Exit()
        {
            if (phase == Phase.Air)
            {
                // Interrupted mid-air (killed): put the body back on the ground at the landing point.
                E.transform.position = target;
                if (E.Agent.enabled) E.Agent.Warp(target);
            }
            E.Agent.updatePosition = true;
            Game.Fx.HideTelegraph();
            E.Anim.ClearPose();
        }
    }
}
