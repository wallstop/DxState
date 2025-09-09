namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;

    public class Transition<T>
    {
        public readonly T from;
        public readonly T to;
        public readonly Func<bool> rule;

        public Transition(T from, T to, Func<bool> rule)
        {
            this.from = from;
            this.to = to;
            this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
        }
    }
}
