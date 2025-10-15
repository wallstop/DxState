namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    [Serializable]
    public sealed class WaitForSecondsState : IState
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        private float _duration;

        [SerializeField]
        private bool _useUnscaledTime = true;

        private float _startTime;

        public WaitForSecondsState(string name, float duration)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name must be provided", nameof(name));
            }

            if (duration < 0f)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(duration),
                    duration,
                    "Duration must be non-negative."
                );
            }

            _name = name;
            _duration = duration;
        }

        public string Name => _name;

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState
        {
            get
            {
                if (_startTime < 0f)
                {
                    return null;
                }

                float elapsed = (_useUnscaledTime ? Time.unscaledTime : Time.time) - _startTime;
                return Mathf.Max(0f, elapsed);
            }
        }

        public async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _startTime = _useUnscaledTime ? Time.unscaledTime : Time.time;
            if (_duration <= 0f)
            {
                progress.Report(1f);
                return;
            }

            float targetTime = _startTime + _duration;
            while ((_useUnscaledTime ? Time.unscaledTime : Time.time) < targetTime)
            {
                await Task.Yield();
                float elapsed = (_useUnscaledTime ? Time.unscaledTime : Time.time) - _startTime;
                float normalized = Mathf.Clamp01(elapsed / _duration);
                progress.Report(normalized);
            }

            progress.Report(1f);
        }

        public void Tick(TickMode mode, float delta) { }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _startTime = -1f;
            progress.Report(1f);
            return default;
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            _startTime = -1f;
            progress.Report(1f);
            return default;
        }
    }
}
