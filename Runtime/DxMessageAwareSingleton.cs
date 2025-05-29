namespace WallstopStudios.DxState
{
    using global::DxMessaging.Core;
    using global::DxMessaging.Unity;
    using UnityEngine;
    using UnityHelpers.Core.Attributes;
    using UnityHelpers.Utils;

    [RequireComponent(typeof(MessagingComponent))]
    public abstract class DxMessageAwareSingleton<T> : RuntimeSingleton<T>
        where T : DxMessageAwareSingleton<T>
    {
        protected MessageRegistrationToken _messageRegistrationToken;

        protected virtual bool MessageRegistrationTiedToEnableStatus => true;

        protected bool _isQuitting;

        [SiblingComponent]
        protected MessagingComponent _messagingComponent;

        protected override void Awake()
        {
            base.Awake();
            SetupMessageHandlers();
        }

        private void SetupMessageHandlers()
        {
            _messageRegistrationToken ??= _messagingComponent.Create(this);
            RegisterMessageHandlers();
        }

        protected virtual void RegisterMessageHandlers()
        {
            // No-op, expectation is that implementations implement their own logic here
        }

        protected virtual void OnEnable()
        {
            if (MessageRegistrationTiedToEnableStatus)
            {
                _messageRegistrationToken?.Enable();
            }
        }

        protected virtual void OnDisable()
        {
            if (_isQuitting)
            {
                return;
            }

            if (MessageRegistrationTiedToEnableStatus)
            {
                _messageRegistrationToken?.Disable();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (_isQuitting)
            {
                return;
            }

            _messageRegistrationToken?.Disable();
            _messageRegistrationToken = null;
        }

        protected override void OnApplicationQuit()
        {
            base.OnApplicationQuit();
            _isQuitting = true;
        }
    }
}
