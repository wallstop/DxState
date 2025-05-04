namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityHelpers.Core.Extension;

    public sealed class StateStack
    {
        public IState CurrentState => 0 < _stack.Count ? _stack[^1] : null;

        private IState PreviousState => 1 < _stack.Count ? _stack[^2] : null;

        public event Action<IState, IState> StatePushed;
        public event Action<IState, IState> StatePopped;
        public event Action<IState, IState> TransitionStart;
        public event Action<IState, IState> TransitionComplete;
        public event Action<IState> Flattened;
        public event Action<List<IState>, IState> HistoryRemoved;

        private readonly List<IState> _stack = new();
        private readonly Dictionary<string, IState> _statesByName = new(StringComparer.Ordinal);

        private bool _isTransitioning;

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

            await PerformTransition(
                async () =>
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
                    StatePushed?.Invoke(previousState, newState);
                },
                newState
            );
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

        public async ValueTask PopAsync()
        {
            if (_stack.Count == 0)
            {
                throw new InvalidOperationException("Cannot pop from an empty stack.");
            }

            IState nextState = PreviousState;
            await PerformTransition(
                async () =>
                {
                    IState stateToPop = CurrentState;
                    await stateToPop.Exit(nextState);
                    _stack.RemoveAt(_stack.Count - 1);
                    StatePopped?.Invoke(stateToPop, nextState);
                    if (nextState != null)
                    {
                        await nextState.Enter(stateToPop);
                    }
                },
                nextState
            );
        }

        public async ValueTask FlattenAsync(IState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            _ = TryRegister(state);
            await PerformTransition(
                async () =>
                {
                    bool targetWasAlreadyActive = CurrentState == state && _stack.Count == 1;
                    while (_stack.Count > 0)
                    {
                        IState stateToExit = CurrentState;
                        if (stateToExit == state && _stack.Count == 1)
                        {
                            break;
                        }

                        await stateToExit.Exit(PreviousState);
                        _stack.RemoveAt(_stack.Count - 1);
                    }

                    if (_stack.Count == 0)
                    {
                        _stack.Add(state);
                        if (!targetWasAlreadyActive)
                        {
                            await state.Enter(PreviousState);
                        }
                    }
                },
                state
            );
            Flattened?.Invoke(state);
        }

        public async ValueTask FlattenAsync(string stateName)
        {
            if (!_statesByName.TryGetValue(stateName, out IState state))
            {
                throw new ArgumentException($"State with name {stateName} does not exist");
            }
            await FlattenAsync(state);
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

            if (targetIndex < 0)
            {
                throw new ArgumentException(
                    $"State '{state.Name}' not found in the stack.",
                    nameof(state)
                );
            }

            if (targetIndex != 0)
            {
                List<IState> removed = _stack.GetRange(0, targetIndex);
                _stack.RemoveRange(0, targetIndex);
                HistoryRemoved?.Invoke(removed, state);
            }
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
            await PerformTransition(
                async () =>
                {
                    while (_stack.Count > 0)
                    {
                        IState stateToExit = CurrentState;
                        IState nextState = PreviousState;
                        await stateToExit.Exit(nextState);
                        _stack.RemoveAt(_stack.Count - 1);
                        StatePopped?.Invoke(stateToExit, nextState);
                    }
                },
                null
            );
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

        private async ValueTask PerformTransition(
            Func<ValueTask> transitionAction,
            IState targetState
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
                TransitionStart?.Invoke(current, targetState);
                await transitionAction();
                TransitionComplete?.Invoke(current, CurrentState);
            }
            finally
            {
                _isTransitioning = false;
            }
        }
    }
}
