namespace WallstopStudios.DxState.State.Stack.Messages
{
    using global::DxMessaging.Core.Attributes;

    [DxUntargetedMessage]
    public readonly partial struct TransitionProgressChangedMessage
    {
        public readonly IState state;
        public readonly float progress;

        public TransitionProgressChangedMessage(IState state, float progress)
        {
            this.state = state;
            this.progress = progress;
        }
    }
}
