namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    [Serializable]
    public sealed class ConditionState : IState
    {
        [SerializeField]
        private string _name;

        private readonly Func<bool> _predicate;
        private readonly Func<float> _progressEvaluator;

        public ConditionState(
            string name,
            Func<bool> predicate,
            Func<float> progressEvaluator = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name must be provided", nameof(name));
            }

            _name = name;
            _predicate = predicate ?? throw new ArgumentNullException(nameof(predicate));
            _progressEvaluator = progressEvaluator;
        }

        public string Name => _name;

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
            if (_predicate())
            {
                progress.Report(1f);
                return;
            }

            while (!_predicate())
            {
                await Task.Yield();
                if (_progressEvaluator != null)
                {
                    float evaluated = _progressEvaluator();
                    progress.Report(Math.Clamp(evaluated, 0f, 1f));
                }
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
            progress.Report(1f);
            return default;
        }
    }
}
