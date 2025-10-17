namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;
    using System.Collections.Generic;
    using UnityHelpers.Core.Attributes;
    using UnityHelpers.Core.Extension;
    using UnityHelpers.Tags;
    using WallstopStudios.UnityHelpers.Utils;
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    [Serializable]
    public abstract class StateComponent : SerializedMessageAwareComponent, IStateComponent
    {
        private static readonly Dictionary<Type, string[]> CachedTagRestrictions =
            new Dictionary<Type, string[]>();
        private static readonly object TagCacheGate = new object();

#if ODIN_INSPECTOR
        [ShowInInspector]
#endif
        public virtual bool IsActive
        {
            get => _isActive;
            private set => _isActive = value;
        }

        public StateMachine<IStateComponent> StateMachine { get; set; }

#if ODIN_INSPECTOR
        [ShowInInspector]
#endif
        protected virtual IEnumerable<string> ImmutableUnableToEnterIfHasTag =>
            Array.Empty<string>();

        [SiblingComponent(Optional = true)]
        protected TagHandler _tagHandler;

        protected bool _isActive;
        private string[] _unableToEnterIfHasTag;

        protected override void Awake()
        {
            base.Awake();
            _unableToEnterIfHasTag = ResolveTagRestrictions();
        }

        public virtual bool ShouldEnter()
        {
            if (IsEnterBlockedByTag())
            {
                return false;
            }

            return !IsActive;
        }

        public void Enter()
        {
            if (IsActive)
            {
                return;
            }

            OnEnter();
            IsActive = true;
        }

        public void Exit()
        {
            if (!IsActive)
            {
                return;
            }

            IsActive = false;
            OnExit();
        }

        public virtual void Log(FormattableString message)
        {
            WallstopStudiosLogger.Log(this, message);
        }

        protected virtual void OnEnter()
        {
            // No-op in base
        }

        protected virtual void OnExit()
        {
            // No-op in base
        }

        protected virtual bool IsEnterBlockedByTag()
        {
            if (_tagHandler == null)
            {
                return false;
            }

            return _tagHandler.HasAnyTag(_unableToEnterIfHasTag);
        }

        private string[] ResolveTagRestrictions()
        {
            Type componentType = GetType();
            lock (TagCacheGate)
            {
                if (CachedTagRestrictions.TryGetValue(componentType, out string[] cachedTags))
                {
                    return cachedTags;
                }
            }

            IEnumerable<string> immutableTags = ImmutableUnableToEnterIfHasTag;
            string[] materializedTags = SnapshotTags(immutableTags);

            lock (TagCacheGate)
            {
                if (!CachedTagRestrictions.TryGetValue(componentType, out string[] existing))
                {
                    CachedTagRestrictions[componentType] = materializedTags;
                    return materializedTags;
                }

                return existing;
            }
        }

        private static string[] SnapshotTags(IEnumerable<string> source)
        {
            if (source == null)
            {
                return Array.Empty<string>();
            }

            if (source is string[] arraySource)
            {
                if (arraySource.Length == 0)
                {
                    return Array.Empty<string>();
                }

                return arraySource;
            }

            if (source is IReadOnlyCollection<string> readOnlyCollection)
            {
                if (readOnlyCollection.Count == 0)
                {
                    return Array.Empty<string>();
                }

                string[] buffer = new string[readOnlyCollection.Count];
                int index = 0;
                foreach (string tag in source)
                {
                    buffer[index] = tag;
                    index++;
                }
                return buffer;
            }

            using PooledResource<List<string>> pooled = Buffers<string>.GetList(
                0,
                out List<string> temporary
            );
            foreach (string tag in source)
            {
                temporary.Add(tag);
            }

            if (temporary.Count == 0)
            {
                return Array.Empty<string>();
            }

            string[] result = new string[temporary.Count];
            for (int i = 0; i < temporary.Count; i++)
            {
                result[i] = temporary[i];
            }

            return result;
        }
    }
}
