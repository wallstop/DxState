namespace WallstopStudios.DxState.State.Stack.Messages
{
    using System.Collections.Generic;
    using global::DxMessaging.Core.Attributes;

    [DxUntargetedMessage]
    public readonly partial struct StateStackHistoryRemovedMessage
    {
        public readonly IReadOnlyList<IState> removed;
        public readonly IState target;

        public StateStackHistoryRemovedMessage(List<IState> removed, IState target)
        {
            this.removed = removed;
            this.target = target;
        }
    }
}
