namespace WallstopStudios.DxState.Tests.EditMode.State.Stack.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public sealed class StateStackDiagnosticsTests
    {
        [UnityTest]
        public IEnumerator RecordsTransitionEvents()
        {
            StateStack stateStack = new StateStack();
            StateStackDiagnostics diagnostics = new StateStackDiagnostics(stateStack, 16, false);

            TestState firstState = new TestState("First");
            TestState secondState = new TestState("Second");

            yield return WaitForValueTask(stateStack.PushAsync(firstState));
            yield return WaitForValueTask(stateStack.PushAsync(secondState));
            yield return WaitForValueTask(stateStack.PopAsync(), _ => { });

            bool recordedPush = diagnostics.Events.Any(
                diagnosticEvent => diagnosticEvent.EventType == StateStackDiagnosticEventType.StatePushed
                    && diagnosticEvent.CurrentState == secondState.Name
            );
            bool recordedPop = diagnostics.Events.Any(
                diagnosticEvent => diagnosticEvent.EventType == StateStackDiagnosticEventType.StatePopped
                    && diagnosticEvent.PreviousState == secondState.Name
            );

            Assert.IsTrue(recordedPush, "Expected diagnostics to capture state push events.");
            Assert.IsTrue(recordedPop, "Expected diagnostics to capture state pop events.");
        }

        [UnityTest]
        public IEnumerator EnforcesCapacity()
        {
            StateStack stateStack = new StateStack();
            StateStackDiagnostics diagnostics = new StateStackDiagnostics(stateStack, 2, false);

            TestState firstState = new TestState("First");
            TestState secondState = new TestState("Second");
            TestState thirdState = new TestState("Third");

            yield return WaitForValueTask(stateStack.PushAsync(firstState));
            yield return WaitForValueTask(stateStack.PushAsync(secondState));
            yield return WaitForValueTask(stateStack.PushAsync(thirdState));

            Assert.LessOrEqual(diagnostics.Events.Count, 2);
        }

        [UnityTest]
        public IEnumerator TracksLatestProgress()
        {
            StateStack stateStack = new StateStack();
            StateStackDiagnostics diagnostics = new StateStackDiagnostics(stateStack, 8, false);
            TestState state = new TestState("ProgressState");

            yield return WaitForValueTask(stateStack.PushAsync(state));
            yield return WaitForValueTask(stateStack.WaitForTransitionCompletionAsync());
            yield return null;

            bool hasProgress = false;
            float recordedProgress = 0f;
            foreach (KeyValuePair<string, float> entry in diagnostics.LatestProgress)
            {
                if (string.Equals(entry.Key, state.Name, StringComparison.Ordinal))
                {
                    recordedProgress = entry.Value;
                    hasProgress = true;
                    break;
                }
            }

            Assert.IsTrue(hasProgress);
            Assert.AreEqual(1f, recordedProgress, 0.0001f);
        }

        [UnityTest]
        public IEnumerator DisposeStopsRecording()
        {
            StateStack stateStack = new StateStack();
            StateStackDiagnostics diagnostics = new StateStackDiagnostics(stateStack, 8, false);
            diagnostics.Dispose();

            TestState state = new TestState("LateState");
            yield return WaitForValueTask(stateStack.PushAsync(state));

            Assert.AreEqual(0, diagnostics.Events.Count);
        }

        [UnityTest]
        public IEnumerator TracksTransitionQueueDepthAndDeferredCount()
        {
            StateStack stateStack = new StateStack();
            StateStackDiagnostics diagnostics = new StateStackDiagnostics(stateStack, 8, false);

            TestState first = new TestState("QueueFirst");
            TestState second = new TestState("QueueSecond");

            ValueTask firstPush = stateStack.PushAsync(first);
            ValueTask secondPush = stateStack.PushAsync(second);

            Assert.AreEqual(1, diagnostics.TransitionQueueDepth);
            Assert.AreEqual(1, diagnostics.PendingDeferredTransitions);
            Assert.AreEqual(1, diagnostics.LifetimeDeferredTransitions);
            Assert.That(
                diagnostics.MaxTransitionQueueDepth,
                Is.GreaterThanOrEqualTo(1)
            );
            Assert.That(
                diagnostics.AverageTransitionQueueDepth,
                Is.GreaterThanOrEqualTo(0f)
            );

            yield return WaitForValueTask(firstPush);
            yield return WaitForValueTask(secondPush);
            yield return WaitForValueTask(stateStack.WaitForTransitionCompletionAsync());

            Assert.AreEqual(0, diagnostics.TransitionQueueDepth);
            Assert.AreEqual(0, diagnostics.PendingDeferredTransitions);
            Assert.AreEqual(1, diagnostics.LifetimeDeferredTransitions);
            Assert.That(
                diagnostics.MaxTransitionQueueDepth,
                Is.GreaterThanOrEqualTo(1)
            );
            Assert.That(
                diagnostics.AverageTransitionQueueDepth,
                Is.GreaterThanOrEqualTo(0f)
            );
        }

        private static IEnumerator WaitForValueTask(ValueTask valueTask)
        {
            Task task = valueTask.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsCanceled)
            {
                throw new OperationCanceledException();
            }
            if (task.IsFaulted)
            {
                Exception exception = task.Exception;
                Exception inner = exception != null ? exception.InnerException : null;
                throw inner ?? exception;
            }
        }

        private static IEnumerator WaitForValueTask<T>(ValueTask<T> valueTask, Action<T> onCompleted)
        {
            Task<T> task = valueTask.AsTask();
            while (!task.IsCompleted)
            {
                yield return null;
            }
            if (task.IsCanceled)
            {
                throw new OperationCanceledException();
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

            public void Tick(TickMode mode, float delta) { }

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
