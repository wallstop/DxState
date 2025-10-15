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

    public sealed class StateStackTickingTests
    {
        private const float FloatTolerance = 0.0001f;

        [SetUp]
        public void SetUp()
        {
            TimeUtility.ResetTime();
        }

        [UnityTest]
        public IEnumerator UpdateTicksActiveStateWithDeltaTime()
        {
            StateStack stateStack = new StateStack();
            TestState activeState = new TestState("Active", TickMode.Update, false);
            yield return WaitForValueTask(stateStack.PushAsync(activeState));

            float expectedDelta = Time.deltaTime;
            stateStack.Update();

            Assert.AreEqual(1, activeState.TickCount);
            Assert.AreEqual(TickMode.Update, activeState.LastTickMode);
            Assert.AreEqual(expectedDelta, activeState.LastTickDelta, FloatTolerance);
        }

        [UnityTest]
        public IEnumerator FixedUpdateTicksActiveStateWithFixedDeltaTime()
        {
            StateStack stateStack = new StateStack();
            TestState activeState = new TestState("Active", TickMode.FixedUpdate, false);
            yield return WaitForValueTask(stateStack.PushAsync(activeState));

            float expectedDelta = Time.fixedDeltaTime;
            stateStack.FixedUpdate();

            Assert.AreEqual(1, activeState.TickCount);
            Assert.AreEqual(TickMode.FixedUpdate, activeState.LastTickMode);
            Assert.AreEqual(expectedDelta, activeState.LastTickDelta, FloatTolerance);
        }

        [UnityTest]
        public IEnumerator LateUpdateTicksActiveStateWithDeltaTime()
        {
            StateStack stateStack = new StateStack();
            TestState activeState = new TestState("Active", TickMode.LateUpdate, false);
            yield return WaitForValueTask(stateStack.PushAsync(activeState));

            float expectedDelta = Time.deltaTime;
            stateStack.LateUpdate();

            Assert.AreEqual(1, activeState.TickCount);
            Assert.AreEqual(TickMode.LateUpdate, activeState.LastTickMode);
            Assert.AreEqual(expectedDelta, activeState.LastTickDelta, FloatTolerance);
        }

        [UnityTest]
        public IEnumerator UpdateTicksInactiveStatesRespectingTickWhenInactive()
        {
            StateStack stateStack = new StateStack();
            TestState inactiveTickingState = new TestState(
                "InactiveTicking",
                TickMode.Update,
                true
            );
            TestState inactiveSilentState = new TestState("InactiveSilent", TickMode.Update, false);
            TestState activeState = new TestState("Active", TickMode.Update, false);

            yield return WaitForValueTask(stateStack.PushAsync(inactiveTickingState));
            yield return WaitForValueTask(stateStack.PushAsync(inactiveSilentState));
            yield return WaitForValueTask(stateStack.PushAsync(activeState));

            float expectedDelta = Time.deltaTime;
            stateStack.Update();

            Assert.AreEqual(1, activeState.TickCount);
            Assert.AreEqual(1, inactiveTickingState.TickCount);
            Assert.AreEqual(0, inactiveSilentState.TickCount);
            Assert.AreEqual(expectedDelta, inactiveTickingState.LastTickDelta, FloatTolerance);
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

        private sealed class TestState : IState
        {
            private readonly string _name;
            private readonly TickMode _tickMode;
            private readonly bool _tickWhenInactive;

            public TestState(string name, TickMode tickMode, bool tickWhenInactive)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("State name is required", nameof(name));
                }

                _name = name;
                _tickMode = tickMode;
                _tickWhenInactive = tickWhenInactive;
            }

            public string Name => _name;

            public TickMode TickMode => _tickMode;

            public bool TickWhenInactive => _tickWhenInactive;

            public float? TimeInState => null;

            public int TickCount { get; private set; }

            public TickMode LastTickMode { get; private set; }

            public float LastTickDelta { get; private set; }

            public ValueTask Enter<TProgress>(
                IState previousState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return new ValueTask();
            }

            public void Tick(TickMode mode, float delta)
            {
                LastTickMode = mode;
                LastTickDelta = delta;
                TickCount++;
            }

            public ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return new ValueTask();
            }

            public ValueTask Remove<TProgress>(
                IReadOnlyList<IState> previousStatesInStack,
                IReadOnlyList<IState> nextStatesInStack,
                TProgress progress
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return new ValueTask();
            }
        }

        private static class TimeUtility
        {
            public static void ResetTime()
            {
                Time.timeScale = 1f;
            }
        }
    }
}
