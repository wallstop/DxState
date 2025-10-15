namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Addressables;
    using WallstopStudios.DxState.Tests.EditMode.TestSupport;

    public sealed class AddressableAssetStateTests
    {
        [UnityTest]
        public IEnumerator EnterLoadsAssetAndExitReleasesWhenConfigured()
        {
            FakeAdapter adapter = new FakeAdapter();
            AddressableAssetState<string> state = new AddressableAssetState<string>(
                "TestAsset",
                "sample_key",
                adapter,
                releaseOnExit: true,
                releaseOnRemove: true
            );

            string loadedAsset = null;
            string releasedAsset = null;
            state.AssetLoaded += asset => loadedAsset = asset;
            state.AssetReleased += asset => releasedAsset = asset;

            ValueTask enterTask = state.Enter(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            Assert.AreEqual("Loaded:sample_key", loadedAsset);
            Assert.AreEqual("Loaded:sample_key", state.Asset);
            Assert.AreEqual(1, adapter.LoadCalls);

            ValueTask exitTask = state.Exit(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(exitTask);

            Assert.AreEqual("Loaded:sample_key", releasedAsset);
            Assert.AreEqual(1, adapter.ReleaseCalls);
            Assert.IsNull(state.Asset);
        }

        [UnityTest]
        public IEnumerator WarmupLoadsEachKey()
        {
            FakeAdapter adapter = new FakeAdapter();
            WarmupAddressablesState state = new WarmupAddressablesState(
                "Warmup",
                new[] { "key1", "key2" },
                adapter
            );

            ValueTask enterTask = state.Enter(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            Assert.AreEqual(2, adapter.LoadCalls);
            Assert.AreEqual(2, adapter.ReleaseCalls);
        }

        private sealed class FakeAdapter : IAddressablesAdapter
        {
            public int LoadCalls { get; private set; }

            public int ReleaseCalls { get; private set; }

            public ValueTask<IAddressableHandle<TAsset>> LoadAssetAsync<TAsset>(
                string key,
                IProgress<float> progress
            )
                where TAsset : class
            {
                LoadCalls++;
                string assetValue = $"Loaded:{key}";
                TAsset asset = assetValue as TAsset;
                if (asset == null)
                {
                    asset = (TAsset)(object)assetValue;
                }

                return new ValueTask<IAddressableHandle<TAsset>>(
                    new FakeHandle<TAsset>(asset)
                );
            }

            public ValueTask ReleaseAsync<TAsset>(IAddressableHandle<TAsset> handle)
                where TAsset : class
            {
                ReleaseCalls++;
                return default;
            }

            private sealed class FakeHandle<TAsset> : IAddressableHandle<TAsset>
                where TAsset : class
            {
                public FakeHandle(TAsset asset)
                {
                    Asset = asset;
                }

                public TAsset Asset { get; }

                public object RawHandle => Asset;
            }
        }
    }
}
