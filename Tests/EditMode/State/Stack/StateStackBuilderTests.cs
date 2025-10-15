namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;

    public sealed class StateStackBuilderTests
    {
        [Test]
        public void BuildThrowsIfNoStatesAdded()
        {
            StateStackBuilder builder = new StateStackBuilder();
            Assert.Throws<InvalidOperationException>(() => builder.Build());
        }

        [UnityTest]
        public IEnumerator ApplyRegistersStatesAndPushesInitial()
        {
            StateStackBuilder builder = new StateStackBuilder();
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            builder.WithState(first, true).WithState(second);
            StateStackConfiguration configuration = builder.Build();
            StateStack stack = new StateStack();

            yield return WaitForValueTask(configuration.ApplyAsync(stack, forceRegister: true));

            Assert.IsTrue(stack.RegisteredStates.ContainsKey(first.Name));
            Assert.IsTrue(stack.RegisteredStates.ContainsKey(second.Name));
            Assert.AreSame(first, stack.CurrentState);
        }

        [UnityTest]
        public IEnumerator ApplyFlattensWhenInitialAlreadyInStack()
        {
            StateStackBuilder builder = new StateStackBuilder();
            TestState initial = new TestState("Initial");
            TestState other = new TestState("Other");
            builder.WithState(initial, true).WithState(other);
            StateStackConfiguration configuration = builder.Build();
            StateStack stack = new StateStack();

            yield return WaitForValueTask(stack.PushAsync(initial));
            yield return WaitForValueTask(stack.PushAsync(other));

            yield return WaitForValueTask(configuration.ApplyAsync(stack, forceRegister: true));

            Assert.AreSame(initial, stack.CurrentState);
            Assert.AreEqual(1, stack.Stack.Count);
        }

        private static IEnumerator WaitForValueTask(ValueTask task)
        {
            System.Threading.Tasks.Task awaited = task.AsTask();
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
                return default;
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
                return default;
            }

            public ValueTask Remove<TProgress>(
                IReadOnlyList<IState> previousStatesInStack,
                IReadOnlyList<IState> nextStatesInStack,
                TProgress progress
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return default;
            }
        }
    }
}
