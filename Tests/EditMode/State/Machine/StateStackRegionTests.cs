namespace WallstopStudios.DxState.Tests.EditMode.State.Machine
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Stack;

    public sealed class StateStackRegionTests
    {
        [Test]
        public void ActivatePushesStateAndDeactivateRemoves()
        {
            StateStack stack = new StateStack();
            RecordingState state = new RecordingState("Menu");
            StateStackRegion region = new StateStackRegion(stack, state);

            region.Activate(new TransitionContext(TransitionCause.Manual));

            Assert.AreSame(state, stack.CurrentState);

            region.Deactivate(new TransitionContext(TransitionCause.Manual));

            Assert.AreEqual(0, stack.Stack.Count);
        }

        [Test]
        public void FlattenOnActivateReactivatesTargetState()
        {
            StateStack stack = new StateStack();
            RecordingState baseState = new RecordingState("Base");
            RecordingState overlay = new RecordingState("Overlay");

            stack.PushAsync(overlay).GetAwaiter().GetResult();
            stack.PushAsync(baseState).GetAwaiter().GetResult();

            StateStackRegion region = new StateStackRegion(
                stack,
                baseState,
                flattenOnActivate: true,
                removeOnDeactivate: false
            );

            stack.PushAsync(overlay).GetAwaiter().GetResult();
            Assert.AreSame(overlay, stack.CurrentState);

            region.Activate(new TransitionContext(TransitionCause.Manual));

            Assert.AreSame(baseState, stack.CurrentState);
        }

        [Test]
        public void PriorityExposesConfiguredValue()
        {
            StateStack stack = new StateStack();
            RecordingState state = new RecordingState("Menu");
            StateStackRegion region = new StateStackRegion(stack, state, priority: 5);

            Assert.AreEqual(5, region.Priority);
        }

        private sealed class RecordingState : IState
        {
            public RecordingState(string name)
            {
                Name = name;
            }

            public string Name { get; }

            public TickMode TickMode => TickMode.None;

            public float? TimeInState => null;

            public bool TickWhenInactive => false;

            public ValueTask Enter<TProgress>(
                IState previousState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return default;
            }

            public void Tick(TickMode mode, float delta) { }

            public ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return default;
            }

            public ValueTask Remove<TProgress>(
                IReadOnlyList<IState> previousStatesInStack,
                IReadOnlyList<IState> nextStatesInStack,
                TProgress progress
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return default;
            }
        }
    }
}
