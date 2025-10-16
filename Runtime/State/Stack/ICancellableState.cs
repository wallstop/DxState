namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public interface ICancellableState : IState
    {
        ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction,
            CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>;

        ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction,
            CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>;

        ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress,
            CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>;
    }
}
