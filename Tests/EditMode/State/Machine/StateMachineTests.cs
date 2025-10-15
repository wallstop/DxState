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
            List<(TestState previous, TestState next, TransitionContext context)> deferred = new List<(TestState, TestState, TransitionContext)>();
            machine.TransitionDeferred += (previous, next, transitionContext) =>
            {
                deferred.Add((previous, next, transitionContext));
            };

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
            Assert.AreEqual(1, deferred.Count);
            Assert.AreSame(second, deferred[0].previous);
            Assert.AreSame(third, deferred[0].next);
            Assert.AreEqual(TransitionCause.Manual, deferred[0].context.Cause);
            Assert.AreEqual(TransitionFlags.ExternalRequest, deferred[0].context.Flags);
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

        [Test]
        public void StructBasedRuleExecutesTransition()
        {
            ToggleRule rule = new ToggleRule(true);
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            Transition<TestState> toSecond = new Transition<TestState>(
                first,
                second,
                rule,
                new TransitionContext(TransitionCause.RuleSatisfied)
            );

            StateMachine<TestState> machine = new StateMachine<TestState>(
                new[] { toSecond },
                first
            );

            machine.Update();

            Assert.AreSame(second, machine.CurrentState);
            Assert.IsFalse(first.IsActive);
            Assert.IsTrue(second.IsActive);
        }

        [Test]
        public void TransitionDeferredRaisedWhenNestedTransitionQueued()
        {
            bool shouldTransition = false;
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            TestState third = new TestState("Third");
            Transition<TestState> toSecond = new Transition<TestState>(
                first,
                second,
                () => shouldTransition
            );

            StateMachine<TestState> machine = new StateMachine<TestState>(
                new[] { toSecond },
                first
            );

            int deferredCount = 0;
            machine.TransitionDeferred += (_, _, _) => deferredCount++;

            second.OnEnterAction = () =>
            {
                machine.ForceTransition(
                    third,
                    new TransitionContext(TransitionCause.Manual)
                );
            };

            shouldTransition = true;
            machine.Update();

            Assert.AreEqual(1, deferredCount);
        }

        [Test]
        public void ConstructorThrowsWhenCurrentStateIsNull()
        {
            TestState validState = new TestState("Valid");
            List<Transition<TestState>> transitions = new List<Transition<TestState>>();

            Assert.Throws<ArgumentException>(
                () => new StateMachine<TestState>(transitions, null)
            );
        }

        [Test]
        public void ConstructorThrowsWhenTransitionContainsNullEntry()
        {
            TestState initial = new TestState("Initial");
            List<Transition<TestState>> transitions = new List<Transition<TestState>>
            {
                null,
            };

            Assert.Throws<ArgumentException>(
                () => new StateMachine<TestState>(transitions, initial)
            );
        }

        [Test]
        public void ConstructorThrowsWhenTransitionFromStateIsNull()
        {
            TestState initial = new TestState("Initial");
            TestState validTarget = new TestState("Target");
            Transition<TestState> invalid = new Transition<TestState>(
                null,
                validTarget,
                () => true
            );
            List<Transition<TestState>> transitions = new List<Transition<TestState>>
            {
                invalid,
            };

            Assert.Throws<ArgumentException>(
                () => new StateMachine<TestState>(transitions, initial)
            );
        }

        [Test]
        public void ConstructorThrowsWhenTransitionToStateIsNull()
        {
            TestState initial = new TestState("Initial");
            Transition<TestState> invalid = new Transition<TestState>(
                initial,
                null,
                () => true
            );
            List<Transition<TestState>> transitions = new List<Transition<TestState>>
            {
                invalid,
            };

            Assert.Throws<ArgumentException>(
                () => new StateMachine<TestState>(transitions, initial)
            );
        }

        [Test]
        public void ConstructorThrowsWhenDuplicateTransitionInstanceProvided()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            Transition<TestState> transition = new Transition<TestState>(
                first,
                second,
                () => true
            );
            List<Transition<TestState>> transitions = new List<Transition<TestState>>
            {
                transition,
                transition,
            };

            Assert.Throws<ArgumentException>(
                () => new StateMachine<TestState>(transitions, first)
            );
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
    }
}
