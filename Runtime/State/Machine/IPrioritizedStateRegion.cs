namespace WallstopStudios.DxState.State.Machine
{
    public interface IPrioritizedStateRegion : IStateRegion
    {
        int Priority { get; }
    }
}
