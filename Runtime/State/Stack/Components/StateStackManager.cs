namespace WallstopStudios.DxState.State.Stack.Components
{
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using global::DxMessaging.Core.Extensions;
    using Messages;

    public sealed class StateStackManager : DxMessageAwareSingleton<StateStackManager>
    {
        public bool IsTransitioning => _stateStack.IsTransitioning;

        public IState CurrentState => _stateStack.CurrentState;
        public IState PreviousState => _stateStack.PreviousState;

        public float Progress => _stateStack.Progress;
        public IReadOnlyDictionary<string, IState> RegisteredStates => _stateStack.RegisteredStates;
        public IReadOnlyList<IState> Stack => _stateStack.Stack;

        private readonly StateStack _stateStack = new();

        protected override void Awake()
        {
            base.Awake();
            _stateStack.OnStatePopped += (previous, current) =>
            {
                StatePoppedMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.OnStatePushed += (previous, current) =>
            {
                StatePushedMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.OnTransitionStart += (previous, next) =>
            {
                TransitionStartMessage message = new(previous, next);
                message.EmitUntargeted();
            };
            _stateStack.OnTransitionComplete += (previous, current) =>
            {
                TransitionCompleteMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.OnFlattened += target =>
            {
                StateStackFlattenedMessage message = new(target);
                message.EmitUntargeted();
            };
            _stateStack.OnTransitionProgress += (state, progress) =>
            {
                TransitionProgressChangedMessage message = new(state, progress);
                message.EmitUntargeted();
            };
            _stateStack.OnStateManuallyRemoved += state =>
            {
                StateManuallyRemovedMessage message = new(state);
                message.EmitUntargeted();
            };
        }

        public int CountOf(IState state)
        {
            return _stateStack.CountOf(state);
        }

        public async ValueTask WaitForTransitionCompletionAsync()
        {
            await _stateStack.WaitForTransitionCompletionAsync();
        }

        public bool TryRegister(IState state, bool force = false)
        {
            return _stateStack.TryRegister(state, force);
        }

        public bool Unregister(IState state)
        {
            return _stateStack.Unregister(state);
        }

        public bool Unregister(string stateName)
        {
            return _stateStack.Unregister(stateName);
        }

        public async ValueTask PushAsync(IState newState)
        {
            await _stateStack.PushAsync(newState);
        }

        public async ValueTask PushAsync(string stateName)
        {
            await _stateStack.PushAsync(stateName);
        }

        public async ValueTask<IState> PopAsync()
        {
            return await _stateStack.PopAsync();
        }

        public async ValueTask<IState> TryPopAsync()
        {
            return await _stateStack.TryPopAsync();
        }

        public async ValueTask FlattenAsync(IState state)
        {
            await _stateStack.FlattenAsync(state);
        }

        public async ValueTask FlattenAsync(string stateName)
        {
            await _stateStack.FlattenAsync(stateName);
        }

        public async ValueTask RemoveAsync(string stateName)
        {
            await _stateStack.RemoveAsync(stateName);
        }

        public async ValueTask RemoveAsync(IState stateToRemove)
        {
            await _stateStack.RemoveAsync(stateToRemove);
        }

        public async ValueTask ClearAsync()
        {
            await _stateStack.ClearAsync();
        }

        private void Update()
        {
            _stateStack.Update();
        }

        private void FixedUpdate()
        {
            _stateStack.FixedUpdate();
        }

        private void LateUpdate()
        {
            _stateStack.LateUpdate();
        }
    }
}
