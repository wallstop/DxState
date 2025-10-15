namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;

    public interface IStateComponent : IStateContext<IStateComponent>
    {
        bool ShouldEnter();
    }
}
