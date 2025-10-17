namespace WallstopStudios.DxState.Tests.PlayMode.State.Stack.Components
{
    using System.Collections;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;

    public sealed class StateStackBootstrapperTests
    {
        [UnityTest]
        public IEnumerator LogsErrorWhenInitialStateMissing()
        {
            GameObject host = new GameObject("Bootstrapper_MissingInitial");
            try
            {
                host.AddComponent<StateStackManager>();
                StateStackBootstrapper bootstrapper = host.AddComponent<StateStackBootstrapper>();
                TestGameState childState = host.AddComponent<TestGameState>();
                childState.Initialize("Child");

                bootstrapper.SetRegisterChildGameStates(false);
                bootstrapper.SetAutoAssignInitialState(false);
                bootstrapper.SetForceRegisterStates(true);
                bootstrapper.SetAdditionalStates(childState);

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("StateStackBootstrapper has no initial state configured")
                );

                yield return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator LogsErrorWhenDuplicateStatesConfigured()
        {
            GameObject host = new GameObject("Bootstrapper_DuplicateStates");
            try
            {
                host.AddComponent<StateStackManager>();
                StateStackBootstrapper bootstrapper = host.AddComponent<StateStackBootstrapper>();
                TestGameState duplicate = host.AddComponent<TestGameState>();
                duplicate.Initialize("Duplicate");

                bootstrapper.SetRegisterChildGameStates(false);
                bootstrapper.SetForceRegisterStates(false);
                bootstrapper.SetAdditionalStates(duplicate, duplicate);

                LogAssert.Expect(
                    LogType.Error,
                    new Regex("Duplicate GameState reference detected")
                );

                yield return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(host);
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
