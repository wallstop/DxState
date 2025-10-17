namespace WallstopStudios.DxState.Tests.PlayMode.Runtime
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using DxMessaging.Unity;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;
    using WallstopStudios.DxState.State.Stack.Messages;
    using WallstopStudios.UnityHelpers.Core.Extension;
    using static WallstopStudios.DxState.Tests.PlayMode.Runtime.CoroutineTestUtilities;

    public sealed class StateStackMessagingRuntimeTests
    {
        [UnityTest]
        public IEnumerator EmitsMessagesWhileDisabledWhenEmitOverrideEnabled()
        {
            GameObject host = new GameObject("Messaging_DisabledOverride");
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                MessagingProbe probe = host.AddComponent<MessagingProbe>();
                MessagingComponent messaging = host.GetComponent<MessagingComponent>();
                messaging.emitMessagesWhenDisabled = true;

                TestState state = host.AddComponent<TestState>();
                state.Initialize("DisabledEmission");

                yield return WaitForFrames(1);

                messaging.enabled = false;
                host.SetActive(false);

                ValueTask pushTask = manager.PushAsync(state);
                yield return pushTask.AsCoroutine();
                yield return manager.WaitForTransitionCompletionAsync().AsCoroutine();

                Assert.IsTrue(
                    probe.PushedMessages.Count > 0,
                    "Expected pushed messages even while components disabled."
                );
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        private sealed class MessagingProbe : MessageAwareComponent
        {
            public List<StatePushedMessage> PushedMessages { get; } =
                new List<StatePushedMessage>();

            protected override bool RegisterForStringMessages => false;

            protected override bool MessageRegistrationTiedToEnableStatus => false;

            protected override void Awake()
            {
                base.Awake();
                Token?.Enable();
            }

            protected override void RegisterMessageHandlers()
            {
                base.RegisterMessageHandlers();
                _ = Token.RegisterUntargeted<StatePushedMessage>(HandlePushed);
            }

            private void HandlePushed(ref StatePushedMessage message)
            {
                PushedMessages.Add(message);
            }
        }

        private sealed class TestState : GameState
        {
            public void Initialize(string stateName)
            {
                _name = stateName;
            }
        }
    }
}
