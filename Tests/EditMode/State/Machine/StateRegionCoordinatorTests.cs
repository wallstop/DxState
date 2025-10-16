namespace WallstopStudios.DxState.Tests.EditMode.State.Machine
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;

    public sealed class StateRegionCoordinatorTests
    {
        [Test]
        public void CustomCoordinatorControlsActivationOrder()
        {
            OrderedCoordinator coordinator = new OrderedCoordinator();
            RecordingRegion first = new RecordingRegion("First");
            RecordingRegion second = new RecordingRegion("Second");

            CoordinatedState state = new CoordinatedState(
                new List<IStateRegion> { first, second },
                coordinator
            );
            PlainState fallback = new PlainState();

            StateMachineBuilder<ICoordinatedState> builder = new StateMachineBuilder<ICoordinatedState>();
            builder
                .AddTransition(state, fallback, () => true)
                .AddTransition(fallback, state, () => false);

            StateMachine<ICoordinatedState> machine = builder.Build(state);
            machine.Update();

            CollectionAssert.AreEqual(
                new[] { "First", "Second" },
                coordinator.ActivationOrder,
                "Coordinator should activate regions in specified order."
            );
            CollectionAssert.AreEqual(
                new[] { "Second", "First" },
                coordinator.DeactivationOrder,
                "Coordinator should deactivate regions in reverse order."
            );
        }

        [Test]
        public void PriorityCoordinatorOrdersByRegionPriority()
        {
            List<string> activationLog = new List<string>();
            List<string> deactivationLog = new List<string>();

            IStateRegion high = new PrioritizedRecordingRegion(
                "High",
                priority: 0,
                activationLog,
                deactivationLog
            );
            IStateRegion mid = new PrioritizedRecordingRegion(
                "Mid",
                priority: 5,
                activationLog,
                deactivationLog
            );
            IStateRegion low = new PrioritizedRecordingRegion(
                "Low",
                priority: 10,
                activationLog,
                deactivationLog
            );

            PriorityStateRegionCoordinator coordinator = new PriorityStateRegionCoordinator();
            IReadOnlyList<IStateRegion> regions = new List<IStateRegion> { low, high, mid };

            coordinator.ActivateRegions(regions, new TransitionContext(TransitionCause.Initialization));

            CollectionAssert.AreEqual(
                new[] { "High", "Mid", "Low" },
                activationLog,
                "Activation order should follow ascending priority"
            );

            activationLog.Clear();

            coordinator.DeactivateRegions(regions, new TransitionContext(TransitionCause.Manual));

            CollectionAssert.AreEqual(
                new[] { "Low", "Mid", "High" },
                deactivationLog,
                "Deactivation order should be reverse priority"
            );
        }

        private interface ICoordinatedState : IStateContext<ICoordinatedState>
        {
        }

        private sealed class PlainState : ICoordinatedState
        {
            public StateMachine<ICoordinatedState> StateMachine { get; set; }

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

        private sealed class CoordinatedState : ICoordinatedState, IHierarchicalStateContext<ICoordinatedState>
        {
            private readonly IReadOnlyList<IStateRegion> _regions;
            private readonly IStateRegionCoordinator _coordinator;

            public CoordinatedState(
                IReadOnlyList<IStateRegion> regions,
                IStateRegionCoordinator coordinator
            )
            {
                _regions = regions;
                _coordinator = coordinator;
            }

            public StateMachine<ICoordinatedState> StateMachine { get; set; }

            public bool IsActive { get; private set; }

            public IReadOnlyList<IStateRegion> Regions => _regions;

            public bool ShouldUpdateRegions => false;

            public IStateRegionCoordinator RegionCoordinator => _coordinator;

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

        private class RecordingRegion : IStateRegion
        {
            private readonly string _name;
            private readonly List<string> _activationLog;
            private readonly List<string> _deactivationLog;

            public RecordingRegion(
                string name,
                List<string> activationLog = null,
                List<string> deactivationLog = null
            )
            {
                _name = name;
                _activationLog = activationLog;
                _deactivationLog = deactivationLog;
            }

            public void Activate(TransitionContext context)
            {
                _activationLog?.Add(_name);
            }

            public void Deactivate(TransitionContext context)
            {
                _deactivationLog?.Add(_name);
            }

            public void Update() { }

            public override string ToString()
            {
                return _name;
            }
        }

        private sealed class PrioritizedRecordingRegion : RecordingRegion, IPrioritizedStateRegion
        {
            public PrioritizedRecordingRegion(
                string name,
                int priority,
                List<string> activationLog,
                List<string> deactivationLog
            )
                : base(name, activationLog, deactivationLog)
            {
                Priority = priority;
            }

            public int Priority { get; }
        }

        private sealed class OrderedCoordinator : IStateRegionCoordinator
        {
            public List<string> ActivationOrder { get; } = new List<string>();

            public List<string> DeactivationOrder { get; } = new List<string>();

            public void ActivateRegions(IReadOnlyList<IStateRegion> regions, TransitionContext context)
            {
                for (int i = 0; i < regions.Count; i++)
                {
                    IStateRegion region = regions[i];
                    ActivationOrder.Add(region.ToString());
                    region.Activate(context);
                }
            }

            public void DeactivateRegions(IReadOnlyList<IStateRegion> regions, TransitionContext context)
            {
                for (int i = regions.Count - 1; i >= 0; i--)
                {
                    IStateRegion region = regions[i];
                    DeactivationOrder.Add(region.ToString());
                    region.Deactivate(context);
                }
            }

            public void UpdateRegions(IReadOnlyList<IStateRegion> regions)
            {
            }
        }
    }
}
