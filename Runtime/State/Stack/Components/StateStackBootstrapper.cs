namespace WallstopStudios.DxState.State.Stack.Components
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::DxMessaging.Unity;
    using UnityEngine;

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

        private void Awake()
        {
            EnsureDependencies();
            RegisterConfiguredStates();
        }

        private async void Start()
        {
            if (!_pushInitialStateOnStart)
            {
                return;
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
                return;
            }

            if (ReferenceEquals(_stateStackManager.CurrentState, _initialState))
            {
                return;
            }

            IReadOnlyList<IState> stack = _stateStackManager.Stack;
            for (int i = 0; i < stack.Count; i++)
            {
                if (ReferenceEquals(stack[i], _initialState))
                {
                    await _stateStackManager.FlattenAsync(_initialState);
                    return;
                }
            }

            await _stateStackManager.PushAsync(_initialState);
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

            for (int i = 0; i < _registrationScratch.Count; i++)
            {
                GameState state = _registrationScratch[i];
                bool registered = _stateStackManager.TryRegister(state, _forceRegisterStates);
                if (!registered && !_forceRegisterStates)
                {
                    Debug.LogError(
                        $"State '{state.name}' is already registered with the stack and was not added again. Enable Force Register to override.",
                        state
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

        private void CollectCandidate(GameState candidate)
        {
            if (candidate == null)
            {
                return;
            }

            if (!_uniqueStateScratch.Add(candidate))
            {
                Debug.LogError(
                    $"Duplicate GameState reference detected by StateStackBootstrapper: '{candidate.name}'.",
                    candidate
                );
                return;
            }

            _registrationScratch.Add(candidate);
        }
    }
}
