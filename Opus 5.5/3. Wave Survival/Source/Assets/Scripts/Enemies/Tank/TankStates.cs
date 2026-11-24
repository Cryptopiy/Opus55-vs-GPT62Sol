using UnityEngine;
using UnityEngine.AI;

namespace WaveSurvival
{
    /// <summary>Tank Chase: lumber toward the player; within 12 m and with line of sight, start the charge.</summary>
    public sealed class TankChaseState : StateOf<TankEnemy>
    {
        float checkTimer;
        public TankChaseState(TankEnemy owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Chase;

        public override void Enter() => checkTimer = 0.2f;

        public override void Tick(float dt)
        {
            E.MoveTo(Game.Player.Position);
            E.Anim.ClearPose();
            checkTimer -= dt;
            if (checkTimer > 0f) return;
            checkTimer = 0.2f;
            if (E.DistanceToPlayer <= GameConfig.Enemy.TankChargeRange && E.CanSeePlayer()) E.Fsm.Change(EnemyState.Charge);
        }
    }

    /// <summary>
    /// Tank Charge: 1 s telegraph (red glow + roar, tracking the player), then a straight 10 m/s run for at most 2 s.
    /// The run line is clipped by NavMesh.Raycast and executed with NavMeshAgent.Move, so the tank never leaves the
    /// NavMesh. A hit deals 40 damage and knocks the player back at 8 m/s. Always ends in a 1.5 s stun.
    /// </summary>
    public sealed class TankChargeState : StateOf<TankEnemy>
    {
        static readonly Color ChargeGlow = new Color(4f, 0.25f, 0.1f);
        bool running, hit;
        float timer, travelled, maxDistance, dustTimer;
        int blockedFrames;
        Vector3 dir;

        public TankChargeState(TankEnemy owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Charge;
        public override string Detail => running ? "charging" : "telegraph";

        public override void Enter()
        {
            E.StopMoving();
            running = false;
            hit = false;
            timer = 0f;
            Game.Audio.PlayAt(SoundId.TankRoar, E.EyePosition, 1f);
            EnemyBase.EventHook?.Invoke(E, "telegraph");
        }

        public override void Tick(float dt)
        {
            PlayerRig player = Game.Player;
            timer += dt;
            if (!running)
            {
                E.FaceTowards(player.Position, 200f, dt);
                E.Anim.SetPose(Pose.Telegraph, 1f, true);
                E.Visuals.SetTint(ChargeGlow, Mathf.Clamp01(timer / GameConfig.Enemy.TankTelegraph) * (0.75f + 0.25f * Mathf.Sin(timer * 30f)));
                if (timer >= GameConfig.Enemy.TankTelegraph) StartRun(player);
                return;
            }

            E.Anim.SetPose(Pose.Charge, 1f);
            E.Visuals.SetTint(ChargeGlow, 1f);
            float step = GameConfig.Enemy.TankChargeSpeed * dt;
            if (travelled + step > maxDistance) step = Mathf.Max(0f, maxDistance - travelled);
            Vector3 before = E.transform.position;
            if (E.AgentReady && step > 0f) E.Agent.Move(dir * step);
            float moved = MathUtil.FlatDistance(before, E.transform.position);
            travelled += moved;
            blockedFrames = step > 0.001f && moved < step * 0.35f ? blockedFrames + 1 : 0;

            dustTimer -= dt;
            if (dustTimer <= 0f)
            {
                dustTimer = 0.06f;
                Game.Fx.DustPuff(E.transform.position, 0.5f);
            }

            Vector3 pp = player.Position;
            if (!hit && MathUtil.FlatDistance(E.transform.position, pp) <= GameConfig.Enemy.TankHitReach && Mathf.Abs(pp.y - E.transform.position.y) < 1.6f)
            {
                hit = true;
                player.Health.Damage(GameConfig.Enemy.TankChargeDamage, E.EyePosition);
                player.Motor.AddImpulse(dir * GameConfig.Enemy.TankKnockback + Vector3.up * 3f);
                player.View.Shake(1.2f);
                Game.Audio.PlayAt(SoundId.Slam, pp, 0.7f, 1.4f);
                EnemyBase.EventHook?.Invoke(E, "charge_hit");
                EndCharge();
                return;
            }
            if (MathUtil.FlatDistance(E.transform.position, pp) < 9f) player.View.Shake(0.25f);
            if (timer >= GameConfig.Enemy.TankChargeMaxTime || travelled >= maxDistance - 0.02f || blockedFrames >= 3) EndCharge();
        }

        void StartRun(PlayerRig player)
        {
            running = true;
            timer = 0f;
            travelled = 0f;
            blockedFrames = 0;
            dir = MathUtil.FlatDirection(E.transform.position, player.Position);
            E.transform.rotation = Quaternion.LookRotation(dir);
            E.Agent.updateRotation = false;
            // Clip the straight line at the NavMesh boundary (walls, cover, platform edge).
            float full = GameConfig.Enemy.TankChargeSpeed * GameConfig.Enemy.TankChargeMaxTime;
            Vector3 start = E.transform.position;
            maxDistance = NavMesh.Raycast(start, start + dir * full, out NavMeshHit nh, E.Filter) ? nh.distance : full;
            Game.Audio.PlayAt(SoundId.Whoosh, E.EyePosition, 0.9f, 0.6f);
            EnemyBase.EventHook?.Invoke(E, "charge_start");
        }

        void EndCharge()
        {
            E.Visuals.SetTint(ChargeGlow, 0f);
            E.Fsm.Change(EnemyState.Stunned);
            EnemyBase.EventHook?.Invoke(E, "stunned");
        }

        public override void Exit()
        {
            E.Agent.updateRotation = true;
            E.Visuals.SetTint(ChargeGlow, 0f);
            E.Anim.ClearPose();
        }
    }
}
