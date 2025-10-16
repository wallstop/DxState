namespace WallstopStudios.DxState.State.Stack
{
    using System;

    public sealed class StateTransitionTimeoutException : StateTransitionException
    {
        public StateTransitionTimeoutException(
            StateTransitionPhase phase,
            IState state,
            TimeSpan timeout,
            Exception innerException
        )
            : base(phase, state, innerException)
        {
            Timeout = timeout;
        }

        public TimeSpan Timeout { get; }
    }
}
