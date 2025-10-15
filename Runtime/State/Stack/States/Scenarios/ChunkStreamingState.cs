namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public interface IChunkStreamingStrategy
    {
        IEnumerable<string> ResolveRequiredChunks(Vector3 anchorPosition);
    }

    public interface IChunkLoader
    {
        ValueTask EnsureLoadedAsync(string chunkId, IProgress<float> progress);

        ValueTask EnsureUnloadedAsync(string chunkId, IProgress<float> progress);
    }

    public sealed class ChunkStreamingState : IState
    {
        private readonly IChunkStreamingStrategy _strategy;
        private readonly IChunkLoader _loader;
        private readonly Func<Vector3> _anchorResolver;
        private readonly float _updateInterval;

        private float _timeSinceLastUpdate;
        private readonly HashSet<string> _activeChunks;

        public ChunkStreamingState(
            string name,
            IChunkStreamingStrategy strategy,
            IChunkLoader loader,
            Func<Vector3> anchorResolver,
            float updateIntervalSeconds = 0.5f
        )
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("ChunkStreamingState requires a name.", nameof(name));
            }

            if (strategy == null)
            {
                throw new ArgumentNullException(nameof(strategy));
            }

            if (loader == null)
            {
                throw new ArgumentNullException(nameof(loader));
            }

            if (anchorResolver == null)
            {
                throw new ArgumentNullException(nameof(anchorResolver));
            }

            Name = name;
            _strategy = strategy;
            _loader = loader;
            _anchorResolver = anchorResolver;
            _updateInterval = Mathf.Max(0.1f, updateIntervalSeconds);
            _activeChunks = new HashSet<string>(StringComparer.Ordinal);
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.Update;

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

            await UpdateChunksAsync(progress).ConfigureAwait(false);
        }

        public void Tick(TickMode mode, float delta)
        {
            if (mode != TickMode.Update)
            {
                return;
            }

            _timeSinceLastUpdate += delta;
            if (_timeSinceLastUpdate < _updateInterval)
            {
                return;
            }

            _timeSinceLastUpdate = 0f;
            _ = UpdateChunksAsync(new Progress<float>(_ => { }));
        }

        public async ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            await UnloadAllChunksAsync(progress).ConfigureAwait(false);
        }

        public async ValueTask Remove<TProgress>(
            IReadOnlyList<IState> previousStatesInStack,
            IReadOnlyList<IState> nextStatesInStack,
            TProgress progress
        )
            where TProgress : IProgress<float>
        {
            await UnloadAllChunksAsync(progress).ConfigureAwait(false);
        }

        private async ValueTask UpdateChunksAsync<TProgress>(TProgress progress)
            where TProgress : IProgress<float>
        {
            Vector3 anchorPosition = _anchorResolver();
            HashSet<string> required = new HashSet<string>(
                _strategy.ResolveRequiredChunks(anchorPosition),
                StringComparer.Ordinal
            );

            List<string> toUnload = new List<string>();
            foreach (string chunk in _activeChunks)
            {
                if (!required.Contains(chunk))
                {
                    toUnload.Add(chunk);
                }
            }

            List<string> toLoad = new List<string>();
            foreach (string chunk in required)
            {
                if (!_activeChunks.Contains(chunk))
                {
                    toLoad.Add(chunk);
                }
            }

            int totalOperations = toUnload.Count + toLoad.Count;
            int completed = 0;
            foreach (string chunk in toUnload)
            {
                await _loader.EnsureUnloadedAsync(chunk, progress).ConfigureAwait(false);
                _activeChunks.Remove(chunk);
                completed++;
                UnityExtensions.ReportProgress(progress, totalOperations == 0 ? 1f : completed / (float)totalOperations);
            }

            foreach (string chunk in toLoad)
            {
                await _loader.EnsureLoadedAsync(chunk, progress).ConfigureAwait(false);
                _activeChunks.Add(chunk);
                completed++;
                UnityExtensions.ReportProgress(progress, totalOperations == 0 ? 1f : completed / (float)totalOperations);
            }

            if (totalOperations == 0)
            {
                UnityExtensions.ReportProgress(progress, 1f);
            }
        }

        private async ValueTask UnloadAllChunksAsync<TProgress>(TProgress progress)
            where TProgress : IProgress<float>
        {
            List<string> chunks = new List<string>(_activeChunks);
            int count = chunks.Count;
            if (count == 0)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return;
            }

            for (int i = 0; i < count; i++)
            {
                string chunk = chunks[i];
                await _loader.EnsureUnloadedAsync(chunk, progress).ConfigureAwait(false);
                _activeChunks.Remove(chunk);
                UnityExtensions.ReportProgress(progress, (i + 1f) / count);
            }
        }
    }
}

