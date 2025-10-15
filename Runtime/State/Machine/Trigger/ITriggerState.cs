namespace WallstopStudios.DxState.State.Machine.Trigger
{
    using WallstopStudios.DxState.State.Machine;

    public interface ITriggerState<TState, TTrigger>
    {
        TState Id { get; }

        bool TryGetTrigger(out TTrigger trigger, out TransitionContext context);

        void OnEnter(
            TriggerStateMachine<TState, TTrigger> machine,
            TState previousState,
            TransitionContext context
        );

        void OnExit(
            TriggerStateMachine<TState, TTrigger> machine,
            TState nextState,
            TransitionContext context
        );

        void Tick();
    }
}
