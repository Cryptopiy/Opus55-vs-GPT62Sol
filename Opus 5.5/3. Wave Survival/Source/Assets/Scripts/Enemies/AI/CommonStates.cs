using UnityEngine;

namespace WaveSurvival
{
    /// <summary>Idle: materialising after a spawn (or waiting while the player is dead), then hands over to the type's main state.</summary>
    public sealed class IdleState : StateOf<EnemyBase>
    {
        readonly EnemyState next;
        public IdleState(EnemyBase owner, EnemyState nextState) : base(owner) { next = nextState; }
        public override EnemyState Id => EnemyState.Idle;

        public override void Enter() => E.StopMoving();

        public override void Tick(float dt)
        {
            if (Game.Player == null || !Game.Player.IsAlive) return;
            if (E.Fsm.TimeInState >= GameConfig.Enemy.SpawnMaterializeTime) E.Fsm.Change(next);
        }
    }

    /// <summary>Stunned: helpless for a fixed time (tank after a charge, S-16: takes x1.5 damage meanwhile).</summary>
    public sealed class StunnedState : StateOf<EnemyBase>
    {
        readonly EnemyState next;
        public float Duration = 1f;
        public StunnedState(EnemyBase owner, EnemyState nextState) : base(owner) { next = nextState; }
        public override EnemyState Id => EnemyState.Stunned;

        public override void Enter() => E.StopMoving();

        public override void Tick(float dt)
        {
            E.Anim.SetPose(Pose.Stunned, 1f, true);
            if (E.Fsm.TimeInState >= Duration)
            {
                E.Anim.ClearPose();
                E.Fsm.Change(next);
            }
        }
    }

    /// <summary>
    /// Dead (S-17): the body topples (animated fall), lies for 5 s, fades out over 1 s and is returned to its pool.
    /// </summary>
    public sealed class DeadState : StateOf<EnemyBase>
    {
        bool fading;
        public DeadState(EnemyBase owner) : base(owner) { }
        public override EnemyState Id => EnemyState.Dead;

        public override void Enter() => fading = false;

        public override void Tick(float dt)
        {
            float t = E.Fsm.TimeInState;
            float delay = GameConfig.Enemy.DeathFadeDelay;
            if (t < delay) return;
            if (!fading)
            {
                fading = true;
                Game.Fx.ReleaseDecalsUnder(E.transform);
                E.Visuals.BeginFade();
            }
            float k = Mathf.Clamp01((t - delay) / GameConfig.Enemy.DeathFadeTime);
            E.Visuals.SetAlpha(1f - k);
            E.Body.Model.localPosition += Vector3.down * (0.25f * dt / GameConfig.Enemy.DeathFadeTime);
            if (k >= 1f) E.Despawn();
        }
    }
}
