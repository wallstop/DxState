namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using WallstopStudios.DxState.State.Machine;

    public sealed class StateMachineDiagnostics<TState>
    {
        private readonly object _gate;
        private readonly Queue<StateMachineDiagnosticEvent<TState>> _recentEvents;
        private readonly int[] _causeCounts;
        private readonly Dictionary<TState, StateMetricsAccumulator> _stateMetrics;
        private readonly Dictionary<TState, StateMachineStateMetrics> _stateMetricsSnapshot;
        private readonly ReadOnlyDictionary<TState, StateMachineStateMetrics> _stateMetricsReadOnly;
        private readonly int _capacity;

        private int _transitionCount;
        private int _deferredCount;
        private bool _hasLastTransition;
        private TransitionExecutionContext<TState> _lastTransition;
        private DateTime _lastTransitionUtc;

        public StateMachineDiagnostics(int capacity)
        {
            _capacity = Math.Max(1, capacity);
            _gate = new object();
            _recentEvents = new Queue<StateMachineDiagnosticEvent<TState>>(_capacity);
            Array causes = Enum.GetValues(typeof(TransitionCause));
            _causeCounts = new int[causes.Length];
            _stateMetrics = new Dictionary<TState, StateMetricsAccumulator>();
            _stateMetricsSnapshot = new Dictionary<TState, StateMachineStateMetrics>();
            _stateMetricsReadOnly = new ReadOnlyDictionary<TState, StateMachineStateMetrics>(
                _stateMetricsSnapshot
            );
        }

        public int TransitionCount
        {
            get
            {
                lock (_gate)
                {
                    return _transitionCount;
                }
            }
        }

        public int DeferredTransitionCount
        {
            get
            {
                lock (_gate)
                {
                    return _deferredCount;
                }
            }
        }

        public DateTime? LastTransitionUtc
        {
            get
            {
                lock (_gate)
                {
                    return _hasLastTransition ? _lastTransitionUtc : null;
                }
            }
        }

        public IReadOnlyCollection<StateMachineDiagnosticEvent<TState>> RecentTransitions
        {
            get
            {
                lock (_gate)
                {
                    return _recentEvents.ToArray();
                }
            }
        }

        public IReadOnlyDictionary<TState, StateMachineStateMetrics> StateMetrics
        {
            get
            {
                lock (_gate)
                {
                    return _stateMetricsReadOnly;
                }
            }
        }

        public int GetTransitionCauseCount(TransitionCause cause)
        {
            int index = (int)cause;
            lock (_gate)
            {
                if (index < 0 || index >= _causeCounts.Length)
                {
                    return 0;
                }

                return _causeCounts[index];
            }
        }

        public bool TryGetLastTransition(out TransitionExecutionContext<TState> context)
        {
            lock (_gate)
            {
                context = _lastTransition;
                return _hasLastTransition;
            }
        }

        public bool TryGetStateMetrics(
            TState state,
            out StateMachineStateMetrics metrics
        )
        {
            lock (_gate)
            {
                if (_stateMetricsSnapshot.TryGetValue(state, out metrics))
                {
                    return true;
                }

                metrics = default;
                return false;
            }
        }

        public void RecordTransition(TransitionExecutionContext<TState> context)
        {
            lock (_gate)
            {
                _transitionCount++;
                _hasLastTransition = true;
                _lastTransition = context;
                _lastTransitionUtc = DateTime.UtcNow;
                IncrementCauseCount(context.Context.Cause);
                AccumulateStateMetrics(context.PreviousState, exited: true);
                AccumulateStateMetrics(context.CurrentState, exited: false);
                EnqueueEvent(StateMachineDiagnosticEvent<TState>.ForExecuted(
                    _lastTransitionUtc,
                    context
                ));
            }
        }

        public void RecordDeferredTransition(
            TState currentState,
            TState requestedState,
            TransitionContext context
        )
        {
            lock (_gate)
            {
                _deferredCount++;
                DateTime timestampUtc = DateTime.UtcNow;
                IncrementCauseCount(context.Cause);
                EnqueueEvent(StateMachineDiagnosticEvent<TState>.ForDeferred(
                    timestampUtc,
                    currentState,
                    requestedState,
                    context
                ));
            }
        }

        public void Clear()
        {
            lock (_gate)
            {
                _recentEvents.Clear();
                _stateMetrics.Clear();
                _stateMetricsSnapshot.Clear();
                Array.Clear(_causeCounts, 0, _causeCounts.Length);
                _transitionCount = 0;
                _deferredCount = 0;
                _hasLastTransition = false;
            }
        }

        private void EnqueueEvent(StateMachineDiagnosticEvent<TState> diagnosticEvent)
        {
            if (_recentEvents.Count == _capacity)
            {
                _recentEvents.Dequeue();
            }

            _recentEvents.Enqueue(diagnosticEvent);
        }

        private void IncrementCauseCount(TransitionCause cause)
        {
            int index = (int)cause;
            if (index < 0 || index >= _causeCounts.Length)
            {
                return;
            }

            _causeCounts[index]++;
        }

        private void AccumulateStateMetrics(TState state, bool exited)
        {
            if (!StateValueIsValid(state))
            {
                return;
            }

            if (!_stateMetrics.TryGetValue(state, out StateMetricsAccumulator accumulator))
            {
                accumulator = new StateMetricsAccumulator();
                _stateMetrics[state] = accumulator;
            }

            DateTime now = DateTime.UtcNow;
            if (exited)
            {
                accumulator.ExitCount++;
                accumulator.LastExitedUtc = now;
            }
            else
            {
                accumulator.EnterCount++;
                accumulator.LastEnteredUtc = now;
            }

            _stateMetricsSnapshot[state] = accumulator.ToSnapshot();
        }

        private static bool StateValueIsValid(TState state)
        {
            Type stateType = typeof(TState);
            if (stateType.IsValueType && Nullable.GetUnderlyingType(stateType) == null)
            {
                return true;
            }

            return !EqualityComparer<TState>.Default.Equals(state, default);
        }

        private sealed class StateMetricsAccumulator
        {
            public int EnterCount;
            public int ExitCount;
            public DateTime? LastEnteredUtc;
            public DateTime? LastExitedUtc;

            public StateMachineStateMetrics ToSnapshot()
            {
                return new StateMachineStateMetrics(
                    EnterCount,
                    ExitCount,
                    LastEnteredUtc,
                    LastExitedUtc
                );
            }
        }
    }

    public enum StateMachineDiagnosticEventType
    {
        TransitionExecuted = 0,
        TransitionDeferred = 1,
    }

    public readonly struct StateMachineDiagnosticEvent<TState>
    {
        private StateMachineDiagnosticEvent(
            StateMachineDiagnosticEventType eventType,
            DateTime timestampUtc,
            TransitionExecutionContext<TState> executionContext,
            bool hasExecutionContext,
            TState previousState,
            TState requestedState,
            TransitionContext context
        )
        {
            EventType = eventType;
            TimestampUtc = timestampUtc;
            ExecutionContext = executionContext;
            HasExecutionContext = hasExecutionContext;
            PreviousState = previousState;
            RequestedState = requestedState;
            Context = context;
        }

        public StateMachineDiagnosticEventType EventType { get; }

        public DateTime TimestampUtc { get; }

        public bool HasExecutionContext { get; }

        public TransitionExecutionContext<TState> ExecutionContext { get; }

        public TState PreviousState { get; }

        public TState RequestedState { get; }

        public TransitionContext Context { get; }

        public static StateMachineDiagnosticEvent<TState> ForExecuted(
            DateTime timestampUtc,
            TransitionExecutionContext<TState> executionContext
        )
        {
            return new StateMachineDiagnosticEvent<TState>(
                StateMachineDiagnosticEventType.TransitionExecuted,
                timestampUtc,
                executionContext,
                true,
                executionContext.PreviousState,
                executionContext.CurrentState,
                executionContext.Context
            );
        }

        public static StateMachineDiagnosticEvent<TState> ForDeferred(
            DateTime timestampUtc,
            TState currentState,
            TState requestedState,
            TransitionContext context
        )
        {
            return new StateMachineDiagnosticEvent<TState>(
                StateMachineDiagnosticEventType.TransitionDeferred,
                timestampUtc,
                default,
                false,
                currentState,
                requestedState,
                context
            );
        }
    }

    public readonly struct StateMachineStateMetrics
    {
        public StateMachineStateMetrics(
            int enterCount,
            int exitCount,
            DateTime? lastEnteredUtc,
            DateTime? lastExitedUtc
        )
        {
            EnterCount = enterCount;
            ExitCount = exitCount;
            LastEnteredUtc = lastEnteredUtc;
            LastExitedUtc = lastExitedUtc;
        }

        public int EnterCount { get; }

        public int ExitCount { get; }

        public DateTime? LastEnteredUtc { get; }

        public DateTime? LastExitedUtc { get; }
    }
}
