namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.SceneManagement;

    public static class SceneStateFactory
    {
        public static SceneState LoadAdditive(
            string sceneName,
            LoadSceneParameters? loadParameters = null,
            bool revertOnRemoval = true
        )
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name must be provided.", nameof(sceneName));
            }

            LoadSceneParameters parameters =
                loadParameters ?? new LoadSceneParameters(LoadSceneMode.Additive);
            return new SceneState(
                sceneName,
                SceneTransitionMode.Addition,
                parameters,
                UnloadSceneOptions.None,
                revertOnRemoval
            );
        }

        public static SceneState Unload(
            string sceneName,
            UnloadSceneOptions unloadOptions = UnloadSceneOptions.None,
            bool revertOnRemoval = false
        )
        {
            if (string.IsNullOrWhiteSpace(sceneName))
            {
                throw new ArgumentException("Scene name must be provided.", nameof(sceneName));
            }

            return new SceneState(
                sceneName,
                SceneTransitionMode.Removal,
                new LoadSceneParameters(LoadSceneMode.Additive),
                unloadOptions,
                revertOnRemoval
            );
        }

        public static StateGroup SwapExclusive(
            string groupName,
            string sceneToUnload,
            string sceneToLoad,
            LoadSceneParameters? loadParameters = null,
            UnloadSceneOptions unloadOptions = UnloadSceneOptions.None,
            bool revertOnRemoval = true
        )
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                throw new ArgumentException("Group name must be provided.", nameof(groupName));
            }

            List<IState> states = new List<IState>(2);
            if (!string.IsNullOrWhiteSpace(sceneToUnload))
            {
                SceneState removal = Unload(sceneToUnload, unloadOptions, revertOnRemoval: false);
                states.Add(removal);
            }

            if (!string.IsNullOrWhiteSpace(sceneToLoad))
            {
                SceneState addition = LoadAdditive(sceneToLoad, loadParameters, revertOnRemoval);
                states.Add(addition);
            }

            if (states.Count == 0)
            {
                throw new InvalidOperationException(
                    "SwapExclusive requires at least one scene to load or unload."
                );
            }

            return new StateGroup(
                groupName,
                states,
                StateGroupMode.Sequential,
                TickMode.None,
                false
            );
        }
    }
}
