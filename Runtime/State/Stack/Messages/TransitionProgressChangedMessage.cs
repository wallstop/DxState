namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    /// <summary>
    /// Published whenever the active transition reports new overall progress.
    /// </summary>
    [DxUntargetedMessage]
    public readonly partial struct TransitionProgressChangedMessage
    {
        /// <summary>
        /// Gets the state associated with the progress update (may be <c>null</c> during stack clear operations).
        /// </summary>
        public IState State => state;

        /// <summary>
        /// Gets the normalized progress value in the range <c>[0, 1]</c>.
        /// </summary>
        public float Progress => progress;

        public readonly IState state;
        public readonly float progress;

        public TransitionProgressChangedMessage(IState state, float progress)
        {
            this.state = state;
            this.progress = progress;
        }
    }
}
