namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;

    public class Transition<T>
    {
        public readonly T from;
        public readonly T to;
        public readonly Func<bool> rule;
        public readonly ITransitionRule ruleStruct;

        private readonly TransitionContext _context;

        public TransitionContext Context => _context;

        public Transition(T from, T to, Func<bool> rule, TransitionContext context = default)
        {
            this.from = from;
            this.to = to;
            this.rule = rule ?? throw new ArgumentNullException(nameof(rule));
            ruleStruct = null;
            _context = context;
        }

        public Transition(T from, T to, ITransitionRule ruleStruct, TransitionContext context = default)
        {
            if (ruleStruct == null)
            {
                throw new ArgumentNullException(nameof(ruleStruct));
            }

            this.from = from;
            this.to = to;
            rule = null;
            this.ruleStruct = ruleStruct;
            _context = context;
        }

        public bool Evaluate()
        {
            if (ruleStruct != null)
            {
                return ruleStruct.Evaluate();
            }

            return rule != null && rule.Invoke();
        }
    }
}

