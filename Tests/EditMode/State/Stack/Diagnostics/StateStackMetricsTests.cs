namespace WallstopStudios.DxState.Tests.EditMode.State.Stack.Diagnostics
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public sealed class StateStackMetricsTests
    {
        [UnityTest]
        public IEnumerator TracksTransitionDurations()
        {
            StateStack stack = new StateStack();
            using StateStackDiagnostics diagnostics = new StateStackDiagnostics(stack, 8, false);
            TestState first = new TestState("First");
            TestState second = new TestState("Second");

            yield return stack.PushAsync(first).AsIEnumerator();
            yield return stack.PushAsync(second).AsIEnumerator();
            yield return stack.PopAsync().AsIEnumerator();

            Assert.GreaterOrEqual(diagnostics.TransitionCount, 1);
            Assert.GreaterOrEqual(diagnostics.LongestTransitionDuration, 0f);
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

    internal static class ValueTaskExtensions
    {
        public static IEnumerator AsIEnumerator(this ValueTask task)
        {
            Task inner = task.AsTask();
            while (!inner.IsCompleted)
            {
                yield return null;
            }

            if (inner.IsFaulted)
            {
                throw inner.Exception;
            }
        }

        public static IEnumerator AsIEnumerator<T>(this ValueTask<T> task)
        {
            Task inner = task.AsTask();
            while (!inner.IsCompleted)
            {
                yield return null;
            }

            if (inner.IsFaulted)
            {
                throw inner.Exception;
            }
        }
    }
}
