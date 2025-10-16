namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
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

    public class StateTransitionException : Exception
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

        public int TransitionQueueDepth => Math.Max(0, _transitionQueue.Count);

        public DeferredTransitionMetrics CurrentDeferredMetrics =>
            new DeferredTransitionMetrics(
                _lifetimeDeferredTransitionCount,
                _currentDeferredTransitionCount
            );

        public IReadOnlyDictionary<string, IState> RegisteredStates => _statesByName;
        public IReadOnlyList<IState> Stack => _stack;
        public IReadOnlyList<StateStackTransitionRecord> TransitionHistory => _transitionHistory;

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

        public enum TransitionOperation
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
                bool shouldRaiseEvents,
                StateTransitionOptions options
            )
            {
                Operation = operation;
                TargetState = targetState;
                ShouldRaiseEvents = shouldRaiseEvents;
                Options = options;
            }

            public TransitionOperation Operation { get; }

            public IState TargetState { get; }

            public bool ShouldRaiseEvents { get; }

            public StateTransitionOptions Options { get; }
        }

        private readonly List<IState> _stack = new List<IState>();
        private readonly Dictionary<string, IState> _statesByName = new Dictionary<string, IState>(
            StringComparer.Ordinal
        );
        private readonly StateTransitionProgressReporter _masterProgress;
        private static readonly IProgress<float> _noOpProgress = new NullProgress();
        private TransitionCompletionSource _transitionWaiter;
        private readonly Queue<QueuedTransition> _transitionQueue = new Queue<QueuedTransition>();
        private readonly List<IState> _removalPreviousBuffer = new List<IState>();
        private readonly List<IState> _removalNextBuffer = new List<IState>();
        private readonly TransitionHistoryBuffer _transitionHistory;

        private int _currentDeferredTransitionCount;
        private int _lifetimeDeferredTransitionCount;

        private bool _isTransitioning;
        private float _latestProgress = 1f;
        private readonly int _mainThreadId;

        private const int DefaultTransitionHistoryCapacity = 64;

        public StateStack()
        {
            _masterProgress = new StateTransitionProgressReporter(this);
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            _transitionHistory = new TransitionHistoryBuffer(DefaultTransitionHistoryCapacity);
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
            EnsureMainThread(nameof(WaitForTransitionCompletionAsync));
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

        public void CopyTransitionHistory(List<StateStackTransitionRecord> buffer)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(nameof(buffer));
            }

            _transitionHistory.CopyTo(buffer);
        }

        public bool TryRegister(IState state, bool force = false)
        {
            if (force)
            {
                EnsureValidStateName(state);
                _statesByName[state.Name] = state;
                return true;
            }
            EnsureValidStateName(state);
            return _statesByName.TryAdd(state.Name, state);
        }

        public bool Unregister(IState state)
        {
            return Unregister(state.Name);
        }

        public bool Unregister(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                throw new ArgumentException(
                    "State name must be provided when unregistering.",
                    nameof(stateName)
                );
            }

            return _statesByName.Remove(stateName);
        }

        public ValueTask PushAsync(IState newState)
        {
            return PushAsync(newState, StateTransitionOptions.Default);
        }

        public ValueTask PushAsync(IState newState, StateTransitionOptions options)
        {
            if (newState == null)
            {
                throw new ArgumentNullException(nameof(newState));
            }

            EnsureMainThread(nameof(PushAsync));
            _ = TryRegister(newState);
            return PerformTransition(TransitionOperation.Push, newState, true, options);
        }

        public ValueTask PushAsync(string stateName)
        {
            return PushAsync(stateName, StateTransitionOptions.Default);
        }

        public ValueTask PushAsync(string stateName, StateTransitionOptions options)
        {
            EnsureMainThread(nameof(PushAsync));
            if (string.IsNullOrWhiteSpace(stateName))
            {
                throw new ArgumentException("State name must be provided.", nameof(stateName));
            }

            if (!_statesByName.TryGetValue(stateName, out IState state))
            {
                throw new ArgumentException(
                    $"State with name {stateName} does not exist",
                    nameof(stateName)
                );
            }
            return PushAsync(state, options);
        }

        private async ValueTask InternalPushAsync(
            IState newState,
            IProgress<float> overallProgress,
            StateTransitionOptions options,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
        )
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
                    StateTransitionPhase.Exit,
                    options,
                    cancellationToken,
                    cancellationScope
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
                    StateTransitionPhase.Enter,
                    options,
                    cancellationToken,
                    cancellationScope
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
                        StateTransitionPhase.EnterRollback,
                        options,
                        cancellationToken,
                        cancellationScope
                    );
                }
                throw;
            }
            OnStatePushed?.Invoke(previousState, newState);
        }

        public ValueTask<IState> PopAsync()
        {
            return PopAsync(StateTransitionOptions.Default);
        }

        public async ValueTask<IState> PopAsync(StateTransitionOptions options)
        {
            EnsureMainThread(nameof(PopAsync));
            if (_stack.Count == 0)
            {
                throw new InvalidOperationException("Cannot pop from an empty stack.");
            }

            IState currentState = CurrentState;
            IState nextState = PreviousState;
            await PerformTransition(TransitionOperation.Pop, nextState, true, options);
            return currentState;
        }

        public ValueTask<IState> TryPopAsync()
        {
            return TryPopAsync(StateTransitionOptions.Default);
        }

        public async ValueTask<IState> TryPopAsync(StateTransitionOptions options)
        {
            EnsureMainThread(nameof(TryPopAsync));
            if (_stack.Count == 0)
            {
                return null;
            }

            return await PopAsync(options);
        }

        private async ValueTask InternalPopAsync(
            IState nextState,
            IProgress<float> overallProgress,
            StateTransitionOptions options,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
        )
        {
            const StateDirection direction = StateDirection.Backward;
            IState stateToPop = CurrentState;
            ScopedProgress exitProgress = new ScopedProgress(overallProgress, 0f, 0.5f);
            await InvokeExitAsync(
                stateToPop,
                nextState,
                exitProgress,
                direction,
                StateTransitionPhase.Exit,
                options,
                cancellationToken,
                cancellationScope
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
                    StateTransitionPhase.EnterRollback,
                    options,
                    cancellationToken,
                    cancellationScope
                );
            }
            else
            {
                overallProgress.Report(1f);
            }
        }

        public ValueTask FlattenAsync(IState state)
        {
            return FlattenAsync(state, StateTransitionOptions.Default);
        }

        public async ValueTask FlattenAsync(IState state, StateTransitionOptions options)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            EnsureMainThread(nameof(FlattenAsync));
            _ = TryRegister(state);
            await PerformTransition(TransitionOperation.Flatten, state, true, options);
            OnFlattened?.Invoke(state);
        }

        public ValueTask FlattenAsync(string stateName)
        {
            return FlattenAsync(stateName, StateTransitionOptions.Default);
        }

        public async ValueTask FlattenAsync(string stateName, StateTransitionOptions options)
        {
            EnsureMainThread(nameof(FlattenAsync));
            if (string.IsNullOrWhiteSpace(stateName))
            {
                throw new ArgumentException("State name must be provided.", nameof(stateName));
            }

            if (!_statesByName.TryGetValue(stateName, out IState state))
            {
                throw new ArgumentException($"State with name {stateName} does not exist");
            }
            await FlattenAsync(state, options);
        }

        private async ValueTask InternalFlattenAsync(
            IState state,
            IProgress<float> overallProgress,
            StateTransitionOptions options,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
        )
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
                    StateTransitionPhase.Exit,
                    options,
                    cancellationToken,
                    cancellationScope
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
                        StateTransitionPhase.EnterRollback,
                        options,
                        cancellationToken,
                        cancellationScope
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
                        StateTransitionPhase.Enter,
                        options,
                        cancellationToken,
                        cancellationScope
                    );
                }
            }
            else
            {
                overallProgress.Report(1f);
            }
        }

        public ValueTask ClearAsync()
        {
            return ClearAsync(StateTransitionOptions.Default);
        }

        public async ValueTask ClearAsync(StateTransitionOptions options)
        {
            EnsureMainThread(nameof(ClearAsync));
            await PerformTransition(TransitionOperation.Clear, null, true, options);
        }

        private async ValueTask InternalClearAsync(
            bool unused,
            IProgress<float> overallProgress,
            StateTransitionOptions options,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
        )
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
                ScopedProgress stepProgress = new ScopedProgress(
                    overallProgress,
                    progressStart,
                    progressScale
                );

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
                    StateTransitionPhase.Exit,
                    options,
                    cancellationToken,
                    cancellationScope
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
                        StateTransitionPhase.EnterRollback,
                        options,
                        cancellationToken,
                        cancellationScope
                    );
                }
                statesExited++;
            }
            overallProgress.Report(1f);
        }

        public ValueTask RemoveAsync(string stateName)
        {
            return RemoveAsync(stateName, StateTransitionOptions.Default);
        }

        public async ValueTask RemoveAsync(string stateName, StateTransitionOptions options)
        {
            EnsureMainThread(nameof(RemoveAsync));
            if (string.IsNullOrWhiteSpace(stateName))
            {
                throw new ArgumentException("State name must be provided.", nameof(stateName));
            }

            if (!_statesByName.TryGetValue(stateName, out IState stateToRemove))
            {
                throw new ArgumentException(
                    $"State with name {stateName} does not exist",
                    nameof(stateName)
                );
            }
            await RemoveAsync(stateToRemove, options);
        }

        public ValueTask RemoveAsync(IState stateToRemove)
        {
            return RemoveAsync(stateToRemove, StateTransitionOptions.Default);
        }

        public async ValueTask RemoveAsync(IState stateToRemove, StateTransitionOptions options)
        {
            EnsureMainThread(nameof(RemoveAsync));
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
            await PerformTransition(
                TransitionOperation.Remove,
                stateToRemove,
                shouldRaiseEvents,
                options
            );
        }

        private async ValueTask InternalRemoveAsync(
            IState stateToRemove,
            IProgress<float> overallProgress,
            StateTransitionOptions options,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
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
                    removeMethodProgress,
                    options,
                    cancellationToken,
                    cancellationScope
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
                        StateTransitionPhase.EnterRollback,
                        options,
                        cancellationToken,
                        cancellationScope
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
                    removeMethodProgress,
                    options,
                    cancellationToken,
                    cancellationScope
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
            StateTransitionPhase phase,
            StateTransitionOptions options,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ValueTask operation;
                if (state is ICancellableState cancellableState)
                {
                    operation = cancellableState.Exit(
                        nextState,
                        progress,
                        direction,
                        cancellationToken
                    );
                }
                else
                {
                    operation = state.Exit(nextState, progress, direction);
                }

                await operation;

                if (!(state is ICancellableState) && cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException exception)
            {
                HandleCancellationException(state, phase, cancellationScope, exception);
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
            StateTransitionPhase phase,
            StateTransitionOptions options,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ValueTask operation;
                if (state is ICancellableState cancellableState)
                {
                    operation = cancellableState.Enter(
                        previousState,
                        progress,
                        direction,
                        cancellationToken
                    );
                }
                else
                {
                    operation = state.Enter(previousState, progress, direction);
                }

                await operation;

                if (!(state is ICancellableState) && cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException exception)
            {
                HandleCancellationException(state, phase, cancellationScope, exception);
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
            IProgress<float> progress,
            StateTransitionOptions options,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
        )
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                ValueTask operation;
                if (state is ICancellableState cancellableState)
                {
                    operation = cancellableState.Remove(
                        previousStates,
                        nextStates,
                        progress,
                        cancellationToken
                    );
                }
                else
                {
                    operation = state.Remove(previousStates, nextStates, progress);
                }

                await operation;

                if (!(state is ICancellableState) && cancellationToken.IsCancellationRequested)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                }
            }
            catch (OperationCanceledException exception)
            {
                HandleCancellationException(
                    state,
                    StateTransitionPhase.Remove,
                    cancellationScope,
                    exception
                );
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

        private static void HandleCancellationException(
            IState state,
            StateTransitionPhase phase,
            TransitionCancellationScope cancellationScope,
            OperationCanceledException exception
        )
        {
            bool canceledByTimeout = cancellationScope.IsTimeout;
            if (canceledByTimeout)
            {
                if (cancellationScope.ThrowOnTimeout)
                {
                    TimeSpan timeout = cancellationScope.Timeout ?? TimeSpan.Zero;
                    throw new StateTransitionTimeoutException(phase, state, timeout, exception);
                }

                throw new StateTransitionCanceledException(phase, state, exception);
            }

            throw new StateTransitionCanceledException(phase, state, exception);
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
            return PerformTransition(
                operation,
                targetState,
                shouldRaiseEvents,
                StateTransitionOptions.Default
            );
        }

        private ValueTask PerformTransition(
            TransitionOperation operation,
            IState targetState,
            bool shouldRaiseEvents,
            StateTransitionOptions options
        )
        {
            StateTransitionOptions effectiveOptions = options;
            if (
                !effectiveOptions.HasTimeout
                && !effectiveOptions.CancellationToken.CanBeCanceled
                && !effectiveOptions.ThrowOnTimeout
            )
            {
                effectiveOptions = StateTransitionOptions.Default;
            }

            TransitionRequest request = new TransitionRequest(
                operation,
                targetState,
                shouldRaiseEvents,
                effectiveOptions
            );
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
            TransitionCancellationScope cancellationScope = new TransitionCancellationScope(
                request.Options
            );
            try
            {
                if (request.ShouldRaiseEvents)
                {
                    OnTransitionStart?.Invoke(transitionStartState, request.TargetState);
                }
                _masterProgress.Report(0f);
                await ExecuteTransitionOperationAsync(
                    request,
                    cancellationScope.Token,
                    cancellationScope
                );
                _masterProgress.Report(1f);
                if (request.ShouldRaiseEvents)
                {
                    OnTransitionComplete?.Invoke(transitionStartState, CurrentState);
                }
                _transitionHistory.Add(
                    new StateStackTransitionRecord(
                        DateTime.UtcNow,
                        request.Operation,
                        transitionStartState,
                        CurrentState,
                        request.TargetState,
                        request.Options,
                        request.ShouldRaiseEvents
                    )
                );
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
                cancellationScope.Dispose();
                _isTransitioning = false;
                TryProcessNextQueuedTransition();
            }
        }

        private ValueTask ExecuteTransitionOperationAsync(
            TransitionRequest request,
            CancellationToken cancellationToken,
            TransitionCancellationScope cancellationScope
        )
        {
            switch (request.Operation)
            {
                case TransitionOperation.Push:
                    return InternalPushAsync(
                        request.TargetState,
                        _masterProgress,
                        request.Options,
                        cancellationToken,
                        cancellationScope
                    );
                case TransitionOperation.Pop:
                    return InternalPopAsync(
                        request.TargetState,
                        _masterProgress,
                        request.Options,
                        cancellationToken,
                        cancellationScope
                    );
                case TransitionOperation.Flatten:
                    return InternalFlattenAsync(
                        request.TargetState,
                        _masterProgress,
                        request.Options,
                        cancellationToken,
                        cancellationScope
                    );
                case TransitionOperation.Clear:
                    return InternalClearAsync(
                        false,
                        _masterProgress,
                        request.Options,
                        cancellationToken,
                        cancellationScope
                    );
                case TransitionOperation.Remove:
                    return InternalRemoveAsync(
                        request.TargetState,
                        _masterProgress,
                        request.Options,
                        cancellationToken,
                        cancellationScope
                    );
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(request.Operation),
                        request.Operation,
                        null
                    );
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

        private sealed class TransitionCancellationScope : IDisposable
        {
            private readonly CancellationToken _originalToken;
            private readonly CancellationTokenSource _timeoutSource;
            private readonly CancellationTokenSource _linkedSource;
            private readonly TimeSpan? _timeout;
            private readonly bool _throwOnTimeout;

            public TransitionCancellationScope(StateTransitionOptions options)
            {
                _originalToken = options.CancellationToken;
                _timeout = options.Timeout;
                _throwOnTimeout = options.ThrowOnTimeout;

                if (!_originalToken.CanBeCanceled && !_timeout.HasValue)
                {
                    Token = CancellationToken.None;
                    return;
                }

                if (_timeout.HasValue)
                {
                    if (_timeout.Value < TimeSpan.Zero)
                    {
                        throw new ArgumentOutOfRangeException(
                            nameof(StateTransitionOptions.Timeout),
                            "Timeout must be non-negative."
                        );
                    }

                    _timeoutSource = new CancellationTokenSource();
                    _timeoutSource.CancelAfter(_timeout.Value);
                }

                if (_originalToken.CanBeCanceled && _timeoutSource != null)
                {
                    _linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
                        _originalToken,
                        _timeoutSource.Token
                    );
                    Token = _linkedSource.Token;
                    return;
                }

                if (_originalToken.CanBeCanceled)
                {
                    Token = _originalToken;
                    return;
                }

                if (_timeoutSource != null)
                {
                    Token = _timeoutSource.Token;
                    return;
                }

                Token = CancellationToken.None;
            }

            public CancellationToken Token { get; }

            public bool ThrowOnTimeout => _throwOnTimeout;

            public TimeSpan? Timeout => _timeout;

            public bool IsTimeout =>
                _timeoutSource != null
                && _timeoutSource.IsCancellationRequested
                && !_originalToken.IsCancellationRequested;

            public void Dispose()
            {
                _linkedSource?.Dispose();
                _timeoutSource?.Dispose();
            }
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

        public enum StateStackTransitionType
        {
            Push = TransitionOperation.Push,
            Pop = TransitionOperation.Pop,
            Flatten = TransitionOperation.Flatten,
            Clear = TransitionOperation.Clear,
            Remove = TransitionOperation.Remove,
        }

        public readonly struct StateStackTransitionRecord
        {
            public StateStackTransitionRecord(
                DateTime timestampUtc,
                TransitionOperation operation,
                IState previousState,
                IState currentState,
                IState requestedTarget,
                StateTransitionOptions options,
                bool raisedEvents
            )
            {
                TimestampUtc = timestampUtc;
                Operation = (StateStackTransitionType)operation;
                PreviousState = previousState;
                CurrentState = currentState;
                RequestedTarget = requestedTarget;
                Options = options;
                RaisedEvents = raisedEvents;
            }

            public DateTime TimestampUtc { get; }

            public StateStackTransitionType Operation { get; }

            public IState PreviousState { get; }

            public IState CurrentState { get; }

            public IState RequestedTarget { get; }

            public StateTransitionOptions Options { get; }

            public bool RaisedEvents { get; }
        }

        private sealed class TransitionHistoryBuffer : IReadOnlyList<StateStackTransitionRecord>
        {
            private readonly StateStackTransitionRecord[] _buffer;
            private int _count;
            private int _startIndex;

            public TransitionHistoryBuffer(int capacity)
            {
                Capacity = capacity <= 0 ? DefaultTransitionHistoryCapacity : capacity;
                _buffer = new StateStackTransitionRecord[Capacity];
                _count = 0;
                _startIndex = 0;
            }

            public int Capacity { get; }

            public StateStackTransitionRecord this[int index]
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

            public void Add(StateStackTransitionRecord record)
            {
                if (Capacity == 0)
                {
                    return;
                }

                int insertIndex = (_startIndex + _count) % Capacity;
                _buffer[insertIndex] = record;
                if (_count < Capacity)
                {
                    _count++;
                    return;
                }

                _startIndex = (_startIndex + 1) % Capacity;
            }

            public void CopyTo(List<StateStackTransitionRecord> buffer)
            {
                buffer.Clear();
                for (int i = 0; i < _count; i++)
                {
                    buffer.Add(this[i]);
                }
            }

            public IEnumerator<StateStackTransitionRecord> GetEnumerator()
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
        }

        private sealed class NullProgress : IProgress<float>
        {
            public void Report(float value) { }
        }

        private void EnsureMainThread(string operationName)
        {
            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                return;
            }

            string message =
                $"StateStack.{operationName} must be invoked from the Unity main thread.";
            Debug.LogError(message);
            throw new InvalidOperationException(message);
        }

        private static void EnsureValidStateName(IState state)
        {
            string name = state.Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "States must expose a non-empty Name. Override the Name property or assign a serialized name.",
                    nameof(state)
                );
            }
        }
    }
}
