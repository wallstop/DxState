namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;
    using Component;
    using UnityHelpers.Core.Extension;

    public sealed class StateMachine<T>
    {
        private readonly Dictionary<T, List<Transition<T>>> _states;
        private readonly Queue<PendingTransition> _pendingTransitions;

        private TransitionExecutionContext<T> _latestTransitionContext;

        private bool _hasTransitionContext;
        private bool _isProcessingTransitions;

        private T _currentState;

        public StateMachine(IEnumerable<Transition<T>> transitions, T currentState)
        {
            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            _states = new Dictionary<T, List<Transition<T>>>();
            _pendingTransitions = new Queue<PendingTransition>();
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

            TransitionContext initializationContext = new TransitionContext(
                TransitionCause.Initialization
            );
            EnqueueTransition(currentState, null, initializationContext);
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
            EnqueueTransition(newState, null, resolvedContext);
        }

        private void TransitionToState(
            T newState,
            Transition<T> transition,
            TransitionContext? overrideContext = null
        )
        {
            EnqueueTransition(newState, transition, overrideContext);
        }

        private void EnqueueTransition(
            T newState,
            Transition<T> transition,
            TransitionContext? overrideContext
        )
        {
            PendingTransition pending = new PendingTransition(newState, transition, overrideContext);
            _pendingTransitions.Enqueue(pending);
            ProcessPendingTransitions();
        }

        private void ProcessPendingTransitions()
        {
            if (_isProcessingTransitions)
            {
                return;
            }

            _isProcessingTransitions = true;
            try
            {
                while (_pendingTransitions.Count > 0)
                {
                    PendingTransition current = _pendingTransitions.Dequeue();
                    ExecutePendingTransition(current);
                }
            }
            finally
            {
                _isProcessingTransitions = false;
            }
        }

        private void ExecutePendingTransition(PendingTransition pending)
        {
            T previousState = _currentState;
            T targetState = pending.NewState;
            Transition<T> sourceTransition = pending.Transition;

            if (!_states.ContainsKey(targetState))
            {
                _states[targetState] = new List<Transition<T>>();
            }

            TransitionContext contextToRecord = ResolveTransitionContext(
                sourceTransition,
                pending.OverrideContext
            );

            if (previousState is IStateContext<T> previousContext)
            {
                previousContext.Exit();
            }

            _currentState = targetState;

            if (targetState is IStateContext<T> targetContext)
            {
                targetContext.StateMachine = this;
            }

            _latestTransitionContext = new TransitionExecutionContext<T>(
                previousState,
                targetState,
                sourceTransition,
                contextToRecord
            );
            _hasTransitionContext = true;

            if (targetState is IStateContext<T> newContext)
            {
                if (LogStateTransitions)
                {
                    string previousName = previousState is object previousObject
                        ? previousObject.GetType().Name
                        : "<null>";
                    string currentName = targetState is object currentObject
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

        private static TransitionContext ResolveTransitionContext(
            Transition<T> transition,
            TransitionContext? overrideContext
        )
        {
            if (overrideContext.HasValue)
            {
                TransitionContext suppliedContext = overrideContext.Value;
                TransitionCause overrideCause = suppliedContext.HasDefinedCause
                    ? suppliedContext.Cause
                    : TransitionCause.Forced;
                return new TransitionContext(overrideCause, suppliedContext.Flags);
            }

            if (transition != null)
            {
                TransitionContext transitionContext = transition.Context;
                TransitionCause transitionCause = transitionContext.HasDefinedCause
                    ? transitionContext.Cause
                    : TransitionCause.RuleSatisfied;
                return new TransitionContext(transitionCause, transitionContext.Flags);
            }

            return new TransitionContext(TransitionCause.Initialization);
        }

        private readonly struct PendingTransition
        {
            public PendingTransition(
                T newState,
                Transition<T> transition,
                TransitionContext? overrideContext
            )
            {
                NewState = newState;
                Transition = transition;
                OverrideContext = overrideContext;
            }

            public T NewState { get; }

            public Transition<T> Transition { get; }

            public TransitionContext? OverrideContext { get; }
        }
    }
}
