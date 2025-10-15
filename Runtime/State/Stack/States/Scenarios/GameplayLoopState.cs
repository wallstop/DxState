namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public sealed class GameplayLoopState : IState
    {
        private readonly FunctionalState _inner;
        private readonly Func<ValueTask> _startGameplay;
        private readonly Func<ValueTask> _pause;
        private readonly Func<ValueTask> _resume;
        private readonly Func<ValueTask> _returnToMenu;

        public GameplayLoopState(
            string name,
            Func<ValueTask> startGameplay,
            Func<ValueTask> pause,
            Func<ValueTask> resume,
            Func<ValueTask> returnToMenu
        )
        {
            _startGameplay =
                startGameplay ?? throw new ArgumentNullException(nameof(startGameplay));
            _pause = pause ?? throw new ArgumentNullException(nameof(pause));
            _resume = resume ?? throw new ArgumentNullException(nameof(resume));
            _returnToMenu = returnToMenu ?? throw new ArgumentNullException(nameof(returnToMenu));

            _inner = new FunctionalState(
                name,
                async (direction, previous, progress) =>
                {
                    if (direction == StateDirection.Forward)
                    {
                        await _startGameplay().ConfigureAwait(false);
                    }

                    progress?.Report(1f);
                },
                async (direction, next, progress) =>
                {
                    await _returnToMenu().ConfigureAwait(false);
                    progress?.Report(1f);
                },
                async (previous, next, progress) =>
                {
                    await _returnToMenu().ConfigureAwait(false);
                    progress?.Report(1f);
                }
            );
        }

        public string Name => _inner.Name;

        public TickMode TickMode => _inner.TickMode;

        public bool TickWhenInactive => _inner.TickWhenInactive;

        public float? TimeInState => _inner.TimeInState;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            return _inner.Enter(previousState, progress, direction);
        }

        public void Tick(TickMode mode, float delta)
        {
            _inner.Tick(mode, delta);
        }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            return _inner.Exit(nextState, progress, direction);
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            return _inner.Remove(previousStatesInStack, nextStatesInStack, progress);
        }

        public ValueTask TriggerPauseAsync()
        {
            return _pause();
        }

        public ValueTask TriggerResumeAsync()
        {
            return _resume();
        }
    }
}
