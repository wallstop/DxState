namespace WallstopStudios.DxState.Tests.PlayMode.Runtime
{
    using System.Collections;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;
    using WallstopStudios.UnityHelpers.Core.Extension;
    using static WallstopStudios.DxState.Tests.PlayMode.Runtime.CoroutineTestUtilities;

    public sealed class StateStackBootstrapperRuntimeTests
    {
        [UnityTest]
        public IEnumerator DoesNotPushWhenDisabledOnStart()
        {
            GameObject host = new GameObject("Bootstrapper_NoAutoPush");
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackBootstrapper bootstrapper = host.AddComponent<StateStackBootstrapper>();
                TestState extraState = host.AddComponent<TestState>();
                extraState.Initialize("Extra");

                bootstrapper.SetRegisterChildGameStates(false);
                bootstrapper.SetAutoAssignInitialState(false);
                bootstrapper.SetAdditionalStates(extraState);
                bootstrapper.SetForceRegisterStates(true);
                bootstrapper.SetPushInitialStateOnStart(false);

                yield return WaitForFrames(3);

                Assert.IsNull(manager.CurrentState, "Expected no state pushed when auto-start disabled.");
                Assert.AreEqual(0, manager.Stack.Count);
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator AutoPushesInitialStateWhenConfigured()
        {
            GameObject host = new GameObject("Bootstrapper_AutoPush");
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackBootstrapper bootstrapper = host.AddComponent<StateStackBootstrapper>();
                TestState initial = host.AddComponent<TestState>();
                initial.Initialize("Initial");

                bootstrapper.SetRegisterChildGameStates(true);
                bootstrapper.SetAutoAssignInitialState(true);
                bootstrapper.SetForceRegisterStates(true);

                yield return WaitForFrames(3);

                Assert.AreSame(initial, manager.CurrentState, "Expected bootstrapper to push initial state.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator DuplicateStatesAllowedWhenForceFlagEnabled()
        {
            GameObject host = new GameObject("Bootstrapper_ForceRegister");
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackBootstrapper bootstrapper = host.AddComponent<StateStackBootstrapper>();
                TestState duplicate = host.AddComponent<TestState>();
                duplicate.Initialize("Dup");

                bootstrapper.SetRegisterChildGameStates(false);
                bootstrapper.SetAutoAssignInitialState(false);
                bootstrapper.SetForceRegisterStates(true);
                bootstrapper.SetAdditionalStates(duplicate, duplicate);

                LogAssert.ignoreFailingMessages = true;
                yield return WaitForFrames(1);
                LogAssert.ignoreFailingMessages = false;

                Assert.IsTrue(manager.RegisteredStates.ContainsKey(duplicate.Name));
                Assert.AreEqual(1, manager.RegisteredStates.Count);
            }
            finally
            {
                Object.DestroyImmediate(host);
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
