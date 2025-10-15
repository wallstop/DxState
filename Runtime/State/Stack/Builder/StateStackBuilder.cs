namespace WallstopStudios.DxState.State.Stack.Builder
{
    using System;
    using System.Collections.Generic;

    public sealed class StateStackBuilder
    {
        private readonly List<IState> _states;
        private readonly HashSet<IState> _unique;
        private IState _initialState;

        public StateStackBuilder()
        {
            _states = new List<IState>();
            _unique = new HashSet<IState>();
        }

        public StateStackBuilder WithState(IState state, bool setAsInitial = false)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (TryAddState(state))
            {
                _states.Add(state);
            }

            if (setAsInitial || _initialState == null)
            {
                _initialState = state;
            }

            return this;
        }

        public StateStackBuilder WithStates(
            IEnumerable<IState> states,
            bool setFirstAsInitial = false
        )
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            bool initialSet = false;
            foreach (IState state in states)
            {
                if (state == null)
                {
                    continue;
                }

                if (TryAddState(state))
                {
                    _states.Add(state);
                }

                if ((setFirstAsInitial && !initialSet) || _initialState == null)
                {
                    _initialState = state;
                    initialSet = true;
                }
            }

            return this;
        }

        public StateStackBuilder WithInitialState(IState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            WithState(state, true);
            return this;
        }

        public StateStackConfiguration Build()
        {
            if (_states.Count == 0)
            {
                throw new InvalidOperationException(
                    "At least one state must be added before building the configuration."
                );
            }

            IState initial = _initialState ?? _states[0];
            IState[] snapshot = _states.ToArray();
            return new StateStackConfiguration(snapshot, initial);
        }

        private bool TryAddState(IState state)
        {
            if (!_unique.Add(state))
            {
                return false;
            }

            for (int i = 0; i < _states.Count; i++)
            {
                if (ReferenceEquals(_states[i], state))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
