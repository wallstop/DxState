namespace WallstopStudios.DxState.Tests.Runtime.State.Stack
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States;
    using WallstopStudios.DxState.Tests.Runtime.TestSupport;

    public sealed class WaitForSecondsStateTests
    {
        [UnityTest]
        public IEnumerator EnterWaitsForDuration()
        {
            WaitForSecondsState state = new WaitForSecondsState("Delay", 0.01f);
            ProgressCollector progress = new ProgressCollector();
            System.Threading.Tasks.ValueTask enterTask = state.Enter(null, progress, StateDirection.Forward);

            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            Assert.GreaterOrEqual(progress.LastValue, 1f);
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
