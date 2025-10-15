namespace WallstopStudios.DxState.State.Stack.Internal
{
    using System;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Sources;

    internal sealed class TransitionCompletionSource : IValueTaskSource
    {
        private static readonly object PoolGate = new object();
        private static TransitionCompletionSource _cached;

        private ManualResetValueTaskSourceCore<bool> _core;
        private bool _inUse;

        private TransitionCompletionSource()
        {
            _core = new ManualResetValueTaskSourceCore<bool>
            {
                RunContinuationsAsynchronously = true,
            };
        }

        public static TransitionCompletionSource Rent()
        {
            lock (PoolGate)
            {
                if (_cached != null)
                {
                    TransitionCompletionSource instance = _cached;
                    _cached = null;
                    instance._inUse = true;
                    instance._core.Reset();
                    return instance;
                }
            }

            TransitionCompletionSource created = new TransitionCompletionSource { _inUse = true };
            created._core.Reset();
            return created;
        }

        public ValueTask AsValueTask()
        {
            return new ValueTask(this, _core.Version);
        }

        public void SetResult()
        {
            _core.SetResult(true);
        }

        public void SetException(Exception exception)
        {
            _core.SetException(exception);
        }

        public void GetResult(short token)
        {
            try
            {
                _core.GetResult(token);
            }
            finally
            {
                ReturnToPool();
            }
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            return _core.GetStatus(token);
        }

        public void OnCompleted(
            Action<object> continuation,
            object state,
            short token,
            ValueTaskSourceOnCompletedFlags flags
        )
        {
            _core.OnCompleted(continuation, state, token, flags);
        }

        private void ReturnToPool()
        {
            if (!_inUse)
            {
                return;
            }

            _inUse = false;
            lock (PoolGate)
            {
                if (_cached == null)
                {
                    _cached = this;
                }
            }
        }
    }
}
