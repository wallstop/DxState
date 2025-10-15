namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    /// <summary>
    /// Published after the active state has been popped from the stack.
    /// </summary>
    [DxUntargetedMessage]
    public readonly partial struct StatePoppedMessage
    {
        /// <summary>
        /// Gets the state that was removed.
        /// </summary>
        public IState RemovedState => previous;

        /// <summary>
        /// Gets the state that became active after the pop (may be <c>null</c>).
        /// </summary>
        public IState CurrentState => current;

        public readonly IState previous;
        public readonly IState current;

        public StatePoppedMessage(IState previous, IState current)
        {
            this.previous = previous;
            this.current = current;
        }
    }
}
