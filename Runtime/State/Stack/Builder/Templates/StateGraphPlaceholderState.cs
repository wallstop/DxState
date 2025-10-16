namespace WallstopStudios.DxState.State.Stack.Builder.Templates
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    [CreateAssetMenu(
        fileName = "StateGraphPlaceholderState",
        menuName = "Wallstop Studios/DxState/State Graph/Placeholder State",
        order = 0
    )]
    public sealed class StateGraphPlaceholderState : ScriptableObject, ICancellableState
    {
        public string Name => string.IsNullOrWhiteSpace(_displayName) ? name : _displayName;

        public TickMode TickMode => _tickMode;

        public bool TickWhenInactive => _tickWhenInactive;

        public float? TimeInState => _enteredTime >= 0f ? Time.time - _enteredTime : null;

        public StateGraphTemplate.StateNodeKind TemplateKind => _templateKind;

        public Color AccentColor => _accentColor;

        public string Notes => _notes;

        [SerializeField]
        private string _displayName = "Placeholder";

        [SerializeField]
        private TickMode _tickMode = TickMode.None;

        [SerializeField]
        private bool _tickWhenInactive;

        [SerializeField]
        private StateGraphTemplate.StateNodeKind _templateKind = StateGraphTemplate.StateNodeKind.Default;

        [SerializeField]
        private Color _accentColor = new Color(0.18f, 0.46f, 0.86f, 1f);

        [SerializeField]
        [TextArea]
        private string _notes;

        private float _enteredTime = -1f;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _enteredTime = Time.time;
            progress?.Report(1f);
            return default;
        }

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction,
            System.Threading.CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Enter(previousState, progress, direction);
        }

        public void Tick(TickMode mode, float delta)
        {
            // Placeholder states intentionally perform no per-frame work.
        }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            progress?.Report(1f);
            _enteredTime = -1f;
            return default;
        }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction,
            System.Threading.CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Exit(nextState, progress, direction);
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            progress?.Report(1f);
            _enteredTime = -1f;
            return default;
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress,
            System.Threading.CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Remove(previousStatesInStack, nextStatesInStack, progress);
        }

#if UNITY_EDITOR
        public void Configure(
            string displayName,
            StateGraphTemplate.StateNodeKind templateKind,
            TickMode tickMode,
            bool tickWhenInactive,
            Color accentColor,
            string notes
        )
        {
            _displayName = displayName;
            _templateKind = templateKind;
            _tickMode = tickMode;
            _tickWhenInactive = tickWhenInactive;
            _accentColor = accentColor;
            _notes = notes;
            if (string.IsNullOrWhiteSpace(name))
            {
                name = displayName;
            }
        }
#endif
    }
}
