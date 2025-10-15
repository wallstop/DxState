namespace WallstopStudios.DxState.State.Machine.Trigger
{
    using WallstopStudios.DxState.State.Machine;

    public readonly struct TriggerTransitionExecutionContext<TState, TTrigger>
    {
        public TriggerTransitionExecutionContext(
            TState previousState,
            TState currentState,
            TTrigger trigger,
            TransitionContext context
        )
        {
            PreviousState = previousState;
            CurrentState = currentState;
            Trigger = trigger;
            Context = context;
        }

        public TState PreviousState { get; }

        public TState CurrentState { get; }

        public TTrigger Trigger { get; }

        public TransitionContext Context { get; }
    }
}
