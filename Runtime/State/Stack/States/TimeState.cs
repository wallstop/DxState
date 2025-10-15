namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    [Serializable]
    public sealed class TimeState : IState
    {
        [field: SerializeField]
        public string Name { get; set; } = $"Time State - {Guid.NewGuid()}";

        [field: SerializeField]
        public float TimeScale { get; set; } = 1f;

        public float? TimeInState => 0 <= _timeEntered ? Time.time - _timeEntered : null;

        [SerializeField]
        private float _timeEntered = -1;

        private float _previousTimeScale = -1;

        public TimeState() { }

        public TimeState(string name, float timeScale)
        {
            Name = name;
            TimeScale = timeScale;
        }

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _timeEntered = Time.time;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = TimeScale;
            return new ValueTask();
        }

        public void Tick(TickMode mode, float delta)
        {
            // No-op
        }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            RestorePreviousTimeScale();
            _timeEntered = -1;
            return new ValueTask();
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            _timeEntered = -1;
            for (int i = 0; i < nextStatesInStack.Count; ++i)
            {
                if (nextStatesInStack[i] is TimeState futureTimeState)
                {
                    futureTimeState._previousTimeScale = _previousTimeScale;
                    _previousTimeScale = -1f;
                    return new ValueTask();
                }
            }

            RestorePreviousTimeScale();
            return new ValueTask();
        }

        private void RestorePreviousTimeScale()
        {
            if (_previousTimeScale < 0f)
            {
                return;
            }

            Time.timeScale = _previousTimeScale;
            _previousTimeScale = -1f;
        }
    }
}
