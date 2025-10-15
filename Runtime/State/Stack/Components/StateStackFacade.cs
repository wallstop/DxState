namespace WallstopStudios.DxState.State.Stack.Components
{
    using System;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.Events;

    [AddComponentMenu("Wallstop Studios/DxState/State Stack Facade")]
    [DisallowMultipleComponent]
    public sealed class StateStackFacade : MonoBehaviour
    {
        [Serializable]
        public sealed class GameStateEvent : UnityEvent<GameState> { }

        [SerializeField]
        private StateStackManager _stateStackManager;

        [SerializeField]
        private GameStateEvent _onStatePushed = new GameStateEvent();

        [SerializeField]
        private GameStateEvent _onStatePopped = new GameStateEvent();

        [SerializeField]
        private GameStateEvent _onStateChanged = new GameStateEvent();

        public GameStateEvent OnStatePushedEvent => _onStatePushed;

        public GameStateEvent OnStatePoppedEvent => _onStatePopped;

        public GameStateEvent OnStateChangedEvent => _onStateChanged;

        private void Awake()
        {
            EnsureManagerReference();
            if (_stateStackManager == null)
            {
                Debug.LogError("StateStackFacade requires an associated StateStackManager.", this);
                enabled = false;
                return;
            }

            SubscribeToManager();
        }

        private void OnDestroy()
        {
            UnsubscribeFromManager();
        }

        public void PushState(GameState state)
        {
            if (!ValidateState(state, nameof(PushState)))
            {
                return;
            }

            RunOperation(_stateStackManager.PushAsync(state));
        }

        public void PushStateByName(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                Debug.LogError("PushStateByName requires a non-empty state name.", this);
                return;
            }

            RunOperation(_stateStackManager.PushAsync(stateName));
        }

        public void PopState()
        {
            RunOperation(_stateStackManager.PopAsync());
        }

        public void TryPopState()
        {
            RunOperation(_stateStackManager.TryPopAsync());
        }

        public void ReplaceState(GameState state)
        {
            if (!ValidateState(state, nameof(ReplaceState)))
            {
                return;
            }

            RunOperation(ReplaceStateAsync(state));
        }

        public void FlattenState(GameState state)
        {
            if (!ValidateState(state, nameof(FlattenState)))
            {
                return;
            }

            RunOperation(_stateStackManager.FlattenAsync(state));
        }

        private void EnsureManagerReference()
        {
            if (_stateStackManager != null)
            {
                return;
            }

            _stateStackManager = GetComponent<StateStackManager>();
        }

        private void SubscribeToManager()
        {
            _stateStackManager.StatePushed += HandleStatePushed;
            _stateStackManager.StatePopped += HandleStatePopped;
            _stateStackManager.TransitionCompleted += HandleTransitionComplete;
        }

        private void UnsubscribeFromManager()
        {
            if (_stateStackManager == null)
            {
                return;
            }

            _stateStackManager.StatePushed -= HandleStatePushed;
            _stateStackManager.StatePopped -= HandleStatePopped;
            _stateStackManager.TransitionCompleted -= HandleTransitionComplete;
        }

        private bool ValidateState(GameState state, string caller)
        {
            if (state != null)
            {
                return true;
            }

            Debug.LogError($"{caller} requires a valid GameState reference.", this);
            return false;
        }

        private void HandleStatePushed(IState previous, IState current)
        {
            GameState currentGameState = current as GameState;
            if (currentGameState == null)
            {
                return;
            }

            _onStatePushed.Invoke(currentGameState);
        }

        private void HandleStatePopped(IState previous, IState current)
        {
            GameState previousGameState = previous as GameState;
            if (previousGameState != null)
            {
                _onStatePopped.Invoke(previousGameState);
            }
        }

        private void HandleTransitionComplete(IState previous, IState current)
        {
            GameState currentGameState = current as GameState;
            _onStateChanged.Invoke(currentGameState);
        }

        private async ValueTask ReplaceStateAsync(GameState state)
        {
            if (_stateStackManager.CurrentState != null)
            {
                await _stateStackManager.PopAsync();
            }

            await _stateStackManager.PushAsync(state);
        }

        private async void RunOperation(ValueTask operation)
        {
            try
            {
                await operation;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }

        private async void RunOperation(ValueTask<IState> operation)
        {
            try
            {
                await operation;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
            }
        }
    }
}
