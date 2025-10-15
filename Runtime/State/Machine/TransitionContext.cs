namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using Component;

    [Flags]
    public enum TransitionFlags
    {
        None = 0,
        Forced = 1 << 0,
        ExternalRequest = 1 << 1,
    }

    public enum TransitionCause
    {
        Unspecified = 0,
        Initialization = 1,
        RuleSatisfied = 2,
        Forced = 3,
        Manual = 4,
    }

    public readonly struct TransitionContext
    {
        public TransitionContext(TransitionCause cause, TransitionFlags flags = TransitionFlags.None)
        {
            Cause = cause;
            Flags = flags;
        }

        public TransitionCause Cause { get; }

        public TransitionFlags Flags { get; }

        public bool HasDefinedCause => Cause != TransitionCause.Unspecified;
    }

    public readonly struct TransitionExecutionContext<T>
    {
        public TransitionExecutionContext(
            T previousState,
            T currentState,
            Transition<T> transition,
            TransitionContext context
        )
        {
            PreviousState = previousState;
            CurrentState = currentState;
            Transition = transition;
            Context = context;
        }

        public T PreviousState { get; }

        public T CurrentState { get; }

        public Transition<T> Transition { get; }

        public TransitionContext Context { get; }
    }
}

