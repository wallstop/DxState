namespace WallstopStudios.DxState.Tests.EditMode.State.Machine
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;

    public sealed class HierarchicalStateMachineTests
    {
        [Test]
        public void ActivatesAndDeactivatesRegionsDuringTransitions()
        {
            TrackingRegion firstRegion = new TrackingRegion();
            TrackingRegion secondRegion = new TrackingRegion();

            HierarchicalTestState hierarchicalState = new HierarchicalTestState(
                "Hierarchical",
                new List<IStateRegion> { firstRegion, secondRegion },
                true
            );
            PlainTestState plainState = new PlainTestState("Plain");

            bool transitionToPlain = false;
            bool transitionToHierarchical = false;

            StateMachineBuilder<ITestState> builder = new StateMachineBuilder<ITestState>();
            builder
                .AddTransition(
                    hierarchicalState,
                    plainState,
                    () => transitionToPlain
                )
                .AddTransition(
                    plainState,
                    hierarchicalState,
                    () => transitionToHierarchical
                );

            StateMachine<ITestState> stateMachine = builder.Build(hierarchicalState);

            Assert.AreEqual(1, hierarchicalState.EnterCount, "Hierarchical state should be entered on initialization.");
            Assert.AreEqual(1, firstRegion.ActivateCount, "First region should be activated on initialization.");
            Assert.AreEqual(1, secondRegion.ActivateCount, "Second region should be activated on initialization.");

            stateMachine.Update();
            Assert.AreEqual(1, firstRegion.UpdateCount, "Regions should be updated during state machine update.");
            Assert.AreEqual(1, secondRegion.UpdateCount, "Regions should be updated during state machine update.");

            transitionToPlain = true;
            stateMachine.Update();

            Assert.AreSame(plainState, stateMachine.CurrentState);
            Assert.AreEqual(1, firstRegion.DeactivateCount, "First region should deactivate when leaving state.");
            Assert.AreEqual(1, secondRegion.DeactivateCount, "Second region should deactivate when leaving state.");

            transitionToPlain = false;
            transitionToHierarchical = true;
            stateMachine.Update();

            Assert.AreSame(hierarchicalState, stateMachine.CurrentState);
            Assert.AreEqual(2, hierarchicalState.EnterCount);
            Assert.AreEqual(2, firstRegion.ActivateCount);
            Assert.AreEqual(2, secondRegion.ActivateCount);
        }

        [Test]
        public void SkipsAutomaticRegionUpdatesWhenRequested()
        {
            TrackingRegion region = new TrackingRegion();
            HierarchicalTestState hierarchicalState = new HierarchicalTestState(
                "Manual",
                new List<IStateRegion> { region },
                false
            );

            StateMachineBuilder<ITestState> builder = new StateMachineBuilder<ITestState>();
            StateMachine<ITestState> stateMachine = builder.Build(hierarchicalState);

            stateMachine.Update();
            Assert.AreEqual(0, region.UpdateCount, "Region updates should be skipped when state opts out.");

            region.Update();
            Assert.AreEqual(1, region.UpdateCount);
        }

        [Test]
        public void StateMachineRegionResetsChildMachineOnActivation()
        {
            LeafState stateA = new LeafState("A");
            LeafState stateB = new LeafState("B");
            bool shouldSwitchToB = false;

            StateMachineBuilder<LeafState> builder = new StateMachineBuilder<LeafState>();
            builder
                .AddTransition(stateA, stateB, () => shouldSwitchToB)
                .AddTransition(stateB, stateA, () => !shouldSwitchToB);

            StateMachine<LeafState> childMachine = builder.Build(stateA);

            StateMachineRegion<LeafState> region = new StateMachineRegion<LeafState>(
                childMachine,
                stateA,
                resetOnActivate: true
            );

            region.Activate(new TransitionContext(TransitionCause.Initialization));
            Assert.AreSame(stateA, childMachine.CurrentState);

            shouldSwitchToB = true;
            childMachine.Update();
            Assert.AreSame(stateB, childMachine.CurrentState);

            shouldSwitchToB = false;
            region.Activate(new TransitionContext(TransitionCause.Manual));
            Assert.AreSame(stateA, childMachine.CurrentState);
        }

        [Test]
        public void StateMachineRegionRestoresHistoryWhenEnabled()
        {
            LeafState stateA = new LeafState("HistoryA");
            LeafState stateB = new LeafState("HistoryB");
            bool shouldSwitchToB = false;

            StateMachineBuilder<LeafState> builder = new StateMachineBuilder<LeafState>();
            builder
                .AddTransition(stateA, stateB, () => shouldSwitchToB)
                .AddTransition(stateB, stateA, () => !shouldSwitchToB);

            StateMachine<LeafState> childMachine = builder.Build(stateA);

            StateMachineRegion<LeafState> region = new StateMachineRegion<LeafState>(
                childMachine,
                stateA,
                resetOnActivate: false,
                activationContext: new TransitionContext(TransitionCause.Manual),
                useHistory: true,
                historyContext: new TransitionContext(TransitionCause.Manual)
            );

            region.Activate(new TransitionContext(TransitionCause.Initialization));
            Assert.AreSame(stateA, childMachine.CurrentState);

            shouldSwitchToB = true;
            childMachine.Update();
            Assert.AreSame(stateB, childMachine.CurrentState);

            region.Deactivate(new TransitionContext(TransitionCause.Manual));

            shouldSwitchToB = false;
            region.Activate(new TransitionContext(TransitionCause.Manual));
            Assert.AreSame(stateB, childMachine.CurrentState);
        }

        private interface ITestState : IStateContext<ITestState>
        {
        }

        private sealed class PlainTestState : ITestState
        {
            private readonly string _name;

            public PlainTestState(string name)
            {
                _name = name;
            }

            public StateMachine<ITestState> StateMachine { get; set; }

            public bool IsActive { get; private set; }

            public int EnterCount { get; private set; }

            public int ExitCount { get; private set; }

            public void Enter()
            {
                IsActive = true;
                EnterCount++;
            }

            public void Exit()
            {
                IsActive = false;
                ExitCount++;
            }

            public void Log(FormattableString message)
            {
            }
        }

        private sealed class HierarchicalTestState : ITestState, IHierarchicalStateContext<ITestState>
        {
            private readonly string _name;
            private readonly IReadOnlyList<IStateRegion> _regions;
            private readonly bool _shouldUpdateRegions;

            public HierarchicalTestState(
                string name,
                IReadOnlyList<IStateRegion> regions,
                bool shouldUpdateRegions
            )
            {
                _name = name;
                _regions = regions;
                _shouldUpdateRegions = shouldUpdateRegions;
            }

            public StateMachine<ITestState> StateMachine { get; set; }

            public bool IsActive { get; private set; }

            public int EnterCount { get; private set; }

            public int ExitCount { get; private set; }

            public IReadOnlyList<IStateRegion> Regions => _regions;

            public bool ShouldUpdateRegions => _shouldUpdateRegions;

            public IStateRegionCoordinator RegionCoordinator => DefaultStateRegionCoordinator.Instance;

            public void Enter()
            {
                IsActive = true;
                EnterCount++;
            }

            public void Exit()
            {
                IsActive = false;
                ExitCount++;
            }

            public void Log(FormattableString message)
            {
            }
        }

        private sealed class TrackingRegion : IStateRegion
        {
            public int ActivateCount { get; private set; }

            public int DeactivateCount { get; private set; }

            public int UpdateCount { get; private set; }

            public void Activate(TransitionContext context)
            {
                ActivateCount++;
            }

            public void Deactivate(TransitionContext context)
            {
                DeactivateCount++;
            }

            public void Update()
            {
                UpdateCount++;
            }
        }

        private sealed class LeafState : IStateContext<LeafState>
        {
            private readonly string _name;

            public LeafState(string name)
            {
                _name = name;
            }

            public StateMachine<LeafState> StateMachine { get; set; }

            public bool IsActive { get; private set; }

            public void Enter()
            {
                IsActive = true;
            }

            public void Exit()
            {
                IsActive = false;
            }

            public void Log(FormattableString message)
            {
            }
        }
    }
}
