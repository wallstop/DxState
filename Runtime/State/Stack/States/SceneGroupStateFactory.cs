namespace WallstopStudios.DxState.State.Stack.States
{
    using System;
    using System.Collections.Generic;
    using UnityEngine.SceneManagement;

    public static class SceneGroupStateFactory
    {
        public static StateGroup CreateSequentialScenes(
            string groupName,
            IEnumerable<string> sceneNames,
            LoadSceneParameters? loadParameters = null,
            UnloadSceneOptions unloadOptions = UnloadSceneOptions.None
        )
        {
            if (sceneNames == null)
            {
                throw new ArgumentNullException(nameof(sceneNames));
            }

            List<IState> states = new List<IState>();
            foreach (string sceneName in sceneNames)
            {
                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    continue;
                }

                states.Add(
                    SceneStateFactory.LoadAdditive(
                        sceneName,
                        loadParameters,
                        revertOnRemoval: true
                    )
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

        public static StateGroup CreateParallelScenes(
            string groupName,
            IEnumerable<string> sceneNames,
            LoadSceneParameters? loadParameters = null,
            UnloadSceneOptions unloadOptions = UnloadSceneOptions.None
        )
        {
            List<IState> states = new List<IState>();
            foreach (string sceneName in sceneNames)
            {
                if (string.IsNullOrWhiteSpace(sceneName))
                {
                    continue;
                }

                states.Add(
                    SceneStateFactory.LoadAdditive(
                        sceneName,
                        loadParameters,
                        revertOnRemoval: true
                    )
                );
            }

            return new StateGroup(
                groupName,
                states,
                StateGroupMode.Parallel,
                TickMode.None,
                false
            );
        }

        public static StateGroup CreateReentrantSafeGroup(
            string groupName,
            IEnumerable<IState> states
        )
        {
            List<IState> materialized = new List<IState>(states);
            return new StateGroup(
                groupName,
                materialized,
                StateGroupMode.Sequential,
                TickMode.Update,
                true
            );
        }
    }
}

