namespace WallstopStudios.DxState.Tests.EditMode.State.Machine
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Machine.Component;

    public sealed class StateMachineTests
    {
        [Test]
        public void ForceTransitionWithinEnterIsQueued()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            TestState third = new TestState("Third");
            List<Transition<TestState>> transitions = new List<Transition<TestState>>();
            StateMachine<TestState> machine = new StateMachine<TestState>(transitions, first);
            List<TransitionExecutionContext<TestState>> history = new List<TransitionExecutionContext<TestState>>();
            machine.TransitionExecuted += context => history.Add(context);

            bool secondExited = false;
            second.OnExitAction = () => secondExited = true;
            bool thirdEntered = false;
            third.OnEnterAction = () => thirdEntered = true;
            second.OnEnterAction = () =>
            {
                TransitionContext chainedContext = new TransitionContext(
                    TransitionCause.Manual,
                    TransitionFlags.ExternalRequest
                );
                second.StateMachine.ForceTransition(third, chainedContext);
            };

            TransitionContext initialContext = new TransitionContext(
                TransitionCause.Manual,
                TransitionFlags.ExternalRequest
            );
            machine.ForceTransition(second, initialContext);

            Assert.AreSame(third, machine.CurrentState);
            Assert.IsFalse(second.IsActive);
            Assert.IsTrue(third.IsActive);
            Assert.IsTrue(secondExited);
            Assert.IsTrue(thirdEntered);
            Assert.AreEqual(2, history.Count);
            Assert.AreSame(second, history[0].CurrentState);
            Assert.AreSame(third, history[1].CurrentState);
            Assert.AreEqual(TransitionCause.Manual, history[0].Context.Cause);
            Assert.AreEqual(TransitionFlags.ExternalRequest, history[0].Context.Flags);
            Assert.AreEqual(TransitionCause.Manual, history[1].Context.Cause);
            Assert.AreEqual(TransitionFlags.ExternalRequest, history[1].Context.Flags);
        }

        [Test]
        public void UpdateProcessesRuleAndExecutesTransition()
        {
            bool shouldTransition = false;
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            Transition<TestState> toSecond = new Transition<TestState>(
                first,
                second,
                () => shouldTransition
            );
            List<Transition<TestState>> transitions = new List<Transition<TestState>>
            {
                toSecond,
            };

            StateMachine<TestState> machine = new StateMachine<TestState>(transitions, first);
            shouldTransition = true;
            machine.Update();

            Assert.AreSame(second, machine.CurrentState);
            Assert.IsFalse(first.IsActive);
            Assert.IsTrue(second.IsActive);
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

            public Action OnEnterAction { get; set; }

            public Action OnExitAction { get; set; }

            public void Enter()
            {
                if (_isActive)
                {
                    return;
                }

                _isActive = true;
                Action enterAction = OnEnterAction;
                if (enterAction != null)
                {
                    enterAction.Invoke();
                }
            }

            public void Exit()
            {
                if (!_isActive)
                {
                    return;
                }

                _isActive = false;
                Action exitAction = OnExitAction;
                if (exitAction != null)
                {
                    exitAction.Invoke();
                }
            }

            public void Log(FormattableString message)
            {
            }
        }
    }
}
