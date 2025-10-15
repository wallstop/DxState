#pragma warning disable CS0618 // Type or member is obsolete
namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States;

    public sealed class SceneStateTests
    {
        [UnityTest]
        public IEnumerator EnterThrowsWhenSceneNameMissing()
        {
            SceneState state = new SceneState
            {
                TransitionMode = SceneTransitionMode.Addition,
                Name = string.Empty,
            };

            yield return AssertFaultedAsync(
                state.Enter(null, new ProgressRecorder(), StateDirection.Forward),
                typeof(InvalidOperationException)
            );
        }

        [UnityTest]
        public IEnumerator EnterThrowsWhenTransitionModeIsNone()
        {
            SceneState state = new SceneState
            {
                TransitionMode = SceneTransitionMode.None,
                Name = "SampleScene",
            };

            yield return AssertFaultedAsync(
                state.Enter(null, new ProgressRecorder(), StateDirection.Forward),
                typeof(InvalidEnumArgumentException)
            );
        }

        [UnityTest]
        public IEnumerator ExitThrowsWhenTransitionModeIsNone()
        {
            SceneState state = new SceneState
            {
                TransitionMode = SceneTransitionMode.None,
                Name = "SampleScene",
            };

            yield return AssertFaultedAsync(
                state.Exit(null, new ProgressRecorder(), StateDirection.Backward),
                typeof(InvalidEnumArgumentException)
            );
        }

        private static IEnumerator AssertFaultedAsync(ValueTask valueTask, Type expectedExceptionType)
        {
            Task task = valueTask.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }

            Assert.IsTrue(task.IsFaulted);
            Assert.IsNotNull(task.Exception);
            Assert.IsNotNull(task.Exception.InnerException);
            Assert.IsInstanceOf(expectedExceptionType, task.Exception.InnerException);
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
