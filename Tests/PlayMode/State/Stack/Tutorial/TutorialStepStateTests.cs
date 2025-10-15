namespace WallstopStudios.DxState.Tests.Runtime.State.Stack.Tutorial
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.Runtime.TestSupport;

    public sealed class TutorialStepStateTests
    {
        [UnityTest]
        public IEnumerator CompletesWhenPredicateSatisfied()
        {
            bool complete = false;
            TutorialStepState state = new TutorialStepState(
                "Step",
                () => complete
            );

            System.Threading.Tasks.ValueTask enterTask = state.Enter(
                null,
                new ProgressCollector(),
                StateDirection.Forward
            );

            Assert.IsFalse(enterTask.IsCompleted);
            complete = true;

            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);
        }

        [UnityTest]
        public IEnumerator ReportsProgressFromProvider()
        {
            bool complete = false;
            float progressValue = 0f;
            ProgressCollector collector = new ProgressCollector();
            TutorialStepState state = new TutorialStepState(
                "ProgressStep",
                () => complete,
                () => progressValue
            );

            System.Threading.Tasks.ValueTask enterTask = state.Enter(
                null,
                collector,
                StateDirection.Forward
            );

            progressValue = 0.5f;
            yield return null;
            Assert.Greater(collector.Value, 0f);

            complete = true;
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);
            Assert.GreaterOrEqual(collector.Value, 1f);
        }

        private sealed class ProgressCollector : System.IProgress<float>
        {
            public float Value { get; private set; }

            public void Report(float value)
            {
                Value = value;
            }
        }
    }
}
