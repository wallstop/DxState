namespace WallstopStudios.DxState.Tests.EditMode.State.Machine
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Machine.Component;
    using WallstopStudios.DxState.State.Machine.Trigger;

    public sealed class TriggerStateMachineTests
    {
        [Test]
        public void UpdateTransitionsWhenTriggerProvided()
        {
            TestTriggerState idleState = new TestTriggerState(StateId.Idle);
            TestTriggerState activeState = new TestTriggerState(StateId.Active);
            List<ITriggerState<StateId, string>> states = new List<ITriggerState<StateId, string>>
            {
                idleState,
                activeState,
            };
            List<TriggerStateTransition<StateId, string>> transitions =
                new List<TriggerStateTransition<StateId, string>>
            {
                new TriggerStateTransition<StateId, string>(StateId.Idle, "Activate", StateId.Active),
            };

            TriggerStateMachine<StateId, string> machine =
                new TriggerStateMachine<StateId, string>(states, transitions, StateId.Idle);
            List<TriggerTransitionExecutionContext<StateId, string>> history =
                new List<TriggerTransitionExecutionContext<StateId, string>>();
            machine.TransitionExecuted += context => history.Add(context);

            idleState.QueueTrigger("Activate");
            machine.Update();

            Assert.AreEqual(StateId.Active, machine.CurrentStateId);
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(StateId.Idle, history[0].PreviousState);
            Assert.AreEqual(StateId.Active, history[0].CurrentState);
            Assert.AreEqual(TransitionCause.RuleSatisfied, history[0].Context.Cause);
            Assert.AreEqual(1, idleState.ExitCalls.Count);
            Assert.AreEqual(1, activeState.EnterCalls.Count);
        }

        [Test]
        public void ForceTransitionHonorsProvidedContext()
        {
            TestTriggerState idleState = new TestTriggerState(StateId.Idle);
            TestTriggerState standbyState = new TestTriggerState(StateId.Standby);
            List<ITriggerState<StateId, string>> states = new List<ITriggerState<StateId, string>>
            {
                idleState,
                standbyState,
            };
            List<TriggerStateTransition<StateId, string>> transitions =
                new List<TriggerStateTransition<StateId, string>>();
            TriggerStateMachine<StateId, string> machine =
                new TriggerStateMachine<StateId, string>(states, transitions, StateId.Idle);
            List<TriggerTransitionExecutionContext<StateId, string>> history =
                new List<TriggerTransitionExecutionContext<StateId, string>>();
            machine.TransitionExecuted += context => history.Add(context);

            TransitionContext forceContext = new TransitionContext(
                TransitionCause.Manual,
                TransitionFlags.ExternalRequest
            );
            machine.ForceTransition(StateId.Standby, forceContext);

            Assert.AreEqual(StateId.Standby, machine.CurrentStateId);
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(TransitionCause.Manual, history[0].Context.Cause);
            Assert.AreEqual(TransitionFlags.ExternalRequest, history[0].Context.Flags);
        }

        [Test]
        public void ReentrantTriggerQueuesTransitionsSafely()
        {
            TestTriggerState idleState = new TestTriggerState(StateId.Idle);
            TestTriggerState activeState = new TestTriggerState(StateId.Active)
            {
                TriggerOnEnter = "Standby",
            };
            TestTriggerState standbyState = new TestTriggerState(StateId.Standby);
            List<ITriggerState<StateId, string>> states = new List<ITriggerState<StateId, string>>
            {
                idleState,
                activeState,
                standbyState,
            };
            List<TriggerStateTransition<StateId, string>> transitions =
                new List<TriggerStateTransition<StateId, string>>
            {
                new TriggerStateTransition<StateId, string>(StateId.Idle, "Activate", StateId.Active),
                new TriggerStateTransition<StateId, string>(StateId.Active, "Standby", StateId.Standby),
            };
            TriggerStateMachine<StateId, string> machine =
                new TriggerStateMachine<StateId, string>(states, transitions, StateId.Idle);

            idleState.QueueTrigger("Activate");
            machine.Update();

            Assert.AreEqual(StateId.Standby, machine.CurrentStateId);
            Assert.AreEqual(1, idleState.ExitCalls.Count);
            Assert.AreEqual(1, activeState.ExitCalls.Count);
            Assert.AreEqual(1, standbyState.EnterCalls.Count);
        }

        [Test]
        public void TransitionTriggerStateEvaluatesUnderlyingTransitions()
        {
            bool shouldActivate = false;
            Transition<StateId> toActive = new Transition<StateId>(
                StateId.Idle,
                StateId.Active,
                () => shouldActivate,
                new TransitionContext(TransitionCause.RuleSatisfied)
            );
            List<Transition<StateId>> idleTransitions = new List<Transition<StateId>>
            {
                toActive,
            };
            TransitionTriggerState<StateId> idleState = new TransitionTriggerState<StateId>(
                StateId.Idle,
                idleTransitions
            );
            TransitionTriggerState<StateId> activeState = new TransitionTriggerState<StateId>(
                StateId.Active,
                Array.Empty<Transition<StateId>>()
            );
            List<ITriggerState<StateId, StateId>> states = new List<ITriggerState<StateId, StateId>>
            {
                idleState,
                activeState,
            };
            List<TriggerStateTransition<StateId, StateId>> transitions =
                new List<TriggerStateTransition<StateId, StateId>>
            {
                new TriggerStateTransition<StateId, StateId>(StateId.Idle, StateId.Active, StateId.Active),
            };

            TriggerStateMachine<StateId, StateId> machine =
                new TriggerStateMachine<StateId, StateId>(states, transitions, StateId.Idle);

            machine.Update();
            Assert.AreEqual(StateId.Idle, machine.CurrentStateId);

            shouldActivate = true;
            machine.Update();

            Assert.AreEqual(StateId.Active, machine.CurrentStateId);
        }

        private enum StateId
        {
            Idle = 0,
            Active = 1,
            Standby = 2,
        }

        private sealed class TestTriggerState : ITriggerState<StateId, string>
        {
            private readonly Queue<QueuedTrigger> _queuedTriggers;

            public TestTriggerState(StateId id)
            {
                Id = id;
                _queuedTriggers = new Queue<QueuedTrigger>();
                EnterCalls = new List<TriggerLifecycleCall>();
                ExitCalls = new List<TriggerLifecycleCall>();
            }

            public StateId Id { get; }

            public string TriggerOnEnter { get; set; }

            public List<TriggerLifecycleCall> EnterCalls { get; }

            public List<TriggerLifecycleCall> ExitCalls { get; }

            public void QueueTrigger(string trigger)
            {
                QueueTrigger(trigger, default);
            }

            public void QueueTrigger(string trigger, TransitionContext context)
            {
                QueuedTrigger queued = new QueuedTrigger(trigger, context);
                _queuedTriggers.Enqueue(queued);
            }

            public bool TryGetTrigger(out string trigger, out TransitionContext context)
            {
                if (_queuedTriggers.Count == 0)
                {
                    trigger = null;
                    context = default;
                    return false;
                }

                QueuedTrigger dequeued = _queuedTriggers.Dequeue();
                trigger = dequeued.Trigger;
                context = dequeued.Context;
                return true;
            }

            public void OnEnter(
                TriggerStateMachine<StateId, string> machine,
                StateId previousState,
                TransitionContext context
            )
            {
                TriggerLifecycleCall call = new TriggerLifecycleCall(previousState, context);
                EnterCalls.Add(call);
                if (!string.IsNullOrEmpty(TriggerOnEnter))
                {
                    TransitionContext chainedContext = new TransitionContext(TransitionCause.RuleSatisfied);
                    QueueTrigger(TriggerOnEnter, chainedContext);
                }
            }

            public void OnExit(
                TriggerStateMachine<StateId, string> machine,
                StateId nextState,
                TransitionContext context
            )
            {
                TriggerLifecycleCall call = new TriggerLifecycleCall(nextState, context);
                ExitCalls.Add(call);
            }

            public void Tick() { }

            private readonly struct QueuedTrigger
            {
                public QueuedTrigger(string trigger, TransitionContext context)
                {
                    Trigger = trigger;
                    Context = context;
                }

                public string Trigger { get; }

                public TransitionContext Context { get; }
            }
        }

        private readonly struct TriggerLifecycleCall
        {
            public TriggerLifecycleCall(StateId relatedState, TransitionContext context)
            {
                RelatedState = relatedState;
                Context = context;
            }

            public StateId RelatedState { get; }

            public TransitionContext Context { get; }
        }
    }
}
