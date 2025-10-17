namespace WallstopStudios.DxState.State.Machine.Trigger
{
    using System;
    using System.Collections.Generic;
    using WallstopStudios.UnityHelpers.Utils;

    public sealed class TriggerStateMachine<TState, TTrigger> : IDisposable
    {
        public TState CurrentStateId => _currentStateId;

        public ITriggerState<TState, TTrigger> CurrentState => _currentState;

        public event Action<TriggerTransitionExecutionContext<TState, TTrigger>> TransitionExecuted;

        private readonly Dictionary<TState, ITriggerState<TState, TTrigger>> _statesById;
        private readonly Dictionary<TState, Dictionary<TTrigger, TriggerStateTransition<TState, TTrigger>>> _transitions;
        private readonly Queue<PendingTransition> _pendingTransitions;
        private PooledResource<Queue<PendingTransition>> _pendingTransitionsLease;

        private ITriggerState<TState, TTrigger> _currentState;
        private TState _currentStateId;
        private bool _isProcessingTransitions;
        private TriggerTransitionExecutionContext<TState, TTrigger> _latestExecutionContext;
        private bool _hasExecutionContext;

        public TriggerStateMachine(
            IEnumerable<ITriggerState<TState, TTrigger>> states,
            IEnumerable<TriggerStateTransition<TState, TTrigger>> transitions,
            TState initialState
        )
        {
            if (states == null)
            {
                throw new ArgumentNullException(nameof(states));
            }

            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            _statesById = new Dictionary<TState, ITriggerState<TState, TTrigger>>();
            _transitions = new Dictionary<TState, Dictionary<TTrigger, TriggerStateTransition<TState, TTrigger>>>();
            _pendingTransitionsLease = Buffers<PendingTransition>.Queue.Get(
                out _pendingTransitions
            );

            foreach (ITriggerState<TState, TTrigger> triggerState in states)
            {
                if (triggerState == null)
                {
                    continue;
                }

                if (_statesById.ContainsKey(triggerState.Id))
                {
                    throw new InvalidOperationException(
                        $"Trigger state '{triggerState.Id}' is registered more than once."
                    );
                }

                _statesById.Add(triggerState.Id, triggerState);
            }

            foreach (TriggerStateTransition<TState, TTrigger> transition in transitions)
            {
                if (!_transitions.TryGetValue(transition.From, out Dictionary<TTrigger, TriggerStateTransition<TState, TTrigger>> mapping))
                {
                    mapping = new Dictionary<TTrigger, TriggerStateTransition<TState, TTrigger>>();
                    _transitions.Add(transition.From, mapping);
                }

                mapping[transition.Trigger] = transition;
            }

            if (!_statesById.TryGetValue(initialState, out ITriggerState<TState, TTrigger> initialInstance))
            {
                throw new ArgumentException(
                    $"No trigger state registered with id '{initialState}'.",
                    nameof(initialState)
                );
            }

            _currentStateId = initialState;
            _currentState = initialInstance;

            TransitionContext enterContext = new TransitionContext(TransitionCause.Initialization);
            _currentState.OnEnter(this, initialState, enterContext);
            _latestExecutionContext = new TriggerTransitionExecutionContext<TState, TTrigger>(
                initialState,
                initialState,
                default,
                enterContext
            );
            _hasExecutionContext = true;
        }

        public bool TryGetLastTransitionContext(
            out TriggerTransitionExecutionContext<TState, TTrigger> context
        )
        {
            context = _latestExecutionContext;
            return _hasExecutionContext;
        }

        public void Update()
        {
            if (_currentState == null)
            {
                return;
            }

            _currentState.Tick();

            ProcessTriggers();
        }

        public void ForceTransition(TState newState, TransitionContext context)
        {
            if (!_statesById.TryGetValue(newState, out ITriggerState<TState, TTrigger> targetState))
            {
                throw new ArgumentException(
                    $"No trigger state registered with id '{newState}'.",
                    nameof(newState)
                );
            }

            TriggerStateTransition<TState, TTrigger> forcedTransition = new TriggerStateTransition<TState, TTrigger>(
                _currentStateId,
                default,
                newState
            );
            EnqueueTransition(forcedTransition, context, targetState, TransitionCause.Forced);
        }

        private void ProcessTriggers()
        {
            if (_currentState == null)
            {
                return;
            }

            bool triggerResolved = true;
            while (triggerResolved)
            {
                triggerResolved = false;
                TransitionContext triggerContext;
                TTrigger trigger;
                bool shouldTransition = _currentState.TryGetTrigger(out trigger, out triggerContext);
                if (!shouldTransition)
                {
                    continue;
                }

                if (!_transitions.TryGetValue(_currentStateId, out Dictionary<TTrigger, TriggerStateTransition<TState, TTrigger>> mapping))
                {
                    throw new InvalidOperationException(
                        $"No transitions configured for state '{_currentStateId}'."
                    );
                }

                if (!mapping.TryGetValue(trigger, out TriggerStateTransition<TState, TTrigger> transition))
                {
                    throw new InvalidOperationException(
                        $"State '{_currentStateId}' does not have a transition for trigger '{trigger}'."
                    );
                }

                if (!_statesById.TryGetValue(transition.To, out ITriggerState<TState, TTrigger> targetState))
                {
                    throw new InvalidOperationException(
                        $"Transition target state '{transition.To}' is not registered."
                    );
                }

                EnqueueTransition(transition, triggerContext, targetState, TransitionCause.RuleSatisfied);
                triggerResolved = true;
            }
        }

        private void EnqueueTransition(
            TriggerStateTransition<TState, TTrigger> transition,
            TransitionContext context,
            ITriggerState<TState, TTrigger> targetState,
            TransitionCause defaultCause
        )
        {
            PendingTransition pendingTransition = new PendingTransition(
                transition,
                context,
                targetState,
                defaultCause
            );
            _pendingTransitions.Enqueue(pendingTransition);
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
                    PendingTransition pending = _pendingTransitions.Dequeue();
                    ExecutePendingTransition(pending);
                }
            }
            finally
            {
                _isProcessingTransitions = false;
            }
        }

        private void ExecutePendingTransition(PendingTransition pending)
        {
            ITriggerState<TState, TTrigger> previousState = _currentState;
            TState previousStateId = _currentStateId;
            _currentState = pending.TargetState;
            _currentStateId = pending.Transition.To;

            TransitionContext exitContext = EnsureCause(pending.Context, pending.DefaultCause);
            if (previousState != null)
            {
                previousState.OnExit(this, _currentStateId, exitContext);
            }

            TransitionContext enterContext = EnsureCause(pending.Context, pending.DefaultCause);
            _currentState.OnEnter(this, previousStateId, enterContext);

            _latestExecutionContext = new TriggerTransitionExecutionContext<TState, TTrigger>(
                previousStateId,
                _currentStateId,
                pending.Transition.Trigger,
                enterContext
            );
            _hasExecutionContext = true;

            TransitionExecuted?.Invoke(_latestExecutionContext);
        }

        private static TransitionContext EnsureCause(
            TransitionContext context,
            TransitionCause defaultCause
        )
        {
            if (context.HasDefinedCause)
            {
                return context;
            }

            return new TransitionContext(defaultCause, context.Flags);
        }

        public void Dispose()
        {
            _pendingTransitions.Clear();
            _pendingTransitionsLease.Dispose();
            _pendingTransitionsLease = default;
        }

        private readonly struct PendingTransition
        {
            public PendingTransition(
                TriggerStateTransition<TState, TTrigger> transition,
                TransitionContext context,
                ITriggerState<TState, TTrigger> targetState,
                TransitionCause defaultCause
            )
            {
                Transition = transition;
                Context = context;
                TargetState = targetState;
                DefaultCause = defaultCause;
            }

            public TriggerStateTransition<TState, TTrigger> Transition { get; }

            public TransitionContext Context { get; }

            public ITriggerState<TState, TTrigger> TargetState { get; }

            public TransitionCause DefaultCause { get; }
        }
    }
}
