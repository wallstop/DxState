namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public interface INetworkConnector
    {
        bool IsConnected { get; }

        ValueTask ConnectAsync(IProgress<float> progress, CancellationToken cancellationToken);

        ValueTask DisconnectAsync(CancellationToken cancellationToken);
    }

    public sealed class NetworkConnectState : IState
    {
        private readonly INetworkConnector _connector;
        private readonly TimeSpan _connectionTimeout;
        private readonly bool _disconnectOnExit;
        private readonly Func<float> _progressProvider;

        public NetworkConnectState(
            string name,
            INetworkConnector connector,
            TimeSpan connectionTimeout,
            bool disconnectOnExit = true,
            Func<float> progressProvider = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("NetworkConnectState requires a name.", nameof(name));
            }

            _connector = connector ?? throw new ArgumentNullException(nameof(connector));
            _connectionTimeout = connectionTimeout;
            _disconnectOnExit = disconnectOnExit;
            _progressProvider = progressProvider;
            Name = name;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => null;

        public async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            if (_connector.IsConnected)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            using CancellationTokenSource timeoutSource = _connectionTimeout > TimeSpan.Zero
                ? new CancellationTokenSource(_connectionTimeout)
                : new CancellationTokenSource();
            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token);

            try
            {
                await _connector.ConnectAsync(new ProgressRelay(progress, _progressProvider), linked.Token);
                UnityExtensions.ReportProgress(progress, 1f);
            }
            catch (OperationCanceledException)
            {
                throw new TimeoutException(
                    $"Connection attempt for '{Name}' exceeded {_connectionTimeout.TotalSeconds:0.##} seconds."
                );
            }
        }

        public void Tick(TickMode mode, float delta) { }

        public async ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (!_disconnectOnExit || !_connector.IsConnected)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            await _connector.DisconnectAsync(CancellationToken.None);
            UnityExtensions.ReportProgress(progress, 1f);
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            return Exit(nextStatesInStack.Count > 0 ? nextStatesInStack[0] : null, progress, StateDirection.Forward);
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
