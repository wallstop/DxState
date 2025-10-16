namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.UnityHelpers.Core.DataStructure;

    public interface IStateMachineDiagnosticsView
    {
        int TransitionCount { get; }

        int DeferredTransitionCount { get; }

        DateTime? LastTransitionUtc { get; }

        void CopyStateMetrics(List<StateMachineStateMetricsRecord> buffer);

        void CopyRecentEvents(List<StateMachineDiagnosticEventRecord> buffer, int maxCount);

        int GetTransitionCauseCount(TransitionCause cause);
    }

    public sealed class StateMachineDiagnostics<TState> : IStateMachineDiagnosticsView, IDisposable
    {
        private readonly ReaderWriterLockSlim _lock;
        private readonly CyclicBuffer<StateMachineDiagnosticEvent<TState>> _recentEvents;
        private readonly int[] _causeCounts;
        private readonly Dictionary<TState, StateMetricsAccumulator> _stateMetrics;
        private readonly Dictionary<TState, StateMachineStateMetrics> _stateMetricsSnapshot;
        private readonly StateMachineEventCollection _eventCollectionView;
        private readonly int _capacity;

        private int _transitionCount;
        private int _deferredCount;
        private bool _hasLastTransition;
        private TransitionExecutionContext<TState> _lastTransition;
        private DateTime _lastTransitionUtc;
        private bool _isDisposed;

        public StateMachineDiagnostics(int capacity)
        {
            _capacity = capacity < 0 ? 0 : capacity;
            _lock = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
            _recentEvents = new CyclicBuffer<StateMachineDiagnosticEvent<TState>>(_capacity);
            TransitionCause[] causes = (TransitionCause[])Enum.GetValues(typeof(TransitionCause));
            _causeCounts = new int[causes.Length];
            _stateMetrics = new Dictionary<TState, StateMetricsAccumulator>();
            _stateMetricsSnapshot = new Dictionary<TState, StateMachineStateMetrics>();
            _eventCollectionView = new StateMachineEventCollection(this);
        }

        public int TransitionCount
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _transitionCount;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public int DeferredTransitionCount
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _deferredCount;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public DateTime? LastTransitionUtc
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _hasLastTransition ? _lastTransitionUtc : (DateTime?)null;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public IReadOnlyCollection<StateMachineDiagnosticEvent<TState>> RecentTransitions => _eventCollectionView;

        public IReadOnlyDictionary<TState, StateMachineStateMetrics> StateMetrics
        {
            get
            {
                _lock.EnterReadLock();
                try
                {
                    return _stateMetricsSnapshot;
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            }
        }

        public int GetTransitionCauseCount(TransitionCause cause)
        {
            int index = (int)cause;
            if (index < 0 || index >= _causeCounts.Length)
            {
                return 0;
            }

            _lock.EnterReadLock();
            try
            {
                return _causeCounts[index];
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryGetLastTransition(out TransitionExecutionContext<TState> context)
        {
            _lock.EnterReadLock();
            try
            {
                context = _lastTransition;
                return _hasLastTransition;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public bool TryGetStateMetrics(
            TState state,
            out StateMachineStateMetrics metrics
        )
        {
            _lock.EnterReadLock();
            try
            {
                if (_stateMetricsSnapshot.TryGetValue(state, out metrics))
                {
                    return true;
                }

                metrics = default;
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void CopyStateMetrics(List<StateMachineStateMetricsRecord> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            _lock.EnterReadLock();
            try
            {
                buffer.Clear();
                foreach (KeyValuePair<TState, StateMachineStateMetrics> entry in _stateMetricsSnapshot)
                {
                    buffer.Add(new StateMachineStateMetricsRecord(entry.Key, entry.Value));
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void CopyRecentEvents(List<StateMachineDiagnosticEventRecord> buffer, int maxCount)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            if (maxCount <= 0)
            {
                buffer.Clear();
                return;
            }

            _lock.EnterReadLock();
            try
            {
                buffer.Clear();
                int total = _recentEvents.Count;
                int startIndex = total - maxCount;
                if (startIndex < 0)
                {
                    startIndex = 0;
                }

                for (int i = startIndex; i < total; i++)
                {
                    StateMachineDiagnosticEvent<TState> entry = _recentEvents[i];
                    buffer.Add(StateMachineDiagnosticEventRecord.From(entry));
                }
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void RecordTransition(TransitionExecutionContext<TState> context)
        {
            _lock.EnterWriteLock();
            try
            {
                if (_isDisposed)
                {
                    return;
                }

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
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void RecordDeferredTransition(
            TState currentState,
            TState requestedState,
            TransitionContext context
        )
        {
            _lock.EnterWriteLock();
            try
            {
                if (_isDisposed)
                {
                    return;
                }

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
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _recentEvents.Clear();
                _stateMetrics.Clear();
                _stateMetricsSnapshot.Clear();
                for (int i = 0; i < _causeCounts.Length; i++)
                {
                    _causeCounts[i] = 0;
                }

                _transitionCount = 0;
                _deferredCount = 0;
                _hasLastTransition = false;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            _lock.EnterWriteLock();
            try
            {
                if (_isDisposed)
                {
                    return;
                }

                _isDisposed = true;
                _recentEvents.Clear();
                _stateMetrics.Clear();
                _stateMetricsSnapshot.Clear();
            }
            finally
            {
                _lock.ExitWriteLock();
                _lock.Dispose();
            }
        }

        private void EnqueueEvent(StateMachineDiagnosticEvent<TState> diagnosticEvent)
        {
            if (_capacity == 0)
            {
                return;
            }

            _recentEvents.Add(diagnosticEvent);
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

        private sealed class StateMachineEventCollection : IReadOnlyCollection<StateMachineDiagnosticEvent<TState>>
        {
            private readonly StateMachineDiagnostics<TState> _owner;

            public StateMachineEventCollection(StateMachineDiagnostics<TState> owner)
            {
                _owner = owner;
            }

            public int Count
            {
                get
                {
                    _owner._lock.EnterReadLock();
                    try
                    {
                        return _owner._recentEvents.Count;
                    }
                    finally
                    {
                        _owner._lock.ExitReadLock();
                    }
                }
            }

            public IEnumerator<StateMachineDiagnosticEvent<TState>> GetEnumerator()
            {
                return new Enumerator(_owner);
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            private sealed class Enumerator : IEnumerator<StateMachineDiagnosticEvent<TState>>
            {
                private readonly StateMachineDiagnostics<TState> _owner;
                private int _index;
                private StateMachineDiagnosticEvent<TState> _current;
                private bool _lockHeld;

                public Enumerator(StateMachineDiagnostics<TState> owner)
                {
                    _owner = owner;
                    _index = -1;
                    _current = default;
                    owner._lock.EnterReadLock();
                    _lockHeld = true;
                }

                public StateMachineDiagnosticEvent<TState> Current => _current;

                object IEnumerator.Current => Current;

                public bool MoveNext()
                {
                    int nextIndex = _index + 1;
                    if (nextIndex >= _owner._recentEvents.Count)
                    {
                        return false;
                    }

                    _index = nextIndex;
                    _current = _owner._recentEvents[nextIndex];
                    return true;
                }

                public void Reset()
                {
                    _index = -1;
                    _current = default;
                }

                public void Dispose()
                {
                    if (_lockHeld)
                    {
                        _owner._lock.ExitReadLock();
                        _lockHeld = false;
                    }
                }
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

    public readonly struct StateMachineStateMetricsRecord
    {
        public StateMachineStateMetricsRecord(object state, StateMachineStateMetrics metrics)
        {
            State = state;
            Metrics = metrics;
        }

        public object State { get; }

        public StateMachineStateMetrics Metrics { get; }
    }

    public readonly struct StateMachineDiagnosticEventRecord
    {
        private StateMachineDiagnosticEventRecord(
            StateMachineDiagnosticEventType eventType,
            DateTime timestampUtc,
            object previousState,
            object requestedState,
            TransitionContext context,
            bool hasExecutionContext
        )
        {
            EventType = eventType;
            TimestampUtc = timestampUtc;
            PreviousState = previousState;
            RequestedState = requestedState;
            Context = context;
            HasExecutionContext = hasExecutionContext;
        }

        public StateMachineDiagnosticEventType EventType { get; }

        public DateTime TimestampUtc { get; }

        public object PreviousState { get; }

        public object RequestedState { get; }

        public TransitionContext Context { get; }

        public bool HasExecutionContext { get; }

        internal static StateMachineDiagnosticEventRecord From<TState>(
            StateMachineDiagnosticEvent<TState> source
        )
        {
            return new StateMachineDiagnosticEventRecord(
                source.EventType,
                source.TimestampUtc,
                source.PreviousState,
                source.RequestedState,
                source.Context,
                source.HasExecutionContext
            );
        }
    }
}
