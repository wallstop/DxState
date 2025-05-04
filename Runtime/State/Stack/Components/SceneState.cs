namespace WallstopStudios.DxState.State.Stack.Components
{
    using System.Threading.Tasks;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    public class SceneState : GameState
    {
        [SerializeField]
        protected Scene _scene;

        public override ValueTask Enter(IState previousState)
        {
            return base.Enter(previousState);
        }

        public override ValueTask Exit(IState nextState)
        {
            return base.Exit(nextState);
        }
    }
}
