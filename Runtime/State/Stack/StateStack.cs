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

        private readonly List<IState> _stack = new();
        private readonly Dictionary<string, IState> _statesByName = new(StringComparer.Ordinal);
        private readonly IProgress<float> _masterProgress;
        private readonly Progress<float> _noOpProgress;
        private TransitionCompletionSource _transitionWaiter;
        private readonly Queue<QueuedTransition> _transitionQueue = new();
        private readonly List<IState> _removalPreviousBuffer = new();
        private readonly List<IState> _removalNextBuffer = new();

        private readonly Func<IState, IProgress<float>, ValueTask> _push;
        private readonly Func<IState, IProgress<float>, ValueTask> _pop;
        private readonly Func<IState, IProgress<float>, ValueTask> _flatten;
        private readonly Func<bool, IProgress<float>, ValueTask> _clear;
        private readonly Func<IState, IProgress<float>, ValueTask> _removeInternalAction;

        private bool _isTransitioning;
        private float _latestProgress = 1f;

        public StateStack()
        {
            _masterProgress = new Progress<float>(value =>
                OnTransitionProgress?.Invoke(CurrentState, value)
            );
            OnTransitionProgress += (_, progress) => _latestProgress = progress;
            _noOpProgress = new Progress<float>(_ => { });
            _push = InternalPushAsync;
            _pop = InternalPopAsync;
            _flatten = InternalFlattenAsync;
            _clear = InternalClearAsync;
            _removeInternalAction = InternalRemoveAsync;
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
            await PerformTransition(_push, newState, newState);
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
                ScopedProgress exitProgress = new(overallProgress, 0f, 0.5f);
                await ExecuteStateOperationAsync(
                    () => previousState.Exit(newState, exitProgress, direction),
                    StateTransitionPhase.Exit,
                    previousState
                );
            }
            else
            {
                overallProgress.Report(0.5f);
            }

            _stack.Add(newState);
            ScopedProgress enterProgress = new(overallProgress, 0.5f, 0.5f);
            try
            {
                await ExecuteStateOperationAsync(
                    () => newState.Enter(previousState, enterProgress, direction),
                    StateTransitionPhase.Enter,
                    newState
                );
            }
            catch
            {
                _stack.RemoveAt(_stack.Count - 1);
                if (previousState != null)
                {
                    await ExecuteStateOperationAsync(
                        () => previousState.Enter(
                            newState,
                            _noOpProgress,
                            StateDirection.Backward
                        ),
                        StateTransitionPhase.EnterRollback,
                        previousState
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
            await PerformTransition(_pop, nextState, nextState);
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
            ScopedProgress exitProgress = new(overallProgress, 0f, 0.5f);
            await ExecuteStateOperationAsync(
                () => stateToPop.Exit(nextState, exitProgress, direction),
                StateTransitionPhase.Exit,
                stateToPop
            );
            _stack.RemoveAt(_stack.Count - 1);
            OnStatePopped?.Invoke(stateToPop, nextState);
            if (nextState != null)
            {
                ScopedProgress revertProgress = new(overallProgress, 0.5f, 0.5f);
                await ExecuteStateOperationAsync(
                    () => nextState.Enter(stateToPop, revertProgress, direction),
                    StateTransitionPhase.EnterRollback,
                    nextState
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
            await PerformTransition(_flatten, state, state);
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

                ScopedProgress exitProgress = new(
                    overallProgress,
                    progressStartForThisExit,
                    progressScaleForThisExit
                );

                await ExecuteStateOperationAsync(
                    () => stateToExit.Exit(previousState, exitProgress, direction),
                    StateTransitionPhase.Exit,
                    stateToExit
                );
                _stack.RemoveAt(_stack.Count - 1);
                statesExited++;
                if (previousState != null)
                {
                    await ExecuteStateOperationAsync(
                        () => previousState.Enter(stateToExit, _noOpProgress, direction),
                        StateTransitionPhase.EnterRollback,
                        previousState
                    );
                }
            }

            if (_stack.Count == 0)
            {
                _stack.Add(state);
                if (!targetWasAlreadyActive)
                {
                    ScopedProgress enterProgress = new(
                        overallProgress,
                        exitPhaseEndProgress,
                        1.0f - exitPhaseEndProgress
                    );

                    await ExecuteStateOperationAsync(
                        () => state.Enter(
                            PreviousState,
                            enterProgress,
                            StateDirection.Forward
                        ),
                        StateTransitionPhase.Enter,
                        state
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
            await PerformTransition(_clear, null);
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
                ScopedProgress stepProgress = new(overallProgress, progressStart, progressScale);

                ScopedProgress exitProgress = new(
                    stepProgress,
                    0f,
                    nextState != null ? 0.7f : 1.0f
                );

                await ExecuteStateOperationAsync(
                    () => stateToExit.Exit(nextState, exitProgress, direction),
                    StateTransitionPhase.Exit,
                    stateToExit
                );
                _stack.RemoveAt(_stack.Count - 1);
                OnStatePopped?.Invoke(stateToExit, nextState);
                if (nextState != null)
                {
                    ScopedProgress revertProgress = new(stepProgress, 0.7f, 0.3f);
                    await ExecuteStateOperationAsync(
                        () => nextState.Enter(stateToExit, revertProgress, direction),
                        StateTransitionPhase.EnterRollback,
                        nextState
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

            await PerformTransition(
                _removeInternalAction,
                stateToRemove,
                stateToRemove,
                shouldInvokeTransition: stateToRemove == CurrentState
            );
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

                ScopedProgress removeMethodProgress = new(overallProgress, 0f, 0.4f);
                await ExecuteStateOperationAsync(
                    () => stateToRemove.Remove(
                        previousStatesBuffer,
                        nextStatesInStackView,
                        removeMethodProgress
                    ),
                    StateTransitionPhase.Remove,
                    stateToRemove
                );

                _stack.RemoveAt(removalIndex);
                if (stateBecomingActive != null)
                {
                    ScopedProgress revertProgress = new(overallProgress, 0.4f, 0.6f);
                    await ExecuteStateOperationAsync(
                        () => stateBecomingActive.Enter(
                            stateToRemove,
                            revertProgress,
                            StateDirection.Backward
                        ),
                        StateTransitionPhase.EnterRollback,
                        stateBecomingActive
                    );
                }
            }
            else
            {
                ScopedProgress removeMethodProgress = new(overallProgress, 0f, 1.0f);
                await ExecuteStateOperationAsync(
                    () => stateToRemove.Remove(
                        previousStatesBuffer,
                        nextStatesInStackView,
                        removeMethodProgress
                    ),
                    StateTransitionPhase.Remove,
                    stateToRemove
                );

                _stack.RemoveAt(removalIndex);
            }

            previousStatesBuffer.Clear();
            nextStatesBuffer.Clear();
            OnStateManuallyRemoved?.Invoke(stateToRemove);
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

        private ValueTask PerformTransition<TContext>(
            Func<TContext, IProgress<float>, ValueTask> transition,
            IState targetState,
            TContext context = default,
            bool shouldInvokeTransition = true
        )
        {
            ValueTask ExecuteTransition()
            {
                return ExecuteTransitionInternal(transition, targetState, context, shouldInvokeTransition);
            }

            if (!_isTransitioning && _transitionQueue.Count == 0)
            {
                return ExecuteTransition();
            }

            TransitionCompletionSource completionSource = TransitionCompletionSource.Rent();
            _transitionQueue.Enqueue(new QueuedTransition(ExecuteTransition, completionSource));
            TryProcessNextQueuedTransition();
            return completionSource.AsValueTask();
        }

        private async ValueTask ExecuteTransitionInternal<TContext>(
            Func<TContext, IProgress<float>, ValueTask> transition,
            IState targetState,
            TContext context,
            bool shouldInvokeTransition
        )
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
                if (shouldInvokeTransition)
                {
                    OnTransitionStart?.Invoke(transitionStartState, targetState);
                }
                _masterProgress.Report(0f);
                await transition(context, _masterProgress);
                _masterProgress.Report(1f);
                if (shouldInvokeTransition)
                {
                    OnTransitionComplete?.Invoke(transitionStartState, CurrentState);
                }
            }
            catch (Exception exception)
            {
                _masterProgress.Report(1f);
                OnTransitionFaulted?.Invoke(transitionStartState, targetState, exception);
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

        private static async ValueTask ExecuteStateOperationAsync(
            Func<ValueTask> operation,
            StateTransitionPhase phase,
            IState state
        )
        {
            try
            {
                await operation();
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

        private void TryProcessNextQueuedTransition()
        {
            if (_isTransitioning)
            {
                return;
            }

            if (_transitionQueue.Count == 0)
            {
                _transitionWaiter?.SetResult();
                _transitionWaiter = null;
                return;
            }

            QueuedTransition queuedTransition = _transitionQueue.Dequeue();
            _ = RunQueuedTransitionAsync(queuedTransition);
        }

        private async Task RunQueuedTransitionAsync(QueuedTransition queuedTransition)
        {
            try
            {
                await queuedTransition.Execute();
                TransitionCompletionSource completionSource = queuedTransition.CompletionSource;
                completionSource?.SetResult();
            }
            catch (Exception exception)
            {
                TransitionCompletionSource completionSource = queuedTransition.CompletionSource;
                completionSource?.SetException(exception);
            }
        }

        private readonly struct QueuedTransition
        {
            public QueuedTransition(
                Func<ValueTask> execute,
                TransitionCompletionSource completionSource
            )
            {
                Execute = execute;
                CompletionSource = completionSource;
            }

            public Func<ValueTask> Execute { get; }

            public TransitionCompletionSource CompletionSource { get; }
        }
    }
}
