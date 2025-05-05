namespace WallstopStudios.DxState.State.Stack
{
    using System.Threading.Tasks;

    public interface IState
    {
        string Name { get; }

        TickMode TickMode => TickMode.None;
        float? TimeInState { get; }
        bool TickWhenInactive => false;

        ValueTask Enter(IState previousState);
        void Tick(TickMode mode, float delta);
        ValueTask Exit(IState nextState);
        ValueTask RevertFrom(IState previousState);

        // TODO: Add removal hooks (for RemoveHistory)
    }
}
