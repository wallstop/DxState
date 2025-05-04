namespace WallstopStudios.DxState.State.Stack.Components
{
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.SceneManagement;
    using UnityHelpers.Core.Extension;

    public class SceneState : GameState
    {
        [SerializeField]
        protected Scene _scene;

        public override async ValueTask Enter(IState previousState)
        {
            await SceneManager.LoadSceneAsync(_scene.name);
        }
    }
}
