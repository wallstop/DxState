namespace WallstopStudios.DxState.State.Stack.States.Systems
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.Audio;

    [Serializable]
    public sealed class AudioSnapshotState : IState
    {
        [SerializeField]
        private string _name;

        [SerializeField]
        private bool _tickWhenInactive;

        [SerializeField]
        private float _transitionTime = 0.25f;

        [SerializeField]
        private AudioMixerSnapshot _snapshot;

        public AudioSnapshotState(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
        }

        public string Name => _name;

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => _tickWhenInactive;

        public float? TimeInState => null;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (_snapshot != null)
            {
                _snapshot.TransitionTo(Mathf.Max(0f, _transitionTime));
            }
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
