namespace WallstopStudios.DxState.Tests.Runtime.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.Runtime.TestSupport;

    public sealed class ExclusiveSceneSetStateTests
    {
        [UnityTest]
        public IEnumerator EnterLoadsAndUnloadsScenesToMatchTargetSet()
        {
            FakeExecutor executor = new FakeExecutor(new[] { "SceneA", "SceneB" });
            ExclusiveSceneSetState state = new ExclusiveSceneSetState(
                "Exclusive",
                new[] { "SceneB", "SceneC" },
                loadParameters: new LoadSceneParameters(LoadSceneMode.Additive),
                sceneExecutor: executor,
                operationAwaiter: executor.AwaitOperation
            );

            ValueTask enterTask = state.Enter(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            CollectionAssert.AreEquivalent(new[] { "SceneB", "SceneC" }, executor.CurrentScenes);
        }

        [UnityTest]
        public IEnumerator RemoveUnloadsTargetScenes()
        {
            FakeExecutor executor = new FakeExecutor(new[] { "SceneX", "SceneY" });
            ExclusiveSceneSetState state = new ExclusiveSceneSetState(
                "ExclusiveRemove",
                new[] { "SceneX" },
                sceneExecutor: executor,
                operationAwaiter: executor.AwaitOperation
            );

            ValueTask enterTask = state.Enter(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            ValueTask removeTask = state.Remove(
                new List<IState>(),
                new List<IState>(),
                new Progress<float>(_ => { })
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(removeTask);

            Assert.AreEqual(0, executor.CurrentScenes.Count);
        }

        private sealed class FakeExecutor : ExclusiveSceneSetState.ISceneOperationExecutor
        {
            private readonly HashSet<string> _currentScenes;
            private readonly Dictionary<AsyncOperation, System.Action> _completions;

            public FakeExecutor(IEnumerable<string> initialScenes)
            {
                _currentScenes = new HashSet<string>(initialScenes);
                _completions = new Dictionary<AsyncOperation, System.Action>();
            }

            public IReadOnlyCollection<string> CurrentScenes => _currentScenes;

            public IEnumerable<string> EnumerateLoadedScenes()
            {
                return _currentScenes;
            }

            public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneParameters parameters)
            {
                TestAsyncOperation operation = new TestAsyncOperation();
                _completions[operation] = () =>
                {
                    _currentScenes.Add(sceneName);
                };
                return operation;
            }

            public AsyncOperation UnloadSceneAsync(string sceneName, UnloadSceneOptions options)
            {
                TestAsyncOperation operation = new TestAsyncOperation();
                _completions[operation] = () =>
                {
                    _currentScenes.Remove(sceneName);
                };
                return operation;
            }

            public ValueTask AwaitOperation(AsyncOperation operation, IProgress<float> progress)
            {
                if (_completions.TryGetValue(operation, out System.Action completion))
                {
                    completion.Invoke();
                    _completions.Remove(operation);
                }
                progress?.Report(1f);
                return default;
            }

            private sealed class TestAsyncOperation : AsyncOperation { }
        }
    }
}
