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

    public sealed class StateGroupTests
    {
        [SetUp]
        public void SetUp()
        {
            Time.timeScale = 1f;
        }

        [UnityTest]
        public IEnumerator SequentialModeInvokesChildrenInOrder()
        {
            List<string> enterOrder = new List<string>();
            TestState childA = new TestState("ChildA", enterOrder);
            TestState childB = new TestState("ChildB", enterOrder);

            StateGroup group = new StateGroup(
                "Root",
                new IState[] { childA, childB },
                StateGroupMode.Sequential
            );

            ProgressRecorder progress = new ProgressRecorder();
            yield return WaitForValueTask(group.Enter(null, progress, StateDirection.Forward));

            Assert.AreEqual(2, enterOrder.Count);
            Assert.AreEqual("ChildA", enterOrder[0]);
            Assert.AreEqual("ChildB", enterOrder[1]);
            Assert.AreEqual(1f, progress.LastValue, 0.0001f);
        }

        [UnityTest]
        public IEnumerator SequentialModeExitInvokesChildrenInReverseOrder()
        {
            List<string> exitOrder = new List<string>();
            TestState childA = new TestState("ChildA", null, exitOrder);
            TestState childB = new TestState("ChildB", null, exitOrder);

            StateGroup group = new StateGroup(
                "Root",
                new IState[] { childA, childB },
                StateGroupMode.Sequential
            );

            ProgressRecorder progress = new ProgressRecorder();
            yield return WaitForValueTask(group.Enter(null, progress, StateDirection.Forward));
            yield return WaitForValueTask(group.Exit(null, progress, StateDirection.Backward));

            Assert.AreEqual(2, exitOrder.Count);
            Assert.AreEqual("ChildB", exitOrder[0]);
            Assert.AreEqual("ChildA", exitOrder[1]);
        }

        [Test]
        public void ParallelModeTicksAllChildren()
        {
            TestState childA = new TestState("ChildA", null, null, TickMode.Update);
            TestState childB = new TestState("ChildB", null, null, TickMode.Update);

            StateGroup group = new StateGroup(
                "Root",
                new IState[] { childA, childB },
                StateGroupMode.Parallel,
                TickMode.Update,
                true
            );

            group.Tick(TickMode.Update, 0.5f);

            Assert.AreEqual(1, childA.TickCount);
            Assert.AreEqual(1, childB.TickCount);
        }

        [UnityTest]
        public IEnumerator RemoveInvokesChildRemove()
        {
            TestState childA = new TestState("ChildA", null, null, TickMode.None);
            TestState childB = new TestState("ChildB", null, null, TickMode.None);

            StateGroup group = new StateGroup(
                "Root",
                new IState[] { childA, childB },
                StateGroupMode.Parallel
            );

            ProgressRecorder progress = new ProgressRecorder();
            yield return WaitForValueTask(group.Enter(null, progress, StateDirection.Forward));
            yield return WaitForValueTask(
                group.Remove(Array.Empty<IState>(), Array.Empty<IState>(), progress)
            );

            Assert.AreEqual(1, childA.RemoveCount);
            Assert.AreEqual(1, childB.RemoveCount);
            Assert.AreEqual(1f, progress.LastValue, 0.0001f);
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
            private readonly List<string> _enterOrder;
            private readonly List<string> _exitOrder;
            private readonly TickMode _tickMode;

            public TestState(
                string name,
                List<string> enterOrder = null,
                List<string> exitOrder = null,
                TickMode tickMode = TickMode.None
            )
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("Name is required", nameof(name));
                }

                _name = name;
                _enterOrder = enterOrder;
                _exitOrder = exitOrder;
                _tickMode = tickMode;
            }

            public string Name => _name;

            public TickMode TickMode => _tickMode;

            public bool TickWhenInactive => false;

            public float? TimeInState => null;

            public int TickCount { get; private set; }

            public int RemoveCount { get; private set; }

            public ValueTask Enter<TProgress>(
                IState previousState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                if (_enterOrder != null)
                {
                    _enterOrder.Add(_name);
                }

                progress.Report(1f);
                return new ValueTask();
            }

            public void Tick(TickMode mode, float delta)
            {
                TickCount++;
            }

            public ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                if (_exitOrder != null)
                {
                    _exitOrder.Add(_name);
                }

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
                RemoveCount++;
                progress.Report(1f);
                return new ValueTask();
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
