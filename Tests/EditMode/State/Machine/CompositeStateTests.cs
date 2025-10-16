namespace WallstopStudios.DxState.Tests.EditMode.State.Machine
{
    using System;
    using System.Collections.Generic;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;

    public sealed class CompositeStateTests
    {
        [Test]
        public void CompositeStateConfiguresRegionsOnlyOnce()
        {
            CountingCompositeState composite = new CountingCompositeState();
            TestRegion region = new TestRegion();
            composite.EnqueueRegion(region);

            StateMachineBuilder<ICompositeState> builder = new StateMachineBuilder<ICompositeState>();
            builder.AddTransition(composite, composite, () => false);

            StateMachine<ICompositeState> stateMachine = builder.Build(composite);
            stateMachine.Update();
            stateMachine.Update();

            Assert.AreEqual(1, composite.ConfigurationCount);
            Assert.IsNotEmpty(composite.Regions);
        }

        [Test]
        public void CompositeStateRegistersRegionsForActivation()
        {
            CountingCompositeState composite = new CountingCompositeState();
            TestRegion region = new TestRegion();
            composite.EnqueueRegion(region);

            PlainCompositeState fallback = new PlainCompositeState();

            StateMachineBuilder<ICompositeState> builder = new StateMachineBuilder<ICompositeState>();
            builder
                .AddTransition(composite, fallback, () => true)
                .AddTransition(fallback, composite, () => false);

            StateMachine<ICompositeState> machine = builder.Build(composite);
            machine.Update();

            Assert.AreEqual(1, region.ActivateCount);
            Assert.AreEqual(1, region.DeactivateCount);
        }

        private interface ICompositeState : IStateContext<ICompositeState>
        {
        }

        private sealed class CountingCompositeState : CompositeState<ICompositeState>, ICompositeState
        {
            private readonly Queue<IStateRegion> _pendingRegions = new Queue<IStateRegion>();

            public int ConfigurationCount { get; private set; }

            public void EnqueueRegion(IStateRegion region)
            {
                if (region == null)
                {
                    return;
                }
                _pendingRegions.Enqueue(region);
            }

            protected override void ConfigureRegions(IList<IStateRegion> regions)
            {
                ConfigurationCount++;
                while (_pendingRegions.Count > 0)
                {
                    regions.Add(_pendingRegions.Dequeue());
                }
            }

            public void Log(FormattableString message)
            {
            }
        }

        private sealed class PlainCompositeState : CompositeState<ICompositeState>, ICompositeState
        {
            protected override void ConfigureRegions(IList<IStateRegion> regions)
            {
            }

            public void Log(FormattableString message)
            {
            }
        }

        private sealed class TestRegion : IStateRegion
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
    }
}
