namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;
    using WallstopStudios.DxState.Pooling;

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
            private const int DefaultCapacity = 32;
            private static readonly object Sync = new object();
            private static PooledTransitionRule[] _buffer = Array.Empty<PooledTransitionRule>();
            private static int _count;

            internal static PooledTransitionRule Rent()
            {
                lock (Sync)
                {
                    if (_count > 0)
                    {
                        _count--;
                        PooledTransitionRule entry = _buffer[_count];
                        _buffer[_count] = null;
                        if (entry != null)
                        {
                            return entry;
                        }
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
                    EnsureCapacity();
                    _buffer[_count] = rule;
                    _count++;
                }
            }

            private static void EnsureCapacity()
            {
                if (_count < _buffer.Length)
                {
                    return;
                }

                int newLength = _buffer.Length == 0 ? DefaultCapacity : _buffer.Length * 2;
                PooledTransitionRule[] newBuffer = WallstopFastArrayPool<PooledTransitionRule>.Rent(
                    newLength
                );
                if (_buffer.Length > 0)
                {
                    Array.Copy(_buffer, newBuffer, _buffer.Length);
                    Array.Clear(_buffer, 0, _buffer.Length);
                    WallstopFastArrayPool<PooledTransitionRule>.Return(_buffer);
                }
                _buffer = newBuffer;
            }
        }
    }
}

