namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;

    public sealed class StateStackOperationsTests
    {
        [UnityTest]
        public IEnumerator PushAsyncRegistersStateAndRaisesEvents()
        {
            StateStack stateStack = new StateStack();
            TestState initialState = new TestState("Initial");
            TestState nextState = new TestState("Next");

            List<StateTransitionEvent> pushedEvents = new List<StateTransitionEvent>();
            List<StateTransitionEvent> startEvents = new List<StateTransitionEvent>();
            List<StateTransitionEvent> completeEvents = new List<StateTransitionEvent>();
            List<float> progressValues = new List<float>();

            stateStack.OnStatePushed += (previous, current) =>
            {
                pushedEvents.Add(new StateTransitionEvent(previous, current));
            };
            stateStack.OnTransitionStart += (previous, current) =>
            {
                startEvents.Add(new StateTransitionEvent(previous, current));
            };
            stateStack.OnTransitionComplete += (previous, current) =>
            {
                completeEvents.Add(new StateTransitionEvent(previous, current));
            };
            stateStack.OnTransitionProgress += (state, progress) =>
            {
                progressValues.Add(progress);
            };

            yield return WaitForValueTask(stateStack.PushAsync(initialState));
            yield return WaitForValueTask(stateStack.PushAsync(nextState));
            yield return WaitForValueTask(stateStack.WaitForTransitionCompletionAsync());

            Assert.AreSame(nextState, stateStack.CurrentState);
            Assert.AreSame(initialState, stateStack.PreviousState);

            Assert.AreEqual(2, stateStack.Stack.Count);
            Assert.IsTrue(stateStack.RegisteredStates.ContainsKey(initialState.Name));
            Assert.IsTrue(stateStack.RegisteredStates.ContainsKey(nextState.Name));

            Assert.AreEqual(2, pushedEvents.Count);
            Assert.AreSame(initialState, pushedEvents[0].Current);
            Assert.AreSame(nextState, pushedEvents[1].Current);

            Assert.AreEqual(2, startEvents.Count);
            Assert.AreSame(initialState, startEvents[0].Current);
            Assert.AreSame(nextState, startEvents[1].Current);

            Assert.AreEqual(2, completeEvents.Count);
            Assert.AreSame(initialState, completeEvents[0].Current);
            Assert.AreSame(nextState, completeEvents[1].Current);

            float observedProgress =
                progressValues.Count > 0 ? progressValues[^1] : stateStack.Progress;
            Assert.AreEqual(1f, observedProgress, 0.0001f);
        }

        [UnityTest]
        public IEnumerator PopAsyncRemovesStateAndRestoresPrevious()
        {
            StateStack stateStack = new StateStack();
            TestState stateA = new TestState("StateA");
            TestState stateB = new TestState("StateB");

            yield return WaitForValueTask(stateStack.PushAsync(stateA));
            yield return WaitForValueTask(stateStack.PushAsync(stateB));

            List<StateTransitionEvent> poppedEvents = new List<StateTransitionEvent>();
            stateStack.OnStatePopped += (previous, current) =>
            {
                poppedEvents.Add(new StateTransitionEvent(previous, current));
            };

            IState poppedState = null;
            yield return WaitForValueTask(stateStack.PopAsync(), value => poppedState = value);

            Assert.AreSame(stateB, poppedState);
            Assert.AreSame(stateA, stateStack.CurrentState);
            Assert.AreEqual(1, poppedEvents.Count);
            Assert.AreSame(stateB, poppedEvents[0].Previous);
            Assert.AreSame(stateA, poppedEvents[0].Current);
        }

        [UnityTest]
        public IEnumerator TryPopAsyncOnEmptyReturnsNull()
        {
            StateStack stateStack = new StateStack();
            IState result = null;
            yield return WaitForValueTask(stateStack.TryPopAsync(), value => result = value);
            Assert.IsNull(result);
        }

        [UnityTest]
        public IEnumerator FlattenAsyncReactivatesTargetState()
        {
            StateStack stateStack = new StateStack();
            TestState baseState = new TestState("Base");
            TestState topState = new TestState("Top");

            yield return WaitForValueTask(stateStack.PushAsync(baseState));
            yield return WaitForValueTask(stateStack.PushAsync(topState));

            List<IState> flattenedTargets = new List<IState>();
            stateStack.OnFlattened += target => flattenedTargets.Add(target);

            yield return WaitForValueTask(stateStack.FlattenAsync(baseState));

            Assert.AreEqual(1, flattenedTargets.Count);
            Assert.AreSame(baseState, flattenedTargets[0]);
            Assert.AreSame(baseState, stateStack.CurrentState);
            Assert.AreEqual(1, stateStack.Stack.Count);
        }

        [UnityTest]
        public IEnumerator RemoveAsyncNonTopStateRaisesManualRemovalEvent()
        {
            StateStack stateStack = new StateStack();
            TestState lowerState = new TestState("Lower");
            TestState middleState = new TestState("Middle");
            TestState upperState = new TestState("Upper");

            yield return WaitForValueTask(stateStack.PushAsync(lowerState));
            yield return WaitForValueTask(stateStack.PushAsync(middleState));
            yield return WaitForValueTask(stateStack.PushAsync(upperState));

            List<IState> removedStates = new List<IState>();
            stateStack.OnStateManuallyRemoved += state => removedStates.Add(state);

            yield return WaitForValueTask(stateStack.RemoveAsync(middleState));

            Assert.AreEqual(1, removedStates.Count);
            Assert.AreSame(middleState, removedStates[0]);
            Assert.AreEqual(2, stateStack.Stack.Count);
            Assert.IsFalse(stateStack.Stack.Contains(middleState));
        }

        [UnityTest]
        public IEnumerator ClearAsyncEmptiesStackAndRaisesPoppedEvents()
        {
            StateStack stateStack = new StateStack();
            TestState stateOne = new TestState("One");
            TestState stateTwo = new TestState("Two");
            TestState stateThree = new TestState("Three");

            yield return WaitForValueTask(stateStack.PushAsync(stateOne));
            yield return WaitForValueTask(stateStack.PushAsync(stateTwo));
            yield return WaitForValueTask(stateStack.PushAsync(stateThree));

            List<StateTransitionEvent> poppedEvents = new List<StateTransitionEvent>();
            stateStack.OnStatePopped += (previous, current) =>
            {
                poppedEvents.Add(new StateTransitionEvent(previous, current));
            };

            yield return WaitForValueTask(stateStack.ClearAsync());

            Assert.AreEqual(0, stateStack.Stack.Count);
            Assert.AreEqual(3, poppedEvents.Count);
            Assert.AreSame(stateThree, poppedEvents[0].Previous);
            Assert.AreSame(stateTwo, poppedEvents[1].Previous);
            Assert.AreSame(stateOne, poppedEvents[2].Previous);
        }

        [UnityTest]
        public IEnumerator WaitForTransitionCompletionAsyncReturnsImmediatelyWhenIdle()
        {
            StateStack stateStack = new StateStack();
            yield return WaitForValueTask(stateStack.WaitForTransitionCompletionAsync());
        }

        [UnityTest]
        public IEnumerator PushAsyncQueuesWhenTransitionInProgress()
        {
            StateStack stateStack = new StateStack();
            AsyncTestState firstState = new AsyncTestState("QueuedFirst");
            AsyncTestState secondState = new AsyncTestState("QueuedSecond");

            ValueTask firstPush = stateStack.PushAsync(firstState);
            ValueTask secondPush = stateStack.PushAsync(secondState);

            yield return WaitForValueTask(firstPush);
            yield return WaitForValueTask(secondPush);

            Assert.AreSame(secondState, stateStack.CurrentState);
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

        private static IEnumerator WaitForValueTask<T>(
            ValueTask<T> valueTask,
            Action<T> onCompleted
        )
        {
            Task<T> task = valueTask.AsTask();
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
            if (onCompleted != null)
            {
                onCompleted(task.Result);
            }
        }

        private sealed class TestState : IState
        {
            private readonly string _name;

            public TestState(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("State name must be provided", nameof(name));
                }

                _name = name;
            }

            public string Name => _name;

            public TickMode TickMode => TickMode.None;

            public bool TickWhenInactive => false;

            public float? TimeInState => null;

            public int EnterCount { get; private set; }

            public int ExitCount { get; private set; }

            public ValueTask Enter<TProgress>(
                IState previousState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                EnterCount++;
                progress.Report(1f);
                return new ValueTask();
            }

            public void Tick(TickMode mode, float delta) { }

            public ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                ExitCount++;
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

        private sealed class StateTransitionEvent
        {
            public StateTransitionEvent(IState previous, IState current)
            {
                Previous = previous;
                Current = current;
            }

            public IState Previous { get; }

            public IState Current { get; }
        }

        private sealed class AsyncTestState : IState
        {
            private readonly string _name;

            public AsyncTestState(string name)
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    throw new ArgumentException("State name must be provided", nameof(name));
                }

                _name = name;
            }

            public string Name => _name;

            public TickMode TickMode => TickMode.None;

            public bool TickWhenInactive => false;

            public float? TimeInState => null;

            public async ValueTask Enter<TProgress>(
                IState previousState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                await Task.Yield();
                progress.Report(1f);
            }

            public void Tick(TickMode mode, float delta) { }

            public async ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                await Task.Yield();
                progress.Report(1f);
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
