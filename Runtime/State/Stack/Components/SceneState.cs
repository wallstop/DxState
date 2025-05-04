namespace WallstopStudios.DxState.State.Stack.Components
{
    using System;
    using System.ComponentModel;
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityHelpers.Core.Extension;

    public enum SceneSelectionMode
    {
        [Obsolete("Please select a real value")]
        None = 0,
        SceneReference = 1,
        SceneName = 2,
    }

    public class SceneState : GameState
    {
        [Header("Scene Selection")]
        [SerializeField]
        protected SceneSelectionMode _mode = SceneSelectionMode.SceneReference;

        [SerializeField]
        protected Scene _scene;

        [SerializeField]
        protected string _sceneName;

        public override async ValueTask Enter(IState previousState)
        {
            switch (_mode)
            {
                case SceneSelectionMode.SceneReference:
                {
                    await SceneManager.LoadSceneAsync(_scene.name);
                    return;
                }
                case SceneSelectionMode.SceneName:
                {
                    await SceneManager.LoadSceneAsync(_sceneName);
                    return;
                }
                default:
                {
                    throw new InvalidEnumArgumentException(
                        nameof(_mode),
                        (int)_mode,
                        typeof(SceneSelectionMode)
                    );
                }
            }
        }
    }
}
