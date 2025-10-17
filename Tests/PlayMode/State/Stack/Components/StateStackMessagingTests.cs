namespace WallstopStudios.DxState.Tests.PlayMode.State.Stack.Components
{
    using System;
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

    public sealed class StateStackMessagingTests
    {
        [UnityTest]
        public IEnumerator EmitsDxMessagingEventsForStackOperations()
        {
            GameObject host = new GameObject("Messaging_Bridge_Test");
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                MessagingProbe probe = host.AddComponent<MessagingProbe>();

                TestGameState first = host.AddComponent<TestGameState>();
                first.Initialize("First");
                TestGameState second = host.AddComponent<TestGameState>();
                second.Initialize("Second");

                manager.TryRegister(first, force: true);
                manager.TryRegister(second, force: true);

                yield return null;

                Assert.IsNotNull(probe.Token);
                Assert.IsTrue(probe.Token.Enabled, "Messaging probe token should be enabled.");

                yield return WaitForValueTask(manager.PushAsync(first));
                yield return WaitForValueTask(manager.PushAsync(second));
                yield return WaitForValueTask(manager.PopAsync());
                yield return WaitForValueTask(manager.FlattenAsync(first));
                yield return WaitForValueTask(manager.PushAsync(second));
                yield return WaitForValueTask(manager.RemoveAsync(second));
                yield return WaitForValueTask(manager.WaitForTransitionCompletionAsync());
                yield return null;

                Assert.GreaterOrEqual(
                    probe.PushedMessages.Count,
                    1,
                    "Expected at least one StatePushedMessage."
                );
                Assert.GreaterOrEqual(
                    probe.PoppedMessages.Count,
                    1,
                    "Expected at least one StatePoppedMessage."
                );
                Assert.GreaterOrEqual(
                    probe.TransitionStartMessages.Count,
                    1,
                    "Expected TransitionStartMessage emissions."
                );
                Assert.GreaterOrEqual(
                    probe.TransitionCompleteMessages.Count,
                    1,
                    "Expected TransitionCompleteMessage emissions."
                );
                Assert.GreaterOrEqual(
                    probe.FlattenedMessages.Count,
                    1,
                    "Expected StateStackFlattenedMessage emissions."
                );
                Assert.GreaterOrEqual(
                    probe.TransitionProgress.Count,
                    1,
                    "Expected TransitionProgressChangedMessage emissions."
                );
                Assert.GreaterOrEqual(
                    probe.ManualRemovalMessages.Count,
                    1,
                    "Expected StateManuallyRemovedMessage emissions."
                );
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static IEnumerator WaitForValueTask(ValueTask valueTask)
        {
            return valueTask.AsCoroutine();
        }

        private static IEnumerator WaitForValueTask<TValue>(ValueTask<TValue> valueTask)
        {
            return valueTask.AsCoroutine();
        }

        private sealed class MessagingProbe : MessageAwareComponent
        {
            public List<StatePushedMessage> PushedMessages { get; } =
                new List<StatePushedMessage>();
            public List<StatePoppedMessage> PoppedMessages { get; } =
                new List<StatePoppedMessage>();
            public List<TransitionStartMessage> TransitionStartMessages { get; } =
                new List<TransitionStartMessage>();
            public List<TransitionCompleteMessage> TransitionCompleteMessages { get; } =
                new List<TransitionCompleteMessage>();
            public List<StateStackFlattenedMessage> FlattenedMessages { get; } =
                new List<StateStackFlattenedMessage>();
            public List<TransitionProgressChangedMessage> TransitionProgress { get; } =
                new List<TransitionProgressChangedMessage>();
            public List<StateManuallyRemovedMessage> ManualRemovalMessages { get; } =
                new List<StateManuallyRemovedMessage>();

            protected override bool RegisterForStringMessages => false;

            protected override bool MessageRegistrationTiedToEnableStatus => false;

            protected override void Awake()
            {
                if (!TryGetComponent(out _messagingComponent))
                {
                    _messagingComponent = gameObject.AddComponent<MessagingComponent>();
                }

                base.Awake();
                Token?.Enable();
            }

            protected override void RegisterMessageHandlers()
            {
                base.RegisterMessageHandlers();
                _ = Token.RegisterUntargeted<StatePushedMessage>(HandlePushed);
                _ = Token.RegisterUntargeted<StatePoppedMessage>(HandlePopped);
                _ = Token.RegisterUntargeted<TransitionStartMessage>(HandleStart);
                _ = Token.RegisterUntargeted<TransitionCompleteMessage>(HandleComplete);
                _ = Token.RegisterUntargeted<StateStackFlattenedMessage>(HandleFlattened);
                _ = Token.RegisterUntargeted<TransitionProgressChangedMessage>(HandleProgress);
                _ = Token.RegisterUntargeted<StateManuallyRemovedMessage>(HandleManualRemoval);
            }

            private void HandlePushed(ref StatePushedMessage message)
            {
                PushedMessages.Add(message);
            }

            private void HandlePopped(ref StatePoppedMessage message)
            {
                PoppedMessages.Add(message);
            }

            private void HandleStart(ref TransitionStartMessage message)
            {
                TransitionStartMessages.Add(message);
            }

            private void HandleComplete(ref TransitionCompleteMessage message)
            {
                TransitionCompleteMessages.Add(message);
            }

            private void HandleFlattened(ref StateStackFlattenedMessage message)
            {
                FlattenedMessages.Add(message);
            }

            private void HandleProgress(ref TransitionProgressChangedMessage message)
            {
                TransitionProgress.Add(message);
            }

            private void HandleManualRemoval(ref StateManuallyRemovedMessage message)
            {
                ManualRemovalMessages.Add(message);
            }
        }

        private sealed class TestGameState : GameState
        {
            public void Initialize(string stateName)
            {
                _name = stateName;
            }
        }
    }
}
