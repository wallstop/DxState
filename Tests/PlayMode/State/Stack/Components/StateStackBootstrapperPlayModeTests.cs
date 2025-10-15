namespace WallstopStudios.DxState.Tests.PlayMode.State.Stack.Components
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;

    public sealed class StateStackBootstrapperPlayModeTests
    {
        [UnityTest]
        public IEnumerator BootstrapperRegistersAndPushesInitialState()
        {
            GameObject stackObject = new GameObject("StateStack_Bootstrap_Test");
            StateStackManager manager =
                (StateStackManager)stackObject.AddComponent(typeof(StateStackManager));
            TestGameState testState = stackObject.AddComponent<TestGameState>();
            stackObject.AddComponent<StateStackBootstrapper>();

            yield return WaitForCurrentState(manager, testState);

            Assert.IsNotNull(
                stackObject.GetComponent<global::DxMessaging.Unity.MessagingComponent>(),
                "Bootstrapper should ensure a MessagingComponent exists"
            );
            Assert.IsTrue(
                manager.RegisteredStates.ContainsKey(testState.Name),
                "Expected test state to be registered"
            );
            Assert.AreSame(testState, manager.CurrentState);
            Assert.IsNotNull(manager.Diagnostics);
            Assert.Greater(manager.Diagnostics.Events.Count, 0);

            Object.Destroy(stackObject);
        }

        private static IEnumerator WaitForCurrentState(StateStackManager manager, IState targetState)
        {
            while (!ReferenceEquals(manager.CurrentState, targetState))
            {
                yield return null;
            }
        }

        private sealed class TestGameState : GameState
        {
            public override string Name => "BootstrapperTestState";
        }
    }
}
