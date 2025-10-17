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
        }
    }
}
