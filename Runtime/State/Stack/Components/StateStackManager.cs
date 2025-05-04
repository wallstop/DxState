namespace WallstopStudios.DxState.State.Stack.Components
{
    using System.Threading.Tasks;
    using global::DxMessaging.Core.Extensions;
    using Messages;

    public sealed class StateStackManager : MessageAwareSingleton<StateStackManager>
    {
        private readonly StateStack _stateStack = new();

        protected override void Awake()
        {
            base.Awake();
            _stateStack.StatePopped += (previous, current) =>
            {
                StatePoppedMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.StatePushed += (previous, current) =>
            {
                StatePushedMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.TransitionStart += (previous, next) =>
            {
                TransitionStartMessage message = new(previous, next);
                message.EmitUntargeted();
            };
            _stateStack.TransitionComplete += (previous, current) =>
            {
                TransitionCompleteMessage message = new(previous, current);
                message.EmitUntargeted();
            };
            _stateStack.Flattened += target =>
            {
                StateStackFlattenedMessage message = new(target);
                message.EmitUntargeted();
            };
            _stateStack.HistoryRemoved += (removed, target) =>
            {
                StateStackHistoryRemovedMessage message = new(removed, target);
                message.EmitUntargeted();
            };
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

        public async ValueTask PopAsync()
        {
            await _stateStack.PopAsync();
        }

        public async ValueTask FlattenAsync(IState state)
        {
            await _stateStack.FlattenAsync(state);
        }

        public async ValueTask FlattenAsync(string stateName)
        {
            await _stateStack.FlattenAsync(stateName);
        }

        public void RemoveHistory(IState state)
        {
            _stateStack.RemoveHistory(state);
        }

        public void RemoveHistory(string stateName)
        {
            _stateStack.RemoveHistory(stateName);
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
