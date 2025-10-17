namespace WallstopStudios.DxState.State.Stack.Components
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using global::DxMessaging.Unity;
    using UnityEngine;
    using WallstopStudios.UnityHelpers.Core.Extension;

    [DefaultExecutionOrder(-500)]
    [RequireComponent(typeof(StateStackManager))]
    [RequireComponent(typeof(MessagingComponent))]
    public sealed class StateStackBootstrapper : MonoBehaviour
    {
        [SerializeField]
        private StateStackManager _stateStackManager;

        [SerializeField]
        private bool _registerChildGameStates = true;

        [SerializeField]
        private GameState[] _additionalStates = Array.Empty<GameState>();

        [SerializeField]
        private bool _forceRegisterStates = true;

        [SerializeField]
        private bool _pushInitialStateOnStart = true;

        [SerializeField]
        private GameState _initialState;

        private MessagingComponent _messagingComponent;
        private readonly List<GameState> _registrationScratch = new List<GameState>();
        private readonly HashSet<GameState> _uniqueStateScratch = new HashSet<GameState>();
        private bool _registeredStatesDuringSetup;
        private bool _autoAssignInitialState = true;

        private void Awake()
        {
            EnsureDependencies();
        }

        private IEnumerator Start()
        {
            RegisterConfiguredStates();

            if (!_pushInitialStateOnStart)
            {
                yield break;
            }

            if (_initialState == null)
            {
                if (_registeredStatesDuringSetup)
                {
                    Debug.LogError(
                        "StateStackBootstrapper has no initial state configured. Assign a state or disable Push Initial State On Start.",
                        this
                    );
                }
                yield break;
            }

            if (ReferenceEquals(_stateStackManager.CurrentState, _initialState))
            {
                yield break;
            }

            IReadOnlyList<IState> stack = _stateStackManager.Stack;
            for (int i = 0; i < stack.Count; i++)
            {
                if (ReferenceEquals(stack[i], _initialState))
                {
                    yield return _stateStackManager.FlattenAsync(_initialState).AsCoroutine();
                    yield break;
                }
            }

            yield return _stateStackManager.PushAsync(_initialState).AsCoroutine();
        }

        private void EnsureDependencies()
        {
            if (_stateStackManager == null)
            {
                _stateStackManager = GetComponent<StateStackManager>();
            }

            if (_stateStackManager == null)
            {
                throw new InvalidOperationException(
                    "StateStackManager is required on the same GameObject as StateStackBootstrapper."
                );
            }

            if (!TryGetComponent(out _messagingComponent))
            {
                _messagingComponent = gameObject.AddComponent<MessagingComponent>();
            }
        }

        private void RegisterConfiguredStates()
        {
            _registrationScratch.Clear();
            _uniqueStateScratch.Clear();
            _registeredStatesDuringSetup = false;

            if (_registerChildGameStates)
            {
                GameState[] discoveredStates = GetComponentsInChildren<GameState>(true);
                for (int i = 0; i < discoveredStates.Length; i++)
                {
                    CollectCandidate(discoveredStates[i]);
                }
            }

            if (_additionalStates != null)
            {
                for (int i = 0; i < _additionalStates.Length; i++)
                {
                    CollectCandidate(_additionalStates[i]);
                }
            }

            IReadOnlyDictionary<string, IState> registeredStates = _stateStackManager.RegisteredStates;

            for (int i = 0; i < _registrationScratch.Count; i++)
            {
                GameState state = _registrationScratch[i];
                IState existingState;
                if (
                    registeredStates != null
                    && registeredStates.TryGetValue(state.Name, out existingState)
                    && ReferenceEquals(existingState, state)
                )
                {
                    continue;
                }
                bool registered = _stateStackManager.TryRegister(state, _forceRegisterStates);
                if (!registered && !_forceRegisterStates)
                {
                    Debug.LogError(
                        $"State '{state.name}' is already registered with the stack and was not added again. Enable Force Register to override.",
                        state
                    );
                }
            }

            if (_autoAssignInitialState && _initialState == null && _registrationScratch.Count > 0)
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

        internal void SetForceRegisterStates(bool value)
        {
            _forceRegisterStates = value;
        }

        internal void SetAdditionalStates(params GameState[] states)
        {
            _additionalStates = states ?? Array.Empty<GameState>();
        }

        internal void SetRegisterChildGameStates(bool value)
        {
            _registerChildGameStates = value;
        }

        internal void SetAutoAssignInitialState(bool value)
        {
            _autoAssignInitialState = value;
        }

        internal void SetPushInitialStateOnStart(bool value)
        {
            _pushInitialStateOnStart = value;
        }

        private void CollectCandidate(GameState candidate)
        {
            if (candidate == null)
            {
                return;
            }

            if (!_uniqueStateScratch.Add(candidate))
            {
                Debug.LogError(
                    "Duplicate GameState reference detected by StateStackBootstrapper.",
                    candidate
                );
                return;
            }

            _registrationScratch.Add(candidate);
        }
    }
}
