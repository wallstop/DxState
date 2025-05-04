namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    [DxUntargetedMessage]
    public readonly partial struct TransitionStartMessage
    {
        public readonly IState previous;
        public readonly IState next;

        public TransitionStartMessage(IState previous, IState next)
        {
            this.previous = previous;
            this.next = next;
        }
    }
}
