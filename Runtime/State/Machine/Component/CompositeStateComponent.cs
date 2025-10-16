namespace WallstopStudios.DxState.State.Machine.Component
{
    using System.Collections.Generic;
    using UnityEngine;
    using WallstopStudios.DxState.State.Machine;

    public abstract class CompositeStateComponent : StateComponent, IHierarchicalStateContext<IStateComponent>
    {
        [SerializeField]
        private bool _updateRegions = true;

        private readonly List<IStateRegion> _regions = new List<IStateRegion>();
        private bool _regionsConfigured;

        public IReadOnlyList<IStateRegion> Regions
        {
            get
            {
                EnsureRegionsConfigured();
                return _regions;
            }
        }

        public bool ShouldUpdateRegions => _updateRegions;

        public virtual IStateRegionCoordinator RegionCoordinator => DefaultStateRegionCoordinator.Instance;

        public override bool ShouldEnter()
        {
            EnsureRegionsConfigured();
            return base.ShouldEnter();
        }

        protected override void OnEnter()
        {
            EnsureRegionsConfigured();
            base.OnEnter();
        }

        protected void RegisterRegion(IStateRegion region)
        {
            EnsureRegionsConfigured();
            if (region == null)
            {
                return;
            }

            if (_regions.Contains(region))
            {
                return;
            }

            _regions.Add(region);
        }

        protected void RegisterRegions(IEnumerable<IStateRegion> regions)
        {
            EnsureRegionsConfigured();
            if (regions == null)
            {
                return;
            }

            foreach (IStateRegion region in regions)
            {
                if (region == null)
                {
                    continue;
                }

                if (_regions.Contains(region))
                {
                    continue;
                }

                _regions.Add(region);
            }
        }

        protected void ClearRegions()
        {
            _regions.Clear();
        }

        protected abstract void ConfigureRegions(IList<IStateRegion> regions);

        private void EnsureRegionsConfigured()
        {
            if (_regionsConfigured)
            {
                return;
            }

            _regions.Clear();
            _regionsConfigured = true;
            ConfigureRegions(_regions);
        }
    }
}
