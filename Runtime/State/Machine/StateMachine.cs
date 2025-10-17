namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;
    using Component;
    using UnityHelpers.Core.Extension;
#if DXSTATE_PROFILING
    using Unity.Profiling;
#endif
    using WallstopStudios.DxState.Pooling;
#if DXSTATE_PROFILING
    using Unity.Profiling;
#endif

    public sealed class StateMachine<T> : IDisposable
    {
        private readonly Dictionary<T, List<Transition<T>>> _states;
        private readonly List<Transition<T>> _globalTransitions;
        private readonly PooledQueue<PendingTransition> _pendingTransitions;
        private readonly TransitionHistoryBuffer _transitionHistory;
#if DXSTATE_PROFILING
        private static readonly ProfilerMarker _transitionMarker = new ProfilerMarker("DxState.StateMachine.Transition");
        private static readonly ProfilerMarker _updateMarker = new ProfilerMarker("DxState.StateMachine.Update");
#endif

        private TransitionExecutionContext<T> _latestTransitionContext;

        private bool _hasTransitionContext;
        private bool _isProcessingTransitions;
        private int _transitionDepth;

        private T _currentState;
        private IHierarchicalStateContext<T> _activeHierarchicalState;
        private IReadOnlyList<IStateRegion> _activeRegions;
        private IStateRegionCoordinator _activeRegionCoordinator;
        private bool _shouldUpdateActiveRegions;
        private T _previousState;
        private bool _hasPreviousState;
        private bool _disposed;

        private const int DefaultTransitionHistoryCapacity = 32;

        public StateMachine(IEnumerable<Transition<T>> transitions, T currentState)
        {
            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            if (IsInvalidStateValue(currentState))
            {
                throw new ArgumentException(
                    "Current state must be a valid non-null instance.",
                    nameof(currentState)
                );
            }

            _states = new Dictionary<T, List<Transition<T>>>();
            _globalTransitions = new List<Transition<T>>();
            _pendingTransitions = new PooledQueue<PendingTransition>();
            _transitionHistory = new TransitionHistoryBuffer(DefaultTransitionHistoryCapacity);
            HashSet<T> discoveredStates = new HashSet<T>();
            HashSet<Transition<T>> uniqueTransitions = new HashSet<Transition<T>>();

            foreach (Transition<T> transition in transitions)
            {
                if (transition == null)
                {
                    throw new ArgumentException(
                        "Transitions cannot contain null entries.",
                        nameof(transitions)
                    );
                }

                bool isGlobal = transition.isGlobal;

                if (!isGlobal && IsInvalidStateValue(transition.from))
                {
                    throw new ArgumentException(
                        "Transition 'from' state must be a valid non-null instance.",
                        nameof(transitions)
                    );
                }

                if (IsInvalidStateValue(transition.to))
                {
                    throw new ArgumentException(
                        "Transition 'to' state must be a valid non-null instance.",
                        nameof(transitions)
                    );
                }

                bool added = uniqueTransitions.Add(transition);
                if (!added)
                {
                    throw new ArgumentException(
                        "Duplicate transition instances are not allowed in the definition set.",
                        nameof(transitions)
                    );
                }

                if (isGlobal)
                {
                    _globalTransitions.Add(transition);
                    discoveredStates.Add(transition.to);

                    if (transition.to is IStateContext<T> globalToContext)
                    {
                        globalToContext.StateMachine = this;
                    }

                    continue;
                }

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

        public IReadOnlyList<IStateRegion> ActiveRegions =>
            _activeRegions ?? Array.Empty<IStateRegion>();

        public IReadOnlyList<TransitionExecutionContext<T>> TransitionHistory => _transitionHistory;

        public int TransitionHistoryCapacity => _transitionHistory.Capacity;
#if DXSTATE_PROFILING
        public static bool ProfilingEnabled { get; set; }
#endif

        public event Action<TransitionExecutionContext<T>> TransitionExecuted;
        public event Action<T, T, TransitionContext> TransitionDeferred;

        public void Update()
        {
#if DXSTATE_PROFILING
            if (ProfilingEnabled)
            {
                using (_updateMarker.Auto())
                {
                    UpdateCore();
                }
                return;
            }
#endif

            UpdateCore();
        }

        private void UpdateCore()
        {
            if (TryExecuteGlobalTransitions())
            {
                UpdateActiveRegions();
                return;
            }

            if (!_states.TryGetValue(_currentState, out List<Transition<T>> transitionsForCurrent))
            {
                UpdateActiveRegions();
                return;
            }

            for (int i = 0; i < transitionsForCurrent.Count; i++)
            {
                Transition<T> transition = transitionsForCurrent[i];
                if (!transition.Evaluate())
                {
                    continue;
                }

                TransitionToState(transition.to, transition);
                UpdateActiveRegions();
                return;
            }

            UpdateActiveRegions();
        }

        public bool TryGetLastTransitionContext(out TransitionExecutionContext<T> context)
        {
            context = _latestTransitionContext;
            return _hasTransitionContext;
        }

        public bool TryGetPreviousState(out T state)
        {
            state = _previousState;
            return _hasPreviousState;
        }

        public bool TryGetActiveHierarchicalState(out IHierarchicalStateContext<T> state)
        {
            state = _activeHierarchicalState;
            return state != null;
        }

        public void CopyTransitionHistory(List<TransitionExecutionContext<T>> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            _transitionHistory.CopyTo(buffer);
        }

        public void CopyActiveRegions(List<IStateRegion> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            buffer.Clear();
            IReadOnlyList<IStateRegion> regions = _activeRegions;
            if (regions == null)
            {
                return;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                buffer.Add(regions[i]);
            }
        }

        public bool TryTransitionToPreviousState(TransitionContext context = default)
        {
            if (!_hasPreviousState)
            {
                return false;
            }

            ForceTransition(_previousState, context);
            return true;
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
            TransitionContext resolvedContext = ResolveTransitionContext(transition, overrideContext);
            EnqueueTransition(newState, transition, resolvedContext);
        }

        private void EnqueueTransition(
            T newState,
            Transition<T> transition,
            TransitionContext context
        )
        {
            if (_transitionDepth > 0)
            {
                TransitionDeferred?.Invoke(_currentState, newState, context);
            }

            PendingTransition pending = new PendingTransition(newState, transition, context);
            _pendingTransitions.Enqueue(pending);
            ProcessPendingTransitions();
        }

        private static bool IsInvalidStateValue(T state)
        {
            Type stateType = typeof(T);
            if (stateType.IsValueType && Nullable.GetUnderlyingType(stateType) == null)
            {
                return false;
            }

            return EqualityComparer<T>.Default.Equals(state, default);
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
#if DXSTATE_PROFILING
            if (ProfilingEnabled)
            {
                using (_transitionMarker.Auto())
                {
                    ExecutePendingTransitionCore(pending);
                }
                return;
            }
#endif

            ExecutePendingTransitionCore(pending);
        }

        private void ExecutePendingTransitionCore(PendingTransition pending)
        {
            _transitionDepth++;
            try
            {
                T previousState = _currentState;
                T targetState = pending.NewState;
                Transition<T> sourceTransition = pending.Transition;

                if (!_states.ContainsKey(targetState))
                {
                    _states[targetState] = new List<Transition<T>>();
                }

                TransitionContext contextToRecord = pending.Context;

                if (_activeHierarchicalState != null)
                {
                    DeactivateActiveHierarchicalState(contextToRecord);
                }

                if (previousState is IStateContext<T> previousContext)
                {
                    previousContext.Exit();
                }

                _currentState = targetState;

                if (targetState is IStateContext<T> targetContext)
                {
                    targetContext.StateMachine = this;
                }

                _previousState = previousState;
                _hasPreviousState = !IsInvalidStateValue(previousState);

                _latestTransitionContext = new TransitionExecutionContext<T>(
                    previousState,
                    targetState,
                    sourceTransition,
                    contextToRecord
                );
                _hasTransitionContext = true;
                _transitionHistory.Add(_latestTransitionContext);

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

                if (targetState is IHierarchicalStateContext<T> hierarchicalState)
                {
                    ActivateHierarchicalState(hierarchicalState, contextToRecord);
                }
                else
                {
                    ClearActiveHierarchicalState();
                }

                TransitionExecuted?.Invoke(_latestTransitionContext);
            }
            finally
            {
                _transitionDepth--;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _pendingTransitions.Dispose();
            _transitionHistory.Dispose();
        }

        ~StateMachine()
        {
            Dispose(false);
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
                TransitionContext context
            )
            {
                NewState = newState;
                Transition = transition;
                Context = context;
            }

            public T NewState { get; }

            public Transition<T> Transition { get; }

            public TransitionContext Context { get; }
        }

        private bool TryExecuteGlobalTransitions()
        {
            if (_globalTransitions.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < _globalTransitions.Count; i++)
            {
                Transition<T> transition = _globalTransitions[i];
                if (!transition.Evaluate())
                {
                    continue;
                }

                TransitionToState(transition.to, transition);
                return true;
            }

            return false;
        }

        private void ActivateHierarchicalState(
            IHierarchicalStateContext<T> hierarchicalState,
            TransitionContext context
        )
        {
            if (hierarchicalState == null)
            {
                return;
            }

            IReadOnlyList<IStateRegion> regions = hierarchicalState.Regions;
            _activeHierarchicalState = hierarchicalState;
            _activeRegions = regions != null && regions.Count > 0 ? regions : null;
            _shouldUpdateActiveRegions = hierarchicalState.ShouldUpdateRegions;
            _activeRegionCoordinator = hierarchicalState.RegionCoordinator
                ?? DefaultStateRegionCoordinator.Instance;

            if (_activeRegions == null)
            {
                return;
            }

            _activeRegionCoordinator.ActivateRegions(_activeRegions, context);
        }

        private void DeactivateActiveHierarchicalState(TransitionContext context)
        {
            if (_activeHierarchicalState == null)
            {
                return;
            }

            IReadOnlyList<IStateRegion> regions = _activeRegions;
            if (regions != null)
            {
                IStateRegionCoordinator coordinator = _activeRegionCoordinator
                    ?? DefaultStateRegionCoordinator.Instance;
                coordinator.DeactivateRegions(regions, context);
            }

            _activeHierarchicalState = null;
            _activeRegions = null;
            _activeRegionCoordinator = null;
            _shouldUpdateActiveRegions = false;
        }

        private void ClearActiveHierarchicalState()
        {
            _activeHierarchicalState = null;
            _activeRegions = null;
            _activeRegionCoordinator = null;
            _shouldUpdateActiveRegions = false;
        }

        private void UpdateActiveRegions()
        {
            if (!_shouldUpdateActiveRegions)
            {
                return;
            }

            IReadOnlyList<IStateRegion> regions = _activeRegions;
            if (regions == null)
            {
                return;
            }

            IStateRegionCoordinator coordinator = _activeRegionCoordinator
                ?? DefaultStateRegionCoordinator.Instance;
            coordinator.UpdateRegions(regions);
        }

        private sealed class TransitionHistoryBuffer : IReadOnlyList<TransitionExecutionContext<T>>, IDisposable
        {
            private TransitionExecutionContext<T>[] _buffer;
            private int _count;
            private int _startIndex;
            private bool _disposed;

            public TransitionHistoryBuffer(int capacity)
            {
                Capacity = capacity <= 0 ? DefaultTransitionHistoryCapacity : capacity;
                Capacity = Math.Max(1, Capacity);
                _buffer = WallstopArrayPool<TransitionExecutionContext<T>>.Rent(
                    Capacity,
                    clear: false
                );
                if (_buffer.Length < Capacity)
                {
                    _buffer = new TransitionExecutionContext<T>[Capacity];
                }
                _count = 0;
                _startIndex = 0;
            }

            public int Capacity { get; }

            public TransitionExecutionContext<T> this[int index]
            {
                get
                {
                    if (index < 0 || index >= _count)
                    {
                        throw new ArgumentOutOfRangeException(nameof(index));
                    }

                    int resolved = (_startIndex + index) % Capacity;
                    return _buffer[resolved];
                }
            }

            public int Count => _count;

            public IEnumerator<TransitionExecutionContext<T>> GetEnumerator()
            {
                for (int i = 0; i < _count; i++)
                {
                    yield return this[i];
                }
            }

            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(TransitionExecutionContext<T> context)
            {
                if (Capacity == 0)
                {
                    return;
                }

                int insertIndex = (_startIndex + _count) % Capacity;
                _buffer[insertIndex] = context;
                if (_count < Capacity)
                {
                    _count++;
                    return;
                }

                _startIndex = (_startIndex + 1) % Capacity;
            }

            public void CopyTo(List<TransitionExecutionContext<T>> buffer)
            {
                buffer.Clear();
                for (int i = 0; i < _count; i++)
                {
                    buffer.Add(this[i]);
                }
            }

            public void Dispose()
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                if (_buffer != null)
                {
                    WallstopArrayPool<TransitionExecutionContext<T>>.Return(
                        _buffer,
                        clear: true
                    );
                    _buffer = null;
                }
                _count = 0;
                _startIndex = 0;
            }
        }
    }
}
