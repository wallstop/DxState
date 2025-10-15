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
                return;
            }

            if (_stateStackManager.CurrentState == _initialState)
            {
                return;
            }

            IReadOnlyList<IState> stack = _stateStackManager.Stack;
            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i] == _initialState)
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
            List<GameState> statesToRegister = new List<GameState>();

            if (_registerChildGameStates)
            {
                GameState[] discoveredStates = GetComponentsInChildren<GameState>(true);
                for (int i = 0; i < discoveredStates.Length; i++)
                {
                    GameState discovered = discoveredStates[i];
                    if (discovered == null)
                    {
                        continue;
                    }

                    if (!statesToRegister.Contains(discovered))
                    {
                        statesToRegister.Add(discovered);
                    }
                }
            }

            if (_additionalStates != null)
            {
                for (int i = 0; i < _additionalStates.Length; i++)
                {
                    GameState additionalState = _additionalStates[i];
                    if (additionalState == null)
                    {
                        continue;
                    }

                    if (!statesToRegister.Contains(additionalState))
                    {
                        statesToRegister.Add(additionalState);
                    }
                }
            }

            for (int i = 0; i < statesToRegister.Count; i++)
            {
                GameState state = statesToRegister[i];
                bool registered = _stateStackManager.TryRegister(state, _forceRegisterStates);
                if (!registered && !_forceRegisterStates)
                {
                    Debug.LogWarning(
                        $"State '{state.name}' is already registered with the stack and was not added again."
                    );
                }
            }

            if (_initialState == null && statesToRegister.Count > 0)
            {
                _initialState = statesToRegister[0];
            }
        }
    }
}
