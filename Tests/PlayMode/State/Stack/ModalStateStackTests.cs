namespace WallstopStudios.DxState.Tests.Runtime.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.Runtime.TestSupport;

    public sealed class ModalStateStackTests
    {
        [UnityTest]
        public IEnumerator EnterPushesInitialStates()
        {
            StateStack modalStack = new StateStack();
            List<IState> initial = new List<IState>
            {
                new StubState("ModalOne"),
                new StubState("ModalTwo"),
            };

            ModalStateStack state = new ModalStateStack(
                "ModalHost",
                modalStack,
                initial
            );

            yield return ValueTaskTestHelpers.WaitForValueTask(
                state.Enter(null, new Progress<float>(_ => { }), StateDirection.Forward)
            );

            Assert.AreEqual(2, modalStack.Stack.Count);
        }

        [UnityTest]
        public IEnumerator ExitClearsStackWhenConfigured()
        {
            StateStack modalStack = new StateStack();
            List<IState> initial = new List<IState> { new StubState("Modal") };
            ModalStateStack state = new ModalStateStack(
                "ModalHost",
                modalStack,
                initial,
                clearOnExit: true
            );
            yield return ValueTaskTestHelpers.WaitForValueTask(
                state.Enter(null, new Progress<float>(_ => { }), StateDirection.Forward)
            );

            yield return ValueTaskTestHelpers.WaitForValueTask(
                state.Exit(null, new Progress<float>(_ => { }), StateDirection.Forward)
            );

            Assert.AreEqual(0, modalStack.Stack.Count);
        }

        private sealed class StubState : IState
        {
            private readonly string _name;

            public StubState(string name)
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
                progress?.Report(1f);
                return default;
            }

            public void Tick(TickMode mode, float delta)
            {
            }

            public ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress?.Report(1f);
                return default;
            }

            public ValueTask Remove<TProgress>(
                IReadOnlyList<IState> previousStatesInStack,
                IReadOnlyList<IState> nextStatesInStack,
                TProgress progress
            )
                where TProgress : IProgress<float>
            {
                progress?.Report(1f);
                return default;
            }
        }
    }
}
