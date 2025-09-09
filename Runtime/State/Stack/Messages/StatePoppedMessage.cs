namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    [DxUntargetedMessage]
    public readonly partial struct StatePoppedMessage
    {
        public readonly IState previous;
        public readonly IState current;

        public StatePoppedMessage(IState previous, IState current)
        {
            this.previous = previous;
            this.current = current;
        }
    }
}
