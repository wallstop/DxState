namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public sealed class ModalStateStack : IState
    {
        private readonly StateStack _modalStack;
        private readonly IReadOnlyList<IState> _initialStates;
        private readonly bool _clearOnExit;

        public ModalStateStack(
            string name,
            StateStack modalStack,
            IReadOnlyList<IState> initialStates = null,
            bool clearOnExit = true
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Modal state stack requires a name.", nameof(name));
            }

            if (modalStack == null)
            {
                throw new ArgumentNullException(nameof(modalStack));
            }

            Name = name;
            _modalStack = modalStack;
            _initialStates = initialStates;
            _clearOnExit = clearOnExit;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => null;

        public async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (_initialStates != null)
            {
                for (int i = 0; i < _initialStates.Count; i++)
                {
                    IState state = _initialStates[i];
                    if (state == null)
                    {
                        continue;
                    }

                    await _modalStack.PushAsync(state);
                }
            }

            progress?.Report(1f);
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
            if (_clearOnExit)
            {
                await _modalStack.ClearAsync();
            }

            progress?.Report(1f);
        }

        public async ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            await _modalStack.ClearAsync();
            progress?.Report(1f);
        }
    }
}
