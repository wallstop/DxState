namespace WallstopStudios.DxState.Tests.Runtime.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;

    public sealed class StateStackTickingPlayModeTests
    {
        private const float FloatTolerance = 0.0001f;

        [UnityTest]
        public IEnumerator UpdateTicksActiveAndInactiveStates()
        {
            StateStack stateStack = new StateStack();
            TestState backgroundState = new TestState("Background", TickMode.Update, true);
            TestState activeState = new TestState("Active", TickMode.Update, false);

            yield return WaitForValueTask(stateStack.PushAsync(backgroundState));
            yield return WaitForValueTask(stateStack.PushAsync(activeState));

            yield return null;

            float expectedDelta = Time.deltaTime;
            stateStack.Update();

            Assert.AreEqual(1, activeState.TickCount);
            Assert.AreEqual(1, backgroundState.TickCount);
            Assert.AreEqual(expectedDelta, backgroundState.LastTickDelta, FloatTolerance);
        }

        [UnityTest]
        public IEnumerator FixedUpdateTicksStatesWithFixedDelta()
        {
            StateStack stateStack = new StateStack();
            TestState activeState = new TestState("Active", TickMode.FixedUpdate, false);

            yield return WaitForValueTask(stateStack.PushAsync(activeState));

            yield return new WaitForFixedUpdate();

            float expectedDelta = Time.fixedDeltaTime;
            stateStack.FixedUpdate();

            Assert.AreEqual(1, activeState.TickCount);
            Assert.AreEqual(TickMode.FixedUpdate, activeState.LastTickMode);
            Assert.AreEqual(expectedDelta, activeState.LastTickDelta, FloatTolerance);
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
    }
}
