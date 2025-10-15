#pragma warning disable CS0618 // Type or member is obsolete
namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States;

    public sealed class SceneStateTests
    {
        [Test]
        public void EnterThrowsWhenSceneNameMissing()
        {
            SceneState state = new SceneState
            {
                TransitionMode = SceneTransitionMode.Addition,
                Name = string.Empty,
            };

            Assert.Throws<InvalidOperationException>(() =>
                state
                    .Enter(null, new ProgressRecorder(), StateDirection.Forward)
                    .GetAwaiter()
                    .GetResult()
            );
        }

        [Test]
        public void EnterThrowsWhenTransitionModeIsNone()
        {
            SceneState state = new SceneState
            {
                TransitionMode = SceneTransitionMode.None,
                Name = "SampleScene",
            };

            Assert.Throws<InvalidEnumArgumentException>(() =>
                state
                    .Enter(null, new ProgressRecorder(), StateDirection.Forward)
                    .GetAwaiter()
                    .GetResult()
            );
        }

        [Test]
        public void ExitThrowsWhenTransitionModeIsNone()
        {
            SceneState state = new SceneState
            {
                TransitionMode = SceneTransitionMode.None,
                Name = "SampleScene",
            };

            Assert.Throws<InvalidEnumArgumentException>(() =>
                state
                    .Exit(null, new ProgressRecorder(), StateDirection.Backward)
                    .GetAwaiter()
                    .GetResult()
            );
        }

        private sealed class ProgressRecorder : IProgress<float>
        {
            public float LastValue { get; private set; }

            public void Report(float value)
            {
                LastValue = value;
            }
        }
    }
}
