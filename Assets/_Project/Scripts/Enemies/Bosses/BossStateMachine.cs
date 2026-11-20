namespace ChickenShooter.Enemies.Bosses
{
    /// <summary>
    /// Base class of every boss state (State pattern). A state owns one behaviour (move, attack, roar ...)
    /// and decides itself when it is finished, then asks the boss for the next state.
    /// </summary>
    public abstract class BossState
    {
        protected BossController boss;
        protected float time;

        public virtual string Name => GetType().Name.Replace("State", "");

        public void Bind(BossController owner) => boss = owner;

        public virtual void Enter() { }

        /// <summary>Called every physics step while active.</summary>
        public void Tick(float dt)
        {
            time += dt;
            OnTick(dt);
        }

        protected abstract void OnTick(float dt);

        public virtual void Exit() { }
    }

    /// <summary>Minimal finite state machine: exactly one active state, explicit transitions.</summary>
    public class BossStateMachine
    {
        private readonly BossController owner;

        public BossState Current { get; private set; }

        public BossStateMachine(BossController owner) => this.owner = owner;

        public void ChangeState(BossState next)
        {
            Current?.Exit();
            Current = next;
            if (Current == null) return;
            Current.Bind(owner);
            Current.Enter();
        }

        public void Tick(float dt) => Current?.Tick(dt);
    }
}
