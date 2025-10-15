namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States;

    public sealed class SceneStateTests
    {
        [Test]
        public void EnterQueuesSingleLoadOperationDuringReentry()
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
            firstEnter.AsTask().GetAwaiter().GetResult();
            secondEnter.AsTask().GetAwaiter().GetResult();

            Assert.IsTrue(state.SceneLoaded);
            Assert.IsNotEmpty(reported);
        }

        [Test]
        public void EnterSkipsLoadWhenSceneAlreadyLoaded()
        {
            TestSceneState state = new TestSceneState("TestScene", SceneTransitionMode.Addition)
            {
                SceneLoaded = true,
            };

            ValueTask enter = state.Enter(null, new Progress<float>(_ => { }), StateDirection.Forward);
            enter.AsTask().GetAwaiter().GetResult();

            Assert.AreEqual(0, state.LoadCallCount);
        }

        [Test]
        public void RevertTriggersUnloadWhenAdditionAndConfigured()
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
            remove.AsTask().GetAwaiter().GetResult();

            Assert.IsFalse(state.SceneLoaded);
        }

        [Test]
        public void EnterThrowsWhenOperationFactoryReturnsNull()
        {
            TestSceneState state = new TestSceneState("TestScene", SceneTransitionMode.Addition)
            {
                SceneLoaded = false,
                ReturnNullOperation = true,
            };

            Assert.Throws<InvalidOperationException>(() =>
                state.Enter(null, new Progress<float>(_ => { }), StateDirection.Forward)
                    .AsTask()
                    .GetAwaiter()
                    .GetResult()
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
