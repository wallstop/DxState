namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;
    using System.Collections.Generic;

    public sealed class PooledTransitionRule : ITransitionRule
    {
        private Func<bool> _delegateRule;
        private ITransitionRule _structRule;
        private bool _isActive;

        private PooledTransitionRule()
        {
        }

        public static PooledTransitionRule Rent(Func<bool> rule)
        {
            if (rule == null)
            {
                throw new ArgumentNullException(nameof(rule));
            }

            PooledTransitionRule wrapper = TransitionRulePool.Rent();
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

            PooledTransitionRule wrapper = TransitionRulePool.Rent();
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
            TransitionRulePool.Return(this);
        }

        private static class TransitionRulePool
        {
            private static readonly object Sync = new object();
            private static readonly Stack<PooledTransitionRule> Pool = new Stack<PooledTransitionRule>();

            internal static PooledTransitionRule Rent()
            {
                lock (Sync)
                {
                    if (Pool.Count > 0)
                    {
                        return Pool.Pop();
                    }
                }

                return new PooledTransitionRule();
            }

            internal static void Return(PooledTransitionRule rule)
            {
                if (rule == null)
                {
                    return;
                }

                lock (Sync)
                {
                    Pool.Push(rule);
                }
            }
        }
    }
}
