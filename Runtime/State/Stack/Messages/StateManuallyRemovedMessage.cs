namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    /// <summary>
    /// Broadcast when <see cref="StateStack.RemoveAsync(IState)"/> succeeds for a non-active state.
    /// Carries the removed instance so listeners can reconcile local caches.
    /// </summary>
    [DxUntargetedMessage]
    public readonly partial struct StateManuallyRemovedMessage
    {
        /// <summary>
        /// Gets the state that was explicitly removed from the stack.
        /// </summary>
        public IState State => state;

        public readonly IState state;

        public StateManuallyRemovedMessage(IState state)
        {
            this.state = state;
        }
    }
}
