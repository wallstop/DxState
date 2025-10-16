namespace WallstopStudios.DxState.State.Stack
{
    using System;

    public sealed class StateTransitionCanceledException : StateTransitionException
    {
        public StateTransitionCanceledException(
            StateTransitionPhase phase,
            IState state,
            Exception innerException
        )
            : base(phase, state, innerException)
        {
        }
    }
}
