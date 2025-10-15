namespace WallstopStudios.DxState.State.Stack.States.Addressables
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using WallstopStudios.DxState.Extensions;

    public sealed class AddressableAssetState<TAsset> : IState
        where TAsset : class
    {
        private readonly string _key;
        private readonly IAddressablesAdapter _adapter;
        private readonly bool _releaseOnExit;
        private readonly bool _releaseOnRemove;

        private IAddressableHandle<TAsset> _handle;

        public AddressableAssetState(
            string name,
            string key,
            IAddressablesAdapter adapter,
            bool releaseOnExit = false,
            bool releaseOnRemove = true
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("State name is required.", nameof(name));
            }

            if (string.IsNullOrWhiteSpace(key))
            {
                throw new ArgumentException("Addressable key must be provided.", nameof(key));
            }

            if (adapter == null)
            {
                throw new ArgumentNullException(nameof(adapter));
            }

            Name = name;
            _key = key;
            _adapter = adapter;
            _releaseOnExit = releaseOnExit;
            _releaseOnRemove = releaseOnRemove;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => null;

        public TAsset Asset => _handle != null ? _handle.Asset : null;

        public event Action<TAsset> AssetLoaded;

        public event Action<TAsset> AssetReleased;

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

            if (_handle != null)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            _handle = await _adapter.LoadAssetAsync<TAsset>(_key, progress).ConfigureAwait(false);
            if (_handle == null)
            {
                throw new InvalidOperationException(
                    $"Adapter returned null handle for key '{_key}'."
                );
            }

            AssetLoaded?.Invoke(_handle.Asset);
            UnityExtensions.ReportProgress(progress, 1f);
        }

        public void Tick(TickMode mode, float delta)
        {
        }

        public async ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (_releaseOnExit)
            {
                await ReleaseInternal(progress).ConfigureAwait(false);
            }
            else
            {
                UnityExtensions.ReportProgress(progress, 1f);
            }
        }

        public async ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            if (_releaseOnRemove)
            {
                await ReleaseInternal(progress).ConfigureAwait(false);
            }
            else
            {
                UnityExtensions.ReportProgress(progress, 1f);
            }
        }

        private async ValueTask ReleaseInternal<TProgress>(TProgress progress)
            where TProgress : IProgress<float>
        {
            if (_handle == null)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            TAsset releasedAsset = _handle.Asset;
            await _adapter.ReleaseAsync<TAsset>(_handle).ConfigureAwait(false);
            _handle = null;

            UnityExtensions.ReportProgress(progress, 1f);
            AssetReleased?.Invoke(releasedAsset);
        }
    }
}
