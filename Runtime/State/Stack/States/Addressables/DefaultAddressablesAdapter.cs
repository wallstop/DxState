#if WALLSTOPSTUDIOS_DXSTATE_ADDRESSABLES
namespace WallstopStudios.DxState.State.Stack.States.Addressables
{
    using System;
    using System.Threading.Tasks;
    using UnityEngine.AddressableAssets;
    using UnityEngine.ResourceManagement.AsyncOperations;

    public sealed class DefaultAddressablesAdapter : IAddressablesAdapter
    {
        public async ValueTask<IAddressableHandle<TAsset>> LoadAssetAsync<TAsset>(
            string key,
            IProgress<float> progress
        )
            where TAsset : class
        {
            AsyncOperationHandle<TAsset> operation = Addressables.LoadAssetAsync<TAsset>(key);
            if (progress != null)
            {
                operation.Completed += _ => progress.Report(1f);
            }
            TAsset asset = await operation.Task.ConfigureAwait(false);
            return new AddressableHandle<TAsset>(operation, asset);
        }

        public ValueTask ReleaseAsync<TAsset>(IAddressableHandle<TAsset> handle)
            where TAsset : class
        {
            if (handle == null)
            {
                return default;
            }

            Addressables.Release(handle.RawHandle);
            return default;
        }

        private sealed class AddressableHandle<TAsset> : IAddressableHandle<TAsset>
            where TAsset : class
        {
            public AddressableHandle(
                AsyncOperationHandle<TAsset> operation,
                TAsset asset
            )
            {
                RawHandle = operation;
                Asset = asset;
            }

            public TAsset Asset { get; }

            public object RawHandle { get; }
        }
    }
}
#endif

