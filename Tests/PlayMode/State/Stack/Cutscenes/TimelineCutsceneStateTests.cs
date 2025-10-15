namespace WallstopStudios.DxState.Tests.Runtime.State.Stack.Cutscenes
{
    using System.Collections;
    using DxState.State.Stack.States;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.Runtime.TestSupport;

    public sealed class TimelineCutsceneStateTests
    {
        [UnityTest]
        public IEnumerator EnterCompletesWhenControllerFinishes()
        {
            TestTimelineController controller = new TestTimelineController();
            TimelineCutsceneState state = new TimelineCutsceneState("Cutscene", controller);

            ProgressCollector progress = new ProgressCollector();
            System.Threading.Tasks.ValueTask enterTask = state.Enter(
                null,
                progress,
                StateDirection.Forward
            );

            Assert.IsFalse(enterTask.IsCompleted);
            controller.Complete();

            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);
            Assert.GreaterOrEqual(progress.Value, 1f);
        }

        [UnityTest]
        public IEnumerator SkipPredicateStopsCutscene()
        {
            TestTimelineController controller = new TestTimelineController();
            bool skip = false;
            bool skipped = false;
            TimelineCutsceneState state = new TimelineCutsceneState(
                "Skippable",
                controller,
                () => skip,
                () => skipped = true
            );

            ProgressCollector progress = new ProgressCollector();
            System.Threading.Tasks.ValueTask enterTask = state.Enter(
                null,
                progress,
                StateDirection.Forward
            );

            Assert.IsFalse(enterTask.IsCompleted);
            skip = true;

            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);
            Assert.IsTrue(skipped);
            Assert.GreaterOrEqual(progress.Value, 1f);
        }

        private sealed class ProgressCollector : System.IProgress<float>
        {
            public float Value { get; private set; }

            public void Report(float value)
            {
                Value = value;
            }
        }

        private sealed class TestTimelineController : ITimelineController
        {
            public event System.Action Completed;

            private bool _isPlaying;

            public bool IsPlaying => _isPlaying;

            public void Play()
            {
                _isPlaying = true;
            }

            public void Stop()
            {
                _isPlaying = false;
                Completed?.Invoke();
            }

            public void ResetToStart() { }

            public void SetPlaybackSpeed(float speed) { }

            public void Complete()
            {
                _isPlaying = false;
                Completed?.Invoke();
            }
        }
    }
}
