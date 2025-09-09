namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    [DxUntargetedMessage]
    public readonly partial struct StateManuallyRemovedMessage
    {
        public readonly IState state;

        public StateManuallyRemovedMessage(IState state)
        {
            this.state = state;
        }
    }
}
