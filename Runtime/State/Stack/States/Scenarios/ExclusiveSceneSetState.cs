namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using WallstopStudios.DxState.Extensions;

    public sealed class ExclusiveSceneSetState : IState
    {
        private readonly HashSet<string> _targetScenes;
        private readonly string[] _targetSceneOrder;
        private readonly LoadSceneParameters _loadParameters;
        private readonly UnloadSceneOptions _unloadOptions;
        private readonly ISceneOperationExecutor _sceneExecutor;
        private readonly Func<AsyncOperation, IProgress<float>, ValueTask> _operationAwaiter;

        public ExclusiveSceneSetState(
            string name,
            IEnumerable<string> targetScenes,
            LoadSceneParameters? loadParameters = null,
            UnloadSceneOptions unloadOptions = UnloadSceneOptions.None,
            ISceneOperationExecutor sceneExecutor = null,
            Func<AsyncOperation, IProgress<float>, ValueTask> operationAwaiter = null
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "ExclusiveSceneSetState requires a name.",
                    nameof(name)
                );
            }

            if (targetScenes == null)
            {
                throw new ArgumentNullException(nameof(targetScenes));
            }

            Name = name;
            List<string> ordered = new List<string>();
            _targetScenes = new HashSet<string>(StringComparer.Ordinal);
            foreach (string scene in targetScenes)
            {
                if (string.IsNullOrWhiteSpace(scene))
                {
                    continue;
                }

                if (_targetScenes.Add(scene))
                {
                    ordered.Add(scene);
                }
            }

            if (_targetScenes.Count == 0)
            {
                throw new ArgumentException(
                    "At least one target scene must be specified.",
                    nameof(targetScenes)
                );
            }

            _targetSceneOrder = ordered.ToArray();
            _loadParameters = loadParameters ?? new LoadSceneParameters(LoadSceneMode.Additive);
            _unloadOptions = unloadOptions;
            _sceneExecutor = sceneExecutor ?? new SceneManagerExecutor();
            _operationAwaiter = operationAwaiter;
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
            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            List<string> scenesToUnload = new List<string>();
            HashSet<string> remainingLoads = new HashSet<string>(
                _targetScenes,
                StringComparer.Ordinal
            );

            foreach (string loadedSceneName in _sceneExecutor.EnumerateLoadedScenes())
            {
                if (_targetScenes.Contains(loadedSceneName))
                {
                    remainingLoads.Remove(loadedSceneName);
                }
                else
                {
                    scenesToUnload.Add(loadedSceneName);
                }
            }

            int totalOperations = scenesToUnload.Count + remainingLoads.Count;
            if (totalOperations == 0)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            int completedOperations = 0;
            foreach (string sceneToUnload in scenesToUnload)
            {
                AsyncOperation unloadOperation = _sceneExecutor.UnloadSceneAsync(
                    sceneToUnload,
                    _unloadOptions
                );
                await AwaitOperation(unloadOperation, progress);
                completedOperations++;
                UnityExtensions.ReportProgress(
                    progress,
                    completedOperations / (float)totalOperations
                );
            }

            foreach (string sceneToLoad in _targetSceneOrder)
            {
                if (!remainingLoads.Contains(sceneToLoad))
                {
                    continue;
                }

                AsyncOperation loadOperation = _sceneExecutor.LoadSceneAsync(
                    sceneToLoad,
                    _loadParameters
                );
                await AwaitOperation(loadOperation, progress);
                completedOperations++;
                UnityExtensions.ReportProgress(
                    progress,
                    completedOperations / (float)totalOperations
                );
            }

            UnityExtensions.ReportProgress(progress, 1f);
        }

        public void Tick(TickMode mode, float delta) { }

        public ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        public async ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            List<string> scenesToUnload = new List<string>(_targetScenes);
            int count = scenesToUnload.Count;
            if (count == 0)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                string sceneName = scenesToUnload[i];
                AsyncOperation unloadOperation = _sceneExecutor.UnloadSceneAsync(
                    sceneName,
                    _unloadOptions
                );
                await AwaitOperation(unloadOperation, progress);
                UnityExtensions.ReportProgress(progress, (i + 1f) / count);
            }

            UnityExtensions.ReportProgress(progress, 1f);
        }

        private ValueTask AwaitOperation(
            AsyncOperation operation,
            IProgress<float> progress
        )
        {
            if (operation == null)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return default;
            }

            if (_operationAwaiter != null)
            {
                return _operationAwaiter(operation, progress);
            }

            return operation.AwaitWithProgress(progress);
        }

        public interface ISceneOperationExecutor
        {
            IEnumerable<string> EnumerateLoadedScenes();

            AsyncOperation LoadSceneAsync(string sceneName, LoadSceneParameters parameters);

            AsyncOperation UnloadSceneAsync(string sceneName, UnloadSceneOptions options);
        }

        internal sealed class SceneManagerExecutor : ISceneOperationExecutor
        {
            public IEnumerable<string> EnumerateLoadedScenes()
            {
                int count = SceneManager.sceneCount;
                for (int i = 0; i < count; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!scene.IsValid())
                    {
                        continue;
                    }

                    yield return scene.name;
                }
            }

            public AsyncOperation LoadSceneAsync(string sceneName, LoadSceneParameters parameters)
            {
                return SceneManager.LoadSceneAsync(sceneName, parameters);
            }

            public AsyncOperation UnloadSceneAsync(string sceneName, UnloadSceneOptions options)
            {
                return SceneManager.UnloadSceneAsync(sceneName, options);
            }
        }
    }
}
