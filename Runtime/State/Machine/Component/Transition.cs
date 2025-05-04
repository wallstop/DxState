namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;

    public sealed class Transition
    {
        public readonly StateComponent from;
        public readonly StateComponent to;
        public readonly Func<bool> rule;

        public Transition(StateComponent from, StateComponent to, Func<bool> rule = null)
        {
            this.from = from;
            this.to = to;
            this.rule = rule != null ? () => rule() && to.ShouldEnter() : to.ShouldEnter;
        }
    }
}
