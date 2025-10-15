namespace WallstopStudios.DxState.State.Stack.States.Scenarios
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.Extensions;

    public sealed class ShaderPreloadState : IState
    {
        private readonly ShaderVariantCollection[] _collections;

        public ShaderPreloadState(string name, IEnumerable<ShaderVariantCollection> collections)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Shader preload state requires a name.", nameof(name));
            }

            if (collections == null)
            {
                throw new ArgumentNullException(nameof(collections));
            }

            Name = name;
            _collections = ToArray(collections);
        }

        public string Name { get; }

        public TickMode TickMode => TickMode.None;

        public bool TickWhenInactive => false;

        public float? TimeInState => null;

        public ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            if (direction != StateDirection.Forward)
            {
                UnityExtensions.ReportProgress(progress, 1f);
                return default;
            }

            int length = _collections.Length;
            for (int i = 0; i < length; i++)
            {
                ShaderVariantCollection collection = _collections[i];
                if (collection == null)
                {
                    continue;
                }

                if (!collection.isWarmedUp)
                {
                    collection.WarmUp();
                }

                UnityExtensions.ReportProgress(progress, (i + 1f) / length);
            }

            UnityExtensions.ReportProgress(progress, 1f);
            return default;
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
            UnityExtensions.ReportProgress(progress, 1f);
            return default;
        }

        private static ShaderVariantCollection[] ToArray(
            IEnumerable<ShaderVariantCollection> source
        )
        {
            if (source is ShaderVariantCollection[] existing)
            {
                return existing;
            }

            List<ShaderVariantCollection> collected = new List<ShaderVariantCollection>();
            foreach (ShaderVariantCollection collection in source)
            {
                collected.Add(collection);
            }

            return collected.ToArray();
        }
    }
}

