namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;

    public sealed class ComponentStateTransition : Transition<IStateComponent>
    {
        public ComponentStateTransition(
            IStateComponent from,
            IStateComponent to,
            Func<bool> rule = null,
            TransitionContext context = default
        )
            : base(
                from,
                to,
                ResolveRule(rule, to),
                context
            ) { }

        private static Func<bool> ResolveRule(Func<bool> rule, IStateComponent target)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            if (rule == null)
            {
                return new Func<bool>(target.ShouldEnter);
            }

            return new Func<bool>(() => rule() && target.ShouldEnter());
        }
    }
}
