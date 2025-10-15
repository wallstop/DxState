namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public sealed class LoadingScreenState : IState
    {
        private readonly GameObject[] _targets;
        private readonly CanvasGroup[] _canvasGroups;
        private readonly Action<float> _progressBinder;
        private readonly bool _disableOnExit;

        public LoadingScreenState(
            string name,
            IEnumerable<GameObject> targets,
            IEnumerable<CanvasGroup> canvasGroups,
            Action<float> progressBinder = null,
            bool disableOnExit = true
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Loading screen state requires a name.", nameof(name));
            }

            Name = name;
            _progressBinder = progressBinder;
            _disableOnExit = disableOnExit;
            _targets = targets != null ? ToArray(targets) : Array.Empty<GameObject>();
            _canvasGroups = canvasGroups != null ? ToArray(canvasGroups) : Array.Empty<CanvasGroup>();
        }

        public string Name { get; }

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
            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return default;
            }

            SetVisibility(true);
            _progressBinder?.Invoke(0f);
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
            if (_disableOnExit)
            {
                SetVisibility(false);
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
            SetVisibility(false);
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        public void UpdateProgress(float value)
        {
            _progressBinder?.Invoke(Mathf.Clamp01(value));
        }

        private void SetVisibility(bool visible)
        {
            for (int i = 0; i < _targets.Length; i++)
            {
                GameObject target = _targets[i];
                if (target == null)
                {
                    continue;
                }

                target.SetActive(visible);
            }

            for (int i = 0; i < _canvasGroups.Length; i++)
            {
                CanvasGroup group = _canvasGroups[i];
                if (group == null)
                {
                    continue;
                }

                group.alpha = visible ? 1f : 0f;
                group.blocksRaycasts = visible;
                group.interactable = visible;
            }
        }

        private static TElement[] ToArray<TElement>(IEnumerable<TElement> source)
        {
            if (source is TElement[] existing)
            {
                return existing;
            }

            List<TElement> collected = new List<TElement>();
            foreach (TElement element in source)
            {
                collected.Add(element);
            }

            return collected.ToArray();
        }
    }
}
