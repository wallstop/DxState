namespace WallstopStudios.DxState.Tests.EditMode.Extensions
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.Extensions;

    public sealed class UnityExtensionsTests
    {
        [UnityTest]
        public IEnumerator AwaitWithProgressHandlesNullOperation()
        {
            List<float> reported = new List<float>();
            IProgress<float> progress = new Progress<float>(value => reported.Add(value));

            yield return WaitForValueTask(UnityExtensions.AwaitWithProgress(null, progress));

            Assert.AreEqual(1, reported.Count);
            Assert.AreEqual(1f, reported[0], 0.0001f);
        }

        [UnityTest]
        public IEnumerator AwaitWithProgressReportsResourceRequestProgress()
        {
            ResourceRequest request = Resources.LoadAsync<TextAsset>("TestData");
            List<float> reported = new List<float>();
            IProgress<float> progress = new Progress<float>(value => reported.Add(value));

            yield return WaitForValueTask(request.AwaitWithProgress(progress));

            Assert.IsNotEmpty(reported);
            Assert.GreaterOrEqual(reported[^1], 1f);
        }

        [UnityTest]
        public IEnumerator AwaitWithProgressRespectsCancellation()
        {
            ResourceRequest request = Resources.LoadAsync<TextAsset>("TestData");
            CancellationTokenSource cts = new CancellationTokenSource();
            cts.Cancel();

            yield return ExpectCanceled(
                request.AwaitWithProgress<IProgress<float>>(null, cancellationToken: cts.Token)
            );
        }

        private static IEnumerator WaitForValueTask(ValueTask task)
        {
            Task awaitedTask = task.AsTask();
            while (!awaitedTask.IsCompleted)
            {
                yield return null;
            }

            // Allow posted progress callbacks to flush onto the main thread.
            yield return null;

            if (awaitedTask.IsFaulted)
            {
                Exception exception = awaitedTask.Exception;
                Exception inner = exception != null ? exception.InnerException : null;
                throw inner ?? exception;
            }

            if (awaitedTask.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }

        private static IEnumerator ExpectCanceled(ValueTask task)
        {
            Task awaitedTask = task.AsTask();
            while (!awaitedTask.IsCompleted)
            {
                yield return null;
            }

            yield return null;

            Assert.IsTrue(awaitedTask.IsCanceled);
        }

        [UnityTest]
        public IEnumerator AwaitWithProgressDoesNotAllocateAfterWarmup()
        {
            // Warm up the driver to clear initial allocations.
            Progress<float> warmupProgress = new Progress<float>(_ => { });
            ResourceRequest warmupRequest = Resources.LoadAsync<TextAsset>("TestData");
            ValueTask warmupTask = warmupRequest.AwaitWithProgress(warmupProgress);
            while (!warmupTask.IsCompleted)
            {
                yield return null;
            }
            warmupTask.GetAwaiter().GetResult();
            yield return null;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            Progress<float> progress = new Progress<float>(_ => { });
            long before = GC.GetAllocatedBytesForCurrentThread();

            ResourceRequest request = Resources.LoadAsync<TextAsset>("TestData");
            ValueTask awaited = request.AwaitWithProgress(progress);
            while (!awaited.IsCompleted)
            {
                yield return null;
            }
            awaited.GetAwaiter().GetResult();
            yield return null;

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.LessOrEqual(after - before, 0);
        }
    }
}
