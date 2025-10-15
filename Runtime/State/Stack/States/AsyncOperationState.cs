namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public readonly struct AsyncOperationStateCallbacks
    {
        public AsyncOperationStateCallbacks(
            Func<AsyncOperationStateContext, AsyncOperation> onEnter,
            Func<AsyncOperationStateContext, AsyncOperation> onExit,
            Func<AsyncOperationStateContext, AsyncOperation> onRemove
        )
        {
            OnEnter = onEnter;
            OnExit = onExit;
            OnRemove = onRemove;
        }

        public Func<AsyncOperationStateContext, AsyncOperation> OnEnter { get; }

        public Func<AsyncOperationStateContext, AsyncOperation> OnExit { get; }

        public Func<AsyncOperationStateContext, AsyncOperation> OnRemove { get; }

        public bool HasEnterCallback => OnEnter != null;

        public bool HasExitCallback => OnExit != null;

        public bool HasRemoveCallback => OnRemove != null;
    }

    public readonly struct AsyncOperationStateContext
    {
        public AsyncOperationStateContext(IState counterpartState, StateDirection direction)
        {
            CounterpartState = counterpartState;
            Direction = direction;
        }

        public IState CounterpartState { get; }

        public StateDirection Direction { get; }
    }

    public sealed class AsyncOperationState : IState
    {
        private readonly AsyncOperationStateCallbacks _callbacks;

        private AsyncOperation _inFlightOperation;

        private readonly Func<AsyncOperation, IProgress<float>, ValueTask> _operationAwaiter;

        public AsyncOperationState(
            string name,
            AsyncOperationStateCallbacks callbacks,
            TickMode tickMode = TickMode.None,
            bool tickWhenInactive = false,
            Func<AsyncOperation, IProgress<float>, ValueTask> operationAwaiter = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("State name must be provided.", nameof(name));
            }

            Name = name;
            _callbacks = callbacks;
            _operationAwaiter = operationAwaiter;
            TickMode = tickMode;
            TickWhenInactive = tickWhenInactive;
        }

        public string Name { get; }

        public TickMode TickMode { get; }

        public bool TickWhenInactive { get; }

        public float? TimeInState => null;

        public async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (direction != StateDirection.Forward)
            {
                ReportCompletion(progress);
                return;
            }

            if (!_callbacks.HasEnterCallback)
            {
                ReportCompletion(progress);
                return;
            }

            AsyncOperationStateContext context = new AsyncOperationStateContext(previousState, direction);
            await ExecuteOperation(_callbacks.OnEnter, context, progress);
        }

        public void Tick(TickMode mode, float delta)
        {
        }

        public async ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (!_callbacks.HasExitCallback)
            {
                ReportCompletion(progress);
                return;
            }

            AsyncOperationStateContext context = new AsyncOperationStateContext(nextState, direction);
            await ExecuteOperation(_callbacks.OnExit, context, progress);
        }

        public async ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            if (!_callbacks.HasRemoveCallback)
            {
                ReportCompletion(progress);
                return;
            }

            IState counterpart = nextStatesInStack.Count > 0 ? nextStatesInStack[0] : null;
            AsyncOperationStateContext context = new AsyncOperationStateContext(
                counterpart,
                StateDirection.Backward
            );
            await ExecuteOperation(_callbacks.OnRemove, context, progress);
        }

        private async ValueTask ExecuteOperation<TProgress>(
            Func<AsyncOperationStateContext, AsyncOperation> operationFactory,
            AsyncOperationStateContext context,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            if (operationFactory == null)
            {
                ReportCompletion(progress);
                return;
            }

            AsyncOperation operation = operationFactory(context);
            if (operation == null)
            {
                ReportCompletion(progress);
                return;
            }

            if (_inFlightOperation != null && _inFlightOperation != operation)
            {
                throw new InvalidOperationException(
                    $"AsyncOperationState '{Name}' cannot start a new operation while another is in flight."
                );
            }

            _inFlightOperation = operation;
            try
            {
            if (_operationAwaiter != null)
            {
                await _operationAwaiter(operation, progress);
            }
            else
            {
                await operation.AwaitWithProgress(progress);
            }
            ReportCompletion(progress);
            }
            finally
            {
                _inFlightOperation = null;
            }
        }

        private static void ReportCompletion<TProgress>(TProgress progress)
            where TProgress : IProgress<float>
        {
            IProgress<float> reporter = progress;
            if (reporter == null)
            {
                return;
            }

            UnityExtensions.ReportProgress(reporter, 1f);
        }
    }
}
