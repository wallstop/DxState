namespace WallstopStudios.DxState.State.Machine
{
    using System.Collections.Generic;
    using Component;
    using UnityHelpers.Core.Extension;

    public sealed class StateMachine<T>
    {
        private readonly Dictionary<T, List<Transition<T>>> _states;

        public T CurrentState => _currentState;

        public bool LogStateTransitions { get; set; }

        private T _currentState;

        public StateMachine(IEnumerable<Transition<T>> transitions, T currentState)
        {
            _states = new Dictionary<T, List<Transition<T>>>();

            foreach (Transition<T> transition in transitions)
            {
                _states.GetOrAdd(transition.from).Add(transition);
                if (transition.from is IStateContext<T> fromContext)
                {
                    fromContext.StateMachine = this;
                }

                if (transition.to is IStateContext<T> toContext)
                {
                    toContext.StateMachine = this;
                }
            }

            TransitionToState(currentState);
        }

        public void Update()
        {
            foreach (Transition<T> transition in _states[_currentState])
            {
                if (transition.rule.Invoke())
                {
                    TransitionToState(transition.to);
                    return;
                }
            }
        }

        private void TransitionToState(T newState)
        {
            if (_currentState is IStateContext<T> currentContext)
            {
                currentContext.Exit();
            }

            _currentState = newState;
            if (newState is IStateContext<T> newContext)
            {
                if (LogStateTransitions)
                {
                    // TODO: Add eventing
                    newContext.Log(
                        $"Transitioning from {_currentState?.GetType().Name} to {newState.GetType().Name}."
                    );
                }
                newContext.Enter();
            }
        }
    }
}
