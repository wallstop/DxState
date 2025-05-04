namespace WallstopStudios.DxState.State.Machine.Component
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityHelpers.Core.Attributes;
    using UnityHelpers.Utils;
#if ODIN_INSPECTOR
    using Sirenix.OdinInspector;
#endif

    [Serializable]
    public abstract class StateComponent : SerializedMessageAwareComponent
    {
#if ODIN_INSPECTOR
        [ShowInInspector]
#endif
        public virtual bool IsActive
        {
            get => _isActive;
            private set => _isActive = value;
        }

        public ComponentStateMachine StateMachine { get; set; }

#if ODIN_INSPECTOR
        [ShowInInspector]
#endif
        protected virtual IEnumerable<string> ImmutableUnableToEnterIfHasTag =>
            Enumerable.Empty<string>();

        [SiblingComponent(optional = true)]
        protected TagHandler _tagHandler;

        protected bool _isActive;
        private string[] _unableToEnterIfHasTag;

        protected override void Awake()
        {
            base.Awake();
            _unableToEnterIfHasTag = ImmutableUnableToEnterIfHasTag.ToArray();
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
    }
}
