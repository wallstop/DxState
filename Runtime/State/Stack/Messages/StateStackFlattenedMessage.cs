namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    /// <summary>
    /// Published after the stack has been flattened to a specific target state.
    /// </summary>
    [DxUntargetedMessage]
    public readonly partial struct StateStackFlattenedMessage
    {
        /// <summary>
        /// Gets the state the stack was flattened to.
        /// </summary>
        public IState TargetState => target;

        public readonly IState target;

        public StateStackFlattenedMessage(IState target)
        {
            this.target = target;
        }
    }
}
