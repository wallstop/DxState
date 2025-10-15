namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States;

    public sealed class TimeStateTests
    {
        private float _originalTimeScale;

        [SetUp]
        public void SetUp()
        {
            _originalTimeScale = Time.timeScale;
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = _originalTimeScale;
        }

        [UnityTest]
        public IEnumerator EnterSetsTimeScale()
        {
            TimeState state = new TimeState("Slow", 0.5f);
            yield return WaitForValueTask(state.Enter(null, new ProgressRecorder(), StateDirection.Forward));
            Assert.AreEqual(0.5f, Time.timeScale, 0.0001f);
        }

        [UnityTest]
        public IEnumerator ExitBackwardRestoresPreviousTimeScale()
        {
            TimeState state = new TimeState("Freeze", 0.25f);
            ProgressRecorder progress = new ProgressRecorder();
            yield return WaitForValueTask(state.Enter(null, progress, StateDirection.Forward));
            yield return WaitForValueTask(state.Exit(null, progress, StateDirection.Backward));

            Assert.AreEqual(_originalTimeScale, Time.timeScale, 0.0001f);
        }

        [UnityTest]
        public IEnumerator RemoveWithoutFutureTimeStatesRestoresPreviousScale()
        {
            TimeState state = new TimeState("Slow", 0.5f);
            ProgressRecorder progress = new ProgressRecorder();
            yield return WaitForValueTask(state.Enter(null, progress, StateDirection.Forward));

            yield return WaitForValueTask(
                state.Remove(Array.Empty<IState>(), Array.Empty<IState>(), progress)
            );

            Assert.AreEqual(_originalTimeScale, Time.timeScale, 0.0001f);
        }

        [UnityTest]
        public IEnumerator RemoveWithFutureTimeStateTransfersPreviousScale()
        {
            TimeState lowerState = new TimeState("Lower", 0.5f);
            TimeState activeState = new TimeState("Active", 0.25f);
            ProgressRecorder progress = new ProgressRecorder();

            yield return WaitForValueTask(lowerState.Enter(null, progress, StateDirection.Forward));
            yield return WaitForValueTask(activeState.Enter(lowerState, progress, StateDirection.Forward));

            List<IState> nextStates = new List<IState> { activeState };
            yield return WaitForValueTask(
                lowerState.Remove(Array.Empty<IState>(), nextStates, progress)
            );

            yield return WaitForValueTask(activeState.Exit(null, progress, StateDirection.Backward));

            Assert.AreEqual(_originalTimeScale, Time.timeScale, 0.0001f);
        }

        private static IEnumerator WaitForValueTask(ValueTask valueTask)
        {
            Task task = valueTask.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsFaulted)
            {
                Exception exception = task.Exception;
                Exception inner = exception != null ? exception.InnerException : null;
                throw inner ?? exception;
            }
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
