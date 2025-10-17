namespace WallstopStudios.DxState.Tests.EditMode.State.Machine
{
    using System.Collections.Generic;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Machine.Component;

    public sealed class StateMachineIntrospectionTests
    {
        [Test]
        public void ExposesStateAndTransitionSnapshots()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");

            StateMachineBuilder<TestState> builder = new StateMachineBuilder<TestState>();
            builder.AddTransition(first, second, () => true);
            builder.RentAnyTransition(first, () => false);
            builder.RentAnyTransition(second, () => true);

            StateMachine<TestState> machine = builder.Build(first);

            List<TestState> states = new List<TestState>();
            machine.CopyStates(states);
            CollectionAssert.Contains(states, first);
            CollectionAssert.Contains(states, second);

            List<Transition<TestState>> outgoing = new List<Transition<TestState>>();
            machine.CopyTransitionsForState(first, outgoing);
            Assert.AreEqual(1, outgoing.Count);
            Assert.AreEqual(second, outgoing[0].to);

            List<Transition<TestState>> globalTransitions = new List<Transition<TestState>>();
            machine.CopyGlobalTransitions(globalTransitions);
            Assert.AreEqual(2, globalTransitions.Count);

            Dictionary<TestState, List<Transition<TestState>>> graph = new Dictionary<TestState, List<Transition<TestState>>>();
            machine.BuildTransitionGraph(graph);
            Assert.IsTrue(graph.ContainsKey(first));
            Assert.IsTrue(graph.ContainsKey(second));
        }

        [Test]
        public void CopyTransitionHistoryRespectsLimit()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            StateMachineBuilder<TestState> builder = new StateMachineBuilder<TestState>();
            builder.AddTransition(first, second, () => true);

            StateMachine<TestState> machine = builder.Build(first);
            machine.Update();

            List<TransitionExecutionContext<TestState>> history = new List<TransitionExecutionContext<TestState>>();
            machine.CopyTransitionHistory(history, 1);
            Assert.AreEqual(1, history.Count);
        }

        [Test]
        public void CopyActiveRegionsReturnsEmptyWhenNoRegions()
        {
            TestState first = new TestState("First");
            StateMachine<TestState> machine = new StateMachineBuilder<TestState>()
                .Build(first);

            List<IStateRegion> regions = new List<IStateRegion>();
            machine.CopyActiveRegions(regions);
            Assert.AreEqual(0, regions.Count);
            Assert.AreEqual(0, machine.PendingTransitionQueueDepth);
        }

        [Test]
        public void TracksPendingTransitionQueueMetrics()
        {
            TestState first = new TestState("First");
            TestState second = new TestState("Second");
            TestState third = new TestState("Third");

            StateMachineBuilder<TestState> builder = new StateMachineBuilder<TestState>();
            builder.AddTransition(first, second, () => true);
            builder.AddTransition(second, third, () => true);

            StateMachine<TestState> machine = builder.Build(first);
            machine.ResetPendingTransitionQueueMetrics();

            int transitionCount = 0;
            machine.TransitionExecuted += context =>
            {
                transitionCount++;
                if (transitionCount == 1)
                {
                    machine.ForceTransition(second, default);
                    machine.ForceTransition(third, default);
                }
            };

            machine.ForceTransition(second, default);

            Assert.GreaterOrEqual(machine.MaxPendingTransitionQueueDepth, 2);
            Assert.Greater(machine.AveragePendingTransitionQueueDepth, 0f);

            machine.ResetPendingTransitionQueueMetrics();
            Assert.AreEqual(0, machine.MaxPendingTransitionQueueDepth);
            Assert.AreEqual(0f, machine.AveragePendingTransitionQueueDepth);
        }

        private sealed class TestState : IStateContext<TestState>
        {
            private readonly string _name;
            private bool _isActive;

            public TestState(string name)
            {
                _name = name;
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

            public void Log(System.FormattableString message)
            {
            }

            public override string ToString()
            {
                return _name;
            }
        }
    }
}
