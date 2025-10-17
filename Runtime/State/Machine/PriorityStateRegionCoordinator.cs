namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;
    using WallstopStudios.UnityHelpers.Utils;

    public sealed class PriorityStateRegionCoordinator : IStateRegionCoordinator
    {
        private readonly IComparer<IStateRegion> _comparer;

        public PriorityStateRegionCoordinator()
            : this(null) { }

        public PriorityStateRegionCoordinator(IComparer<IStateRegion> comparer)
        {
            _comparer = comparer ?? new PriorityComparer();
        }

        public void ActivateRegions(IReadOnlyList<IStateRegion> regions, TransitionContext context)
        {
            IterateInPriorityOrder(regions, region => region.Activate(context));
        }

        public void DeactivateRegions(IReadOnlyList<IStateRegion> regions, TransitionContext context)
        {
            IterateInPriorityOrder(regions, region => region.Deactivate(context), reverse: true);
        }

        public void UpdateRegions(IReadOnlyList<IStateRegion> regions)
        {
            IterateInPriorityOrder(regions, region => region.Update());
        }

        private void IterateInPriorityOrder(
            IReadOnlyList<IStateRegion> regions,
            Action<IStateRegion> action,
            bool reverse = false
        )
        {
            if (regions == null || regions.Count == 0)
            {
                return;
            }

            using PooledResource<List<IStateRegion>> pooled = Buffers<IStateRegion>.GetList(
                regions.Count,
                out List<IStateRegion> buffer
            );

            for (int i = 0; i < regions.Count; i++)
            {
                IStateRegion region = regions[i];
                if (region == null)
                {
                    continue;
                }

                buffer.Add(region);
            }

            buffer.Sort(_comparer);

            if (reverse)
            {
                for (int i = buffer.Count - 1; i >= 0; i--)
                {
                    action(buffer[i]);
                }
                return;
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                action(buffer[i]);
            }
        }

        private sealed class PriorityComparer : IComparer<IStateRegion>
        {
            public int Compare(IStateRegion x, IStateRegion y)
            {
                if (ReferenceEquals(x, y))
                {
                    return 0;
                }

                if (x is IPrioritizedStateRegion prioritizedX)
                {
                    if (y is IPrioritizedStateRegion prioritizedY)
                    {
                        return prioritizedX.Priority.CompareTo(prioritizedY.Priority);
                    }

                    return -1;
                }

                if (y is IPrioritizedStateRegion prioritizedYOnly)
                {
                    return 1;
                }

                return 0;
            }
        }
    }
}
