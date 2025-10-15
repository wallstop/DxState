namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    /// <summary>
    /// Published after a new state becomes the active top of the stack.
    /// </summary>
    [DxUntargetedMessage]
    public readonly partial struct StatePushedMessage
    {
        /// <summary>
        /// Gets the state that was active before the push (may be <c>null</c> when the stack was empty).
        /// </summary>
        public IState PreviousState => previous;

        /// <summary>
        /// Gets the state that was pushed and is now active.
        /// </summary>
        public IState CurrentState => current;

        public readonly IState previous;
        public readonly IState current;

        public StatePushedMessage(IState previous, IState current)
        {
            this.previous = previous;
            this.current = current;
        }
    }
}
