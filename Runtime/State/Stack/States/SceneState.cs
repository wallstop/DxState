namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using Extensions;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    [Serializable]
    public sealed class SceneState : IState
    {
        private const float SceneTargetProgress = 0.9f;

        [field: SerializeField]
        public string Name { get; set; }

        [field: SerializeField]
        public SceneTransitionMode TransitionMode { get; set; }

        [field: SerializeField]
        public LoadSceneParameters LoadSceneParameters { get; set; }

        [field: SerializeField]
        public UnloadSceneOptions UnloadSceneOptions { get; set; }

        [field: SerializeField]
        public bool RevertOnRemoval { get; set; } = true;

        public float? TimeInState => 0 <= _timeEntered ? Time.time - _timeEntered : null;

        [SerializeField]
        private float _timeEntered = -1;

        public SceneState() { }

        public SceneState(
            string name,
            SceneTransitionMode transitionMode,
            LoadSceneParameters? loadSceneParameters = null,
            UnloadSceneOptions unloadSceneOptions = UnloadSceneOptions.None,
            bool revertOnRemoval = true
        )
        {
            Name = name;
            TransitionMode = transitionMode;
            LoadSceneParameters =
                loadSceneParameters ?? new LoadSceneParameters(LoadSceneMode.Additive);
            UnloadSceneOptions = unloadSceneOptions;
            RevertOnRemoval = revertOnRemoval;
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
                return;
            }

            string name = Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Scene name cannot be null/empty");
            }

            SceneTransitionMode transitionMode = TransitionMode;
            switch (transitionMode)
            {
                case SceneTransitionMode.Addition:
                {
                    await SceneManager
                        .LoadSceneAsync(name, LoadSceneParameters)
                        .AwaitWithProgress(progress, total: SceneTargetProgress);
                    return;
                }
                case SceneTransitionMode.Removal:
                {
                    await SceneManager
                        .UnloadSceneAsync(name, UnloadSceneOptions)
                        .AwaitWithProgress(progress, total: SceneTargetProgress);
                    return;
                }
                default:
                {
                    throw new InvalidEnumArgumentException(
                        nameof(transitionMode),
                        (int)transitionMode,
                        typeof(SceneTransitionMode)
                    );
                }
            }
        }

        public void Tick(TickMode mode, float delta) { }

        public async ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            _timeEntered = -1;
            if (direction == StateDirection.Backward)
            {
                await Revert(progress);
            }
        }

        public async ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            _timeEntered = -1;
            for (int i = 0; i < nextStatesInStack.Count; ++i)
            {
                if (nextStatesInStack[i] is SceneState)
                {
                    return;
                }
            }

            await Revert(progress);
        }

        private async ValueTask Revert<TProgress>(TProgress progress)
            where TProgress : IProgress<float>
        {
            if (!RevertOnRemoval)
            {
                return;
            }

            string name = Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Scene name cannot be null/empty");
            }

            SceneTransitionMode transitionMode = TransitionMode;
            switch (transitionMode)
            {
                case SceneTransitionMode.Removal:
                {
                    await SceneManager
                        .LoadSceneAsync(name, LoadSceneParameters)
                        .AwaitWithProgress(progress, total: SceneTargetProgress);
                    return;
                }
                case SceneTransitionMode.Addition:
                {
                    await SceneManager
                        .UnloadSceneAsync(name, UnloadSceneOptions)
                        .AwaitWithProgress(progress, total: SceneTargetProgress);
                    return;
                }
                default:
                {
                    throw new InvalidEnumArgumentException(
                        nameof(transitionMode),
                        (int)transitionMode,
                        typeof(SceneTransitionMode)
                    );
                }
            }
        }
    }
}
