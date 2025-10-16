namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Threading.Tasks;
    using WallstopStudios.DxState.State.Stack;

    public sealed class StateStackRegion : IPrioritizedStateRegion
    {
        private readonly StateStack _stateStack;
        private readonly IState _targetState;
        private readonly bool _flattenOnActivate;
        private readonly bool _removeOnDeactivate;
        private readonly bool _forceRegister;
        private readonly int _priority;

        public StateStackRegion(
            StateStack stateStack,
            IState targetState,
            bool flattenOnActivate = false,
            bool removeOnDeactivate = true,
            bool forceRegister = true,
            int priority = 0
        )
        {
            _stateStack = stateStack ?? throw new ArgumentNullException(nameof(stateStack));
            _targetState = targetState ?? throw new ArgumentNullException(nameof(targetState));
            _flattenOnActivate = flattenOnActivate;
            _removeOnDeactivate = removeOnDeactivate;
            _forceRegister = forceRegister;
            _priority = priority;
        }

        public int Priority => _priority;

        public void Activate(TransitionContext context)
        {
            if (_forceRegister)
            {
                _stateStack.TryRegister(_targetState, force: true);
            }

            if (_flattenOnActivate)
            {
                AwaitOperation(_stateStack.FlattenAsync(_targetState));
                return;
            }

            AwaitOperation(_stateStack.PushAsync(_targetState));
        }

        public void Deactivate(TransitionContext context)
        {
            if (!_removeOnDeactivate)
            {
                return;
            }

            try
            {
                AwaitOperation(_stateStack.RemoveAsync(_targetState));
            }
            catch (ArgumentException)
            {
                // State already absent; ignore
            }
        }

        public void Update()
        {
            // No-op; StateStackManager drives Update on the stack.
        }

        private static void AwaitOperation(ValueTask task)
        {
            task.GetAwaiter().GetResult();
        }

        private static void AwaitOperation<TValue>(ValueTask<TValue> task)
        {
            task.GetAwaiter().GetResult();
        }
    }
}
