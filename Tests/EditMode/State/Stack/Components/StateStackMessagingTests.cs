namespace WallstopStudios.DxState.Tests.EditMode.State.Stack.Components
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

                yield return WaitForValueTask(manager.PushAsync(first));
                yield return WaitForValueTask(manager.PushAsync(second));
                yield return WaitForValueTask(manager.PopAsync());
                yield return WaitForValueTask(manager.FlattenAsync(first));
                yield return WaitForValueTask(manager.PushAsync(second));
                yield return WaitForValueTask(manager.RemoveAsync(second));
                yield return WaitForValueTask(manager.WaitForTransitionCompletionAsync());
                yield return null;

                Assert.GreaterOrEqual(probe.PushedMessages.Count, 1, "Expected at least one StatePushedMessage.");
                Assert.GreaterOrEqual(probe.PoppedMessages.Count, 1, "Expected at least one StatePoppedMessage.");
                Assert.GreaterOrEqual(probe.TransitionStartMessages.Count, 1, "Expected TransitionStartMessage emissions.");
                Assert.GreaterOrEqual(probe.TransitionCompleteMessages.Count, 1, "Expected TransitionCompleteMessage emissions.");
                Assert.GreaterOrEqual(probe.FlattenedMessages.Count, 1, "Expected StateStackFlattenedMessage emissions.");
                Assert.GreaterOrEqual(probe.TransitionProgress.Count, 1, "Expected TransitionProgressChangedMessage emissions.");
                Assert.GreaterOrEqual(probe.ManualRemovalMessages.Count, 1, "Expected StateManuallyRemovedMessage emissions.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static IEnumerator WaitForValueTask(ValueTask valueTask)
        {
            return WaitForValueTaskInternal(valueTask.AsTask());
        }

        private static IEnumerator WaitForValueTask<TValue>(ValueTask<TValue> valueTask)
        {
            return WaitForValueTaskInternal(valueTask.AsTask());
        }

        private static IEnumerator WaitForValueTaskInternal(Task awaited)
        {
            while (!awaited.IsCompleted)
            {
                yield return null;
            }

            if (awaited.IsFaulted)
            {
                Exception exception = awaited.Exception;
                Exception inner = exception != null ? exception.InnerException : null;
                throw inner ?? exception;
            }

            if (awaited.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }

        private sealed class MessagingProbe : MessageAwareComponent
        {
            public List<StatePushedMessage> PushedMessages { get; } = new List<StatePushedMessage>();
            public List<StatePoppedMessage> PoppedMessages { get; } = new List<StatePoppedMessage>();
            public List<TransitionStartMessage> TransitionStartMessages { get; } = new List<TransitionStartMessage>();
            public List<TransitionCompleteMessage> TransitionCompleteMessages { get; } = new List<TransitionCompleteMessage>();
            public List<StateStackFlattenedMessage> FlattenedMessages { get; } = new List<StateStackFlattenedMessage>();
            public List<TransitionProgressChangedMessage> TransitionProgress { get; } = new List<TransitionProgressChangedMessage>();
            public List<StateManuallyRemovedMessage> ManualRemovalMessages { get; } = new List<StateManuallyRemovedMessage>();

            protected override bool RegisterForStringMessages => false;

            protected override void RegisterMessageHandlers()
            {
                base.RegisterMessageHandlers();
                _ = Token.RegisterUntargeted<StatePushedMessage>(msg => PushedMessages.Add(msg));
                _ = Token.RegisterUntargeted<StatePoppedMessage>(msg => PoppedMessages.Add(msg));
                _ = Token.RegisterUntargeted<TransitionStartMessage>(msg => TransitionStartMessages.Add(msg));
                _ = Token.RegisterUntargeted<TransitionCompleteMessage>(msg => TransitionCompleteMessages.Add(msg));
                _ = Token.RegisterUntargeted<StateStackFlattenedMessage>(msg => FlattenedMessages.Add(msg));
                _ = Token.RegisterUntargeted<TransitionProgressChangedMessage>(msg => TransitionProgress.Add(msg));
                _ = Token.RegisterUntargeted<StateManuallyRemovedMessage>(msg => ManualRemovalMessages.Add(msg));
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
