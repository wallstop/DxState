namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    [DxUntargetedMessage]
    public readonly partial struct StateStackFlattenedMessage
    {
        public readonly IState target;

        public StateStackFlattenedMessage(IState target)
        {
            this.target = target;
        }
    }
}
