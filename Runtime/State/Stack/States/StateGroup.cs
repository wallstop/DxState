namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Buffers;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Pooling;

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
        private readonly object _parallelProgressGate;

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
            _childStates = CopyChildStates(childStates);
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
            _parallelProgressGate = new object();
        }

        private static IState[] CopyChildStates(IEnumerable<IState> source)
        {
            if (source == null)
            {
                return Array.Empty<IState>();
            }

            if (source is ICollection<IState> collection)
            {
                if (collection.Count == 0)
                {
                    return Array.Empty<IState>();
                }

                IState[] destination = new IState[collection.Count];
                collection.CopyTo(destination, 0);
                return destination;
            }

            List<IState> buffer = new List<IState>();
            foreach (IState child in source)
            {
                buffer.Add(child);
            }

            if (buffer.Count == 0)
            {
                return Array.Empty<IState>();
            }

            IState[] result = new IState[buffer.Count];
            buffer.CopyTo(result, 0);
            return result;
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
                    using (
                        ParallelProgressAggregator aggregator = new ParallelProgressAggregator(
                            _childStates.Length,
                            progress,
                            _parallelProgressGate
                        )
                    )
                    using (
                        PooledArray<ValueTask> pooledOperations = PooledArray<ValueTask>.Rent(
                            _childStates.Length
                        )
                    )
                    {
                        ValueTask[] operations = pooledOperations.Array;
                        int scheduledCount = 0;
                        for (int i = 0; i < _childStates.Length; ++i)
                        {
                            IState child = _childStates[i];
                            ParallelProgressAggregator.ProgressReporter reporter =
                                aggregator.CreateReporter(i);

                            operations[scheduledCount] = ExecuteAndCallback(child, reporter);
                            scheduledCount++;
                        }

                        await AwaitAll(operations, scheduledCount);
                        for (int i = 0; i < scheduledCount; i++)
                        {
                            operations[i] = default;
                        }
                    }

                    async ValueTask ExecuteAndCallback(
                        IState child,
                        ParallelProgressAggregator.ProgressReporter reporter
                    )
                    {
                        await child.Enter(previousStateOfGroup, reporter, direction);
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
                    using (
                        ParallelProgressAggregator aggregator = new ParallelProgressAggregator(
                            _childStates.Length,
                            progress,
                            _parallelProgressGate
                        )
                    )
                    using (
                        PooledArray<ValueTask> pooledOperations = PooledArray<ValueTask>.Rent(
                            _childStates.Length
                        )
                    )
                    {
                        ValueTask[] operations = pooledOperations.Array;
                        int scheduledCount = 0;
                        for (int i = 0; i < _childStates.Length; ++i)
                        {
                            IState child = _childStates[i];
                            ParallelProgressAggregator.ProgressReporter reporter =
                                aggregator.CreateReporter(i);

                            operations[scheduledCount] = ExecuteAndCallback(child, reporter);
                            scheduledCount++;
                        }

                        await AwaitAll(operations, scheduledCount);
                        for (int i = 0; i < scheduledCount; i++)
                        {
                            operations[i] = default;
                        }
                    }

                    async ValueTask ExecuteAndCallback(
                        IState child,
                        ParallelProgressAggregator.ProgressReporter reporter
                    )
                    {
                        await child.Exit(nextStateOfGroup, reporter, direction);
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

            switch (_mode)
            {
                case StateGroupMode.Sequential:
                {
                    for (int i = 0; i < _childStates.Length; ++i)
                    {
                        IState child = _childStates[i];
                        ScopedProgress childProgress = new(
                            progress,
                            i / (float)_childStates.Length,
                            1f / _childStates.Length
                        );

                        await child.Remove(previousStatesInStack, nextStatesInStack, childProgress);
                    }

                    progress.Report(1f);
                    _currentSequentialChildIndex = -1;
                    break;
                }
                case StateGroupMode.Parallel:
                {
                    using (
                        ParallelProgressAggregator aggregator = new ParallelProgressAggregator(
                            _childStates.Length,
                            progress,
                            _parallelProgressGate
                        )
                    )
                    using (
                        PooledArray<ValueTask> pooledOperations = PooledArray<ValueTask>.Rent(
                            _childStates.Length
                        )
                    )
                    {
                        ValueTask[] operations = pooledOperations.Array;
                        int scheduledCount = 0;
                        for (int i = 0; i < _childStates.Length; ++i)
                        {
                            IState child = _childStates[i];
                            ParallelProgressAggregator.ProgressReporter reporter =
                                aggregator.CreateReporter(i);

                            operations[scheduledCount] = child.Remove(
                                previousStatesInStack,
                                nextStatesInStack,
                                reporter
                            );
                            scheduledCount++;
                        }

                        await AwaitAll(operations, scheduledCount);
                        for (int i = 0; i < scheduledCount; i++)
                        {
                            operations[i] = default;
                        }
                    }

                    progress.Report(1f);
                    _currentSequentialChildIndex = -1;
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

        private static async ValueTask AwaitAll(ValueTask[] operations, int count)
        {
            for (int i = 0; i < count; i++)
            {
                await operations[i];
            }
        }

        private sealed class ParallelProgressAggregator : IDisposable
        {
            private readonly float[] _values;
            private readonly IProgress<float> _overallProgress;
            private readonly object _gate;
            private readonly int _length;
            private float _sum;

            public ParallelProgressAggregator(
                int length,
                IProgress<float> overallProgress,
                object gate
            )
            {
                _values = ArrayPool<float>.Shared.Rent(length);
                _overallProgress = overallProgress;
                _gate = gate ?? new object();
                _length = length;
                _sum = 0f;

                for (int i = 0; i < length; i++)
                {
                    _values[i] = 0f;
                }
            }

            public ProgressReporter CreateReporter(int index)
            {
                return new ProgressReporter(this, index);
            }

            public void Dispose()
            {
                ArrayPool<float>.Shared.Return(_values, true);
            }

            private void Report(int index, float value)
            {
                float clamped = Mathf.Clamp01(value);
                lock (_gate)
                {
                    float previous = _values[index];
                    _values[index] = clamped;
                    _sum += clamped - previous;
                    _overallProgress?.Report(_sum / _length);
                }
            }

            internal readonly struct ProgressReporter : IProgress<float>
            {
                private readonly ParallelProgressAggregator _aggregator;
                private readonly int _index;

                public ProgressReporter(ParallelProgressAggregator aggregator, int index)
                {
                    _aggregator = aggregator;
                    _index = index;
                }

                public void Report(float value)
                {
                    _aggregator.Report(_index, value);
                }
            }
        }
    }
}
