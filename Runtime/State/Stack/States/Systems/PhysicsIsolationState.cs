namespace WallstopStudios.DxState.State.Stack.States.Systems
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;

    [Serializable]
    public sealed class PhysicsIsolationState : IState
    {
        [Serializable]
        public struct LayerPair
        {
            public int LayerA;
            public int LayerB;
            public bool IgnoreCollisions;
        }

        [SerializeField]
        private string _name;

        [SerializeField]
        private LayerPair[] _layerPairs;

        private bool[] _previousIgnoreStates;

        public PhysicsIsolationState(string name, LayerPair[] layerPairs)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name must be provided", nameof(name));
            }

            _name = name;
            _layerPairs = layerPairs ?? Array.Empty<LayerPair>();
            _previousIgnoreStates = Array.Empty<bool>();
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
            Apply(ignore: true);
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
            Restore();
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
            Restore();
            progress.Report(1f);
            return default;
        }

        private void Apply(bool ignore)
        {
            LayerPair[] pairs = _layerPairs;
            if (pairs == null || pairs.Length == 0)
            {
                _previousIgnoreStates = Array.Empty<bool>();
                return;
            }

            bool[] previousStates = new bool[pairs.Length];
            for (int i = 0; i < pairs.Length; i++)
            {
                LayerPair pair = pairs[i];
                previousStates[i] = Physics.GetIgnoreLayerCollision(pair.LayerA, pair.LayerB);
                bool desired = ignore ? pair.IgnoreCollisions : previousStates[i];
                Physics.IgnoreLayerCollision(pair.LayerA, pair.LayerB, desired);
            }

            _previousIgnoreStates = previousStates;
        }

        private void Restore()
        {
            LayerPair[] pairs = _layerPairs;
            bool[] previousStates = _previousIgnoreStates;
            if (pairs == null || previousStates == null)
            {
                return;
            }

            int count = Math.Min(pairs.Length, previousStates.Length);
            for (int i = 0; i < count; i++)
            {
                LayerPair pair = pairs[i];
                bool previous = previousStates[i];
                Physics.IgnoreLayerCollision(pair.LayerA, pair.LayerB, previous);
            }
        }
    }
}
