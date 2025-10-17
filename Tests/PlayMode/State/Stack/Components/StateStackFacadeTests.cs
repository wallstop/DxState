namespace WallstopStudios.DxState.Tests.PlayMode.State.Stack.Components
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;
    using WallstopStudios.UnityHelpers.Core.Extension;

    public sealed class StateStackFacadeTests
    {
        [UnityTest]
        public IEnumerator PushStateInvokesUnityEvents()
        {
            GameObject host = new GameObject("Facade_Push_Test");
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackFacade facade = host.AddComponent<StateStackFacade>();

                TestGameState firstState = host.AddComponent<TestGameState>();
                firstState.Initialize("FirstState");
                manager.TryRegister(firstState, true);

                yield return null;

                List<GameState> pushedInvocations = new List<GameState>();
                List<GameState> changedInvocations = new List<GameState>();
                facade.OnStatePushedEvent.AddListener(pushedInvocations.Add);
                facade.OnStateChangedEvent.AddListener(changedInvocations.Add);

                facade.PushState(firstState);
                yield return WaitForValueTask(manager.WaitForTransitionCompletionAsync());
                yield return null;

                Assert.AreSame(firstState, manager.CurrentState);
                Assert.AreEqual(1, pushedInvocations.Count);
                Assert.AreSame(firstState, pushedInvocations[0]);
                Assert.AreEqual(1, changedInvocations.Count);
                Assert.AreSame(firstState, changedInvocations[0]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator ReplaceStateSwapsActiveState()
        {
            GameObject host = new GameObject("Facade_Replace_Test");
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackFacade facade = host.AddComponent<StateStackFacade>();

                TestGameState initialState = host.AddComponent<TestGameState>();
                initialState.Initialize("InitialState");
                TestGameState replacementState = host.AddComponent<TestGameState>();
                replacementState.Initialize("ReplacementState");
                manager.TryRegister(initialState, true);
                manager.TryRegister(replacementState, true);

                yield return null;

                List<GameState> poppedInvocations = new List<GameState>();
                List<GameState> changedInvocations = new List<GameState>();
                facade.OnStatePoppedEvent.AddListener(poppedInvocations.Add);
                facade.OnStateChangedEvent.AddListener(changedInvocations.Add);

                facade.PushState(initialState);
                yield return WaitForValueTask(manager.WaitForTransitionCompletionAsync());
                yield return null;

                facade.ReplaceState(replacementState);
                yield return WaitForValueTask(manager.WaitForTransitionCompletionAsync());
                yield return null;

                Assert.AreSame(replacementState, manager.CurrentState);
                Assert.IsTrue(poppedInvocations.Count >= 1);
                Assert.AreSame(initialState, poppedInvocations[0]);
                Assert.IsTrue(changedInvocations.Count >= 2);
                Assert.AreSame(replacementState, changedInvocations[^1]);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        private static IEnumerator WaitForValueTask(ValueTask task)
        {
            return task.AsCoroutine();
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
