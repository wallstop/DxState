namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;

    public class Transition<T>
    {
        public readonly T from;
        public readonly T to;
        public readonly Func<bool> rule;

        public TransitionContext Context => _context;

        private readonly TransitionContext _context;

        public Transition(T from, T to, Func<bool> rule, TransitionContext context = default)
        {
            this.from = from;
            this.to = to;
            this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
            _context = context;
        }
    }
}
