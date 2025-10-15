namespace WallstopStudios.DxState.State.Stack.Builder
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public sealed class StateStackConfiguration
    {
        private readonly IReadOnlyList<IState> _states;
        private readonly IState _initialState;

        public StateStackConfiguration(IReadOnlyList<IState> states, IState initialState)
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            if (states.Count == 0)
            {
                throw new ArgumentException(
                    "States collection cannot be empty.",
                    nameof(states)
                );
            }

            if (initialState == null)
            {
                throw new ArgumentNullException(nameof(initialState));
            }

            _states = states;
            _initialState = initialState;
        }

        public IReadOnlyList<IState> States => _states;

        public IState InitialState => _initialState;

        public async ValueTask ApplyAsync(
            StateStack stateStack,
            bool forceRegister = false,
            bool ensureInitialActive = true
        )
        {
            if (stateStack == null)
            {
                throw new ArgumentNullException(nameof(stateStack));
            }

            for (int i = 0; i < _states.Count; i++)
            {
                IState state = _states[i];
                stateStack.TryRegister(state, forceRegister);
            }

            if (!ensureInitialActive)
            {
                return;
            }

            if (ReferenceEquals(stateStack.CurrentState, _initialState))
            {
                return;
            }

            bool initialInStack = false;
            IReadOnlyList<IState> activeStack = stateStack.Stack;
            for (int i = 0; i < activeStack.Count; i++)
            {
                if (ReferenceEquals(activeStack[i], _initialState))
                {
                    initialInStack = true;
                    break;
                }
            }

            if (initialInStack)
            {
                await stateStack.FlattenAsync(_initialState);
                return;
            }

            await stateStack.PushAsync(_initialState);
        }
    }
}
