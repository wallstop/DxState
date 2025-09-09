namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public enum StateDirection
    {
        [Obsolete("Please use a valid value.")]
        None = 0,
        Forward = 1 << 0,
        Backward = 1 << 1,
    }

    public interface IState
    {
        string Name { get; }

        TickMode TickMode => TickMode.None;
        float? TimeInState { get; }
        bool TickWhenInactive => false;

        ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>;
        void Tick(TickMode mode, float delta);
        ValueTask Exit<TProgress>(IState nextState, TProgress progress, StateDirection direction)
            where TProgress : IProgress<float>;

        ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>;
    }
}
