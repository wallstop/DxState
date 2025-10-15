namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States;

    public sealed class ConditionStateTests
    {
        [UnityTest]
        public IEnumerator EnterWaitsUntilPredicateSatisfied()
        {
            bool ready = false;
            ConditionState state = new ConditionState("Condition", () => ready);
            ProgressCollector progress = new ProgressCollector();
            System.Threading.Tasks.ValueTask enterTask = state.Enter(null, progress, StateDirection.Forward);

            yield return null;
            ready = true;
            yield return WaitForValueTask(enterTask);

            Assert.GreaterOrEqual(progress.LastValue, 1f);
        }

        private static IEnumerator WaitForValueTask(System.Threading.Tasks.ValueTask task)
        {
            System.Threading.Tasks.Task awaited = task.AsTask();
            while (!awaited.IsCompleted)
            {
                yield return null;
            }

            if (awaited.IsFaulted)
            {
                System.Exception exception = awaited.Exception;
                System.Exception inner = exception != null ? exception.InnerException : null;
                throw inner ?? exception;
            }

            if (awaited.IsCanceled)
            {
                throw new System.OperationCanceledException();
            }
        }

        private sealed class ProgressCollector : System.IProgress<float>
        {
            public float LastValue { get; private set; }

            public void Report(float value)
            {
                LastValue = value;
            }
        }
    }
}
