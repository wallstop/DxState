namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using Extensions;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    // TODO: Make serializable
    public sealed class SceneState : IState
    {
        public string Name { get; set; }
        public SceneTransitionMode TransitionMode { get; set; }
        public LoadSceneParameters LoadSceneParameters { get; set; }

        public UnloadSceneOptions UnloadSceneOptions { get; set; }

        public bool RevertOnRemoval { get; set; }

        public float? TimeInState => 0 <= _timeEntered ? Time.time - _timeEntered : null;

        private float _timeEntered = -1;

        public SceneState() { }

        public SceneState(
            string name,
            SceneTransitionMode transitionMode,
            LoadSceneParameters? loadSceneParameters = null,
            UnloadSceneOptions unloadSceneOptions = UnloadSceneOptions.None,
            bool revertOnRemoval = false
        )
        {
            Name = name;
            TransitionMode = transitionMode;
            LoadSceneParameters =
                loadSceneParameters ?? new LoadSceneParameters(LoadSceneMode.Additive);
            UnloadSceneOptions = unloadSceneOptions;
            RevertOnRemoval = revertOnRemoval;
        }

        public async ValueTask Enter<TProgress>(IState previousState, TProgress progress)
            where TProgress : IProgress<float>
        {
            _timeEntered = Time.time;
            string name = Name;
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"Scene name cannot be null/empty", nameof(name));
            }

            SceneTransitionMode transitionMode = TransitionMode;
            switch (transitionMode)
            {
                case SceneTransitionMode.Addition:
                {
                    await SceneManager
                        .LoadSceneAsync(name, LoadSceneParameters)
                        .AwaitWithProgress(progress);
                    return;
                }
                case SceneTransitionMode.Removal:
                {
                    await SceneManager
                        .UnloadSceneAsync(name, UnloadSceneOptions)
                        .AwaitWithProgress(progress);
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

        public ValueTask Exit<TProgress>(IState nextState, TProgress progress)
            where TProgress : IProgress<float>
        {
            _timeEntered = -1;
            return new ValueTask();
        }

        public async ValueTask RevertFrom<TProgress>(IState previousState, TProgress progress)
            where TProgress : IProgress<float>
        {
            _timeEntered = Time.time;
            if (!RevertOnRemoval)
            {
                return;
            }

            string name = Name;
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException($"Scene name cannot be null/empty", nameof(name));
            }

            SceneTransitionMode transitionMode = TransitionMode;
            switch (transitionMode)
            {
                case SceneTransitionMode.Removal:
                {
                    await SceneManager
                        .LoadSceneAsync(name, LoadSceneParameters)
                        .AwaitWithProgress(progress);
                    return;
                }
                case SceneTransitionMode.Addition:
                {
                    await SceneManager
                        .UnloadSceneAsync(name, UnloadSceneOptions)
                        .AwaitWithProgress(progress);
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
