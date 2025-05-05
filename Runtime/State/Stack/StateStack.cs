// ReSharper disable ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityHelpers.Core.Extension;

    public sealed class StateStack
    {
        public bool IsTransitioning => _isTransitioning;

        // ReSharper disable once MemberCanBePrivate.Global
        public IState CurrentState => 0 < _stack.Count ? _stack[^1] : null;

        // ReSharper disable once MemberCanBePrivate.Global
        public IState PreviousState => 1 < _stack.Count ? _stack[^2] : null;

        public IReadOnlyDictionary<string, IState> RegisteredStates => _statesByName;
        public IReadOnlyList<IState> Stack => _stack;

        public event Action<IState, IState> OnStatePushed;
        public event Action<IState, IState> OnStatePopped;
        public event Action<IState, IState> OnTransitionStart;
        public event Action<IState, IState> OnTransitionComplete;
        public event Action<IState> OnFlattened;
        public event Action<List<IState>, IState> OnHistoryRemoved;

        private readonly List<IState> _stack = new();
        private readonly Dictionary<string, IState> _statesByName = new(StringComparer.Ordinal);
        private TaskCompletionSource<bool> _transitionWaiter;

        // Cached to avoid allocations
        private readonly Func<IState, ValueTask> _push;
        private readonly Func<IState, ValueTask> _pop;
        private readonly Func<IState, ValueTask> _flatten;
        private readonly Func<bool, ValueTask> _clear;

        private bool _isTransitioning;

        public StateStack()
        {
            _push = InternalPushAsync;
            _pop = InternalPopAsync;
            _flatten = InternalFlattenAsync;
            _clear = InternalClearAsync;
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
            if (!_isTransitioning)
            {
                return new ValueTask();
            }
            _transitionWaiter ??= new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            return new ValueTask(_transitionWaiter.Task);
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
            if (CurrentState == newState)
            {
                return;
            }

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

        private async ValueTask InternalPushAsync(IState newState)
        {
            IState previousState = CurrentState;
            if (previousState == newState)
            {
                return;
            }
            if (previousState != null)
            {
                await previousState.Exit(newState);
            }

            _stack.Add(newState);
            await newState.Enter(previousState);
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

        private async ValueTask InternalPopAsync(IState nextState)
        {
            IState stateToPop = CurrentState;
            await stateToPop.Exit(nextState);
            _stack.RemoveAt(_stack.Count - 1);
            OnStatePopped?.Invoke(stateToPop, nextState);
            if (nextState != null)
            {
                await nextState.RevertFrom(stateToPop);
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

        private async ValueTask InternalFlattenAsync(IState state)
        {
            bool targetWasAlreadyActive = CurrentState == state && _stack.Count == 1;
            while (_stack.Count > 0)
            {
                IState stateToExit = CurrentState;
                if (stateToExit == state && _stack.Count == 1)
                {
                    break;
                }

                IState previousState = PreviousState;
                await stateToExit.Exit(previousState);
                _stack.RemoveAt(_stack.Count - 1);
                if (previousState != null)
                {
                    await previousState.RevertFrom(stateToExit);
                }
            }

            if (_stack.Count == 0)
            {
                _stack.Add(state);
                if (!targetWasAlreadyActive)
                {
                    await state.Enter(PreviousState);
                }
            }
        }

        public void RemoveHistory(IState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (_isTransitioning)
            {
                throw new InvalidOperationException(
                    "Cannot remove history while state stack is transitioning."
                );
            }

            int targetIndex = -1;
            for (int i = 0; i < _stack.Count; ++i)
            {
                if (_stack[i] == state)
                {
                    targetIndex = i;
                    break;
                }
            }

            switch (targetIndex)
            {
                case < 0:
                    throw new ArgumentException(
                        $"State '{state.Name}' not found in the stack.",
                        nameof(state)
                    );
                case 0:
                    return;
            }

            List<IState> removed = _stack.GetRange(0, targetIndex);
            _stack.RemoveRange(0, targetIndex);
            OnHistoryRemoved?.Invoke(removed, state);
        }

        public void RemoveHistory(string stateName)
        {
            if (!_statesByName.TryGetValue(stateName, out IState state))
            {
                throw new ArgumentException(
                    $"State with name {stateName} does not exist",
                    nameof(stateName)
                );
            }
            RemoveHistory(state);
        }

        public async ValueTask ClearAsync()
        {
            await PerformTransition(_clear, null);
        }

        private async ValueTask InternalClearAsync(bool unused)
        {
            while (_stack.Count > 0)
            {
                IState stateToExit = CurrentState;
                IState nextState = PreviousState;
                await stateToExit.Exit(nextState);
                _stack.RemoveAt(_stack.Count - 1);
                OnStatePopped?.Invoke(stateToExit, nextState);
                if (nextState != null)
                {
                    await nextState.RevertFrom(stateToExit);
                }
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

            IState current = CurrentState;
            if (current == null)
            {
                return;
            }
            if (!current.TickMode.HasFlagNoAlloc(tickMode))
            {
                return;
            }
            current.Tick(tickMode, Time.fixedDeltaTime);
        }

        private async ValueTask PerformTransition<TContext>(
            Func<TContext, ValueTask> transitionAction,
            IState targetState,
            TContext context = default
        )
        {
            if (_isTransitioning)
            {
                throw new InvalidOperationException(
                    "Cannot perform transition while state stack is already transitioning"
                );
            }

            _isTransitioning = true;
            try
            {
                IState current = CurrentState;
                OnTransitionStart?.Invoke(current, targetState);
                await transitionAction(context);
                OnTransitionComplete?.Invoke(current, CurrentState);
            }
            finally
            {
                _isTransitioning = false;
                _transitionWaiter?.TrySetResult(true);
                _transitionWaiter = null;
            }
        }
    }
}
