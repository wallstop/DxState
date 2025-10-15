namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
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
    using WallstopStudios.DxState.State.Stack.States;
    using WallstopStudios.DxState.Tests.EditMode.TestSupport;

    public sealed class SceneStateTests
    {
        [UnityTest]
        public IEnumerator EnterQueuesSingleLoadOperationDuringReentry()
        {
            TestSceneState state = new TestSceneState("TestScene", SceneTransitionMode.Addition);
            List<float> reported = new List<float>();
            Progress<float> progress = new Progress<float>(value => reported.Add(value));

            ValueTask firstEnter = state.Enter(null, progress, StateDirection.Forward);
            Assert.IsFalse(firstEnter.IsCompleted);
            ValueTask secondEnter = state.Enter(null, progress, StateDirection.Forward);
            Assert.IsFalse(secondEnter.IsCompleted);
            Assert.AreEqual(1, state.LoadCallCount);

            state.CompleteOperation();
            yield return ValueTaskTestHelpers.WaitForValueTask(firstEnter);
            yield return ValueTaskTestHelpers.WaitForValueTask(secondEnter);

            Assert.IsTrue(state.SceneLoaded);
            Assert.IsNotEmpty(reported);
        }

        [UnityTest]
        public IEnumerator EnterSkipsLoadWhenSceneAlreadyLoaded()
        {
            TestSceneState state = new TestSceneState("TestScene", SceneTransitionMode.Addition)
            {
                SceneLoaded = true,
            };

            ValueTask enter = state.Enter(null, new Progress<float>(_ => { }), StateDirection.Forward);
            yield return ValueTaskTestHelpers.WaitForValueTask(enter);

            Assert.AreEqual(0, state.LoadCallCount);
        }

        [UnityTest]
        public IEnumerator RevertTriggersUnloadWhenAdditionAndConfigured()
        {
            TestSceneState state = new TestSceneState("TestScene", SceneTransitionMode.Addition)
            {
                SceneLoaded = true,
                RevertOnRemoval = true,
            };

            ValueTask remove = state.Remove(
                Array.Empty<IState>(),
                Array.Empty<IState>(),
                new Progress<float>(_ => { })
            );
            Assert.IsFalse(remove.IsCompleted);
            Assert.AreEqual(1, state.UnloadCallCount);

            state.CompleteOperation();
            yield return ValueTaskTestHelpers.WaitForValueTask(remove);

            Assert.IsFalse(state.SceneLoaded);
        }

        [UnityTest]
        public IEnumerator EnterThrowsWhenOperationFactoryReturnsNull()
        {
            TestSceneState state = new TestSceneState("TestScene", SceneTransitionMode.Addition)
            {
                SceneLoaded = false,
                ReturnNullOperation = true,
            };

            ValueTask enter = state.Enter(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );
            yield return ValueTaskTestHelpers.ExpectFaulted(
                enter,
                exception => Assert.IsInstanceOf<InvalidOperationException>(exception)
            );
        }

        private sealed class TestSceneState : SceneState
        {
            private TaskCompletionSource<bool> _pendingOperation;
            private OperationKind _operationKind;

            public TestSceneState(string name, SceneTransitionMode transitionMode)
                : base(name, transitionMode) { }

            public int LoadCallCount { get; private set; }

            public int UnloadCallCount { get; private set; }

            public bool SceneLoaded { get; set; }

            public bool ReturnNullOperation { get; set; }

            public void CompleteOperation()
            {
                TaskCompletionSource<bool> completion = _pendingOperation;
                if (completion == null)
                {
                    return;
                }

                if (_operationKind == OperationKind.Load)
                {
                    SceneLoaded = true;
                }
                else if (_operationKind == OperationKind.Unload)
                {
                    SceneLoaded = false;
                }

                completion.TrySetResult(true);
                _pendingOperation = null;
                _operationKind = OperationKind.None;
            }

            protected override bool IsSceneLoaded(string sceneName)
            {
                return SceneLoaded;
            }

            protected override AsyncOperation CreateLoadOperation(
                string sceneName,
                LoadSceneParameters parameters
            )
            {
                LoadCallCount++;
                _operationKind = OperationKind.Load;
                if (ReturnNullOperation)
                {
                    ReturnNullOperation = false;
                    return null;
                }

                _pendingOperation = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                return new FakeAsyncOperation();
            }

            protected override AsyncOperation CreateUnloadOperation(
                string sceneName,
                UnloadSceneOptions options
            )
            {
                UnloadCallCount++;
                _operationKind = OperationKind.Unload;
                if (ReturnNullOperation)
                {
                    ReturnNullOperation = false;
                    return null;
                }

                _pendingOperation = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
                return new FakeAsyncOperation();
            }

            protected override Task AwaitSceneOperationAsync(
                AsyncOperation operation,
                IProgress<float> progress
            )
            {
                TaskCompletionSource<bool> completion = _pendingOperation;
                if (completion == null)
                {
                    return Task.CompletedTask;
                }

                return completion.Task;
            }

            private enum OperationKind
            {
                None = 0,
                Load = 1,
                Unload = 2,
            }

            private sealed class FakeAsyncOperation : AsyncOperation { }
        }
    }
}
