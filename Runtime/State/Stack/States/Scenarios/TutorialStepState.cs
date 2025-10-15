namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public sealed class TutorialStepState : IState
    {
        private readonly Func<bool> _completionPredicate;
        private readonly Func<float> _progressProvider;
        private readonly Action _onEnter;
        private readonly Action _onExit;
        private readonly float _pollInterval;

        private float _timeEntered = -1f;

        public TutorialStepState(
            string name,
            Func<bool> completionPredicate,
            Func<float> progressProvider = null,
            Action onEnter = null,
            Action onExit = null,
            float pollIntervalSeconds = 0f
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("TutorialStepState requires a name.", nameof(name));
            }

            _completionPredicate = completionPredicate ?? throw new ArgumentNullException(nameof(completionPredicate));
            _progressProvider = progressProvider;
            _onEnter = onEnter;
            _onExit = onExit;
            _pollInterval = Mathf.Max(0f, pollIntervalSeconds);
            Name = name;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => _timeEntered >= 0 ? Time.time - _timeEntered : null;

        public async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _timeEntered = Time.time;
            _onEnter?.Invoke();

            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            float pollTimer = 0f;
            while (!_completionPredicate())
            {
                if (_progressProvider != null)
                {
                    UnityExtensions.ReportProgress(progress, Mathf.Clamp01(_progressProvider()));
                }

                if (_pollInterval > 0f)
                {
                    await Task.Delay(TimeSpan.FromSeconds(_pollInterval));
                }
                else
                {
                    await Task.Yield();
                }

                pollTimer += _pollInterval > 0f ? _pollInterval : Time.deltaTime;
                if (pollTimer > 10f)
                {
                    pollTimer = 0f;
                }
            }

            UnityExtensions.ReportProgress(progress, 1f);
        }

        public void Tick(TickMode mode, float delta) { }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _timeEntered = -1f;
            _onExit?.Invoke();
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
            _onExit?.Invoke();
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }
    }
}
