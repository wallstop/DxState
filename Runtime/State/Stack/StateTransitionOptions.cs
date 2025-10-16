namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Threading;

    public readonly struct StateTransitionOptions
    {
        public static StateTransitionOptions Default => new StateTransitionOptions(
            default,
            null,
            true
        );

        public StateTransitionOptions(
            CancellationToken cancellationToken,
            TimeSpan? timeout,
            bool throwOnTimeout = true
        )
        {
            CancellationToken = cancellationToken;
            Timeout = timeout;
            ThrowOnTimeout = throwOnTimeout;
        }

        public CancellationToken CancellationToken { get; }

        public TimeSpan? Timeout { get; }

        public bool ThrowOnTimeout { get; }

        public bool HasTimeout => Timeout.HasValue;

        public static StateTransitionOptions FromCancellation(CancellationToken cancellationToken)
        {
            return new StateTransitionOptions(cancellationToken, null, true);
        }

        public static StateTransitionOptions FromTimeout(
            TimeSpan timeout,
            bool throwOnTimeout = true
        )
        {
            return new StateTransitionOptions(default, timeout, throwOnTimeout);
        }

        public StateTransitionOptions WithTimeout(
            TimeSpan timeout,
            bool throwOnTimeout = true
        )
        {
            return new StateTransitionOptions(CancellationToken, timeout, throwOnTimeout);
        }

        public StateTransitionOptions WithCancellation(CancellationToken cancellationToken)
        {
            return new StateTransitionOptions(cancellationToken, Timeout, ThrowOnTimeout);
        }
    }
}
