namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public interface IDialogueController
    {
        event Action Completed;

        bool IsRunning { get; }

        void StartDialogue();

        void Skip();

        void Cancel();
    }

    public sealed class DialogueState : IState
    {
        private readonly IDialogueController _controller;
        private readonly Func<bool> _skipRequested;
        private readonly Action _onStarted;
        private readonly Action _onCompleted;
        private readonly Action _onSkipped;

        private float _timeInState = -1f;

        public DialogueState(
            string name,
            IDialogueController controller,
            Func<bool> skipRequested = null,
            Action onStarted = null,
            Action onCompleted = null,
            Action onSkipped = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("DialogueState requires a name.", nameof(name));
            }

            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            Name = name;
            _skipRequested = skipRequested;
            _onStarted = onStarted;
            _onCompleted = onCompleted;
            _onSkipped = onSkipped;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => _timeInState >= 0f ? Time.time - _timeInState : null;

        public async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _timeInState = Time.time;
            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );
            bool completed = false;

            void OnCompleted()
            {
                if (completed)
                {
                    return;
                }

                completed = true;
                completionSource.TrySetResult(true);
            }

            _controller.Completed += OnCompleted;

            try
            {
                _onStarted?.Invoke();
                _controller.StartDialogue();

                while (!completed)
                {
                    if (_skipRequested != null && _skipRequested())
                    {
                        _controller.Skip();
                        completed = true;
                        _onSkipped?.Invoke();
                        completionSource.TrySetResult(true);
                        break;
                    }

                    await Task.Yield();
                }

                await completionSource.Task;
                _onCompleted?.Invoke();
                UnityExtensions.ReportProgress(progress, 1f);
            }
            finally
            {
                _controller.Completed -= OnCompleted;
            }
        }

        public void Tick(TickMode mode, float delta) { }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _timeInState = -1f;
            if (_controller.IsRunning)
            {
                _controller.Cancel();
            }

            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        public ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            if (_controller.IsRunning)
            {
                _controller.Cancel();
            }

            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }
    }
}
