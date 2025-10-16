namespace WallstopStudios.DxState.State.Machine
{
    using System;
    using System.Collections.Generic;

    public abstract class CompositeState<TState> : IHierarchicalStateContext<TState>
    {
        private readonly List<IStateRegion> _regions;
        private readonly bool _shouldUpdateRegions;
        private bool _regionsConfigured;

        protected CompositeState(bool shouldUpdateRegions = true)
        {
            _regions = new List<IStateRegion>();
            _shouldUpdateRegions = shouldUpdateRegions;
        }

        public StateMachine<TState> StateMachine { get; set; }

        public bool IsActive { get; private set; }

        public IReadOnlyList<IStateRegion> Regions
        {
            get
            {
                EnsureRegionsConfigured();
                return _regions;
            }
        }

        public bool ShouldUpdateRegions => _shouldUpdateRegions;

        public virtual IStateRegionCoordinator RegionCoordinator => DefaultStateRegionCoordinator.Instance;

        public void Enter()
        {
            EnsureRegionsConfigured();
            IsActive = true;
            OnEnter();
        }

        public void Exit()
        {
            OnExit();
            IsActive = false;
        }

        public virtual void Log(FormattableString message) { }

        protected abstract void ConfigureRegions(IList<IStateRegion> regions);

        protected virtual void OnEnter() { }

        protected virtual void OnExit() { }

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
