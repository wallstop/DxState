namespace WallstopStudios.DxState.Tests.EditMode.State.Machine.Diagnostics
{
    using System;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Machine.Component;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public sealed class StateMachineDiagnosticsTests
    {
        [Test]
        public void RecordsRecentTransitions()
        {
            TestState idle = new TestState("Idle");
            TestState active = new TestState("Active");
            Transition<TestState> transition = new Transition<TestState>(
                idle,
                active,
                new ToggleRule(true),
                new TransitionContext(TransitionCause.RuleSatisfied)
            );

            StateMachine<TestState> machine = new StateMachine<TestState>(
                new[] { transition },
                idle
            );

            StateMachineDiagnostics<TestState> diagnostics = new StateMachineDiagnostics<TestState>(
                4
            );
            machine.AttachDiagnostics(diagnostics);

            machine.Update();

            Assert.AreEqual(1, diagnostics.RecentTransitions.Count);
            foreach (TransitionExecutionContext<TestState> context in diagnostics.RecentTransitions)
            {
                Assert.AreSame(idle, context.PreviousState);
                Assert.AreSame(active, context.CurrentState);
            }
        }

        private sealed class TestState : IStateContext<TestState>
        {
            private readonly string _name;

            public TestState(string name)
            {
                _name = name;
            }

            public StateMachine<TestState> StateMachine { get; set; }

            public bool IsActive { get; private set; }

            public void Enter()
            {
                IsActive = true;
            }

            public void Exit()
            {
                IsActive = false;
            }

            public void Log(FormattableString message) { }
        }

        private readonly struct ToggleRule : ITransitionRule
        {
            private readonly bool _value;

            public ToggleRule(bool value)
            {
                _value = value;
            }

            public bool Evaluate()
            {
                return _value;
            }
        }
    }
}
