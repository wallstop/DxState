namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WallstopStudios.DxState.Extensions;

    internal sealed class FunctionalState : IState
    {
        private readonly Func<StateDirection, IState, IProgress<float>, ValueTask> _onEnter;
        private readonly Func<StateDirection, IState, IProgress<float>, ValueTask> _onExit;
        private readonly Func<
            IReadOnlyList<IState>,
            IReadOnlyList<IState>,
            IProgress<float>,
            ValueTask
        > _onRemove;

        public FunctionalState(
            string name,
            Func<StateDirection, IState, IProgress<float>, ValueTask> onEnter,
            Func<StateDirection, IState, IProgress<float>, ValueTask> onExit,
            Func<
                IReadOnlyList<IState>,
                IReadOnlyList<IState>,
                IProgress<float>,
                ValueTask
            > onRemove,
            TickMode tickMode = TickMode.None,
            bool tickWhenInactive = false
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("FunctionalState requires a name.", nameof(name));
            }

            Name = name;
            _onEnter = onEnter;
            _onExit = onExit;
            _onRemove = onRemove;
            TickMode = tickMode;
            TickWhenInactive = tickWhenInactive;
        }

        public string Name { get; }

        public TickMode TickMode { get; }

        public bool TickWhenInactive { get; }

        public float? TimeInState => null;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (_onEnter != null)
            {
                return _onEnter(direction, previousState, progress);
            }

            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        public void Tick(TickMode mode, float delta)
        {
        }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (_onExit != null)
            {
                return _onExit(direction, nextState, progress);
            }

            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            if (_onRemove != null)
            {
                return _onRemove(previousStatesInStack, nextStatesInStack, progress);
            }

            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }
    }
}

