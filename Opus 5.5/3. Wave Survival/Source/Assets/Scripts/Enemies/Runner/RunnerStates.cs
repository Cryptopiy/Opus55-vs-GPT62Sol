using UnityEngine;

namespace WaveSurvival
{
    /// <summary>
    /// Runner Chase: sprint at the player. With neighbours nearby, aim for a flank point rotated
    /// FlankSide * 70 deg around the player (re-evaluated as the runner moves, so it curves around the player) and
    /// apply separation steering, so a pack fans out instead of lining up in single file.
    /// </summary>
    public sealed class RunnerChaseState : StateOf<RunnerEnemy>
    {
        string detail;
        public RunnerChaseState(RunnerEnemy owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Chase;
        public override string Detail => detail;

        public override void Tick(float dt)
        {
            PlayerRig player = Game.Player;
            Vector3 p = player.Position;
            Vector3 me = E.transform.position;
            float dist = MathUtil.FlatDistance(me, p);
            if (dist <= GameConfig.Enemy.RunnerRange && Mathf.Abs(me.y - p.y) < 1.4f)
            {
                E.Fsm.Change(EnemyState.Attack);
                return;
            }

            int neighbours = E.Neighbours(out Vector3 separation);
            Vector3 dest = p;
            detail = "direct";
            if (neighbours >= 1 && dist > GameConfig.Enemy.RunnerDirectDistance && Mathf.Abs(E.FlankSide) > 0.01f)
            {
                Vector3 fromPlayer = MathUtil.FlatDirection(p, me);
                Vector3 slot = MathUtil.RotateY(fromPlayer, E.FlankSide * GameConfig.Enemy.RunnerFlankAngle);
                Vector3 flankPoint = p + slot * GameConfig.Enemy.RunnerFlankRadius;
                if (Game.Arena.SampleHumanoid(flankPoint, 2.5f, out Vector3 sampled)) dest = sampled;
                detail = E.FlankSide < 0f ? "flank L" : "flank R";
            }
            E.MoveTo(dest);

            if (separation.sqrMagnitude > 1e-4f && E.AgentReady)
                E.Agent.Move(separation * (GameConfig.Enemy.RunnerSeparationStrength * dt));
            E.Anim.ClearPose();
        }
    }

    /// <summary>Runner Attack: stop, face the player, 0.3 s wind-up (arm raised), strike for 15, repeat every 1 s.</summary>
    public sealed class RunnerAttackState : StateOf<RunnerEnemy>
    {
        float timer;
        bool struck;
        public RunnerAttackState(RunnerEnemy owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Attack;
        public override string Detail => struck ? "strike" : "wind-up";

        public override void Enter()
        {
            E.StopMoving();
            timer = 0f;
            struck = false;
        }

        public override void Tick(float dt)
        {
            PlayerRig player = Game.Player;
            float dist = MathUtil.FlatDistance(E.transform.position, player.Position);
            E.FaceTowards(player.Position, 720f, dt);
            timer += dt;
            if (!struck)
            {
                E.Anim.SetPose(Pose.Windup, 1f);
                // The player escaped during the wind-up: give chase again.
                if (dist > GameConfig.Enemy.RunnerRange + 1.2f)
                {
                    E.Fsm.Change(EnemyState.Chase);
                    return;
                }
                if (timer >= GameConfig.Enemy.RunnerWindup)
                {
                    struck = true;
                    Game.Audio.PlayAt(SoundId.Whoosh, E.EyePosition, 0.5f, 1.6f);
                    Vector3 toPlayer = MathUtil.FlatDirection(E.transform.position, player.Position);
                    bool inFront = Vector3.Angle(E.transform.forward, toPlayer) < 75f;
                    if (dist <= GameConfig.Enemy.RunnerRange + 0.25f && inFront)
                    {
                        player.Health.Damage(GameConfig.Enemy.RunnerDamage, E.EyePosition);
                        EnemyBase.EventHook?.Invoke(E, "melee_hit");
                    }
                }
            }
            else
            {
                E.Anim.SetPose(Pose.Strike, 1f);
            }
            if (timer >= GameConfig.Enemy.RunnerAttackInterval)
            {
                E.Anim.ClearPose();
                if (dist <= GameConfig.Enemy.RunnerRange) { timer = 0f; struck = false; }
                else E.Fsm.Change(EnemyState.Chase);
            }
        }

        public override void Exit() => E.Anim.ClearPose();
    }
}
