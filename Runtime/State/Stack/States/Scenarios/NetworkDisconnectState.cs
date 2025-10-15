namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public interface INetworkDisconnector
    {
        bool IsConnected { get; }

        ValueTask DisconnectAsync(IProgress<float> progress, CancellationToken cancellationToken);
    }

    public sealed class NetworkDisconnectState : IState
    {
        private readonly INetworkDisconnector _disconnector;
        private readonly TimeSpan _timeout;
        private readonly Func<float> _progressProvider;
        private float _timeInState;

        public NetworkDisconnectState(
            string name,
            INetworkDisconnector disconnector,
            TimeSpan timeout,
            Func<float> progressProvider = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "NetworkDisconnectState requires a name.",
                    nameof(name)
                );
            }

            _disconnector = disconnector ?? throw new ArgumentNullException(nameof(disconnector));
            _timeout = timeout;
            _progressProvider = progressProvider;
            Name = name;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => _timeInState >= 0 ? Time.time - _timeInState : null;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _timeInState = Time.time;
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        public void Tick(TickMode mode, float delta) { }

        public async ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (!_disconnector.IsConnected)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            using CancellationTokenSource timeoutSource =
                _timeout > TimeSpan.Zero
                    ? new CancellationTokenSource(_timeout)
                    : new CancellationTokenSource();
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(
                timeoutSource.Token
            );

            try
            {
                await _disconnector.DisconnectAsync(
                    new ProgressRelay(progress, _progressProvider),
                    linked.Token
                );
                UnityExtensions.ReportProgress(progress, 1f);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Disconnect attempt for '{Name}' exceeded {_timeout.TotalSeconds:0.##} seconds."
                );
            }
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            return Exit(null, progress, StateDirection.Forward);
        }

        private readonly struct ProgressRelay : IProgress<float>
        {
            private readonly IProgress<float> _inner;
            private readonly Func<float> _provider;

            public ProgressRelay(IProgress<float> inner, Func<float> provider)
            {
                _inner = inner;
                _provider = provider;
            }

            public void Report(float value)
            {
                if (_inner == null)
                {
                    return;
                }

                if (_provider != null)
                {
                    _inner.Report(Mathf.Clamp01(_provider()));
                    return;
                }

                _inner.Report(Mathf.Clamp01(value));
            }
        }
    }
}
