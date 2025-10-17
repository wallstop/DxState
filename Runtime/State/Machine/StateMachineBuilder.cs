namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using Component;

    public sealed class StateMachineBuilder<TState>
    {
        private readonly List<Transition<TState>> _transitions;
        private readonly ReadOnlyCollection<Transition<TState>> _transitionsView;
        private readonly HashSet<Transition<TState>> _uniqueTransitions;

        public StateMachineBuilder()
        {
            _transitions = new List<Transition<TState>>();
            _transitionsView = _transitions.AsReadOnly();
            _uniqueTransitions = new HashSet<Transition<TState>>();
        }

        public IReadOnlyList<Transition<TState>> Transitions => _transitionsView;

        public StateMachineBuilder<TState> AddTransition(Transition<TState> transition)
        {
            if (transition == null)
            {
                throw new ArgumentNullException(nameof(transition));
            }

            RegisterTransition(transition);
            return this;
        }

        public StateMachineBuilder<TState> AddTransition(
            TState from,
            TState to,
            Func<bool> rule,
            TransitionContext context = default
        )
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            Transition<TState> transition = new Transition<TState>(from, to, rule, context);
            return AddTransition(transition);
        }

        public StateMachineBuilder<TState> AddTransition(
            TState from,
            TState to,
            ITransitionRule rule,
            TransitionContext context = default
        )
        {
            Transition<TState> transition = new Transition<TState>(from, to, rule, context);
            return AddTransition(transition);
        }

        public StateMachineBuilder<TState> AddAnyTransition(
            TState to,
            Func<bool> rule,
            TransitionContext context = default
        )
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            Transition<TState> transition = new Transition<TState>(
                default,
                to,
                rule,
                context,
                isGlobal: true
            );
            return AddTransition(transition);
        }

        public StateMachineBuilder<TState> AddAnyTransition(
            TState to,
            ITransitionRule rule,
            TransitionContext context = default
        )
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            Transition<TState> transition = new Transition<TState>(
                default,
                to,
                rule,
                context,
                isGlobal: true
            );
            return AddTransition(transition);
        }

        public StateMachineBuilder<TState> RentTransition(
            TState from,
            TState to,
            Func<bool> rule,
            TransitionContext context = default
        )
        {
            PooledTransitionRule pooledRule = PooledTransitionRule.Rent(rule);
            Transition<TState> transition = new Transition<TState>(from, to, pooledRule, context);
            return AddTransition(transition);
        }

        public StateMachineBuilder<TState> RentTransition(
            TState from,
            TState to,
            ITransitionRule rule,
            TransitionContext context = default
        )
        {
            PooledTransitionRule pooledRule = PooledTransitionRule.Rent(rule);
            Transition<TState> transition = new Transition<TState>(from, to, pooledRule, context);
            return AddTransition(transition);
        }

        public StateMachineBuilder<TState> RentAnyTransition(
            TState to,
            Func<bool> rule,
            TransitionContext context = default
        )
        {
            PooledTransitionRule pooledRule = PooledTransitionRule.Rent(rule);
            Transition<TState> transition = new Transition<TState>(
                default,
                to,
                pooledRule,
                context,
                isGlobal: true
            );
            return AddTransition(transition);
        }

        public StateMachineBuilder<TState> RentAnyTransition(
            TState to,
            ITransitionRule rule,
            TransitionContext context = default
        )
        {
            PooledTransitionRule pooledRule = PooledTransitionRule.Rent(rule);
            Transition<TState> transition = new Transition<TState>(
                default,
                to,
                pooledRule,
                context,
                isGlobal: true
            );
            return AddTransition(transition);
        }

        public StateMachine<TState> Build(TState initialState)
        {
            List<Transition<TState>> snapshot = new List<Transition<TState>>(_transitions.Count);
            for (int i = 0; i < _transitions.Count; i++)
            {
                snapshot.Add(_transitions[i]);
            }

            return new StateMachine<TState>(snapshot, initialState);
        }

        private void RegisterTransition(Transition<TState> transition)
        {
            bool added = _uniqueTransitions.Add(transition);
            if (!added)
            {
                throw new InvalidOperationException(
                    "The provided transition has already been registered with this builder."
                );
            }

            _transitions.Add(transition);
        }
    }
}
