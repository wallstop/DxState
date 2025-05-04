namespace WallstopStudios.DxState.State.Stack
{
    using System.Threading.Tasks;

    public interface IState
    {
        string Name { get; }

        TickMode TickMode => TickMode.None;
        bool TickWhenInactive => false;

        ValueTask Enter(IState previousState);
        void Tick(TickMode mode, float delta);
        ValueTask Exit(IState nextState);

        void Pause();
        void Resume();
    }
}
