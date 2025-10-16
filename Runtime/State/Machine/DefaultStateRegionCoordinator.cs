namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;

    public sealed class DefaultStateRegionCoordinator : IStateRegionCoordinator
    {
        public static readonly DefaultStateRegionCoordinator Instance = new DefaultStateRegionCoordinator();

        private DefaultStateRegionCoordinator() { }

        public void ActivateRegions(IReadOnlyList<IStateRegion> regions, TransitionContext context)
        {
            if (regions == null)
            {
                return;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                regions[i]?.Activate(context);
            }
        }

        public void DeactivateRegions(IReadOnlyList<IStateRegion> regions, TransitionContext context)
        {
            if (regions == null)
            {
                return;
            }

            for (int i = regions.Count - 1; i >= 0; i--)
            {
                regions[i]?.Deactivate(context);
            }
        }

        public void UpdateRegions(IReadOnlyList<IStateRegion> regions)
        {
            if (regions == null)
            {
                return;
            }

            for (int i = 0; i < regions.Count; i++)
            {
                regions[i]?.Update();
            }
        }
    }
}
