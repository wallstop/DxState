namespace WallstopStudios.DxState.Tests.EditMode.State.Machine
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Machine.Component;

    public sealed class StateMachineBuilderTests
    {
        [Test]
        public void BuildCreatesStateMachineAndAppliesTransitions()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            StateMachineBuilder<TestState> builder = new StateMachineBuilder<TestState>();
            ToggleRule rule = new ToggleRule(true);
            builder.AddTransition(first, second, rule, new TransitionContext(TransitionCause.RuleSatisfied));

            StateMachine<TestState> machine = builder.Build(first);
            machine.Update();

            Assert.AreSame(second, machine.CurrentState);
            Assert.IsFalse(first.IsActive);
            Assert.IsTrue(second.IsActive);
        }

        [Test]
        public void AddTransitionThrowsWhenDuplicateInstanceProvided()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            Func<bool> rule = () => true;
            Transition<TestState> transition = new Transition<TestState>(first, second, rule);
            StateMachineBuilder<TestState> builder = new StateMachineBuilder<TestState>();
            builder.AddTransition(transition);

            Assert.Throws<InvalidOperationException>(() => builder.AddTransition(transition));
        }

        [Test]
        public void TransitionsPropertyExposesReadOnlyView()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            Func<bool> rule = () => true;
            StateMachineBuilder<TestState> builder = new StateMachineBuilder<TestState>();
            builder.AddTransition(first, second, rule);

            IReadOnlyList<Transition<TestState>> transitions = builder.Transitions;
            IList<Transition<TestState>> listInterface = (IList<Transition<TestState>>)transitions;

            Assert.AreEqual(1, transitions.Count);
            Assert.Throws<NotSupportedException>(() => listInterface.RemoveAt(0));
        }

        [Test]
        public void ComponentTransitionsRespectShouldEnter()
        {
            TestComponentState idle = new TestComponentState("Idle")
            {
                AllowEnter = true,
            };
            TestComponentState active = new TestComponentState("Active")
            {
                AllowEnter = false,
            };

            StateMachineBuilder<IStateComponent> builder = new StateMachineBuilder<IStateComponent>();
            builder.AddTransition(new ComponentStateTransition(idle, active));

            StateMachine<IStateComponent> machine = builder.Build(idle);

            machine.Update();
            Assert.AreSame(idle, machine.CurrentState);

            active.AllowEnter = true;
            machine.Update();

            Assert.AreSame(active, machine.CurrentState);
            Assert.IsFalse(idle.IsActive);
            Assert.IsTrue(active.IsActive);
        }

        private sealed class TestState : IStateContext<TestState>
        {
            private readonly string _name;
            private bool _isActive;

            public TestState(string name)
            {
                _name = name ?? throw new ArgumentNullException(nameof(name));
            }

            public StateMachine<TestState> StateMachine { get; set; }

            public bool IsActive => _isActive;

            public void Enter()
            {
                _isActive = true;
            }

            public void Exit()
            {
                _isActive = false;
            }

            public void Log(FormattableString message)
            {
            }

            public override string ToString()
            {
                return _name;
            }
        }

        private readonly struct ToggleRule : ITransitionRule
        {
            private readonly bool _shouldTransition;

            public ToggleRule(bool shouldTransition)
            {
                _shouldTransition = shouldTransition;
            }

            public bool Evaluate()
            {
                return _shouldTransition;
            }
        }

        private sealed class TestComponentState : IStateComponent
        {
            private readonly string _name;
            private bool _isActive;

            public TestComponentState(string name)
            {
                _name = name ?? throw new ArgumentNullException(nameof(name));
            }

            public bool AllowEnter { get; set; }

            public StateMachine<IStateComponent> StateMachine { get; set; }

            public bool IsActive => _isActive;

            public bool ShouldEnter()
            {
                return AllowEnter && !_isActive;
            }

            public void Enter()
            {
                if (_isActive)
                {
                    return;
                }

                _isActive = true;
            }

            public void Exit()
            {
                if (!_isActive)
                {
                    return;
                }

                _isActive = false;
            }

            public void Log(FormattableString message)
            {
            }

            public override string ToString()
            {
                return _name;
            }
        }
    }
}
