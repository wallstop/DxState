namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;

    public sealed class StateMachineRegion<TState> : IStateRegion
    {
        private readonly StateMachine<TState> _stateMachine;
        private readonly TState _initialState;
        private readonly bool _resetOnActivate;
        private readonly bool _useHistory;
        private readonly TransitionContext _activationContext;
        private readonly TransitionContext _historyContext;
        private bool _hasActivated;
        private TState _lastActiveState;
        private bool _hasHistory;

        public StateMachineRegion(
            StateMachine<TState> stateMachine,
            TState initialState,
            bool resetOnActivate = true,
            TransitionContext activationContext = default,
            bool useHistory = false,
            TransitionContext historyContext = default
        )
        {
            _stateMachine = stateMachine ?? throw new ArgumentNullException(nameof(stateMachine));
            _initialState = initialState;
            _resetOnActivate = resetOnActivate;
            _useHistory = useHistory;
            _activationContext = activationContext.HasDefinedCause
                ? activationContext
                : new TransitionContext(TransitionCause.Forced, TransitionFlags.Forced);
            _historyContext = historyContext.HasDefinedCause
                ? historyContext
                : new TransitionContext(TransitionCause.Manual, TransitionFlags.Forced);
        }

        public StateMachine<TState> StateMachine => _stateMachine;

        public void Activate(TransitionContext context)
        {
            if (_useHistory && _hasHistory)
            {
                TransitionContext activation = context.HasDefinedCause ? context : _historyContext;
                _stateMachine.ForceTransition(_lastActiveState, activation);
                _hasActivated = true;
                return;
            }

            if (!_hasActivated || _resetOnActivate)
            {
                _stateMachine.ForceTransition(_initialState, _activationContext);
            }

            _hasActivated = true;
        }

        public void Deactivate(TransitionContext context)
        {
            if (_useHistory)
            {
                _lastActiveState = _stateMachine.CurrentState;
                _hasHistory = !EqualityComparer<TState>.Default.Equals(
                    _lastActiveState,
                    default
                );
                return;
            }

            _lastActiveState = default;
            _hasHistory = false;
        }

        public void Update()
        {
            _stateMachine.Update();
        }
    }
}
