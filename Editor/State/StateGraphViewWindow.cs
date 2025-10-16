#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using UnityEditor;
    using UnityEditor.Experimental.GraphView;
    using UnityEditor.UIElements;
    using UnityEngine;
    using UnityEngine.UIElements;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;
    using WallstopStudios.DxState.State.Stack.Components;

    public sealed class StateGraphViewWindow : EditorWindow
    {
        private StateGraphAsset _graphAsset;
        private SerializedObject _graphSerialized;
        private SerializedProperty _stacksProperty;
        private StateGraph _graph;
        private string _stackName;
        private StateStackManager _targetManager;

        private Toolbar _toolbar;
        private ObjectField _graphField;
        private PopupField<string> _stackPopup;
        private ObjectField _managerField;
        private Button _refreshButton;
        private Button _syncButton;
        private Label _statusLabel;

        private readonly List<string> _stackOptions = new List<string>();
        private StateStackGraphView _graphView;

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
            RefreshGraphData(repopulate: true);
            HighlightGraph();
        }

        private void OnEnable()
        {
            ConstructUI();
            EditorApplication.update += OnEditorUpdate;
            RefreshGraphData(repopulate: true);
            HighlightGraph();
        }

        private void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (_graphView != null)
            {
                _graphView.DisposeView();
                _graphView = null;
            }
            rootVisualElement.Clear();
        }

        private void ConstructUI()
        {
            rootVisualElement.style.flexDirection = FlexDirection.Column;
            rootVisualElement.style.flexGrow = 1f;
            rootVisualElement.Clear();

            _toolbar = new Toolbar();
            rootVisualElement.Add(_toolbar);

            _graphField = new ObjectField("Graph")
            {
                objectType = typeof(StateGraphAsset),
                value = _graphAsset,
            };
            _graphField.RegisterValueChangedCallback(evt =>
            {
                _graphAsset = evt.newValue as StateGraphAsset;
                RefreshGraphData(repopulate: true);
                HighlightGraph();
            });
            _toolbar.Add(_graphField);

            _stackPopup = new PopupField<string>("Stack", _stackOptions, _stackOptions.Count > 0 ? 0 : -1)
            {
                style = { minWidth = 160f },
            };
            _stackPopup.RegisterValueChangedCallback(evt =>
            {
                _stackName = evt.newValue;
                PopulateGraphView();
                HighlightGraph();
            });
            _toolbar.Add(_stackPopup);

            _managerField = new ObjectField("Manager")
            {
                objectType = typeof(StateStackManager),
                allowSceneObjects = true,
                value = _targetManager,
            };
            _managerField.RegisterValueChangedCallback(evt =>
            {
                _targetManager = evt.newValue as StateStackManager;
                HighlightGraph();
            });
            _toolbar.Add(_managerField);

            _refreshButton = new Button(() => RefreshGraphData(repopulate: true)) { text = "Refresh" };
            _toolbar.Add(_refreshButton);

            _syncButton = new Button(HighlightGraph) { text = "Sync Active" };
            _toolbar.Add(_syncButton);

            _statusLabel = new Label();
            _toolbar.Add(_statusLabel);

            _graphView = new StateStackGraphView
            {
                GraphModified = () =>
                {
                    RefreshGraphData(repopulate: false);
                    HighlightGraph();
                },
            };
            _graphView.StretchToParentSize();
            rootVisualElement.Add(_graphView);
        }

        private void RefreshGraphData(bool repopulate)
        {
            if (_graphAsset != null)
            {
                _graphSerialized = new SerializedObject(_graphAsset);
                _stacksProperty = _graphSerialized.FindProperty("_stacks");
                try
                {
                    _graph = _graphAsset.BuildGraph();
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, _graphAsset);
                    _graph = null;
                }
            }
            else
            {
                _graphSerialized = null;
                _stacksProperty = null;
                _graph = null;
            }

            UpdateStackOptions();

            if (repopulate)
            {
                PopulateGraphView();
            }
        }

        private void UpdateStackOptions()
        {
            _stackOptions.Clear();
            if (_stacksProperty != null)
            {
                for (int i = 0; i < _stacksProperty.arraySize; i++)
                {
                    SerializedProperty stackProperty = _stacksProperty.GetArrayElementAtIndex(i);
                    SerializedProperty nameProperty = stackProperty.FindPropertyRelative("_name");
                    string name = nameProperty != null ? nameProperty.stringValue : null;
                    if (!string.IsNullOrEmpty(name))
                    {
                        _stackOptions.Add(name);
                    }
                }
            }

            if (_stackOptions.Count == 0)
            {
                _stackOptions.Add("<default>");
                _stackName = null;
            }
            else if (string.IsNullOrEmpty(_stackName) || !_stackOptions.Contains(_stackName))
            {
                _stackName = _stackOptions[0];
            }

            if (_stackPopup != null)
            {
                _stackPopup.choices = _stackOptions;
                _stackPopup.value = _stackName ?? _stackOptions[0];
            }
        }

        private void PopulateGraphView()
        {
            SerializedProperty stackProperty = FindStackProperty(_stackName);
            StateStackConfiguration configuration = ResolveConfiguration(_graph, _stackName);
            _graphView?.Populate(_graphSerialized, stackProperty, _graphAsset, configuration);
        }

        private SerializedProperty FindStackProperty(string stackName)
        {
            if (_stacksProperty == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(stackName))
            {
                for (int i = 0; i < _stacksProperty.arraySize; i++)
                {
                    SerializedProperty stackProperty = _stacksProperty.GetArrayElementAtIndex(i);
                    SerializedProperty nameProperty = stackProperty.FindPropertyRelative("_name");
                    if (nameProperty != null && nameProperty.stringValue == stackName)
                    {
                        return stackProperty;
                    }
                }
            }

            return _stacksProperty.arraySize > 0 ? _stacksProperty.GetArrayElementAtIndex(0) : null;
        }

        private static StateStackConfiguration ResolveConfiguration(StateGraph graph, string stackName)
        {
            if (graph == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(stackName) && graph.TryGetStack(stackName, out StateStackConfiguration configuration))
            {
                return configuration;
            }

            foreach (KeyValuePair<string, StateStackConfiguration> entry in graph.Stacks)
            {
                return entry.Value;
            }

            return null;
        }

        private void HighlightGraph()
        {
            _graphView?.Highlight(_targetManager);
            UpdateStatusLabel();
        }

        private void UpdateStatusLabel()
        {
            if (_statusLabel == null)
            {
                return;
            }

            string stackLabel = string.IsNullOrEmpty(_stackName) ? "<default>" : _stackName;
            string managerLabel = _targetManager != null ? _targetManager.name : "<none>";
            _statusLabel.text = $"Stack: {stackLabel} | Manager: {managerLabel}";
        }

        private void OnEditorUpdate()
        {
            if (_targetManager == null || _graphView == null)
            {
                return;
            }

            _graphView.Highlight(_targetManager);
        }

        private sealed class StateStackGraphView : GraphView
        {
            private const float NodeWidth = 200f;
            private const float NodeHeight = 70f;
            private const float HorizontalSpacing = 240f;
            private const float VerticalSpacing = 140f;

            private SerializedObject _serializedGraph;
            private SerializedProperty _stackProperty;
            private StateGraphAsset _graphAsset;
            private StateStackConfiguration _configuration;
            private StateStackManager _currentManager;

            private readonly List<StateNode> _nodes;

            public Action GraphModified { get; set; }

            public StateStackGraphView()
            {
                _nodes = new List<StateNode>();
                this.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());

                GridBackground grid = new GridBackground();
                Insert(0, grid);
                grid.StretchToParentSize();

                graphViewChanged += OnGraphViewChanged;
            }

            public void DisposeView()
            {
                graphViewChanged -= OnGraphViewChanged;
                DeleteElements(graphElements.ToList());
                _nodes.Clear();
                _serializedGraph = null;
                _stackProperty = null;
                _graphAsset = null;
                _configuration = null;
                _currentManager = null;
            }

            public void Populate(
                SerializedObject serializedGraph,
                SerializedProperty stackProperty,
                StateGraphAsset graphAsset,
                StateStackConfiguration configuration
            )
            {
                _serializedGraph = serializedGraph;
                _stackProperty = stackProperty;
                _graphAsset = graphAsset;
                _configuration = configuration;

                DeleteElements(graphElements.ToList());
                _nodes.Clear();

                if (_stackProperty == null)
                {
                    return;
                }

                SerializedProperty statesProperty = _stackProperty.FindPropertyRelative("_states");
                if (statesProperty == null)
                {
                    return;
                }

                IReadOnlyList<IState> resolvedStates = configuration?.States;
                for (int i = 0; i < statesProperty.arraySize; i++)
                {
                    SerializedProperty referenceProperty = statesProperty.GetArrayElementAtIndex(i);
                    SerializedProperty stateProperty = referenceProperty.FindPropertyRelative("_state");
                    SerializedProperty initialProperty = referenceProperty.FindPropertyRelative("_setAsInitial");

                    UnityEngine.Object stateObject = stateProperty != null ? stateProperty.objectReferenceValue : null;
                    IState resolvedState = null;
                    if (resolvedStates != null && i < resolvedStates.Count)
                    {
                        resolvedState = resolvedStates[i];
                    }
                    if (resolvedState == null && stateObject is IState stateInterface)
                    {
                        resolvedState = stateInterface;
                    }

                    bool isInitial = initialProperty != null && initialProperty.boolValue;
                    StateNode node = new StateNode(this, stateObject, resolvedState, isInitial)
                    {
                        ArrayIndex = i,
                    };

                    int column = i % 4;
                    int row = i / 4;
                    float x = column * HorizontalSpacing;
                    float y = row * VerticalSpacing;
                    node.SetPosition(new Rect(x, y, NodeWidth, NodeHeight));
                    AddElement(node);
                    _nodes.Add(node);
                }

                CreateSequentialEdges();
                Highlight(_currentManager);
            }

            public void Highlight(StateStackManager manager)
            {
                _currentManager = manager;
                foreach (StateNode node in _nodes)
                {
                    node.SetActiveState(false, false);
                }

                if (manager == null)
                {
                    return;
                }

                IReadOnlyList<IState> stack = manager.Stack;
                if (stack == null)
                {
                    return;
                }

                IState current = stack.Count > 0 ? stack[stack.Count - 1] : null;
                for (int i = 0; i < stack.Count; i++)
                {
                    IState state = stack[i];
                    if (state == null)
                    {
                        continue;
                    }

                    for (int n = 0; n < _nodes.Count; n++)
                    {
                        StateNode node = _nodes[n];
                        if (node.StateObject is IState nodeState && ReferenceEquals(nodeState, state)
                            || ReferenceEquals(node.ResolvedState, state))
                        {
                            node.SetActiveState(true, ReferenceEquals(state, current));
                        }
                    }
                }
            }

            public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
            {
                base.BuildContextualMenu(evt);
                UnityEngine.Object selected = Selection.activeObject;
                DropdownMenuAction.Status status = selected is IState
                    ? DropdownMenuAction.Status.Normal
                    : DropdownMenuAction.Status.Disabled;
                evt.menu.AppendAction(
                    "Add Selected State",
                    _ => AddStateReference(selected as UnityEngine.Object),
                    status
                );
            }

            public override EventPropagation DeleteSelection()
            {
                List<StateNode> nodesToRemove = selection.OfType<StateNode>().ToList();
                if (nodesToRemove.Count > 0)
                {
                    for (int i = 0; i < nodesToRemove.Count; i++)
                    {
                        RemoveState(nodesToRemove[i], applyChanges: false);
                    }

                    ApplySerializedChanges();
                    Populate(_serializedGraph, _stackProperty, _graphAsset, _configuration);
                    Highlight(_currentManager);
                    return EventPropagation.Stop;
                }

                return base.DeleteSelection();
            }

            private GraphViewChange OnGraphViewChanged(GraphViewChange change)
            {
                if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
                {
                    foreach (Edge edge in change.edgesToCreate)
                    {
                        RemoveElement(edge);
                    }
                    change.edgesToCreate.Clear();
                }

                if (change.movedElements != null)
                {
                    bool movedNode = change.movedElements.OfType<StateNode>().Any();
                    if (movedNode)
                    {
                        ApplyNodeOrdering();
                    }
                }

                return change;
            }

            public void DisposeEdges()
            {
                DeleteElements(graphElements.OfType<Edge>().ToList());
            }

            public void DisposeNodes()
            {
                DeleteElements(graphElements.OfType<StateNode>().ToList());
            }

            private void CreateSequentialEdges()
            {
                DisposeEdges();

                for (int i = 0; i < _nodes.Count - 1; i++)
                {
                    StateNode current = _nodes[i];
                    StateNode next = _nodes[i + 1];
                    Edge connection = current.Output.ConnectTo(next.Input);
                    AddElement(connection);
                    current.RefreshPorts();
                    next.RefreshPorts();
                }
            }

            private void AddStateReference(UnityEngine.Object stateObject)
            {
                if (_serializedGraph == null || _stackProperty == null)
                {
                    return;
                }

                SerializedProperty statesProperty = _stackProperty.FindPropertyRelative("_states");
                if (statesProperty == null)
                {
                    return;
                }

                Undo.RecordObject(_graphAsset, "Add State Reference");
                int newIndex = statesProperty.arraySize;
                statesProperty.arraySize++;
                SerializedProperty entry = statesProperty.GetArrayElementAtIndex(newIndex);
                SerializedProperty stateProperty = entry.FindPropertyRelative("_state");
                SerializedProperty initialProperty = entry.FindPropertyRelative("_setAsInitial");
                if (stateProperty != null)
                {
                    stateProperty.objectReferenceValue = stateObject;
                }
                if (initialProperty != null)
                {
                    initialProperty.boolValue = newIndex == 0;
                }

                ApplySerializedChanges();
                Populate(_serializedGraph, _stackProperty, _graphAsset, _configuration);
                Highlight(_currentManager);
            }

            private void RemoveState(StateNode node, bool applyChanges = true)
            {
                if (_serializedGraph == null || _stackProperty == null)
                {
                    return;
                }

                SerializedProperty statesProperty = _stackProperty.FindPropertyRelative("_states");
                if (statesProperty == null)
                {
                    return;
                }

                int index = node.ArrayIndex;
                if (index < 0 || index >= statesProperty.arraySize)
                {
                    index = _nodes.IndexOf(node);
                }
                if (index < 0 || index >= statesProperty.arraySize)
                {
                    return;
                }

                Undo.RecordObject(_graphAsset, "Remove State Reference");
                statesProperty.DeleteArrayElementAtIndex(index);
                if (index < statesProperty.arraySize)
                {
                    statesProperty.DeleteArrayElementAtIndex(index);
                }

                if (applyChanges)
                {
                    ApplySerializedChanges();
                    Populate(_serializedGraph, _stackProperty, _graphAsset, _configuration);
                    Highlight(_currentManager);
                }
            }

            private void SetInitialState(StateNode node)
            {
                Undo.RecordObject(_graphAsset, "Set Initial State");
                foreach (StateNode n in _nodes)
                {
                    n.IsInitial = ReferenceEquals(n, node);
                }

                ApplySerializedChanges();
                Populate(_serializedGraph, _stackProperty, _graphAsset, _configuration);
                Highlight(_currentManager);
            }

            private void ApplyNodeOrdering()
            {
                if (_serializedGraph == null || _stackProperty == null)
                {
                    return;
                }

                SerializedProperty statesProperty = _stackProperty.FindPropertyRelative("_states");
                if (statesProperty == null)
                {
                    return;
                }

                Undo.RecordObject(_graphAsset, "Reorder States");

                List<StateNode> orderedNodes = _nodes
                    .OrderBy(node => node.GetPosition().x)
                    .ThenBy(node => node.GetPosition().y)
                    .ToList();

                if (statesProperty.arraySize != orderedNodes.Count)
                {
                    return;
                }

                for (int i = 0; i < orderedNodes.Count; i++)
                {
                    StateNode node = orderedNodes[i];
                    node.ArrayIndex = i;
                    SerializedProperty entry = statesProperty.GetArrayElementAtIndex(i);
                    SerializedProperty stateProperty = entry.FindPropertyRelative("_state");
                    SerializedProperty initialProperty = entry.FindPropertyRelative("_setAsInitial");
                    if (stateProperty != null)
                    {
                        stateProperty.objectReferenceValue = node.StateObject;
                    }
                    if (initialProperty != null)
                    {
                        initialProperty.boolValue = node.IsInitial;
                    }
                }

                _nodes.Clear();
                _nodes.AddRange(orderedNodes);

                ApplySerializedChanges();
            }

            private void ApplySerializedChanges()
            {
                _serializedGraph?.ApplyModifiedProperties();
                if (_graphAsset != null)
                {
                    EditorUtility.SetDirty(_graphAsset);
                    AssetDatabase.SaveAssets();
                }
                GraphModified?.Invoke();
            }

            private sealed class StateNode : Node
            {
                private readonly StateStackGraphView _owner;
                private readonly Label _descriptionLabel;
                private readonly Color _baseColor;
                private readonly Color _initialColor = new Color(0.2f, 0.5f, 0.3f, 1f);
                private readonly Color _missingColor = new Color(0.6f, 0.2f, 0.2f, 1f);
                private readonly Color _activeColor = new Color(0.35f, 0.45f, 0.75f, 1f);
                private readonly Color _currentColor = new Color(0.4f, 0.7f, 0.95f, 1f);

                public UnityEngine.Object StateObject { get; set; }
                public IState ResolvedState { get; }
                public Port Input { get; }
                public Port Output { get; }
                public int ArrayIndex { get; set; }

                private bool _isInitial;
                public bool IsInitial
                {
                    get => _isInitial;
                    set
                    {
                        _isInitial = value;
                        RefreshAppearance();
                    }
                }

                public StateNode(
                    StateStackGraphView owner,
                    UnityEngine.Object stateObject,
                    IState resolvedState,
                    bool isInitial
                )
                {
                    _owner = owner;
                    StateObject = stateObject;
                    ResolvedState = resolvedState;
                    _baseColor = new Color(0.2f, 0.2f, 0.28f, 1f);
                    _descriptionLabel = new Label();

                    this.title = stateObject != null
                        ? stateObject.name
                        : (resolvedState != null ? resolvedState.Name : "<missing>");

                    _descriptionLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    mainContainer.Add(_descriptionLabel);

                    Input = InstantiatePort(
                        Orientation.Horizontal,
                        Direction.Input,
                        Port.Capacity.Multi,
                        typeof(bool)
                    );
                    Input.portName = string.Empty;
                    inputContainer.Add(Input);

                    Output = InstantiatePort(
                        Orientation.Horizontal,
                        Direction.Output,
                        Port.Capacity.Single,
                        typeof(bool)
                    );
                    Output.portName = string.Empty;
                    outputContainer.Add(Output);

                    IsInitial = isInitial;
                    RefreshAppearance();

                    this.RegisterCallback<MouseDownEvent>(evt =>
                    {
                        if (evt.clickCount == 2 && StateObject != null)
                        {
                            EditorGUIUtility.PingObject(StateObject);
                        }
                    });
                }

                public void SetActiveState(bool isActive, bool isCurrent)
                {
                    Color color = DetermineBaseColor();
                    if (StateObject != null)
                    {
                        if (isCurrent)
                        {
                            color = _currentColor;
                        }
                        else if (isActive)
                        {
                            color = _activeColor;
                        }
                    }

                    mainContainer.style.backgroundColor = color;
                }

                private Color DetermineBaseColor()
                {
                    if (StateObject == null)
                    {
                        return _missingColor;
                    }

                    return IsInitial ? _initialColor : _baseColor;
                }

                public void RefreshAppearance()
                {
                    mainContainer.style.backgroundColor = DetermineBaseColor();
                    if (StateObject == null)
                    {
                        _descriptionLabel.text = "Missing reference";
                        _descriptionLabel.style.color = Color.red;
                    }
                    else
                    {
                        string description = ResolvedState != null
                            ? ResolvedState.GetType().Name
                            : StateObject.GetType().Name;
                        _descriptionLabel.text = description;
                        _descriptionLabel.style.color = Color.white;
                    }
                }

                public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
                {
                    base.BuildContextualMenu(evt);
                    evt.menu.AppendAction(
                        "Set As Initial",
                        _ => _owner.SetInitialState(this),
                        IsInitial
                            ? DropdownMenuAction.Status.Checked
                            : DropdownMenuAction.Status.Normal
                    );
                    evt.menu.AppendAction(
                        "Remove",
                        _ => _owner.RemoveState(this),
                        DropdownMenuAction.Status.Normal
                    );
                    evt.menu.AppendSeparator();
                    evt.menu.AppendAction(
                        "Ping State",
                        _ => EditorGUIUtility.PingObject(StateObject),
                        StateObject != null
                            ? DropdownMenuAction.Status.Normal
                            : DropdownMenuAction.Status.Disabled
                    );
                }
            }
        }
    }
}
#endif
