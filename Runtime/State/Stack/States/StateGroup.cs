namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Linq;
    using System.Threading.Tasks;
    using UnityEngine;

    public enum StateGroupMode
    {
        [Obsolete("Please use a valid value")]
        None = 0,
        Sequential = 1,
        Parallel = 2,
    }

    public sealed class StateGroup : IState
    {
        public string Name { get; set; }
        public TickMode TickMode { get; set; }
        public bool TickWhenInactive { get; set; }

        public float? TimeInState => 0 <= _groupEnterTime ? Time.time - _groupEnterTime : null;

        private readonly IState[] _childStates;
        private readonly StateGroupMode _mode;
        private readonly IDictionary<
            string,
            Func<IState, StateDirection, ValueTask>
        > _onChildEnterFinishedCallbacks;
        private readonly IDictionary<
            string,
            Func<IState, StateDirection, ValueTask>
        > _onChildExitFinishedCallbacks;

        private float _groupEnterTime = -1;
        private int _currentSequentialChildIndex = -1;

        public StateGroup(
            string name,
            IEnumerable<IState> childStates,
            StateGroupMode mode,
            TickMode groupTickMode = TickMode.None,
            bool groupTickWhenInactive = false,
            IDictionary<
                string,
                Func<IState, StateDirection, ValueTask>
            > onChildEnterFinishedCallbacks = null,
            IDictionary<
                string,
                Func<IState, StateDirection, ValueTask>
            > onChildExitFinishedCallbacks = null
        )
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            _childStates = childStates?.ToArray() ?? Array.Empty<IState>();
            _mode = mode;
            TickMode = groupTickMode;
            TickWhenInactive = groupTickWhenInactive;

#pragma warning disable CS0618 // Type or member is obsolete
            if (_mode == StateGroupMode.None)
#pragma warning restore CS0618 // Type or member is obsolete
            {
                throw new ArgumentException(
                    "StateGroupMode.None is obsolete and not a valid mode.",
                    nameof(mode)
                );
            }

            if (_childStates.Length == 0 && (Debug.isDebugBuild || Application.isEditor))
            {
                Debug.LogWarning(
                    $"StateGroup '{Name}' initialized in mode '{_mode}' with no child states. It will complete operations immediately."
                );
            }

            _onChildEnterFinishedCallbacks =
                onChildEnterFinishedCallbacks
                ?? new Dictionary<string, Func<IState, StateDirection, ValueTask>>();
            _onChildExitFinishedCallbacks =
                onChildExitFinishedCallbacks
                ?? new Dictionary<string, Func<IState, StateDirection, ValueTask>>();
        }

        public async ValueTask Enter<TProgress>(
            IState previousStateOfGroup,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _groupEnterTime = Time.time;
            _currentSequentialChildIndex = -1;

            if (_childStates.Length == 0)
            {
                progress.Report(1f);
                return;
            }

            switch (_mode)
            {
                case StateGroupMode.Sequential:
                {
                    IState previousChildInSequence = previousStateOfGroup;
                    for (int i = 0; i < _childStates.Length; ++i)
                    {
                        _currentSequentialChildIndex = i;
                        IState child = _childStates[i];
                        ScopedProgress childScopedProgress = new(
                            progress,
                            (float)i / _childStates.Length,
                            1.0f / _childStates.Length
                        );

                        await child.Enter(previousChildInSequence, childScopedProgress, direction);
                        if (
                            _onChildEnterFinishedCallbacks.TryGetValue(
                                child.Name,
                                out Func<IState, StateDirection, ValueTask> callback
                            )
                        )
                        {
                            await callback(child, direction);
                        }

                        previousChildInSequence = child;
                    }

                    break;
                }
                case StateGroupMode.Parallel:
                {
                    float[] childProgressValues = new float[_childStates.Length];
                    List<Task> enterTasks = new(_childStates.Length);

                    for (int i = 0; i < _childStates.Length; ++i)
                    {
                        IState child = _childStates[i];
                        int capturedChildIndex = i;

                        Progress<float> childSpecificProgress = new(pValue =>
                        {
                            lock (childProgressValues)
                            {
                                childProgressValues[capturedChildIndex] = Mathf.Clamp01(pValue);
                                progress.Report(
                                    childProgressValues.Sum() / childProgressValues.Length
                                );
                            }
                        });

                        enterTasks.Add(ExecuteAndCallback());
                        continue;

                        async Task ExecuteAndCallback()
                        {
                            await child.Enter(
                                previousStateOfGroup,
                                childSpecificProgress,
                                direction
                            );
                            if (
                                _onChildEnterFinishedCallbacks.TryGetValue(
                                    child.Name,
                                    out Func<IState, StateDirection, ValueTask> callback
                                )
                            )
                            {
                                await callback(child, direction);
                            }
                        }
                    }

                    await Task.WhenAll(enterTasks);
                    break;
                }
                default:
                {
                    throw new InvalidEnumArgumentException(
                        nameof(_mode),
                        (int)_mode,
                        typeof(StateGroupMode)
                    );
                }
            }
            progress.Report(1f);
        }

        public async ValueTask Exit<TProgress>(
            IState nextStateOfGroup,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (_groupEnterTime < 0)
            {
                progress.Report(1f);
                return;
            }

            _groupEnterTime = -1;
            if (_childStates.Length == 0)
            {
                progress.Report(1f);
                return;
            }

            switch (_mode)
            {
                case StateGroupMode.Sequential:
                {
                    IState nextChildInSequence = nextStateOfGroup;
                    for (int i = _childStates.Length - 1; i >= 0; i--)
                    {
                        _currentSequentialChildIndex = i;
                        IState child = _childStates[i];
                        float progressOffset =
                            (float)(_childStates.Length - 1 - i) / _childStates.Length;
                        ScopedProgress childScopedProgress = new(
                            progress,
                            progressOffset,
                            1.0f / _childStates.Length
                        );

                        await child.Exit(nextChildInSequence, childScopedProgress, direction);
                        if (
                            _onChildExitFinishedCallbacks.TryGetValue(
                                child.Name,
                                out Func<IState, StateDirection, ValueTask> callback
                            )
                        )
                        {
                            await callback(child, direction);
                        }

                        nextChildInSequence = child;
                    }

                    _currentSequentialChildIndex = -1;
                    break;
                }
                case StateGroupMode.Parallel:
                {
                    float[] childProgressValues = new float[_childStates.Length];
                    List<Task> exitTasks = new(_childStates.Length);

                    for (int i = 0; i < _childStates.Length; ++i)
                    {
                        IState child = _childStates[i];
                        int capturedChildIndex = i;

                        Progress<float> childSpecificProgress = new(pValue =>
                        {
                            lock (childProgressValues)
                            {
                                childProgressValues[capturedChildIndex] = Mathf.Clamp01(pValue);
                                progress.Report(
                                    childProgressValues.Sum() / childProgressValues.Length
                                );
                            }
                        });

                        exitTasks.Add(ExecuteAndCallback());
                        continue;

                        async Task ExecuteAndCallback()
                        {
                            await child.Exit(nextStateOfGroup, childSpecificProgress, direction);
                            if (
                                _onChildExitFinishedCallbacks.TryGetValue(
                                    child.Name,
                                    out Func<IState, StateDirection, ValueTask> callback
                                )
                            )
                            {
                                await callback(child, direction);
                            }
                        }
                    }

                    await Task.WhenAll(exitTasks);
                    break;
                }
                default:
                {
                    throw new InvalidEnumArgumentException(
                        nameof(_mode),
                        (int)_mode,
                        typeof(StateGroupMode)
                    );
                }
            }
            progress.Report(1f);
        }

        public void Tick(TickMode mode, float delta)
        {
            if (0 <= _groupEnterTime && !TickWhenInactive)
            {
                return;
            }

            if ((TickMode & mode) != mode && mode != TickMode.None)
            {
                return;
            }

            switch (_mode)
            {
                case StateGroupMode.Sequential:
                {
                    if (
                        _currentSequentialChildIndex >= 0
                        && _currentSequentialChildIndex < _childStates.Length
                    )
                    {
                        IState activeChild = _childStates[_currentSequentialChildIndex];

                        if ((activeChild.TickMode & mode) == mode && mode != TickMode.None)
                        {
                            activeChild.Tick(mode, delta);
                        }
                    }

                    break;
                }
                case StateGroupMode.Parallel:
                {
                    foreach (IState child in _childStates)
                    {
                        if ((child.TickMode & mode) == mode && mode != TickMode.None)
                        {
                            child.Tick(mode, delta);
                        }
                    }

                    break;
                }
                default:
                {
                    throw new InvalidEnumArgumentException(
                        nameof(_mode),
                        (int)_mode,
                        typeof(StateGroupMode)
                    );
                }
            }
        }

        public async ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            float groupEnterTime = _groupEnterTime;
            _groupEnterTime = -1;
            if (groupEnterTime < 0 && Array.TrueForAll(_childStates, c => c.TimeInState == null))
            {
                progress.Report(1f);
                return;
            }

            if (_childStates.Length == 0)
            {
                progress.Report(1f);
                return;
            }

            float[] childProgressValues = new float[_childStates.Length];
            List<Task> removeTasks = new(_childStates.Length);

            for (int i = 0; i < _childStates.Length; ++i)
            {
                IState child = _childStates[i];
                int capturedChildIndex = i;

                Progress<float> childSpecificProgress = new(pValue =>
                {
                    lock (childProgressValues)
                    {
                        childProgressValues[capturedChildIndex] = Mathf.Clamp01(pValue);
                        progress.Report(childProgressValues.Sum() / childProgressValues.Length);
                    }
                });

                Task childTask = child
                    .Remove(previousStatesInStack, nextStatesInStack, childSpecificProgress)
                    .AsTask();
                switch (_mode)
                {
                    case StateGroupMode.Sequential:
                    {
                        await childTask;
                        break;
                    }
                    case StateGroupMode.Parallel:
                    {
                        removeTasks.Add(childTask);
                        break;
                    }
                    default:
                    {
                        throw new InvalidEnumArgumentException(
                            nameof(_mode),
                            (int)_mode,
                            typeof(StateGroupMode)
                        );
                    }
                }
            }
            await Task.WhenAll(removeTasks);
            progress.Report(1f);
            _currentSequentialChildIndex = -1;
        }
    }
}
