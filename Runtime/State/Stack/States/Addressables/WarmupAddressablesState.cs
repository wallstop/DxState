namespace WallstopStudios.DxState.State.Stack.States.Addressables
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WallstopStudios.DxState.Extensions;

    public sealed class WarmupAddressablesState : IState
    {
        private readonly string[] _keys;
        private readonly IAddressablesAdapter _adapter;

        public WarmupAddressablesState(string name, IReadOnlyList<string> keys, IAddressablesAdapter adapter)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Warmup state requires a name.", nameof(name));
            }

            if (keys == null || keys.Count == 0)
            {
                throw new ArgumentException("At least one addressable key must be specified.", nameof(keys));
            }

            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            Name = name;
            _adapter = adapter;
            _keys = new string[keys.Count];
            for (int i = 0; i < keys.Count; i++)
            {
                _keys[i] = keys[i];
            }
        }

        public string Name { get; }

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
            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            int totalKeys = _keys.Length;
            for (int i = 0; i < totalKeys; i++)
            {
                string key = _keys[i];
                IAddressableHandle<object> handle = await _adapter
                    .LoadAssetAsync<object>(key, progress)
                    .ConfigureAwait(false);
                await _adapter.ReleaseAsync<object>(handle).ConfigureAwait(false);
                float normalizedProgress = (i + 1f) / totalKeys;
                UnityExtensions.ReportProgress(progress, normalizedProgress);
            }
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
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }
    }
}
