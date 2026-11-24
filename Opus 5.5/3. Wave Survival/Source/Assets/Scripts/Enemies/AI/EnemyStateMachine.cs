namespace WaveSurvival
{
    /// <summary>One state of an enemy's finite-state machine. Each state is its own class (section 4 of the spec).</summary>
    public abstract class EnemyStateBase
    {
        public abstract EnemyState Id { get; }
        public virtual void Enter() { }
        public abstract void Tick(float dt);
        public virtual void Exit() { }
        /// <summary>Extra text for the F2 AI debug label (e.g. "retreat", "flank L").</summary>
        public virtual string Detail => null;
    }

    /// <summary>Typed base so each state can reach its owner's specific members without casts.</summary>
    public abstract class StateOf<T> : EnemyStateBase where T : EnemyBase
    {
        protected readonly T E;
        protected StateOf(T owner) { E = owner; }
    }

    /// <summary>Finite-state machine with one instance of every state per enemy (no allocations at runtime).</summary>
    public sealed class EnemyStateMachine
    {
        readonly EnemyStateBase[] states = new EnemyStateBase[8];

        public EnemyStateBase Current { get; private set; }
        public EnemyState CurrentId => Current != null ? Current.Id : EnemyState.Idle;
        public float TimeInState { get; private set; }

        public void Add(EnemyStateBase state) => states[(int)state.Id] = state;

        public bool Has(EnemyState id) => states[(int)id] != null;

        public void Change(EnemyState id)
        {
            EnemyStateBase next = states[(int)id];
            if (next == null) return;
            Current?.Exit();
            Current = next;
            TimeInState = 0f;
            next.Enter();
        }

        public void Tick(float dt)
        {
            TimeInState += dt;
            Current?.Tick(dt);
        }

        public void Clear()
        {
            Current = null;
            TimeInState = 0f;
        }
    }
}
