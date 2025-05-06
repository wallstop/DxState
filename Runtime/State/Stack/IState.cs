namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Threading.Tasks;

    public interface IState
    {
        string Name { get; }

        TickMode TickMode => TickMode.None;
        float? TimeInState { get; }
        bool TickWhenInactive => false;

        ValueTask Enter<TProgress>(IState previousState, TProgress progress)
            where TProgress : IProgress<float>;
        void Tick(TickMode mode, float delta);
        ValueTask Exit<TProgress>(IState nextState, TProgress progress)
            where TProgress : IProgress<float>;
        ValueTask RevertFrom<TProgress>(IState previousState, TProgress progress)
            where TProgress : IProgress<float>;

        // TODO: Add removal hooks (for RemoveHistory)
    }
}
