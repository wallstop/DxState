namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;
    using Component;
    using UnityHelpers.Core.Extension;

    public sealed class StateMachine<T>
    {
        private readonly Dictionary<T, List<Transition<T>>> _states;

        private TransitionExecutionContext<T> _latestTransitionContext;

        private bool _hasTransitionContext;

        private T _currentState;

        public StateMachine(IEnumerable<Transition<T>> transitions, T currentState)
        {
            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            _states = new Dictionary<T, List<Transition<T>>>();
            HashSet<T> discoveredStates = new HashSet<T>();

            foreach (Transition<T> transition in transitions)
            {
                List<Transition<T>> transitionsFromState = _states.GetOrAdd(transition.from);
                transitionsFromState.Add(transition);

                discoveredStates.Add(transition.from);
                discoveredStates.Add(transition.to);

                if (transition.from is IStateContext<T> fromContext)
                {
                    fromContext.StateMachine = this;
                }

                if (transition.to is IStateContext<T> toContext)
                {
                    toContext.StateMachine = this;
                }
            }

            discoveredStates.Add(currentState);

            foreach (T state in discoveredStates)
            {
                if (_states.ContainsKey(state))
                {
                    continue;
                }

                _states[state] = new List<Transition<T>>();
            }

            TransitionToState(
                currentState,
                null,
                new TransitionContext(TransitionCause.Initialization)
            );
        }

        public T CurrentState => _currentState;

        public bool LogStateTransitions { get; set; }

        public event Action<TransitionExecutionContext<T>> TransitionExecuted;

        public void Update()
        {
            if (!_states.TryGetValue(_currentState, out List<Transition<T>> transitionsForCurrent))
            {
                return;
            }

            for (int i = 0; i < transitionsForCurrent.Count; i++)
            {
                Transition<T> transition = transitionsForCurrent[i];
                if (transition.rule == null)
                {
                    continue;
                }

                if (transition.rule.Invoke())
                {
                    TransitionToState(transition.to, transition);
                    return;
                }
            }
        }

        public bool TryGetLastTransitionContext(out TransitionExecutionContext<T> context)
        {
            context = _latestTransitionContext;
            return _hasTransitionContext;
        }

        public void ForceTransition(T newState, TransitionContext context)
        {
            TransitionCause causeToApply = context.HasDefinedCause
                ? context.Cause
                : TransitionCause.Forced;
            TransitionContext resolvedContext = new TransitionContext(causeToApply, context.Flags);
            if (!_states.ContainsKey(newState))
            {
                _states[newState] = new List<Transition<T>>();
            }
            TransitionToState(newState, null, resolvedContext);
        }

        private void TransitionToState(
            T newState,
            Transition<T> transition,
            TransitionContext? overrideContext = null
        )
        {
            T previousState = _currentState;

            if (previousState is IStateContext<T> currentContext)
            {
                currentContext.Exit();
            }

            _currentState = newState;

            if (!_states.ContainsKey(newState))
            {
                _states[newState] = new List<Transition<T>>();
            }

            TransitionContext contextToRecord;
            if (overrideContext.HasValue)
            {
                TransitionContext suppliedContext = overrideContext.Value;
                TransitionCause overrideCause = suppliedContext.HasDefinedCause
                    ? suppliedContext.Cause
                    : TransitionCause.Forced;
                contextToRecord = new TransitionContext(overrideCause, suppliedContext.Flags);
            }
            else if (transition != null)
            {
                TransitionContext transitionContext = transition.Context;
                TransitionCause transitionCause = transitionContext.HasDefinedCause
                    ? transitionContext.Cause
                    : TransitionCause.RuleSatisfied;
                contextToRecord = new TransitionContext(transitionCause, transitionContext.Flags);
            }
            else
            {
                contextToRecord = new TransitionContext(TransitionCause.Initialization);
            }

            _latestTransitionContext = new TransitionExecutionContext<T>(
                previousState,
                newState,
                transition,
                contextToRecord
            );
            _hasTransitionContext = true;

            if (newState is IStateContext<T> newContext)
            {
                if (LogStateTransitions)
                {
                    string previousName = previousState is object previousObject
                        ? previousObject.GetType().Name
                        : "<null>";
                    string currentName = newState is object currentObject
                        ? currentObject.GetType().Name
                        : "<null>";
                    newContext.Log(
                        $"Transitioning from {previousName} to {currentName} ({contextToRecord.Cause})."
                    );
                }

                newContext.Enter();
            }

            TransitionExecuted?.Invoke(_latestTransitionContext);
        }
    }
}
