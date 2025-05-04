namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    [DxUntargetedMessage]
    public readonly partial struct StatePushedMessage
    {
        public readonly IState previous;
        public readonly IState current;

        public StatePushedMessage(IState previous, IState current)
        {
            this.previous = previous;
            this.current = current;
        }
    }
}
