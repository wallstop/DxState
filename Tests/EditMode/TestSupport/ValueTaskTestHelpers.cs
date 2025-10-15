namespace WallstopStudios.DxState.Tests.EditMode.TestSupport
{
    using System;
    using System.Collections;
    using System.Threading.Tasks;
    using NUnit.Framework;

    internal static class ValueTaskTestHelpers
    {
        public static IEnumerator WaitForValueTask(ValueTask valueTask)
        {
            Task task = valueTask.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsCanceled)
            {
                Assert.Fail("ValueTask was cancelled unexpectedly.");
            }

            if (task.IsFaulted)
            {
                Exception resolved = task.Exception != null ? task.Exception.InnerException ?? task.Exception : null;
                Assert.Fail(resolved != null ? resolved.Message : "ValueTask faulted without exception.");
            }
        }

        public static IEnumerator WaitForValueTask<T>(ValueTask<T> valueTask, Action<T> onCompleted)
        {
            Task<T> task = valueTask.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            if (task.IsCanceled)
            {
                Assert.Fail("ValueTask was cancelled unexpectedly.");
            }

            if (task.IsFaulted)
            {
                Exception resolved = task.Exception != null ? task.Exception.InnerException ?? task.Exception : null;
                Assert.Fail(resolved != null ? resolved.Message : "ValueTask faulted without exception.");
            }

            if (onCompleted != null)
            {
                onCompleted(task.Result);
            }
        }

        public static IEnumerator ExpectFaulted(ValueTask valueTask, Action<Exception> onFaulted)
        {
            Task task = valueTask.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(task.IsFaulted, "ValueTask did not fault as expected.");
            Exception resolved = task.Exception != null ? task.Exception.InnerException ?? task.Exception : null;
            onFaulted?.Invoke(resolved);
        }
    }
}
