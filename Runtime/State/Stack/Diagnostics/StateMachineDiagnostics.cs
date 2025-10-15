namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using System;
    using System.Collections.Generic;
    using WallstopStudios.DxState.State.Machine;

    public sealed class StateMachineDiagnostics<TState>
    {
        private readonly Queue<TransitionExecutionContext<TState>> _recentTransitions;
        private readonly int _capacity;

        public StateMachineDiagnostics(int capacity)
        {
            _capacity = Math.Max(1, capacity);
            _recentTransitions = new Queue<TransitionExecutionContext<TState>>(_capacity);
        }

        public IReadOnlyCollection<TransitionExecutionContext<TState>> RecentTransitions => _recentTransitions;

        public void RecordTransition(TransitionExecutionContext<TState> context)
        {
            if (_recentTransitions.Count == _capacity)
            {
                _recentTransitions.Dequeue();
            }

            _recentTransitions.Enqueue(context);
        }

        public void Clear()
        {
            _recentTransitions.Clear();
        }
    }
}
