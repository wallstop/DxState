namespace WallstopStudios.DxState.State.Stack.States.Addressables
{
    using System;
    using System.Threading.Tasks;

    public interface IAddressablesAdapter
    {
        ValueTask<IAddressableHandle<TAsset>> LoadAssetAsync<TAsset>(
            string key,
            IProgress<float> progress
        )
            where TAsset : class;

        ValueTask ReleaseAsync<TAsset>(IAddressableHandle<TAsset> handle)
            where TAsset : class;
    }

    public interface IAddressableHandle<out TAsset>
        where TAsset : class
    {
        TAsset Asset { get; }

        object RawHandle { get; }
    }
}

