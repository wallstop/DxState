namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    /// <summary>
    /// Published when a transition has been scheduled to move from the previous active state to the next.
    /// </summary>
    [DxUntargetedMessage]
    public readonly partial struct TransitionStartMessage
    {
        /// <summary>
        /// Gets the state that was active before the transition started.
        /// </summary>
        public IState PreviousState => previous;

        /// <summary>
        /// Gets the target state that will become active when the transition completes.
        /// </summary>
        public IState NextState => next;

        public readonly IState previous;
        public readonly IState next;

        public TransitionStartMessage(IState previous, IState next)
        {
            this.previous = previous;
            this.next = next;
        }
    }
}
