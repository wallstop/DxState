namespace WallstopStudios.DxState.Samples.StateGraph
{
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;
    using WallstopStudios.DxState.State.Stack.Components;

    public sealed class StateGraphBootstrap : MonoBehaviour
    {
        [SerializeField]
        private StateStackManager _stackManager;

        [SerializeField]
        private GameState _mainMenu;

        [SerializeField]
        private GameState _loading;

        [SerializeField]
        private GameState _gameplay;

        [SerializeField]
        private GameState _pause;

        [SerializeField]
        private bool _applyOnAwake = true;

        [SerializeField]
        private bool _forceRegister = true;

        private StateGraph _graph;

        private async void Awake()
        {
            if (_stackManager == null)
            {
                _stackManager = GetComponent<StateStackManager>();
            }

            _graph = BuildGraph();

            if (_applyOnAwake)
            {
                await ApplyStackAsync("Gameplay");
            }
        }

        [ContextMenu("Apply Menu Stack")]
        private async void ApplyMenu()
        {
            await ApplyStackAsync("Menu");
        }

        [ContextMenu("Apply Gameplay Stack")]
        private async void ApplyGameplay()
        {
            await ApplyStackAsync("Gameplay");
        }

        private StateGraph BuildGraph()
        {
            return new StateGraphBuilder()
                .Stack(
                    "Menu",
                    stack => stack
                        .State(_mainMenu, setAsInitial: true)
                        .State(_loading)
                )
                .Stack(
                    "Gameplay",
                    stack => stack
                        .State(_loading)
                        .State(_gameplay, setAsInitial: true)
                        .State(_pause)
                )
                .Build();
        }

        private async Task ApplyStackAsync(string stackName)
        {
            if (_graph == null)
            {
                _graph = BuildGraph();
            }

            if (!_graph.TryGetStack(stackName, out StateStackConfiguration configuration))
            {
                Debug.LogWarning($"Stack '{stackName}' was not found in the graph.");
                return;
            }

            await configuration.ApplyAsync(_stackManager.Stack, _forceRegister);
        }
    }
}
