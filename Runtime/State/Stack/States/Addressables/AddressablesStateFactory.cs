namespace WallstopStudios.DxState.State.Stack.States.Addressables
{
    using System;

    public static class AddressablesStateFactory
    {
        public static AddressableAssetState<TAsset> LoadAsset<TAsset>(
            string name,
            string key,
            IAddressablesAdapter adapter,
            bool releaseOnExit = false,
            bool releaseOnRemove = true
        )
            where TAsset : class
        {
            return new AddressableAssetState<TAsset>(
                name,
                key,
                adapter,
                releaseOnExit,
                releaseOnRemove
            );
        }

        public static WarmupAddressablesState Warmup(
            string name,
            string[] keys,
            IAddressablesAdapter adapter
        )
        {
            return new WarmupAddressablesState(name, keys, adapter);
        }
    }
}

