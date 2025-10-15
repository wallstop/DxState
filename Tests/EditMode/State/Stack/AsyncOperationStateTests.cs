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
    using WallstopStudios.DxState.State.Stack.States;
    using WallstopStudios.DxState.Tests.EditMode.TestSupport;

    public sealed class AsyncOperationStateTests
    {
        [UnityTest]
        public IEnumerator EnterAwaiterCompletesAndReportsProgress()
        {
            TestAwaiter awaiter = new TestAwaiter();
            AsyncOperationStateCallbacks callbacks = new AsyncOperationStateCallbacks(
                _ => new TestAsyncOperation(),
                null,
                null
            );
            AsyncOperationState state = new AsyncOperationState(
                "AsyncOperation",
                callbacks,
                TickMode.None,
                false,
                awaiter.AwaitAsyncOperation
            );

            List<float> reportedProgress = new List<float>();
            Progress<float> progress = new Progress<float>(value => reportedProgress.Add(value));

            ValueTask enterTask = state.Enter(null, progress, StateDirection.Forward);
            Assert.IsFalse(enterTask.IsCompleted);

            awaiter.Complete();
            awaiter.Complete();
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            Assert.IsNotEmpty(reportedProgress);
            Assert.AreEqual(1f, reportedProgress[^1], 0.0001f);
        }

        [UnityTest]
        public IEnumerator ConcurrentOperationThrows()
        {
            BlockingAwaiter awaiter = new BlockingAwaiter();
            AsyncOperationStateCallbacks callbacks = new AsyncOperationStateCallbacks(
                _ => new TestAsyncOperation(),
                null,
                null
            );

            AsyncOperationState state = new AsyncOperationState(
                "Concurrent",
                callbacks,
                TickMode.None,
                false,
                awaiter.AwaitAsyncOperation
            );

            ValueTask first = state.Enter(null, new Progress<float>(_ => { }), StateDirection.Forward);
            Assert.IsFalse(first.IsCompleted);

            ValueTask second = state.Enter(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Forward
            );

            yield return ValueTaskTestHelpers.ExpectFaulted(
                second,
                exception => Assert.IsInstanceOf<InvalidOperationException>(exception)
            );

            awaiter.Complete();
            yield return ValueTaskTestHelpers.WaitForValueTask(first);
        }

        private sealed class TestAsyncOperation : AsyncOperation { }

        private sealed class TestAwaiter
        {
            private readonly TaskCompletionSource<bool> _completion;

            public TestAwaiter()
            {
                _completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
            }

            public ValueTask AwaitAsyncOperation(
                AsyncOperation operation,
                IProgress<float> progress
            )
            {
                progress?.Report(0.5f);
                return new ValueTask(_completion.Task);
            }

            public void Complete()
            {
                _completion.TrySetResult(true);
            }
        }

        private sealed class BlockingAwaiter
        {
            private readonly TaskCompletionSource<bool> _completion;

            public BlockingAwaiter()
            {
                _completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
            }

            public ValueTask AwaitAsyncOperation(
                AsyncOperation operation,
                IProgress<float> progress
            )
            {
                return new ValueTask(_completion.Task);
            }

            public void Complete()
            {
                _completion.TrySetResult(true);
            }
        }
    }
}
