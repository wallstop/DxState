namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.EditMode.TestSupport;

    public sealed class ChunkStreamingStateTests
    {
        [UnityTest]
        public IEnumerator EnterLoadsInitialChunks()
        {
            Vector3 anchor = Vector3.zero;
            TestStrategy strategy = new TestStrategy(position => new[] { "Chunk_0_0" });
            TestLoader loader = new TestLoader();
            ChunkStreamingState state = new ChunkStreamingState(
                "Chunks",
                strategy,
                loader,
                () => anchor,
                updateIntervalSeconds: 0.1f
            );

            ValueTask enterTask = state.Enter(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            CollectionAssert.Contains(loader.LoadedChunks, "Chunk_0_0");
        }

        [UnityTest]
        public IEnumerator TickUpdatesChunksWhenAnchorMoves()
        {
            Vector3 anchor = Vector3.zero;
            TestStrategy strategy = new TestStrategy(position =>
            {
                if (position.x > 5f)
                {
                    return new[] { "Chunk_1_0" };
                }

                return new[] { "Chunk_0_0" };
            });
            TestLoader loader = new TestLoader();
            ChunkStreamingState state = new ChunkStreamingState(
                "Chunks",
                strategy,
                loader,
                () => anchor,
                updateIntervalSeconds: 0.1f
            );
            ValueTask enterTask = state.Enter(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            anchor = new Vector3(10f, 0f, 0f);
            state.Tick(TickMode.Update, 0.2f);
            yield return null;

            CollectionAssert.Contains(loader.LoadedChunks, "Chunk_1_0");
            CollectionAssert.Contains(loader.UnloadedChunks, "Chunk_0_0");
        }

        private sealed class TestStrategy : IChunkStreamingStrategy
        {
            private readonly Func<Vector3, IEnumerable<string>> _resolver;

            public TestStrategy(Func<Vector3, IEnumerable<string>> resolver)
            {
                _resolver = resolver;
            }

            public IEnumerable<string> ResolveRequiredChunks(Vector3 anchorPosition)
            {
                return _resolver(anchorPosition);
            }
        }

        private sealed class TestLoader : IChunkLoader
        {
            public List<string> LoadedChunks { get; } = new List<string>();
            public List<string> UnloadedChunks { get; } = new List<string>();

            public ValueTask EnsureLoadedAsync(string chunkId, IProgress<float> progress)
            {
                LoadedChunks.Add(chunkId);
                progress?.Report(1f);
                return default;
            }

            public ValueTask EnsureUnloadedAsync(string chunkId, IProgress<float> progress)
            {
                UnloadedChunks.Add(chunkId);
                progress?.Report(1f);
                return default;
            }
        }
    }
}
