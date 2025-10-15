namespace WallstopStudios.DxState.State.Stack.Components
{
    using System;
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

        public event Action<IState, IState> StatePushed;
        public event Action<IState, IState> StatePopped;
        public event Action<IState, IState> TransitionCompleted;

        private readonly StateStack _stateStack = new();
        [Header("Beginner Setup")]
        [SerializeField]
        private bool _autoAddMessagingComponent = true;

        [SerializeField]
        private bool _registerChildGameStates = true;

        [SerializeField]
        private bool _forceRegisterStates = true;

        [SerializeField]
        private bool _pushInitialStateOnStart = true;

        [SerializeField]
        private GameState _initialState;

        [SerializeField]
        private List<GameState> _additionalStates = new List<GameState>();

        [SerializeField]
        private bool _enableDiagnosticsLogging;

        [SerializeField]
        [Min(1)]
        private int _diagnosticHistoryCapacity = 64;

        [SerializeField]
        private Diagnostics.StateStackLoggingProfile _loggingProfile;

        private StateStackDiagnostics _diagnostics;
        private readonly List<GameState> _registrationScratch = new List<GameState>();
        private readonly HashSet<GameState> _uniqueStateScratch = new HashSet<GameState>();
        private bool _registeredStatesDuringSetup;

        protected override void Awake()
        {
            EnsureMessagingComponent();
            base.Awake();
            SubscribeToStateStackEvents();
            _diagnostics = new StateStackDiagnostics(
                _stateStack,
                _diagnosticHistoryCapacity,
                _enableDiagnosticsLogging
            );
            ConfigureLoggingProfile();
            RegisterConfiguredStates();
        }

        protected override void OnDestroy()
        {
            _diagnostics?.Dispose();
            _diagnostics = null;
            TeardownLoggingProfile();
            UnsubscribeFromStateStackEvents();
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

        private void Start()
        {
            if (!_pushInitialStateOnStart)
            {
                return;
            }

            if (_initialState == null)
            {
                if (_registeredStatesDuringSetup)
                {
                    ReportConfigurationError("Initial state is not assigned.");
                }
                return;
            }

            PushInitialStateAsync();
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

        private void EnsureMessagingComponent()
        {
            if (!_autoAddMessagingComponent)
            {
                return;
            }

            global::DxMessaging.Unity.MessagingComponent existing;
            if (TryGetComponent(out existing))
            {
                return;
            }

            gameObject.AddComponent<global::DxMessaging.Unity.MessagingComponent>();
        }

        private void RegisterConfiguredStates()
        {
            _registeredStatesDuringSetup = false;
            _registrationScratch.Clear();
            _uniqueStateScratch.Clear();

            if (_registerChildGameStates)
            {
                GameState[] discovered = GetComponentsInChildren<GameState>(true);
                for (int i = 0; i < discovered.Length; i++)
                {
                    TryAddRegistrationCandidate(discovered[i]);
                }
            }

            if (_additionalStates != null)
            {
                for (int i = 0; i < _additionalStates.Count; i++)
                {
                    TryAddRegistrationCandidate(_additionalStates[i]);
                }
            }

            for (int i = 0; i < _registrationScratch.Count; i++)
            {
                GameState candidate = _registrationScratch[i];
                bool registered = _stateStack.TryRegister(candidate, _forceRegisterStates);
                if (!registered && !_forceRegisterStates)
                {
                    ReportConfigurationError(
                        $"State '{candidate.name}' is already registered. Enable Force Register to override."
                    );
                }
            }

            if (_initialState == null && _registrationScratch.Count > 0)
            {
                _initialState = _registrationScratch[0];
            }

            if (_registrationScratch.Count > 0)
            {
                _registeredStatesDuringSetup = true;
            }

            _registrationScratch.Clear();
            _uniqueStateScratch.Clear();
        }

        private void TryAddRegistrationCandidate(GameState candidate)
        {
            if (candidate == null)
            {
                return;
            }

            if (!_uniqueStateScratch.Add(candidate))
            {
                ReportConfigurationError(
                    $"Duplicate state reference detected for '{candidate.name}'."
                );
                return;
            }

            _registrationScratch.Add(candidate);
        }

        private async void PushInitialStateAsync()
        {
            if (ReferenceEquals(CurrentState, _initialState))
            {
                return;
            }

            IReadOnlyList<IState> activeStack = _stateStack.Stack;
            for (int i = 0; i < activeStack.Count; i++)
            {
                if (ReferenceEquals(activeStack[i], _initialState))
                {
                    await _stateStack.FlattenAsync(_initialState);
                    return;
                }
            }

            await _stateStack.PushAsync(_initialState);
        }

        private void SubscribeToStateStackEvents()
        {
            _stateStack.OnStatePopped += HandleStatePopped;
            _stateStack.OnStatePushed += HandleStatePushed;
            _stateStack.OnTransitionStart += HandleTransitionStart;
            _stateStack.OnTransitionComplete += HandleTransitionComplete;
            _stateStack.OnFlattened += HandleStateFlattened;
            _stateStack.OnTransitionProgress += HandleTransitionProgress;
            _stateStack.OnStateManuallyRemoved += HandleStateManuallyRemoved;
        }

        private void UnsubscribeFromStateStackEvents()
        {
            _stateStack.OnStatePopped -= HandleStatePopped;
            _stateStack.OnStatePushed -= HandleStatePushed;
            _stateStack.OnTransitionStart -= HandleTransitionStart;
            _stateStack.OnTransitionComplete -= HandleTransitionComplete;
            _stateStack.OnFlattened -= HandleStateFlattened;
            _stateStack.OnTransitionProgress -= HandleTransitionProgress;
            _stateStack.OnStateManuallyRemoved -= HandleStateManuallyRemoved;
        }

        private void HandleStatePushed(IState previous, IState current)
        {
            StatePushedMessage message = new StatePushedMessage(previous, current);
            message.EmitUntargeted();
            StatePushed?.Invoke(previous, current);
        }

        private void HandleStatePopped(IState previous, IState current)
        {
            StatePoppedMessage message = new StatePoppedMessage(previous, current);
            message.EmitUntargeted();
            StatePopped?.Invoke(previous, current);
        }

        private void HandleTransitionStart(IState previous, IState next)
        {
            TransitionStartMessage message = new TransitionStartMessage(previous, next);
            message.EmitUntargeted();
        }

        private void HandleTransitionComplete(IState previous, IState current)
        {
            TransitionCompleteMessage message = new TransitionCompleteMessage(previous, current);
            message.EmitUntargeted();
            TransitionCompleted?.Invoke(previous, current);
        }

        private void HandleStateFlattened(IState target)
        {
            StateStackFlattenedMessage message = new StateStackFlattenedMessage(target);
            message.EmitUntargeted();
        }

        private void HandleTransitionProgress(IState state, float progress)
        {
            TransitionProgressChangedMessage message = new TransitionProgressChangedMessage(state, progress);
            message.EmitUntargeted();
        }

        private void HandleStateManuallyRemoved(IState state)
        {
            StateManuallyRemovedMessage message = new StateManuallyRemovedMessage(state);
            message.EmitUntargeted();
        }

        private void ReportConfigurationError(string details)
        {
            Debug.LogError($"StateStackManager configuration issue: {details}", this);
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
