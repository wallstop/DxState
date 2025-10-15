namespace WallstopStudios.DxState.State.Stack.Components
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::DxMessaging.Core.Extensions;
    using Messages;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public sealed class StateStackManager : DxMessageAwareSingleton<StateStackManager>
    {
        public bool IsTransitioning => _stateStack.IsTransitioning;

        public IState CurrentState => _stateStack.CurrentState;
        public IState PreviousState => _stateStack.PreviousState;

        public float Progress => _stateStack.Progress;
        public IReadOnlyDictionary<string, IState> RegisteredStates => _stateStack.RegisteredStates;
        public IReadOnlyList<IState> Stack => _stateStack.Stack;

        public StateStackDiagnostics Diagnostics => _diagnostics;

        private readonly StateStack _stateStack = new();
        [SerializeField]
        private bool _enableDiagnosticsLogging;

        [SerializeField]
        [Min(1)]
        private int _diagnosticHistoryCapacity = 64;

        [SerializeField]
        private Diagnostics.StateStackLoggingProfile _loggingProfile;

        private StateStackDiagnostics _diagnostics;

        protected override void Awake()
        {
            base.Awake();
            _diagnostics = new StateStackDiagnostics(
                _stateStack,
                _diagnosticHistoryCapacity,
                _enableDiagnosticsLogging
            );
            ConfigureLoggingProfile();
            _stateStack.OnStatePopped += (previous, current) =>
            {
                StatePoppedMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.OnStatePushed += (previous, current) =>
            {
                StatePushedMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.OnTransitionStart += (previous, next) =>
            {
                TransitionStartMessage message = new(previous, next);
                message.EmitUntargeted();
            };
            _stateStack.OnTransitionComplete += (previous, current) =>
            {
                TransitionCompleteMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.OnFlattened += target =>
            {
                StateStackFlattenedMessage message = new(target);
                message.EmitUntargeted();
            };
            _stateStack.OnTransitionProgress += (state, progress) =>
            {
                TransitionProgressChangedMessage message = new(state, progress);
                message.EmitUntargeted();
            };
            _stateStack.OnStateManuallyRemoved += state =>
            {
                StateManuallyRemovedMessage message = new(state);
                message.EmitUntargeted();
            };
        }

        protected override void OnDestroy()
        {
            _diagnostics?.Dispose();
            _diagnostics = null;
            TeardownLoggingProfile();
            base.OnDestroy();
        }

        public int CountOf(IState state)
        {
            return _stateStack.CountOf(state);
        }

        public async ValueTask WaitForTransitionCompletionAsync()
        {
            await _stateStack.WaitForTransitionCompletionAsync();
        }

        public bool TryRegister(IState state, bool force = false)
        {
            return _stateStack.TryRegister(state, force);
        }

        public bool Unregister(IState state)
        {
            return _stateStack.Unregister(state);
        }

        public bool Unregister(string stateName)
        {
            return _stateStack.Unregister(stateName);
        }

        public async ValueTask PushAsync(IState newState)
        {
            await _stateStack.PushAsync(newState);
        }

        public async ValueTask PushAsync(string stateName)
        {
            await _stateStack.PushAsync(stateName);
        }

        public async ValueTask<IState> PopAsync()
        {
            return await _stateStack.PopAsync();
        }

        public async ValueTask<IState> TryPopAsync()
        {
            return await _stateStack.TryPopAsync();
        }

        public async ValueTask FlattenAsync(IState state)
        {
            await _stateStack.FlattenAsync(state);
        }

        public async ValueTask FlattenAsync(string stateName)
        {
            await _stateStack.FlattenAsync(stateName);
        }

        public async ValueTask RemoveAsync(string stateName)
        {
            await _stateStack.RemoveAsync(stateName);
        }

        public async ValueTask RemoveAsync(IState stateToRemove)
        {
            await _stateStack.RemoveAsync(stateToRemove);
        }

        public async ValueTask ClearAsync()
        {
            await _stateStack.ClearAsync();
        }

        private void Update()
        {
            _stateStack.Update();
        }

        private void FixedUpdate()
        {
            _stateStack.FixedUpdate();
        }

        private void LateUpdate()
        {
            _stateStack.LateUpdate();
        }

        private void ConfigureLoggingProfile()
        {
            if (_loggingProfile == null)
            {
                return;
            }

            if (_loggingProfile.LogTransitions)
            {
                _stateStack.OnTransitionComplete += HandleLoggedTransition;
            }

            if (_loggingProfile.LogProgress)
            {
                _stateStack.OnTransitionProgress += HandleLoggedProgress;
            }
        }

        private void TeardownLoggingProfile()
        {
            if (_loggingProfile == null)
            {
                return;
            }

            if (_loggingProfile.LogTransitions)
            {
                _stateStack.OnTransitionComplete -= HandleLoggedTransition;
            }

            if (_loggingProfile.LogProgress)
            {
                _stateStack.OnTransitionProgress -= HandleLoggedProgress;
            }
        }

        private void HandleLoggedTransition(IState previous, IState current)
        {
            string category = !string.IsNullOrWhiteSpace(_loggingProfile?.LogCategory)
                ? _loggingProfile.LogCategory
                : "DxState";
            Debug.LogFormat(
                "[{0}] Transition complete: {1} -> {2}",
                category,
                previous?.Name ?? "<none>",
                current?.Name ?? "<none>"
            );
        }

        private void HandleLoggedProgress(IState state, float progress)
        {
            string category = !string.IsNullOrWhiteSpace(_loggingProfile?.LogCategory)
                ? _loggingProfile.LogCategory
                : "DxState";
            Debug.LogFormat(
                "[{0}] Progress {1:P0} ({2})",
                category,
                progress,
                state?.Name ?? "<none>"
            );
        }
    }
}
