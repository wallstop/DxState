namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;

    public sealed class PriorityStateRegionCoordinator : IStateRegionCoordinator
    {
        private readonly IComparer<IStateRegion> _comparer;
        private readonly List<IStateRegion> _buffer;

        public PriorityStateRegionCoordinator()
            : this(null) { }

        public PriorityStateRegionCoordinator(IComparer<IStateRegion> comparer)
        {
            _comparer = comparer ?? new PriorityComparer();
            _buffer = new List<IStateRegion>();
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

            _buffer.Clear();
            for (int i = 0; i < regions.Count; i++)
            {
                IStateRegion region = regions[i];
                if (region == null)
                {
                    continue;
                }

                _buffer.Add(region);
            }

            _buffer.Sort(_comparer);

            if (reverse)
            {
                for (int i = _buffer.Count - 1; i >= 0; i--)
                {
                    action(_buffer[i]);
                }
                return;
            }

            for (int i = 0; i < _buffer.Count; i++)
            {
                action(_buffer[i]);
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
