namespace WallstopStudios.DxState.State.Machine
{
    using System.Collections.Generic;

    public interface IStateRegionCoordinator
    {
        void ActivateRegions(IReadOnlyList<IStateRegion> regions, TransitionContext context);

        void DeactivateRegions(IReadOnlyList<IStateRegion> regions, TransitionContext context);

        void UpdateRegions(IReadOnlyList<IStateRegion> regions);
    }
}
