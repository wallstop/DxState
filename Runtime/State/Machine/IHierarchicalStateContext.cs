namespace WallstopStudios.DxState.State.Machine
{
    using System.Collections.Generic;

    public interface IHierarchicalStateContext<TState> : IStateContext<TState>
    {
        IReadOnlyList<IStateRegion> Regions { get; }

        bool ShouldUpdateRegions { get; }

        IStateRegionCoordinator RegionCoordinator { get; }
    }
}
