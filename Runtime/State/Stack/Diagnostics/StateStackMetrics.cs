namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using System;

    public sealed class StateStackMetrics : IDisposable
    {
        private readonly StateStack _stateStack;
        private readonly object _gate;

        private int _transitionCount;
        private float _totalTransitionDuration;
        private float _longestTransition;
        private DateTime _lastTransitionStart;
        private bool _isMeasuring;

        public StateStackMetrics(StateStack stateStack)
        {
            _stateStack = stateStack ?? throw new ArgumentNullException(nameof(stateStack));
            _gate = new object();
            Subscribe();
        }

        public int TransitionCount
        {
            get
            {
                lock (_gate)
                {
                    return _transitionCount;
                }
            }
        }

        public float AverageTransitionDuration
        {
            get
            {
                lock (_gate)
                {
                    if (_transitionCount == 0)
                    {
                        return 0f;
                    }

                    return _totalTransitionDuration / _transitionCount;
                }
            }
        }

        public float LongestTransitionDuration
        {
            get
            {
                lock (_gate)
                {
                    return _longestTransition;
                }
            }
        }

        public void Dispose()
        {
            Unsubscribe();
        }

        private void Subscribe()
        {
            _stateStack.OnTransitionStart += HandleTransitionStart;
            _stateStack.OnTransitionComplete += HandleTransitionComplete;
        }

        private void Unsubscribe()
        {
            _stateStack.OnTransitionStart -= HandleTransitionStart;
            _stateStack.OnTransitionComplete -= HandleTransitionComplete;
        }

        private void HandleTransitionStart(IState _, IState __)
        {
            lock (_gate)
            {
                _isMeasuring = true;
                _lastTransitionStart = DateTime.UtcNow;
            }
        }

        private void HandleTransitionComplete(IState _, IState __)
        {
            lock (_gate)
            {
                if (!_isMeasuring)
                {
                    return;
                }

                _isMeasuring = false;
                float duration = (float)(DateTime.UtcNow - _lastTransitionStart).TotalSeconds;
                _transitionCount++;
                _totalTransitionDuration += duration;
                if (duration > _longestTransition)
                {
                    _longestTransition = duration;
                }
            }
        }
    }
}
