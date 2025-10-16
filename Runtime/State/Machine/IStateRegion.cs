namespace WallstopStudios.DxState.State.Machine
{
    using System;

    public interface IStateRegion
    {
        void Activate(TransitionContext context);

        void Deactivate(TransitionContext context);

        void Update();
    }
}
