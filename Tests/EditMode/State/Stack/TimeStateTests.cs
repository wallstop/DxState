namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
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

        [Test]
        public void EnterSetsTimeScale()
        {
            TimeState state = new TimeState("Slow", 0.5f);
            state.Enter(null, new ProgressRecorder(), StateDirection.Forward).GetAwaiter().GetResult();
            Assert.AreEqual(0.5f, Time.timeScale, 0.0001f);
        }

        [Test]
        public void ExitBackwardRestoresPreviousTimeScale()
        {
            TimeState state = new TimeState("Freeze", 0.25f);
            state.Enter(null, new ProgressRecorder(), StateDirection.Forward).GetAwaiter().GetResult();

            state.Exit(null, new ProgressRecorder(), StateDirection.Backward).GetAwaiter().GetResult();

            Assert.AreEqual(_originalTimeScale, Time.timeScale, 0.0001f);
        }

        [Test]
        public void RemoveWithoutFutureTimeStatesRestoresPreviousScale()
        {
            TimeState state = new TimeState("Slow", 0.5f);
            state.Enter(null, new ProgressRecorder(), StateDirection.Forward).GetAwaiter().GetResult();

            state.Remove(
                Array.Empty<IState>(),
                Array.Empty<IState>(),
                new ProgressRecorder()
            ).GetAwaiter().GetResult();

            Assert.AreEqual(_originalTimeScale, Time.timeScale, 0.0001f);
        }

        [Test]
        public void RemoveWithFutureTimeStateTransfersPreviousScale()
        {
            TimeState lowerState = new TimeState("Lower", 0.5f);
            TimeState activeState = new TimeState("Active", 0.25f);

            lowerState.Enter(null, new ProgressRecorder(), StateDirection.Forward).GetAwaiter().GetResult();
            activeState.Enter(lowerState, new ProgressRecorder(), StateDirection.Forward).GetAwaiter().GetResult();

            List<IState> nextStates = new List<IState> { activeState };
            lowerState.Remove(Array.Empty<IState>(), nextStates, new ProgressRecorder()).GetAwaiter().GetResult();

            activeState.Exit(null, new ProgressRecorder(), StateDirection.Backward).GetAwaiter().GetResult();

            Assert.AreEqual(_originalTimeScale, Time.timeScale, 0.0001f);
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
