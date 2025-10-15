namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityHelpers.Core.Extension;
    using WallstopStudios.DxState.State.Stack.Internal;

    public enum StateTransitionPhase
    {
        Exit,
        Enter,
        Remove,
        EnterRollback,
        ExitRollback,
        RemoveRollback,
    }

    public sealed class StateTransitionException : Exception
    {
        public StateTransitionException(
            StateTransitionPhase phase,
            IState state,
            Exception innerException
        )
            : base(
                $"State transition failed during {phase} on state '{state?.Name ?? "<null>"}'.",
                innerException
            )
        {
            Phase = phase;
            State = state;
            StateName = state != null ? state.Name : null;
        }

        public StateTransitionPhase Phase { get; }

        public IState State { get; }

        public string StateName { get; }
    }

    public sealed class StateStack
    {
        public bool IsTransitioning => _isTransitioning;

        public IState CurrentState => 0 < _stack.Count ? _stack[^1] : null;

        public IState PreviousState => 1 < _stack.Count ? _stack[^2] : null;

        public float Progress => _latestProgress;

        public IReadOnlyDictionary<string, IState> RegisteredStates => _statesByName;
        public IReadOnlyList<IState> Stack => _stack;

        public event Action<IState, IState> OnStatePushed;
        public event Action<IState, IState> OnStatePopped;
        public event Action<IState, IState> OnTransitionStart;
        public event Action<IState, IState> OnTransitionComplete;
        public event Action<IState, float> OnTransitionProgress;
        public event Action<IState> OnStateManuallyRemoved;
        public event Action<IState> OnFlattened;
        public event Action<IState, IState, Exception> OnTransitionFaulted;
        public event Action<int> OnTransitionQueueDepthChanged;
        public event Action<DeferredTransitionMetrics> OnDeferredTransitionMetricsChanged;

        public readonly struct DeferredTransitionMetrics
        {
            public DeferredTransitionMetrics(int lifetime, int pending)
            {
                LifetimeDeferred = Math.Max(0, lifetime);
                PendingDeferred = Math.Max(0, pending);
            }

            public int LifetimeDeferred { get; }

            public int PendingDeferred { get; }
        }

        private enum TransitionOperation
        {
            Push,
            Pop,
            Flatten,
            Clear,
            Remove,
        }

        private readonly struct TransitionRequest
        {
            public TransitionRequest(
                TransitionOperation operation,
                IState targetState,
                bool shouldRaiseEvents
            )
            {
                Operation = operation;
                TargetState = targetState;
                ShouldRaiseEvents = shouldRaiseEvents;
            }

            public TransitionOperation Operation { get; }

            public IState TargetState { get; }

            public bool ShouldRaiseEvents { get; }
        }

        private readonly List<IState> _stack = new List<IState>();
        private readonly Dictionary<string, IState> _statesByName = new Dictionary<string, IState>(StringComparer.Ordinal);
        private readonly StateTransitionProgressReporter _masterProgress;
        private static readonly IProgress<float> _noOpProgress = new NullProgress();
        private TransitionCompletionSource _transitionWaiter;
        private readonly Queue<QueuedTransition> _transitionQueue = new Queue<QueuedTransition>();
        private readonly List<IState> _removalPreviousBuffer = new List<IState>();
        private readonly List<IState> _removalNextBuffer = new List<IState>();

        private int _currentDeferredTransitionCount;
        private int _lifetimeDeferredTransitionCount;

        private bool _isTransitioning;
        private float _latestProgress = 1f;

        public StateStack()
        {
            _masterProgress = new StateTransitionProgressReporter(this);
        }

        private void RecordTransitionProgress(float value)
        {
            _latestProgress = value;
            OnTransitionProgress?.Invoke(CurrentState, value);
        }

        public int CountOf(IState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            int count = 0;
            foreach (IState existing in _stack)
            {
                if (existing == state)
                {
                    ++count;
                }
            }

            return count;
        }

        public ValueTask WaitForTransitionCompletionAsync()
        {
            if (!_isTransitioning && _transitionQueue.Count == 0)
            {
                return new ValueTask();
            }
            if (_transitionWaiter == null)
            {
                _transitionWaiter = TransitionCompletionSource.Rent();
            }
            return _transitionWaiter.AsValueTask();
        }

        public bool TryRegister(IState state, bool force = false)
        {
            if (force)
            {
                _statesByName[state.Name] = state;
                return true;
            }

            return _statesByName.TryAdd(state.Name, state);
        }

        public bool Unregister(IState state)
        {
            return Unregister(state.Name);
        }

        public bool Unregister(string stateName)
        {
            return _statesByName.Remove(stateName);
        }

        public async ValueTask PushAsync(IState newState)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            _ = TryRegister(newState);
            await PerformTransition(TransitionOperation.Push, newState);
        }

        public async ValueTask PushAsync(string stateName)
        {
            if (!_statesByName.TryGetValue(stateName, out IState state))
            {
                throw new ArgumentException(
                    $"State with name {stateName} does not exist",
                    nameof(stateName)
                );
            }
            await PushAsync(state);
        }

        private async ValueTask InternalPushAsync(IState newState, IProgress<float> overallProgress)
        {
            const StateDirection direction = StateDirection.Forward;
            IState previousState = CurrentState;
            if (previousState == newState)
            {
                overallProgress.Report(1f);
                return;
            }
            if (previousState != null)
            {
                ScopedProgress exitProgress = new ScopedProgress(overallProgress, 0f, 0.5f);
                await InvokeExitAsync(
                    previousState,
                    newState,
                    exitProgress,
                    direction,
                    StateTransitionPhase.Exit
                );
            }
            else
            {
                overallProgress.Report(0.5f);
            }

            _stack.Add(newState);
            ScopedProgress enterProgress = new ScopedProgress(overallProgress, 0.5f, 0.5f);
            try
            {
                await InvokeEnterAsync(
                    newState,
                    previousState,
                    enterProgress,
                    direction,
                    StateTransitionPhase.Enter
                );
            }
            catch (StateTransitionException)
            {
                _stack.RemoveAt(_stack.Count - 1);
                if (previousState != null)
                {
                    await InvokeEnterAsync(
                        previousState,
                        newState,
                        _noOpProgress,
                        StateDirection.Backward,
                        StateTransitionPhase.EnterRollback
                    );
                }
                throw;
            }
            OnStatePushed?.Invoke(previousState, newState);
        }

        public async ValueTask<IState> PopAsync()
        {
            if (_stack.Count == 0)
            {
                throw new InvalidOperationException("Cannot pop from an empty stack.");
            }

            IState currentState = CurrentState;
            IState nextState = PreviousState;
            await PerformTransition(TransitionOperation.Pop, nextState);
            return currentState;
        }

        public async ValueTask<IState> TryPopAsync()
        {
            if (_stack.Count == 0)
            {
                return null;
            }

            return await PopAsync();
        }

        private async ValueTask InternalPopAsync(IState nextState, IProgress<float> overallProgress)
        {
            const StateDirection direction = StateDirection.Backward;
            IState stateToPop = CurrentState;
            ScopedProgress exitProgress = new ScopedProgress(overallProgress, 0f, 0.5f);
            await InvokeExitAsync(
                stateToPop,
                nextState,
                exitProgress,
                direction,
                StateTransitionPhase.Exit
            );
            _stack.RemoveAt(_stack.Count - 1);
            OnStatePopped?.Invoke(stateToPop, nextState);
            if (nextState != null)
            {
                ScopedProgress revertProgress = new ScopedProgress(overallProgress, 0.5f, 0.5f);
                await InvokeEnterAsync(
                    nextState,
                    stateToPop,
                    revertProgress,
                    direction,
                    StateTransitionPhase.EnterRollback
                );
            }
            else
            {
                overallProgress.Report(1f);
            }
        }

        public async ValueTask FlattenAsync(IState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            _ = TryRegister(state);
            await PerformTransition(TransitionOperation.Flatten, state);
            OnFlattened?.Invoke(state);
        }

        public async ValueTask FlattenAsync(string stateName)
        {
            if (!_statesByName.TryGetValue(stateName, out IState state))
            {
                throw new ArgumentException($"State with name {stateName} does not exist");
            }
            await FlattenAsync(state);
        }

        private async ValueTask InternalFlattenAsync(IState state, IProgress<float> overallProgress)
        {
            int initialStackCount = _stack.Count;
            int statesExited = 0;
            const float exitPhaseEndProgress = 0.9f;

            bool targetWasAlreadyActive = CurrentState == state && _stack.Count == 1;
            while (_stack.Count > 0)
            {
                const StateDirection direction = StateDirection.Backward;
                IState stateToExit = CurrentState;
                if (stateToExit == state && _stack.Count == 1)
                {
                    break;
                }

                IState previousState = PreviousState;
                float progressStartForThisExit =
                    statesExited / (float)initialStackCount * exitPhaseEndProgress;
                float progressScaleForThisExit =
                    1 / (float)initialStackCount * exitPhaseEndProgress;

                ScopedProgress exitProgress = new ScopedProgress(
                    overallProgress,
                    progressStartForThisExit,
                    progressScaleForThisExit
                );

                await InvokeExitAsync(
                    stateToExit,
                    previousState,
                    exitProgress,
                    direction,
                    StateTransitionPhase.Exit
                );
                _stack.RemoveAt(_stack.Count - 1);
                statesExited++;
                if (previousState != null)
                {
                    await InvokeEnterAsync(
                        previousState,
                        stateToExit,
                        _noOpProgress,
                        direction,
                        StateTransitionPhase.EnterRollback
                    );
                }
            }

            if (_stack.Count == 0)
            {
                _stack.Add(state);
                if (!targetWasAlreadyActive)
                {
                    ScopedProgress enterProgress = new ScopedProgress(
                        overallProgress,
                        exitPhaseEndProgress,
                        1.0f - exitPhaseEndProgress
                    );

                    await InvokeEnterAsync(
                        state,
                        PreviousState,
                        enterProgress,
                        StateDirection.Forward,
                        StateTransitionPhase.Enter
                    );
                }
            }
            else
            {
                overallProgress.Report(1f);
            }
        }

        public async ValueTask ClearAsync()
        {
            await PerformTransition(TransitionOperation.Clear, null);
        }

        private async ValueTask InternalClearAsync(bool unused, IProgress<float> overallProgress)
        {
            int initialStackCount = _stack.Count;
            int statesExited = 0;
            const StateDirection direction = StateDirection.Backward;
            while (_stack.Count > 0)
            {
                IState stateToExit = CurrentState;
                IState nextState = PreviousState;
                float progressStart = statesExited / (float)initialStackCount;
                float progressScale = 1 / (float)initialStackCount;
                ScopedProgress stepProgress = new ScopedProgress(overallProgress, progressStart, progressScale);

                ScopedProgress exitProgress = new ScopedProgress(
                    stepProgress,
                    0f,
                    nextState != null ? 0.7f : 1.0f
                );

                await InvokeExitAsync(
                    stateToExit,
                    nextState,
                    exitProgress,
                    direction,
                    StateTransitionPhase.Exit
                );
                _stack.RemoveAt(_stack.Count - 1);
                OnStatePopped?.Invoke(stateToExit, nextState);
                if (nextState != null)
                {
                    ScopedProgress revertProgress = new ScopedProgress(stepProgress, 0.7f, 0.3f);
                    await InvokeEnterAsync(
                        nextState,
                        stateToExit,
                        revertProgress,
                        direction,
                        StateTransitionPhase.EnterRollback
                    );
                }
                statesExited++;
            }
            overallProgress.Report(1f);
        }

        public async ValueTask RemoveAsync(string stateName)
        {
            if (!_statesByName.TryGetValue(stateName, out IState stateToRemove))
            {
                throw new ArgumentException(
                    $"State with name {stateName} does not exist",
                    nameof(stateName)
                );
            }
            await RemoveAsync(stateToRemove);
        }

        public async ValueTask RemoveAsync(IState stateToRemove)
        {
            if (stateToRemove == null)
            {
                throw new ArgumentNullException(nameof(stateToRemove));
            }

            if (!_stack.Contains(stateToRemove))
            {
                throw new ArgumentException(
                    $"State '{stateToRemove.Name}' not found in the stack and cannot be removed.",
                    nameof(stateToRemove)
                );
            }

            bool shouldRaiseEvents = stateToRemove == CurrentState;
            await PerformTransition(TransitionOperation.Remove, stateToRemove, shouldRaiseEvents);
        }

        private async ValueTask InternalRemoveAsync(
            IState stateToRemove,
            IProgress<float> overallProgress
        )
        {
            int removalIndex = _stack.IndexOf(stateToRemove);
            if (removalIndex < 0)
            {
                Debug.LogError(
                    $"InternalRemoveAsync: stateToRemove '{stateToRemove.Name}' was not found in the stack. This should have been caught earlier."
                );
                overallProgress.Report(1f);
                return;
            }

            bool wasCurrentActiveState = removalIndex == _stack.Count - 1;

            List<IState> previousStatesBuffer = _removalPreviousBuffer;
            previousStatesBuffer.Clear();
            for (int i = 0; i < removalIndex; i++)
            {
                previousStatesBuffer.Add(_stack[i]);
            }

            List<IState> nextStatesBuffer = _removalNextBuffer;
            IReadOnlyList<IState> nextStatesInStackView;
            if (wasCurrentActiveState)
            {
                nextStatesInStackView = Array.Empty<IState>();
                nextStatesBuffer.Clear();
            }
            else
            {
                nextStatesBuffer.Clear();
                for (int i = removalIndex + 1; i < _stack.Count; i++)
                {
                    nextStatesBuffer.Add(_stack[i]);
                }

                nextStatesInStackView = nextStatesBuffer;
            }

            if (wasCurrentActiveState)
            {
                IState stateBecomingActive = removalIndex > 0 ? _stack[removalIndex - 1] : null;

                ScopedProgress removeMethodProgress = new ScopedProgress(overallProgress, 0f, 0.4f);
                await InvokeRemoveAsync(
                    stateToRemove,
                    previousStatesBuffer,
                    nextStatesInStackView,
                    removeMethodProgress
                );

                _stack.RemoveAt(removalIndex);
                if (stateBecomingActive != null)
                {
                    ScopedProgress revertProgress = new ScopedProgress(overallProgress, 0.4f, 0.6f);
                    await InvokeEnterAsync(
                        stateBecomingActive,
                        stateToRemove,
                        revertProgress,
                        StateDirection.Backward,
                        StateTransitionPhase.EnterRollback
                    );
                }
            }
            else
            {
                ScopedProgress removeMethodProgress = new ScopedProgress(overallProgress, 0f, 1.0f);
                await InvokeRemoveAsync(
                    stateToRemove,
                    previousStatesBuffer,
                    nextStatesInStackView,
                    removeMethodProgress
                );

                _stack.RemoveAt(removalIndex);
            }

            previousStatesBuffer.Clear();
            nextStatesBuffer.Clear();
            OnStateManuallyRemoved?.Invoke(stateToRemove);
        }

        private static async ValueTask InvokeExitAsync(
            IState state,
            IState nextState,
            IProgress<float> progress,
            StateDirection direction,
            StateTransitionPhase phase
        )
        {
            try
            {
                await state.Exit(nextState, progress, direction);
            }
            catch (StateTransitionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new StateTransitionException(phase, state, exception);
            }
        }

        private static async ValueTask InvokeEnterAsync(
            IState state,
            IState previousState,
            IProgress<float> progress,
            StateDirection direction,
            StateTransitionPhase phase
        )
        {
            try
            {
                await state.Enter(previousState, progress, direction);
            }
            catch (StateTransitionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new StateTransitionException(phase, state, exception);
            }
        }

        private static async ValueTask InvokeRemoveAsync(
            IState state,
            IReadOnlyList<IState> previousStates,
            IReadOnlyList<IState> nextStates,
            IProgress<float> progress
        )
        {
            try
            {
                await state.Remove(previousStates, nextStates, progress);
            }
            catch (StateTransitionException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new StateTransitionException(StateTransitionPhase.Remove, state, exception);
            }
        }

        public void Update()
        {
            PerformUpdate(TickMode.Update);
        }

        public void FixedUpdate()
        {
            PerformUpdate(TickMode.FixedUpdate);
        }

        public void LateUpdate()
        {
            PerformUpdate(TickMode.LateUpdate);
        }

        private void PerformUpdate(TickMode tickMode)
        {
            if (_isTransitioning)
            {
                return;
            }

            if (tickMode == TickMode.None)
            {
                return;
            }

            float delta = ResolveDeltaForTick(tickMode);

            int stackCount = _stack.Count;
            if (stackCount == 0)
            {
                return;
            }

            IState current = _stack[stackCount - 1];
            if (current != null && current.TickMode.HasFlagNoAlloc(tickMode))
            {
                current.Tick(tickMode, delta);
            }

            if (stackCount <= 1)
            {
                return;
            }

            for (int i = 0; i < stackCount - 1; ++i)
            {
                IState inactiveState = _stack[i];
                if (!inactiveState.TickWhenInactive)
                {
                    continue;
                }

                if (!inactiveState.TickMode.HasFlagNoAlloc(tickMode))
                {
                    continue;
                }

                inactiveState.Tick(tickMode, delta);
            }
        }

        private static float ResolveDeltaForTick(TickMode tickMode)
        {
            if ((tickMode & TickMode.FixedUpdate) == TickMode.FixedUpdate)
            {
                return Time.fixedDeltaTime;
            }

            return Time.deltaTime;
        }

        private ValueTask PerformTransition(
            TransitionOperation operation,
            IState targetState,
            bool shouldRaiseEvents = true
        )
        {
            TransitionRequest request = new TransitionRequest(operation, targetState, shouldRaiseEvents);
            if (!_isTransitioning && _transitionQueue.Count == 0)
            {
                return ExecuteTransitionInternal(request);
            }

            TransitionCompletionSource completionSource = TransitionCompletionSource.Rent();
            _transitionQueue.Enqueue(new QueuedTransition(request, completionSource, true));
            OnTransitionQueueDepthChanged?.Invoke(_transitionQueue.Count);
            _currentDeferredTransitionCount++;
            _lifetimeDeferredTransitionCount++;
            PublishDeferredMetrics();
            TryProcessNextQueuedTransition();
            return completionSource.AsValueTask();
        }

        private async ValueTask ExecuteTransitionInternal(TransitionRequest request)
        {
            if (_isTransitioning)
            {
                throw new InvalidOperationException(
                    "Cannot perform transition while state stack is already transitioning"
                );
            }

            _isTransitioning = true;
            IState transitionStartState = CurrentState;
            try
            {
                if (request.ShouldRaiseEvents)
                {
                    OnTransitionStart?.Invoke(transitionStartState, request.TargetState);
                }
                _masterProgress.Report(0f);
                await ExecuteTransitionOperationAsync(request);
                _masterProgress.Report(1f);
                if (request.ShouldRaiseEvents)
                {
                    OnTransitionComplete?.Invoke(transitionStartState, CurrentState);
                }
            }
            catch (Exception exception)
            {
                _masterProgress.Report(1f);
                OnTransitionFaulted?.Invoke(transitionStartState, request.TargetState, exception);
                _transitionWaiter?.SetException(exception);
                _transitionWaiter = null;
                throw;
            }
            finally
            {
                _isTransitioning = false;
                TryProcessNextQueuedTransition();
            }
        }

        private ValueTask ExecuteTransitionOperationAsync(TransitionRequest request)
        {
            switch (request.Operation)
            {
                case TransitionOperation.Push:
                    return InternalPushAsync(request.TargetState, _masterProgress);
                case TransitionOperation.Pop:
                    return InternalPopAsync(request.TargetState, _masterProgress);
                case TransitionOperation.Flatten:
                    return InternalFlattenAsync(request.TargetState, _masterProgress);
                case TransitionOperation.Clear:
                    return InternalClearAsync(false, _masterProgress);
                case TransitionOperation.Remove:
                    return InternalRemoveAsync(request.TargetState, _masterProgress);
                default:
                    throw new ArgumentOutOfRangeException(nameof(request.Operation), request.Operation, null);
            }
        }

        private void TryProcessNextQueuedTransition()
        {
            if (_isTransitioning)
            {
                return;
            }

            if (_transitionQueue.Count == 0)
            {
                OnTransitionQueueDepthChanged?.Invoke(0);
                if (_currentDeferredTransitionCount != 0)
                {
                    _currentDeferredTransitionCount = 0;
                    PublishDeferredMetrics();
                }
                _transitionWaiter?.SetResult();
                _transitionWaiter = null;
                return;
            }

            QueuedTransition queuedTransition = _transitionQueue.Dequeue();
            if (queuedTransition.CountedAsDeferred && _currentDeferredTransitionCount > 0)
            {
                _currentDeferredTransitionCount--;
                PublishDeferredMetrics();
            }
            OnTransitionQueueDepthChanged?.Invoke(_transitionQueue.Count);
            _ = RunQueuedTransitionAsync(queuedTransition);
        }

        private async Task RunQueuedTransitionAsync(QueuedTransition queuedTransition)
        {
            try
            {
                await ExecuteTransitionInternal(queuedTransition.Request);
                TransitionCompletionSource completionSource = queuedTransition.CompletionSource;
                completionSource?.SetResult();
            }
            catch (Exception exception)
            {
                TransitionCompletionSource completionSource = queuedTransition.CompletionSource;
                completionSource?.SetException(exception);
            }
        }

        private void PublishDeferredMetrics()
        {
            Action<DeferredTransitionMetrics> handler = OnDeferredTransitionMetricsChanged;
            if (handler == null)
            {
                return;
            }

            DeferredTransitionMetrics metrics = new DeferredTransitionMetrics(
                _lifetimeDeferredTransitionCount,
                _currentDeferredTransitionCount
            );
            handler.Invoke(metrics);
        }

        private readonly struct QueuedTransition
        {
            public QueuedTransition(
                TransitionRequest request,
                TransitionCompletionSource completionSource,
                bool countedAsDeferred
            )
            {
                Request = request;
                CompletionSource = completionSource;
                CountedAsDeferred = countedAsDeferred;
            }

            public TransitionRequest Request { get; }

            public TransitionCompletionSource CompletionSource { get; }

            public bool CountedAsDeferred { get; }
        }

        private sealed class StateTransitionProgressReporter : IProgress<float>
        {
            private readonly StateStack _owner;

            public StateTransitionProgressReporter(StateStack owner)
            {
                _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            }

            public void Report(float value)
            {
                _owner.RecordTransitionProgress(value);
            }
        }

        private sealed class NullProgress : IProgress<float>
        {
            public void Report(float value) { }
        }
    }
}
