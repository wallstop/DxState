namespace WallstopStudios.DxState.Tests.Runtime.State.Stack.Dialogue
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.Runtime.TestSupport;

    public sealed class DialogueStateTests
    {
        [UnityTest]
        public IEnumerator EnterCompletesWhenDialogueFinishes()
        {
            TestDialogueController controller = new TestDialogueController();
            DialogueState state = new DialogueState("Dialogue", controller, null, null, () => controller.MarkCompleted());

            ProgressCollector progress = new ProgressCollector();
            System.Threading.Tasks.ValueTask enterTask = state.Enter(
                null,
                progress,
                StateDirection.Forward
            );

            Assert.IsFalse(enterTask.IsCompleted);
            controller.Finish();

            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);
            Assert.GreaterOrEqual(progress.Value, 1f);
            Assert.IsTrue(controller.CompletedInvoked);
        }

        [UnityTest]
        public IEnumerator SkipRequestTerminatesDialogue()
        {
            bool skip = false;
            TestDialogueController controller = new TestDialogueController();
            DialogueState state = new DialogueState(
                "Skippable",
                controller,
                () => skip,
                onSkipped: controller.MarkSkipped
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
            Assert.GreaterOrEqual(progress.Value, 1f);
            Assert.IsTrue(controller.Skipped);
            Assert.IsTrue(controller.SkipInvoked);
        }

        private sealed class ProgressCollector : System.IProgress<float>
        {
            public float Value { get; private set; }

            public void Report(float value)
            {
                Value = value;
            }
        }

        private sealed class TestDialogueController : IDialogueController
        {
            public event System.Action Completed;

            public bool CompletedInvoked { get; private set; }

            public bool SkipInvoked { get; private set; }

            public bool Skipped { get; private set; }

            public bool IsRunning { get; private set; }

            public void StartDialogue()
            {
                IsRunning = true;
            }

            public void Skip()
            {
                SkipInvoked = true;
                Skipped = true;
                IsRunning = false;
                Completed?.Invoke();
            }

            public void Cancel()
            {
                IsRunning = false;
            }

            public void Finish()
            {
                IsRunning = false;
                Completed?.Invoke();
            }

            public void MarkCompleted()
            {
                CompletedInvoked = true;
            }

            public void MarkSkipped()
            {
                Skipped = true;
            }
        }
    }
}
