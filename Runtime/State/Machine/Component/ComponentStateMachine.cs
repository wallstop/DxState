namespace WallstopStudios.DxState.State.Machine.Component
{
    using System.Collections.Generic;
    using UnityHelpers.Core.Extension;

    public sealed class ComponentStateMachine
    {
        private readonly Dictionary<StateComponent, List<Transition>> _states;

        public StateComponent CurrentState => _currentState;

        public bool LogStateTransitions { get; set; }

        private StateComponent _currentState;

        public ComponentStateMachine(
            IEnumerable<Transition> transitions,
            StateComponent currentState
        )
        {
            _states = new Dictionary<StateComponent, List<Transition>>();

            foreach (Transition transition in transitions)
            {
                _states.GetOrAdd(transition.from).Add(transition);
                transition.from.StateMachine = this;
                transition.to.StateMachine = this;
            }

            TransitionToState(currentState);
        }

        public void Update()
        {
            foreach (Transition transition in _states[_currentState])
            {
                if (transition.rule.Invoke())
                {
                    TransitionToState(transition.to);
                    return;
                }
            }
        }

        private void TransitionToState(StateComponent newState)
        {
            if (_currentState != null && _currentState.IsActive)
            {
                _currentState.Exit();
            }

            if (LogStateTransitions)
            {
                // TODO: Add eventing
                newState.Log(
                    $"Transitioning from {_currentState?.GetType().Name} to {newState.GetType().Name}."
                );
            }

            _currentState = newState;
            _currentState.Enter();
        }
    }
}
