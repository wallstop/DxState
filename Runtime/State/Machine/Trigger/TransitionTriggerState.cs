namespace WallstopStudios.DxState.State.Machine.Trigger
{
    using System;
    using System.Collections.Generic;
    using Component;

    public sealed class TransitionTriggerState<TState> : ITriggerState<TState, TState>
    {
        private readonly List<Transition<TState>> _transitions;
        private readonly Action<TriggerStateMachine<TState, TState>, TState, TransitionContext> _onEnter;
        private readonly Action<TriggerStateMachine<TState, TState>, TState, TransitionContext> _onExit;
        private readonly Action _tick;

        public TransitionTriggerState(
            TState id,
            IEnumerable<Transition<TState>> transitions,
            Action<TriggerStateMachine<TState, TState>, TState, TransitionContext> onEnter = null,
            Action<TriggerStateMachine<TState, TState>, TState, TransitionContext> onExit = null,
            Action tick = null
        )
        {
            if (transitions == null)
            {
                throw new ArgumentNullException(nameof(transitions));
            }

            Id = id;
            _transitions = new List<Transition<TState>>();
            foreach (Transition<TState> transition in transitions)
            {
                if (transition == null)
                {
                    continue;
                }

                _transitions.Add(transition);
            }
            _onEnter = onEnter;
            _onExit = onExit;
            _tick = tick;
        }

        public TState Id { get; }

        public bool TryGetTrigger(out TState trigger, out TransitionContext context)
        {
            for (int i = 0; i < _transitions.Count; i++)
            {
                Transition<TState> transition = _transitions[i];
                if (!transition.Evaluate())
                {
                    continue;
                }

                trigger = transition.to;
                context = transition.Context;
                return true;
            }

            trigger = default;
            context = default;
            return false;
        }

        public void OnEnter(
            TriggerStateMachine<TState, TState> machine,
            TState previousState,
            TransitionContext context
        )
        {
            if (_onEnter != null)
            {
                _onEnter.Invoke(machine, previousState, context);
            }
        }

        public void OnExit(
            TriggerStateMachine<TState, TState> machine,
            TState nextState,
            TransitionContext context
        )
        {
            if (_onExit != null)
            {
                _onExit.Invoke(machine, nextState, context);
            }
        }

        public void Tick()
        {
            if (_tick != null)
            {
                _tick.Invoke();
            }
        }
    }
}
