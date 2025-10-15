namespace WallstopStudios.DxState.State.Stack.Internal
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Threading.Tasks.Sources;

    internal sealed class TransitionCompletionSource : IValueTaskSource
    {
        private const short InitialVersion = 1;

        private static readonly object PoolGate = new object();
        private static TransitionCompletionSource _cachedInstance;

        private readonly object _instanceGate;

        private Action<object> _continuation;
        private object _continuationState;
        private ExecutionContext _executionContext;
        private SynchronizationContext _synchronizationContext;
        private Exception _exception;

        private short _version;
        private bool _inUse;
        private bool _runContinuationsAsynchronously;
        private ValueTaskSourceStatus _status;

        private TransitionCompletionSource()
        {
            _instanceGate = new object();
            _version = (short)(InitialVersion - 1);
        }

        public static TransitionCompletionSource Rent()
        {
            lock (PoolGate)
            {
                if (_cachedInstance != null)
                {
                    TransitionCompletionSource pooled = _cachedInstance;
                    _cachedInstance = null;
                    pooled.PrepareForUse();
                    return pooled;
                }
            }

            TransitionCompletionSource created = new TransitionCompletionSource();
            created.PrepareForUse();
            return created;
        }

        public ValueTask AsValueTask()
        {
            EnsureInUse();
            return new ValueTask(this, _version);
        }

        public void SetResult()
        {
            SignalCompletion(ValueTaskSourceStatus.Succeeded, null);
        }

        public void SetException(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            SignalCompletion(ValueTaskSourceStatus.Faulted, exception);
        }

        public void GetResult(short token)
        {
            VerifyToken(token);

            try
            {
                switch (_status)
                {
                    case ValueTaskSourceStatus.Succeeded:
                    {
                        return;
                    }
                    case ValueTaskSourceStatus.Faulted:
                    {
                        throw _exception;
                    }
                    case ValueTaskSourceStatus.Canceled:
                    {
                        throw new TaskCanceledException();
                    }
                    default:
                    {
                        throw new InvalidOperationException(
                            "TransitionCompletionSource.GetResult called before completion."
                        );
                    }
                }
            }
            finally
            {
                ReturnToPool();
            }
        }

        public ValueTaskSourceStatus GetStatus(short token)
        {
            VerifyToken(token);
            return _status;
        }

        public void OnCompleted(
            Action<object> continuation,
            object state,
            short token,
            ValueTaskSourceOnCompletedFlags flags
        )
        {
            if (continuation == null)
            {
                throw new ArgumentNullException(nameof(continuation));
            }

            VerifyToken(token);

            ExecutionContext capturedExecutionContext = null;
            if ((flags & ValueTaskSourceOnCompletedFlags.FlowExecutionContext) != 0)
            {
                capturedExecutionContext = ExecutionContext.Capture();
            }

            SynchronizationContext capturedSynchronizationContext = null;
            if ((flags & ValueTaskSourceOnCompletedFlags.UseSchedulingContext) != 0)
            {
                capturedSynchronizationContext = SynchronizationContext.Current;
            }

            bool runAsynchronously = _runContinuationsAsynchronously;

            Action<object> continuationToInvoke = null;
            object continuationState = null;
            ExecutionContext executionContextToUse = null;
            SynchronizationContext synchronizationContextToUse = null;
            bool shouldInvokeImmediately = false;

            lock (_instanceGate)
            {
                if (_status != ValueTaskSourceStatus.Pending)
                {
                    continuationToInvoke = continuation;
                    continuationState = state;
                    executionContextToUse = capturedExecutionContext;
                    synchronizationContextToUse = capturedSynchronizationContext;
                    shouldInvokeImmediately = true;
                }
                else
                {
                    _continuation = continuation;
                    _continuationState = state;
                    _executionContext = capturedExecutionContext;
                    _synchronizationContext = capturedSynchronizationContext;
                    _runContinuationsAsynchronously = runAsynchronously;

                    if (_status != ValueTaskSourceStatus.Pending)
                    {
                        continuationToInvoke = _continuation;
                        continuationState = _continuationState;
                        executionContextToUse = _executionContext;
                        synchronizationContextToUse = _synchronizationContext;
                        shouldInvokeImmediately = true;
                        _continuation = null;
                        _continuationState = null;
                        _executionContext = null;
                        _synchronizationContext = null;
                    }
                }
            }

            if (shouldInvokeImmediately)
            {
                InvokeContinuation(
                    continuationToInvoke,
                    continuationState,
                    executionContextToUse,
                    synchronizationContextToUse,
                    runAsynchronously
                );
            }
        }

        private void SignalCompletion(ValueTaskSourceStatus status, Exception exception)
        {
            Action<object> continuationToInvoke = null;
            object continuationState = null;
            ExecutionContext executionContextToUse = null;
            SynchronizationContext synchronizationContextToUse = null;
            bool runAsynchronously;

            lock (_instanceGate)
            {
                if (_status != ValueTaskSourceStatus.Pending)
                {
                    return;
                }

                _status = status;
                _exception = exception;

                continuationToInvoke = _continuation;
                continuationState = _continuationState;
                executionContextToUse = _executionContext;
                synchronizationContextToUse = _synchronizationContext;
                runAsynchronously = _runContinuationsAsynchronously || status != ValueTaskSourceStatus.Succeeded;

                _continuation = null;
                _continuationState = null;
                _executionContext = null;
                _synchronizationContext = null;
            }

            InvokeContinuation(
                continuationToInvoke,
                continuationState,
                executionContextToUse,
                synchronizationContextToUse,
                runAsynchronously
            );
        }

        private void InvokeContinuation(
            Action<object> continuation,
            object state,
            ExecutionContext executionContext,
            SynchronizationContext synchronizationContext,
            bool runAsynchronously
        )
        {
            if (continuation == null)
            {
                return;
            }

            ContinuationWorkItem workItem = new ContinuationWorkItem(
                continuation,
                state,
                synchronizationContext,
                runAsynchronously
            );

            if (executionContext != null)
            {
                ExecutionContext.Run(
                    executionContext,
                    static contextState => InvokeContinuationCore((ContinuationWorkItem)contextState),
                    workItem
                );
                return;
            }

            InvokeContinuationCore(workItem);
        }

        private static void InvokeContinuationCore(ContinuationWorkItem workItem)
        {
            if (workItem.SynchronizationContext != null)
            {
                if (workItem.RunAsynchronously)
                {
                    workItem.SynchronizationContext.Post(PostCallback, workItem);
                    return;
                }

                workItem.SynchronizationContext.Send(SendCallback, workItem);
                return;
            }

            if (workItem.RunAsynchronously)
            {
                Task.Run(() => workItem.Continuation(workItem.State));
                return;
            }

            workItem.Continuation(workItem.State);
        }

        private static void PostCallback(object state)
        {
            ContinuationWorkItem workItem = (ContinuationWorkItem)state;
            workItem.Continuation(workItem.State);
        }

        private static void SendCallback(object state)
        {
            ContinuationWorkItem workItem = (ContinuationWorkItem)state;
            workItem.Continuation(workItem.State);
        }

        private void PrepareForUse()
        {
            lock (_instanceGate)
            {
                _inUse = true;
                _status = ValueTaskSourceStatus.Pending;
                _exception = null;
                _continuation = null;
                _continuationState = null;
                _executionContext = null;
                _synchronizationContext = null;
                _runContinuationsAsynchronously = true;

                unchecked
                {
                    _version++;
                    if (_version == 0)
                    {
                        _version = InitialVersion;
                    }
                }
            }
        }

        private void ReturnToPool()
        {
            lock (_instanceGate)
            {
                if (!_inUse)
                {
                    return;
                }

                _inUse = false;
                _status = ValueTaskSourceStatus.Pending;
                _exception = null;
                _continuation = null;
                _continuationState = null;
                _executionContext = null;
                _synchronizationContext = null;
                _runContinuationsAsynchronously = true;
            }

            lock (PoolGate)
            {
                if (_cachedInstance == null)
                {
                    _cachedInstance = this;
                }
            }
        }

        private void EnsureInUse()
        {
            if (!_inUse)
            {
                throw new InvalidOperationException(
                    "TransitionCompletionSource cannot create ValueTask when not rented."
                );
            }
        }

        private void VerifyToken(short token)
        {
            if (!_inUse)
            {
                throw new InvalidOperationException(
                    "TransitionCompletionSource token verification attempted when source is not active."
                );
            }

            if (token != _version)
            {
                throw new InvalidOperationException(
                    "TransitionCompletionSource token does not match the current version."
                );
            }
        }

        private readonly struct ContinuationWorkItem
        {
            public ContinuationWorkItem(
                Action<object> continuation,
                object state,
                SynchronizationContext synchronizationContext,
                bool runAsynchronously
            )
            {
                Continuation = continuation;
                State = state;
                SynchronizationContext = synchronizationContext;
                RunAsynchronously = runAsynchronously;
            }

            public Action<object> Continuation { get; }

            public object State { get; }

            public SynchronizationContext SynchronizationContext { get; }

            public bool RunAsynchronously { get; }
        }
    }
}
