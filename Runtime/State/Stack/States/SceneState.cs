namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using WallstopStudios.DxState.Extensions;
    using AsyncOperation = UnityEngine.AsyncOperation;

    [Serializable]
    public class SceneState : IState
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

        private bool _isTransitioning;
        private AsyncOperation _activeOperation;
        private Task _activeOperationTask;
        private int _activeReferenceCount;

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
                ReportCompletion(progress);
                return;
            }

            string name = Name;
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Scene name cannot be null/empty");
            }

            bool acquiredReference = TryAcquireReference();
            SceneTransitionMode transitionMode = TransitionMode;
            try
            {
                switch (transitionMode)
                {
                    case SceneTransitionMode.Addition:
                    {
                        await LoadSceneAsync(name, progress);
                        return;
                    }
                    case SceneTransitionMode.Removal:
                    {
                        await UnloadSceneAsync(name, progress);
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
            catch
            {
                if (acquiredReference)
                {
                    ReleaseReference(force: true);
                }
                throw;
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
            else
            {
                ReportCompletion(progress);
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
                    ReleaseReference(force: false);
                    ReportCompletion(progress);
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
                ReleaseReference(force: false);
                ReportCompletion(progress);
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
                    await LoadSceneAsync(name, progress);
                    return;
                }
                case SceneTransitionMode.Addition:
                {
                    bool shouldUnload = ReleaseReference(force: false);
                    if (!shouldUnload)
                    {
                        ReportCompletion(progress);
                        return;
                    }

                    await UnloadSceneAsync(name, progress);
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

        private async ValueTask LoadSceneAsync<TProgress>(string sceneName, TProgress progress)
            where TProgress : IProgress<float>
        {
            if (IsSceneOperationInFlight())
            {
                await AwaitExistingOperation(progress);
                return;
            }

            if (IsSceneLoaded(sceneName))
            {
                ReportCompletion(progress);
                return;
            }

            _isTransitioning = true;
            try
            {
                AsyncOperation operation = CreateLoadOperation(sceneName, LoadSceneParameters);
                await AwaitOperationInternal(operation, progress);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async ValueTask UnloadSceneAsync<TProgress>(string sceneName, TProgress progress)
            where TProgress : IProgress<float>
        {
            if (IsSceneOperationInFlight())
            {
                await AwaitExistingOperation(progress);
                return;
            }

            if (!IsSceneLoaded(sceneName))
            {
                ReportCompletion(progress);
                return;
            }

            _isTransitioning = true;
            try
            {
                AsyncOperation operation = CreateUnloadOperation(sceneName, UnloadSceneOptions);
                await AwaitOperationInternal(operation, progress);
            }
            finally
            {
                _isTransitioning = false;
            }
        }

        private async ValueTask AwaitExistingOperation<TProgress>(TProgress progress)
            where TProgress : IProgress<float>
        {
            Task activeTask = _activeOperationTask;
            if (activeTask == null)
            {
                ReportCompletion(progress);
                return;
            }

            await activeTask.ConfigureAwait(false);
            ReportCompletion(progress);
        }

        private async ValueTask AwaitOperationInternal<TProgress>(
            AsyncOperation operation,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            if (operation == null)
            {
                ReportCompletion(progress);
                throw new InvalidOperationException($"Scene operation for '{Name}' returned null.");
            }

            _activeOperation = operation;
            Task awaitingTask = AwaitSceneOperationAsync(operation, progress);
            _activeOperationTask = awaitingTask;
            try
            {
                await awaitingTask.ConfigureAwait(false);
                ReportCompletion(progress);
            }
            finally
            {
                _activeOperation = null;
                _activeOperationTask = null;
            }
        }

        protected virtual Task AwaitSceneOperationAsync(
            AsyncOperation operation,
            IProgress<float> progress
        )
        {
            return operation.AwaitWithProgress(progress, total: SceneTargetProgress).AsTask();
        }

        protected virtual AsyncOperation CreateLoadOperation(
            string sceneName,
            LoadSceneParameters parameters
        )
        {
            return SceneManager.LoadSceneAsync(sceneName, parameters);
        }

        protected virtual AsyncOperation CreateUnloadOperation(
            string sceneName,
            UnloadSceneOptions options
        )
        {
            return SceneManager.UnloadSceneAsync(sceneName, options);
        }

        protected virtual bool IsSceneLoaded(string sceneName)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            return scene.IsValid() && scene.isLoaded;
        }

        private bool IsSceneOperationInFlight()
        {
            return _isTransitioning;
        }

        private bool TryAcquireReference()
        {
            if (TransitionMode != SceneTransitionMode.Addition)
            {
                return false;
            }

            _activeReferenceCount++;
            return true;
        }

        private bool ReleaseReference(bool force)
        {
            if (TransitionMode != SceneTransitionMode.Addition)
            {
                return true;
            }

            if (_activeReferenceCount <= 0)
            {
                if (force)
                {
                    _activeReferenceCount = 0;
                    return true;
                }

                string sceneName = Name;
                if (string.IsNullOrEmpty(sceneName))
                {
                    return false;
                }

                return IsSceneLoaded(sceneName);
            }

            _activeReferenceCount--;
            if (force)
            {
                if (_activeReferenceCount < 0)
                {
                    _activeReferenceCount = 0;
                }
                return true;
            }

            return _activeReferenceCount == 0;
        }

        private static void ReportCompletion<TProgress>(TProgress progress)
            where TProgress : IProgress<float>
        {
            IProgress<float> reporter = progress;
            if (reporter != null)
            {
                UnityExtensions.ReportProgress(reporter, 1f);
            }
        }
    }
}
