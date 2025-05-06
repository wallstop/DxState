namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;

    public sealed class ComponentStateTransition : Transition<StateComponent>
    {
        public ComponentStateTransition(StateComponent from, StateComponent to, Func<bool> rule)
            : base(from, to, rule != null ? () => rule() && to.ShouldEnter() : to.ShouldEnter) { }
    }
}
