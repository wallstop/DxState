namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using UnityEngine;

    public enum StateStackDiagnosticEventType
    {
        TransitionStart = 0,
        TransitionComplete = 1,
        StatePushed = 2,
        StatePopped = 3,
        StateFlattened = 4,
        StateRemoved = 5,
    }

    public readonly struct StateStackDiagnosticEvent
    {
        public StateStackDiagnosticEvent(
            StateStackDiagnosticEventType eventType,
            string previousState,
            string currentState,
            int stackDepth,
            DateTime timestampUtc
        )
        {
            EventType = eventType;
            PreviousState = previousState;
            CurrentState = currentState;
            StackDepth = stackDepth;
            TimestampUtc = timestampUtc;
        }

        public StateStackDiagnosticEventType EventType { get; }

        public string PreviousState { get; }

        public string CurrentState { get; }

        public int StackDepth { get; }

        public DateTime TimestampUtc { get; }

        public override string ToString()
        {
            return $"{TimestampUtc:O} {EventType} (prev: {PreviousState}, current: {CurrentState}, depth: {StackDepth})";
        }
    }

    public sealed class StateStackDiagnostics : IDisposable
    {
        private const string NullStateName = "<null>";

        private readonly StateStack _stateStack;
        private readonly List<StateStackDiagnosticEvent> _events;
        private readonly int _maxEventCount;
        private readonly bool _logEvents;
        private readonly Dictionary<string, float> _latestProgress;
        private readonly ReadOnlyDictionary<string, float> _latestProgressView;
        private readonly StateStackMetrics _metrics;
        private bool _isDisposed;

        public StateStackDiagnostics(StateStack stateStack, int maxEventCount, bool logEvents)
        {
            if (stateStack == null)
            {
                throw new ArgumentNullException(nameof(stateStack));
            }

            _stateStack = stateStack;
            _maxEventCount = Math.Max(1, maxEventCount);
            _logEvents = logEvents;
            _events = new List<StateStackDiagnosticEvent>(_maxEventCount);
            _latestProgress = new Dictionary<string, float>(StringComparer.Ordinal);
            _latestProgressView = new ReadOnlyDictionary<string, float>(_latestProgress);

            _metrics = new StateStackMetrics(stateStack);
            Subscribe();
        }

        public IReadOnlyList<StateStackDiagnosticEvent> Events => _events;

        public IReadOnlyDictionary<string, float> LatestProgress => _latestProgressView;

        public int Capacity => _maxEventCount;

        public bool LoggingEnabled => _logEvents;

        public int TransitionCount => _metrics.TransitionCount;

        public float AverageTransitionDuration => _metrics.AverageTransitionDuration;

        public float LongestTransitionDuration => _metrics.LongestTransitionDuration;

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            Unsubscribe();
            _metrics.Dispose();
        }

        private void Subscribe()
        {
            _stateStack.OnTransitionStart += HandleTransitionStart;
            _stateStack.OnTransitionComplete += HandleTransitionComplete;
            _stateStack.OnStatePushed += HandleStatePushed;
            _stateStack.OnStatePopped += HandleStatePopped;
            _stateStack.OnFlattened += HandleStateFlattened;
            _stateStack.OnStateManuallyRemoved += HandleStateRemoved;
            _stateStack.OnTransitionProgress += HandleTransitionProgress;
        }

        private void Unsubscribe()
        {
            _stateStack.OnTransitionStart -= HandleTransitionStart;
            _stateStack.OnTransitionComplete -= HandleTransitionComplete;
            _stateStack.OnStatePushed -= HandleStatePushed;
            _stateStack.OnStatePopped -= HandleStatePopped;
            _stateStack.OnFlattened -= HandleStateFlattened;
            _stateStack.OnStateManuallyRemoved -= HandleStateRemoved;
            _stateStack.OnTransitionProgress -= HandleTransitionProgress;
        }

        private void HandleTransitionStart(IState previousState, IState targetState)
        {
            RecordEvent(StateStackDiagnosticEventType.TransitionStart, previousState, targetState);
        }

        private void HandleTransitionComplete(IState previousState, IState currentState)
        {
            RecordEvent(StateStackDiagnosticEventType.TransitionComplete, previousState, currentState);
            HandleTransitionProgress(currentState, 1f);
        }

        private void HandleStatePushed(IState previousState, IState currentState)
        {
            RecordEvent(StateStackDiagnosticEventType.StatePushed, previousState, currentState);
        }

        private void HandleStatePopped(IState previousState, IState currentState)
        {
            RecordEvent(StateStackDiagnosticEventType.StatePopped, previousState, currentState);
        }

        private void HandleStateFlattened(IState targetState)
        {
            RecordEvent(StateStackDiagnosticEventType.StateFlattened, targetState, targetState);
        }

        private void HandleStateRemoved(IState removedState)
        {
            RecordEvent(StateStackDiagnosticEventType.StateRemoved, removedState, removedState);
        }

        private void HandleTransitionProgress(IState state, float progress)
        {
            if (state == null)
            {
                _latestProgress[NullStateName] = progress;
                return;
            }

            _latestProgress[state.Name] = progress;
        }

        private void RecordEvent(
            StateStackDiagnosticEventType eventType,
            IState previousState,
            IState currentState
        )
        {
            if (_isDisposed)
            {
                return;
            }

            string previousName = previousState != null ? previousState.Name : NullStateName;
            string currentName = currentState != null ? currentState.Name : NullStateName;
            StateStackDiagnosticEvent diagnosticEvent = new StateStackDiagnosticEvent(
                eventType,
                previousName,
                currentName,
                _stateStack.Stack.Count,
                DateTime.UtcNow
            );

            if (_events.Count == _maxEventCount)
            {
                _events.RemoveAt(0);
            }

            _events.Add(diagnosticEvent);

            if (_logEvents)
            {
                Debug.Log(
                    $"[DxState] {diagnosticEvent.EventType} prev='{diagnosticEvent.PreviousState}' current='{diagnosticEvent.CurrentState}' depth={diagnosticEvent.StackDepth}"
                );
            }
        }

        public float? GetProgressForState(string stateName)
        {
            if (string.IsNullOrWhiteSpace(stateName))
            {
                return null;
            }

            if (!_latestProgress.TryGetValue(stateName, out float value))
            {
                return null;
            }

            return value;
        }

        public StateStackMetricSnapshot GetMetricsSnapshot()
        {
            return new StateStackMetricSnapshot(
                _metrics.TransitionCount,
                _metrics.AverageTransitionDuration,
                _metrics.LongestTransitionDuration
            );
        }
    }
}
