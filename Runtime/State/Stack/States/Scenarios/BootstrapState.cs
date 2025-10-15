namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public sealed class BootstrapState : IState
    {
        private readonly IReadOnlyList<Func<IProgress<float>, ValueTask>> _steps;

        public BootstrapState(
            string name,
            IReadOnlyList<Func<IProgress<float>, ValueTask>> steps
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("BootstrapState requires a name.", nameof(name));
            }

            if (steps == null || steps.Count == 0)
            {
                throw new ArgumentException("At least one bootstrap step must be provided.", nameof(steps));
            }

            Name = name;
            _steps = steps;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => null;

        public async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            int stepCount = _steps.Count;
            for (int i = 0; i < stepCount; i++)
            {
                Func<IProgress<float>, ValueTask> step = _steps[i];
                if (step == null)
                {
                    continue;
                }

                ScopedProgress stepProgress = new ScopedProgress(
                    progress,
                    (float)i / stepCount,
                    1f / stepCount
                );
                await step(stepProgress).ConfigureAwait(false);
            }

            UnityExtensions.ReportProgress(progress, 1f);
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
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        private readonly struct ScopedProgress : IProgress<float>
        {
            private readonly IProgress<float> _inner;
            private readonly float _offset;
            private readonly float _scale;

            public ScopedProgress(IProgress<float> inner, float offset, float scale)
            {
                _inner = inner;
                _offset = offset;
                _scale = scale;
            }

            public void Report(float value)
            {
                IProgress<float> reporter = _inner;
                if (reporter == null)
                {
                    return;
                }

                float clamped = Mathf.Clamp01(value);
                reporter.Report(_offset + (clamped * _scale));
            }
        }
    }
}
