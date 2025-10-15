namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    /// <summary>
    /// Published after a transition completes and the new state becomes active.
    /// </summary>
    [DxUntargetedMessage]
    public readonly partial struct TransitionCompleteMessage
    {
        /// <summary>
        /// Gets the state that was active before completion.
        /// </summary>
        public IState PreviousState => previous;

        /// <summary>
        /// Gets the state that is now active.
        /// </summary>
        public IState CurrentState => current;

        public readonly IState previous;
        public readonly IState current;

        public TransitionCompleteMessage(IState previous, IState current)
        {
            this.previous = previous;
            this.current = current;
        }
    }
}
