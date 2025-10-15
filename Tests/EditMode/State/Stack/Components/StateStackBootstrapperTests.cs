namespace WallstopStudios.DxState.Tests.EditMode.State.Stack.Components
{
    using System.Collections;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;

    public sealed class StateStackBootstrapperTests
    {
        [UnityTest]
        public IEnumerator BootstrapperRegistersAndPushesInitialState()
        {
            GameObject stackObject = new GameObject("StateStack_Bootstrap_Test");
            StateStackManager manager = (StateStackManager)
                stackObject.AddComponent(typeof(StateStackManager));
            stackObject.AddComponent<StateStackBootstrapper>();
            TestGameState testState = stackObject.AddComponent<TestGameState>();

            yield return null;
            yield return null;

            Assert.IsNotNull(
                stackObject.GetComponent<global::DxMessaging.Unity.MessagingComponent>(),
                "Bootstrapper should ensure a MessagingComponent exists"
            );
            Assert.IsTrue(
                manager.RegisteredStates.ContainsKey(testState.Name),
                "Expected test state to be registered"
            );
            Assert.AreSame(testState, manager.CurrentState);

            Object.DestroyImmediate(stackObject);
        }

        private sealed class TestGameState : GameState
        {
            public override string Name => "BootstrapperTestState";
        }
    }
}
