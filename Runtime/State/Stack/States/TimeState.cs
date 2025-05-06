namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Threading.Tasks;
    using UnityEngine;

    public sealed class TimeState : IState
    {
        public string Name { get; set; } = $"Time State - {Guid.NewGuid()}";

        public float TimeScale { get; set; } = 1f;

        public float? TimeInState => 0 <= _timeEntered ? Time.time - _timeEntered : null;

        private float _timeEntered = -1;

        private float _previousTimeScale;

        public TimeState() { }

        public TimeState(string name, float timeScale)
        {
            Name = name;
            TimeScale = timeScale;
        }

        public ValueTask Enter<TProgress>(IState previousState, TProgress progress)
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

        public ValueTask Exit<TProgress>(IState nextState, TProgress progress)
            where TProgress : IProgress<float>
        {
            _timeEntered = -1;
            return new ValueTask();
        }

        public ValueTask RevertFrom<TProgress>(IState previousState, TProgress progress)
            where TProgress : IProgress<float>
        {
            _timeEntered = Time.time;
            _previousTimeScale = Time.timeScale;
            Time.timeScale = _previousTimeScale;
            return new ValueTask();
        }
    }
}
