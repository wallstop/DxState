namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using NUnit.Framework;
    using UnityEngine.SceneManagement;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States;

    public sealed class SceneStateFactoryTests
    {
        [Test]
        public void LoadAdditiveCreatesAdditionState()
        {
            SceneState state = SceneStateFactory.LoadAdditive(
                "SampleScene",
                new LoadSceneParameters(LoadSceneMode.Single),
                true
            );

            Assert.AreEqual("SampleScene", state.Name);
            Assert.AreEqual(SceneTransitionMode.Addition, state.TransitionMode);
            Assert.AreEqual(LoadSceneMode.Single, state.LoadSceneParameters.loadSceneMode);
            Assert.AreEqual(UnloadSceneOptions.None, state.UnloadSceneOptions);
            Assert.IsTrue(state.RevertOnRemoval);
        }

        [Test]
        public void UnloadCreatesRemovalState()
        {
            SceneState state = SceneStateFactory.Unload(
                "Level_A",
                UnloadSceneOptions.UnloadAllEmbeddedSceneObjects,
                false
            );

            Assert.AreEqual("Level_A", state.Name);
            Assert.AreEqual(SceneTransitionMode.Removal, state.TransitionMode);
            Assert.AreEqual(UnloadSceneOptions.UnloadAllEmbeddedSceneObjects, state.UnloadSceneOptions);
            Assert.IsFalse(state.RevertOnRemoval);
        }

        [Test]
        public void SwapExclusiveBuildsSequentialGroup()
        {
            StateGroup group = SceneStateFactory.SwapExclusive(
                "SwapGroup",
                "OldScene",
                "NewScene"
            );

            Assert.AreEqual("SwapGroup", group.Name);
        }

        [Test]
        public void SwapExclusiveValidatesSceneArguments()
        {
            Assert.Throws<InvalidOperationException>(() => SceneStateFactory.SwapExclusive(
                "EmptyGroup",
                string.Empty,
                string.Empty
            ));
        }

        [Test]
        public void LoadAdditiveValidatesSceneName()
        {
            Assert.Throws<ArgumentException>(() => SceneStateFactory.LoadAdditive(""));
        }
    }
}
