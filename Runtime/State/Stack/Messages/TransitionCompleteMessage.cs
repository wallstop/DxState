namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    [DxUntargetedMessage]
    public readonly partial struct TransitionCompleteMessage
    {
        public readonly IState previous;
        public readonly IState current;

        public TransitionCompleteMessage(IState previous, IState current)
        {
            this.previous = previous;
            this.current = current;
        }
    }
}
