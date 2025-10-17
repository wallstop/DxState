namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;
    using WallstopStudios.UnityHelpers.Utils;

    public sealed class PooledTransitionRule : ITransitionRule
    {
        private Func<bool> _delegateRule;
        private ITransitionRule _structRule;
        private bool _isActive;
        private PooledResource<PooledTransitionRule> _lease;

        private static readonly WallstopGenericPool<PooledTransitionRule> Pool =
            new WallstopGenericPool<PooledTransitionRule>(
                () => new PooledTransitionRule()
            );
        private static int _activeCount;
        private static int _peakActiveCount;
        private static long _totalRentals;
        private static long _totalReleases;

        private PooledTransitionRule()
        {
        }

        public static PooledTransitionRule Rent(Func<bool> rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            PooledResource<PooledTransitionRule> lease = Pool.Get(
                out PooledTransitionRule wrapper
            );
            wrapper._lease = lease;
            wrapper._delegateRule = rule;
            wrapper._structRule = null;
            wrapper._isActive = true;
            RegisterAcquisition();
            return wrapper;
        }

        public static PooledTransitionRule Rent(ITransitionRule rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            PooledResource<PooledTransitionRule> lease = Pool.Get(
                out PooledTransitionRule wrapper
            );
            wrapper._lease = lease;
            wrapper._delegateRule = null;
            wrapper._structRule = rule;
            wrapper._isActive = true;
            RegisterAcquisition();
            return wrapper;
        }

        public bool Evaluate()
        {
            if (_structRule != null)
            {
                return _structRule.Evaluate();
            }

            return _delegateRule != null && _delegateRule.Invoke();
        }

        internal void Release()
        {
            if (!_isActive)
            {
                return;
            }

            _isActive = false;
            _delegateRule = null;
            _structRule = null;
            _lease.Dispose();
            _lease = default;
            RegisterRelease();
        }

        internal void ReleaseForTesting()
        {
            Release();
        }

        private static void RegisterAcquisition()
        {
            int active = System.Threading.Interlocked.Increment(ref _activeCount);
            System.Threading.Interlocked.Increment(ref _totalRentals);

            int currentPeak = _peakActiveCount;
            while (active > currentPeak)
            {
                int previous = System.Threading.Interlocked.CompareExchange(
                    ref _peakActiveCount,
                    active,
                    currentPeak
                );
                if (previous == currentPeak)
                {
                    break;
                }
                currentPeak = previous;
            }
        }

        private static void RegisterRelease()
        {
            System.Threading.Interlocked.Increment(ref _totalReleases);
            System.Threading.Interlocked.Decrement(ref _activeCount);
        }

        public static PooledTransitionRuleMetrics GetMetrics()
        {
            return new PooledTransitionRuleMetrics(
                System.Threading.Volatile.Read(ref _activeCount),
                System.Threading.Volatile.Read(ref _peakActiveCount),
                System.Threading.Volatile.Read(ref _totalRentals),
                System.Threading.Volatile.Read(ref _totalReleases)
            );
        }

        public static void ResetMetrics()
        {
            if (System.Threading.Volatile.Read(ref _activeCount) != 0)
            {
                throw new InvalidOperationException(
                    "Cannot reset pooled transition rule metrics while rules are still active."
                );
            }

            System.Threading.Interlocked.Exchange(ref _activeCount, 0);
            System.Threading.Interlocked.Exchange(ref _peakActiveCount, 0);
            System.Threading.Interlocked.Exchange(ref _totalRentals, 0);
            System.Threading.Interlocked.Exchange(ref _totalReleases, 0);
        }
    }

    public readonly struct PooledTransitionRuleMetrics
    {
        public PooledTransitionRuleMetrics(
            int activeCount,
            int peakActiveCount,
            long totalRentals,
            long totalReleases
        )
        {
            ActiveCount = activeCount;
            PeakActiveCount = peakActiveCount;
            TotalRentals = totalRentals;
            TotalReleases = totalReleases;
        }

        public int ActiveCount { get; }

        public int PeakActiveCount { get; }

        public long TotalRentals { get; }

        public long TotalReleases { get; }
    }
}
