#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEditor.Experimental.GraphView;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;

    public sealed class StateGraphViewWindow : EditorWindow
    {
        private StateGraphAsset _graphAsset;
        private string _stackName;
        private StateGraph _graph;
        private GraphView _graphView;

        public static void Open(StateGraphAsset graphAsset, string stackName)
        {
            if (graphAsset == null)
            {
                throw new ArgumentNullException(nameof(graphAsset));
            }

            StateGraphViewWindow window = GetWindow<StateGraphViewWindow>();
            window.titleContent = new GUIContent("State Graph View");
            window.Initialize(graphAsset, stackName);
            window.Show();
        }

        private void Initialize(StateGraphAsset graphAsset, string stackName)
        {
            _graphAsset = graphAsset;
            _stackName = stackName;
            BuildGraph();
        }

        private void OnEnable()
        {
            ConstructGraphView();
            BuildGraph();
        }

        private void OnDisable()
        {
            if (_graphView != null && rootVisualElement.Contains(_graphView))
            {
                rootVisualElement.Remove(_graphView);
            }
        }

        private void BuildGraph()
        {
            if (_graphAsset == null)
            {
                _graph = null;
                return;
            }

            try
            {
                _graph = _graphAsset.BuildGraph();
                PopulateGraphView();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, _graphAsset);
            }
        }

        private void ConstructGraphView()
        {
            if (_graphView != null && rootVisualElement.Contains(_graphView))
            {
                return;
            }

            StateStackGraphView graphView = new StateStackGraphView();
            graphView.StretchToParentSize();
            _graphView = graphView;
            rootVisualElement.Add(_graphView);
        }

        private void PopulateGraphView()
        {
            if (!(_graphView is StateStackGraphView stateStackGraphView))
            {
                return;
            }

            stateStackGraphView.Populate(_graph, _stackName);
        }

        private sealed class StateStackGraphView : GraphView
        {
            private const float NodeWidth = 180f;
            private const float NodeHeight = 60f;
            private const float HorizontalSpacing = 220f;
            private const float VerticalSpacing = 120f;

            public StateStackGraphView()
            {
                this.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());

                GridBackground grid = new GridBackground();
                Insert(0, grid);
                grid.StretchToParentSize();
            }

            public void Populate(StateGraph graph, string stackName)
            {
                graphViewChanged -= ClearOnGraphViewChanged;
                DeleteElements(graphElements);
                graphViewChanged += ClearOnGraphViewChanged;

                if (graph == null)
                {
                    return;
                }

                StateStackConfiguration configuration = ResolveConfiguration(graph, stackName);
                if (configuration == null)
                {
                    return;
                }

                IReadOnlyList<IState> states = configuration.States;
                if (states == null || states.Count == 0)
                {
                    return;
                }

                Dictionary<IState, Node> nodeLookup = new Dictionary<IState, Node>();

                for (int i = 0; i < states.Count; i++)
                {
                    IState state = states[i];
                    Node node = CreateNode(state, configuration.InitialState == state);
                    float x = i * HorizontalSpacing;
                    float y = 0f;
                    node.SetPosition(new Rect(x, y, NodeWidth, NodeHeight));
                    AddElement(node);
                    nodeLookup[state] = node;
                }

                for (int i = 0; i < states.Count - 1; i++)
                {
                    IState current = states[i];
                    IState next = states[i + 1];
                    if (nodeLookup.TryGetValue(current, out Node fromNode)
                        && nodeLookup.TryGetValue(next, out Node toNode))
                    {
                        Edge edge = fromNode.InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
                        edge.portName = string.Empty;
                        fromNode.outputContainer.Add(edge);
                        Port input = toNode.InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
                        input.portName = string.Empty;
                        toNode.inputContainer.Add(input);
                        fromNode.RefreshPorts();
                        toNode.RefreshPorts();
                        Edge connection = edge.ConnectTo(input);
                        AddElement(connection);
                    }
                }
            }

            private GraphViewChange ClearOnGraphViewChanged(GraphViewChange change)
            {
                return change;
            }

            private static StateStackConfiguration ResolveConfiguration(StateGraph graph, string stackName)
            {
                if (graph == null)
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(stackName)
                    && graph.TryGetStack(stackName, out StateStackConfiguration namedConfiguration))
                {
                    return namedConfiguration;
                }

                foreach (KeyValuePair<string, StateStackConfiguration> entry in graph.Stacks)
                {
                    return entry.Value;
                }

                return null;
            }

            private Node CreateNode(IState state, bool isInitial)
            {
                Node node = new Node
                {
                    title = state != null ? state.Name : "<missing>",
                };

                Label label = new Label();
                if (state == null)
                {
                    label.text = "Missing reference";
                    label.style.color = Color.red;
                }
                else
                {
                    label.text = state.GetType().Name;
                }

                node.mainContainer.style.backgroundColor = isInitial
                    ? new Color(0.2f, 0.45f, 0.2f, 1f)
                    : new Color(0.2f, 0.2f, 0.25f, 1f);

                node.mainContainer.Add(label);
                node.RefreshExpandedState();
                node.RefreshPorts();
                return node;
            }
        }
    }
}
#endif
