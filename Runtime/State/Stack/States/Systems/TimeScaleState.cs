namespace WallstopStudios.DxState.State.Stack.States.Systems
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    [Serializable]
    public sealed class TimeScaleState : IState
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        private float _timeScale = 1f;

        [SerializeField]
        private bool _revertOnExit = true;

        private float _previousTimeScale;

        public TimeScaleState(string name, float timeScale)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _timeScale = timeScale;
        }

        public string Name => _name;

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => null;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _previousTimeScale = Time.timeScale;
            Time.timeScale = Mathf.Max(0f, _timeScale);
            progress.Report(1f);
            return default;
        }

        public void Tick(TickMode mode, float delta) { }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (_revertOnExit)
            {
                Time.timeScale = _previousTimeScale;
            }

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
            if (_revertOnExit)
            {
                Time.timeScale = _previousTimeScale;
            }

            progress.Report(1f);
            return default;
        }
    }
}
