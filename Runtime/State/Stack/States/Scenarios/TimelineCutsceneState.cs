namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.Playables;
    using WallstopStudios.DxState.Extensions;

    public sealed class TimelineCutsceneState : IState
    {
        private readonly ITimelineController _controller;
        private readonly Func<bool> _skipRequested;
        private readonly Action _onSkipped;
        private readonly bool _rewindOnEnter;
        private readonly bool _stopOnExit;
        private readonly float? _playbackSpeed;

        private float _timeEntered = -1f;

        public TimelineCutsceneState(
            string name,
            ITimelineController controller,
            Func<bool> skipRequested = null,
            Action onSkipped = null,
            bool rewindOnEnter = true,
            bool stopOnExit = true,
            float? playbackSpeed = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("TimelineCutsceneState requires a name.", nameof(name));
            }

            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            Name = name;
            _skipRequested = skipRequested;
            _onSkipped = onSkipped;
            _rewindOnEnter = rewindOnEnter;
            _stopOnExit = stopOnExit;
            _playbackSpeed = playbackSpeed;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => _timeEntered >= 0 ? Time.time - _timeEntered : null;

        public static TimelineCutsceneState Create(
            string name,
            PlayableDirector director,
            Func<bool> skipRequested = null,
            Action onSkipped = null,
            bool rewindOnEnter = true,
            bool stopOnExit = true,
            float? playbackSpeed = null
        )
        {
            return new TimelineCutsceneState(
                name,
                new PlayableDirectorTimelineController(director),
                skipRequested,
                onSkipped,
                rewindOnEnter,
                stopOnExit,
                playbackSpeed
            );
        }

        public async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _timeEntered = Time.time;
            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            if (_rewindOnEnter)
            {
                _controller.ResetToStart();
            }

            if (_playbackSpeed.HasValue)
            {
                _controller.SetPlaybackSpeed(_playbackSpeed.Value);
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
                _controller.Play();

                while (!completed)
                {
                    if (_skipRequested != null && _skipRequested())
                    {
                        completed = true;
                        _controller.Stop();
                        _onSkipped?.Invoke();
                        break;
                    }

                    await Task.Yield();
                }

                if (!completed)
                {
                    await completionSource.Task;
                }

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
            _timeEntered = -1f;
            if (_stopOnExit && _controller.IsPlaying)
            {
                _controller.Stop();
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
            if (_controller.IsPlaying)
            {
                _controller.Stop();
            }

            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        private sealed class PlayableDirectorTimelineController : ITimelineController
        {
            private readonly PlayableDirector _director;

            public PlayableDirectorTimelineController(PlayableDirector director)
            {
                _director = director ?? throw new ArgumentNullException(nameof(director));
                _director.stopped += HandleStopped;
            }

            public event Action Completed;

            public bool IsPlaying => _director != null && _director.state == PlayState.Playing;

            public void Play()
            {
                _director.Play();
            }

            public void Stop()
            {
                _director.Stop();
            }

            public void ResetToStart()
            {
                _director.time = 0;
                _director.Evaluate();
            }

            public void SetPlaybackSpeed(float speed)
            {
                float resolvedSpeed = Mathf.Approximately(speed, 0f) ? 1f : speed;
                PlayableGraph graph = _director.playableGraph;
                if (!graph.IsValid())
                {
                    return;
                }

                int rootCount = graph.GetRootPlayableCount();
                for (int i = 0; i < rootCount; i++)
                {
                    Playable playable = graph.GetRootPlayable(i);
                    if (!playable.IsValid())
                    {
                        continue;
                    }

                    playable.SetSpeed(resolvedSpeed);
                }
            }

            private void HandleStopped(PlayableDirector director)
            {
                Completed?.Invoke();
            }
        }
    }
}
