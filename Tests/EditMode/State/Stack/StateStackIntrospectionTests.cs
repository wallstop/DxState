namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;

    public sealed class StateStackIntrospectionTests
    {
        [UnityTest]
        public IEnumerator CopyMethodsExposeCurrentStateAndHistory()
        {
            StateStack stack = new StateStack();
            TestState first = new TestState("First");
            TestState second = new TestState("Second");

            yield return WaitForValueTask(stack.PushAsync(first));
            yield return WaitForValueTask(stack.PushAsync(second));
            yield return WaitForValueTask(stack.PopAsync());
            yield return WaitForValueTask(stack.WaitForTransitionCompletionAsync());

            List<IState> stackSnapshot = new List<IState>();
            stack.CopyStack(stackSnapshot);
            Assert.AreEqual(1, stackSnapshot.Count);
            Assert.AreSame(first, stackSnapshot[0]);

            List<IState> registeredStates = new List<IState>();
            stack.CopyRegisteredStates(registeredStates);
            Assert.AreEqual(2, registeredStates.Count);

            List<StateStack.StateStackTransitionRecord> history =
                new List<StateStack.StateStackTransitionRecord>();
            stack.CopyTransitionHistory(history, 1);
            Assert.AreEqual(1, history.Count);
        }

        private static IEnumerator WaitForValueTask(ValueTask task)
        {
            Task awaited = task.AsTask();
            while (!awaited.IsCompleted)
            {
                yield return null;
            }

            if (awaited.IsFaulted)
            {
                Exception exception = awaited.Exception;
                Exception inner = exception != null ? exception.InnerException : null;
                throw inner ?? exception;
            }

            if (awaited.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }

        private static IEnumerator WaitForValueTask<T>(ValueTask<T> task)
        {
            Task<T> awaited = task.AsTask();
            while (!awaited.IsCompleted)
            {
                yield return null;
            }

            if (awaited.IsFaulted)
            {
                Exception exception = awaited.Exception;
                Exception inner = exception != null ? exception.InnerException : null;
                throw inner ?? exception;
            }

            if (awaited.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }

        private sealed class TestState : IState
        {
            private readonly string _name;

            public TestState(string name)
            {
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
