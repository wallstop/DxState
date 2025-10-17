namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public readonly struct StateStackSnapshot
    {
        private readonly IState[] _states;
        private readonly bool _hasPreviousState;
        private readonly IState _previousState;

        private StateStackSnapshot(IState[] states, bool hasPreviousState, IState previousState)
        {
            _states = states ?? Array.Empty<IState>();
            _hasPreviousState = hasPreviousState;
            _previousState = previousState;
        }

        public IReadOnlyList<IState> States => _states;

        public bool HasPreviousState => _hasPreviousState;

        public IState PreviousState => _previousState;

        public static StateStackSnapshot Capture(StateStack stack)
        {
            if (stack == null)
            {
                throw new ArgumentNullException(nameof(stack));
            }

            IReadOnlyList<IState> activeStates = stack.Stack;
            IState[] snapshotStates = new IState[activeStates.Count];
            for (int i = 0; i < activeStates.Count; i++)
            {
                snapshotStates[i] = activeStates[i];
            }

            IState previousState = stack.PreviousState;
            bool hasPrevious = previousState != null;
            return new StateStackSnapshot(snapshotStates, hasPrevious, previousState);
        }

        public async ValueTask RestoreAsync(StateStack target)
        {
            await RestoreAsync(target, StateTransitionOptions.Default);
        }

        public async ValueTask RestoreAsync(StateStack target, StateTransitionOptions options)
        {
            if (target == null)
            {
                throw new ArgumentNullException(nameof(target));
            }

            await target.ClearAsync(options);

            for (int i = 0; i < _states.Length; i++)
            {
                IState state = _states[i];
                if (state == null)
                {
                    continue;
                }

                target.TryRegister(state, force: true);
                await target.PushAsync(state, options);
            }
        }
    }
}
