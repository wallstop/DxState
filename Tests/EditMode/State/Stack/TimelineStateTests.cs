namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States;
    using WallstopStudios.DxState.Tests.EditMode.TestSupport;

    public sealed class TimelineStateTests
    {
        [UnityTest]
        public IEnumerator EnterWaitsForCompletionWhenConfigured()
        {
            TestTimelineController controller = new TestTimelineController();
            TimelineState state = new TimelineState(
                "Timeline",
                controller,
                rewindOnEnter: false,
                waitForCompletion: true,
                stopOnExit: true
            );

            List<float> progressValues = new List<float>();
            Progress<float> progress = new Progress<float>(value => progressValues.Add(value));

            ValueTask enterTask = state.Enter(null, progress, StateDirection.Forward);
            Assert.IsFalse(enterTask.IsCompleted);
            Assert.IsTrue(controller.PlayInvoked);

            controller.Complete();
            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);

            Assert.IsTrue(controller.StoppedInvoked);
            Assert.IsNotEmpty(progressValues);
            Assert.AreEqual(1f, progressValues[^1], 0.0001f);
        }

        [UnityTest]
        public IEnumerator ExitStopsPlaybackWhenConfigured()
        {
            TestTimelineController controller = new TestTimelineController();
            controller.SetPlaying(true);
            TimelineState state = new TimelineState(
                "TimelineExit",
                controller,
                rewindOnEnter: false,
                waitForCompletion: false,
                stopOnExit: true
            );

            ValueTask exitTask = state.Exit(
                null,
                new Progress<float>(_ => { }),
                StateDirection.Backward
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(exitTask);

            Assert.IsTrue(controller.StoppedInvoked);
        }

        private sealed class TestTimelineController : ITimelineController
        {
            private bool _isPlaying;

            public event Action Completed;

            public bool PlayInvoked { get; private set; }

            public bool StoppedInvoked { get; private set; }

            public bool ResetInvoked { get; private set; }

            public bool IsPlaying => _isPlaying;

            public void Play()
            {
                PlayInvoked = true;
                _isPlaying = true;
            }

            public void Stop()
            {
                StoppedInvoked = true;
                _isPlaying = false;
            }

            public void ResetToStart()
            {
                ResetInvoked = true;
            }

            public void SetPlaybackSpeed(float speed)
            {
            }

            public void Complete()
            {
                _isPlaying = false;
                Completed?.Invoke();
                Stop();
            }

            public void SetPlaying(bool isPlaying)
            {
                _isPlaying = isPlaying;
            }
        }
    }
}
