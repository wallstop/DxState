namespace WallstopStudios.DxState.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;

    public abstract class GameState : SerializedMessageAwareComponent, ICancellableState
    {
        public virtual TickMode TickMode => TickMode.None;

        public virtual string Name => string.IsNullOrWhiteSpace(_name) ? name : _name;

        public virtual float? TimeInState =>
            0 <= _stateEnteredTime ? Time.time - _stateEnteredTime : null;

        [SerializeField]
        protected string _name;

        protected float _stateEnteredTime = -1;

        public virtual ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _stateEnteredTime = Time.time;
            return new ValueTask();
        }

        public virtual ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction,
            CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Enter(previousState, progress, direction);
        }

        public virtual void Tick(TickMode mode, float delta) { }

        public virtual ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _stateEnteredTime = -1;
            return new ValueTask();
        }

        public virtual ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction,
            CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Exit(nextState, progress, direction);
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            _stateEnteredTime = -1;
            return new ValueTask();
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress,
            CancellationToken cancellationToken
        )
            where TProgress : IProgress<float>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Remove(previousStatesInStack, nextStatesInStack, progress);
        }

        protected virtual void OnValidate()
        {
            if (Application.isEditor && !Application.isPlaying && string.IsNullOrWhiteSpace(_name))
            {
                _name = name;
            }
        }
    }
}
