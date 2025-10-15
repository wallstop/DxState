namespace WallstopStudios.DxState.Samples.Bootstrap
{
    using System;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;

    public sealed class ExampleGameState : GameState
    {
        [SerializeField]
        private string _enterLogMessage = "ExampleGameState: enter";

        [SerializeField]
        private string _exitLogMessage = "ExampleGameState: exit";

        public override async ValueTask Enter<TProgress>(
            IState previousState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            await base.Enter(previousState, progress, direction);
            Debug.Log(_enterLogMessage, this);
        }

        public override async ValueTask Exit<TProgress>(
            IState nextState,
            TProgress progress,
            StateDirection direction
        )
            where TProgress : IProgress<float>
        {
            await base.Exit(nextState, progress, direction);
            Debug.Log(_exitLogMessage, this);
        }
    }
}
