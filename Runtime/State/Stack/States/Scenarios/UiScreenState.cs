namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public sealed class UiScreenState : IState
    {
        private readonly GameObject[] _objects;
        private readonly CanvasGroup[] _canvasGroups;
        private readonly Action<bool> _onToggle;

        public UiScreenState(
            string name,
            IEnumerable<GameObject> objects,
            IEnumerable<CanvasGroup> canvasGroups = null,
            Action<bool> onToggle = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("UiScreenState requires a name.", nameof(name));
            }

            Name = name;
            _objects = objects != null ? ToArray(objects) : Array.Empty<GameObject>();
            _canvasGroups = canvasGroups != null ? ToArray(canvasGroups) : Array.Empty<CanvasGroup>();
            _onToggle = onToggle;
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
            SetVisible(true);
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
            SetVisible(false);
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
            SetVisible(false);
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < _objects.Length; i++)
            {
                GameObject target = _objects[i];
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

            _onToggle?.Invoke(visible);
        }

        private static TElement[] ToArray<TElement>(IEnumerable<TElement> source)
        {
            if (source is TElement[] array)
            {
                return array;
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

