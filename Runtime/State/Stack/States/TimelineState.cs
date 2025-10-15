namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.Playables;
    using WallstopStudios.DxState.Extensions;

    public interface ITimelineController
    {
        event Action Completed;

        bool IsPlaying { get; }

        void Play();

        void Stop();

        void ResetToStart();

        void SetPlaybackSpeed(float speed);
    }

    public sealed class TimelineState : IState
    {
        private readonly ITimelineController _controller;
        private readonly bool _rewindOnEnter;
        private readonly bool _waitForCompletion;
        private readonly bool _stopOnExit;
        private readonly float? _playbackSpeed;

        private float _enteredTime = -1f;

        public TimelineState(
            string name,
            ITimelineController controller,
            bool rewindOnEnter = true,
            bool waitForCompletion = true,
            bool stopOnExit = true,
            float? playbackSpeed = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("TimelineState requires a name.", nameof(name));
            }

            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            Name = name;
            _controller = controller;
            _rewindOnEnter = rewindOnEnter;
            _waitForCompletion = waitForCompletion;
            _stopOnExit = stopOnExit;
            _playbackSpeed = playbackSpeed;
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => _enteredTime >= 0f ? Time.time - _enteredTime : null;

        public static TimelineState Create(
            string name,
            PlayableDirector director,
            bool rewindOnEnter = true,
            bool waitForCompletion = true,
            bool stopOnExit = true,
            float? playbackSpeed = null
        )
        {
            return new TimelineState(
                name,
                new PlayableDirectorTimelineController(director),
                rewindOnEnter,
                waitForCompletion,
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
            _enteredTime = Time.time;

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

            if (!_waitForCompletion)
            {
                _controller.Play();
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            TaskCompletionSource<bool> completionSource = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously
            );

            void OnCompleted()
            {
                completionSource.TrySetResult(true);
            }

            _controller.Completed += OnCompleted;

            try
            {
                _controller.Play();
                if (_controller.IsPlaying)
                {
                    await completionSource.Task.ConfigureAwait(false);
                }
                UnityExtensions.ReportProgress(progress, 1f);
            }
            finally
            {
                _controller.Completed -= OnCompleted;
            }
        }

        public void Tick(TickMode mode, float delta)
        {
        }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _enteredTime = -1f;
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

            public bool IsPlaying
            {
                get
                {
                    return _director != null && _director.state == PlayState.Playing;
                }
            }

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
                float clampedSpeed = Mathf.Approximately(speed, 0f) ? 1f : speed;
                PlayableGraph graph = _director.playableGraph;
                if (!graph.IsValid())
                {
                    return;
                }

                int rootCount = graph.GetRootPlayableCount();
                for (int i = 0; i < rootCount; ++i)
                {
                    Playable playable = graph.GetRootPlayable(i);
                    if (!playable.IsValid())
                    {
                        continue;
                    }

                    playable.SetSpeed(clampedSpeed);
                }
            }

            private void HandleStopped(PlayableDirector director)
            {
                Completed?.Invoke();
            }
        }
    }
}
