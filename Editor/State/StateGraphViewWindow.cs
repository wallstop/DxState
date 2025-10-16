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
    using WallstopStudios.DxState.State.Stack.Diagnostics;

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
        private TwoPaneSplitView _contentSplitView;
        private VisualElement _inspectorPanel;
        private Label _inspectorTitleLabel;
        private IMGUIContainer _inspectorGuiContainer;
        private Editor _currentInspectorEditor;
        private UnityEngine.Object _currentInspectorTarget;
        private int _selectedTransitionIndex = -1;

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
            DisposeInspectorEditor();
            rootVisualElement.Clear();
            _contentSplitView = null;
            _inspectorPanel = null;
            _inspectorTitleLabel = null;
            _inspectorGuiContainer = null;
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

            _stackPopup = new PopupField<string>(
                "Stack",
                _stackOptions,
                _stackOptions.Count > 0 ? 0 : -1
            )
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

            _refreshButton = new Button(() => RefreshGraphData(repopulate: true))
            {
                text = "Refresh",
            };
            _toolbar.Add(_refreshButton);

            _syncButton = new Button(HighlightGraph) { text = "Sync Active" };
            _toolbar.Add(_syncButton);

            _statusLabel = new Label();
            _toolbar.Add(_statusLabel);

            _contentSplitView = new TwoPaneSplitView(
                0,
                600f,
                TwoPaneSplitViewOrientation.Horizontal
            );
            _contentSplitView.style.flexGrow = 1f;
            rootVisualElement.Add(_contentSplitView);

            _graphView = new StateStackGraphView
            {
                GraphModified = () =>
                {
                    RefreshGraphData(repopulate: false);
                    HighlightGraph();
                },
            };
            _graphView.StateSelected = HandleStateSelection;
            _graphView.TransitionSelected = HandleTransitionSelection;
            _graphView.StretchToParentSize();
            _contentSplitView.Add(_graphView);

            BuildInspectorPanel();
            _contentSplitView.Add(_inspectorPanel);
        }

        private void BuildInspectorPanel()
        {
            _inspectorPanel = new VisualElement();
            _inspectorPanel.style.flexGrow = 1f;
            _inspectorPanel.style.flexDirection = FlexDirection.Column;
            _inspectorPanel.style.minWidth = 280f;

            _inspectorTitleLabel = new Label("Inspector");
            _inspectorTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            _inspectorTitleLabel.style.paddingLeft = 8f;
            _inspectorTitleLabel.style.paddingTop = 6f;
            _inspectorTitleLabel.style.paddingBottom = 6f;
            _inspectorPanel.Add(_inspectorTitleLabel);

            _inspectorGuiContainer = new IMGUIContainer(DrawInspector);
            _inspectorGuiContainer.style.flexGrow = 1f;
            _inspectorGuiContainer.style.paddingLeft = 4f;
            _inspectorGuiContainer.style.paddingRight = 4f;
            _inspectorGuiContainer.style.paddingTop = 4f;
            _inspectorGuiContainer.style.paddingBottom = 4f;
            _inspectorPanel.Add(_inspectorGuiContainer);

            _inspectorGuiContainer.MarkDirtyRepaint();
        }

        private void HandleStateSelection(UnityEngine.Object stateObject)
        {
            if (ReferenceEquals(_currentInspectorTarget, stateObject))
            {
                return;
            }

            DisposeInspectorEditor();
            _currentInspectorTarget = stateObject;
            _selectedTransitionIndex = -1;

            if (_inspectorTitleLabel != null)
            {
                _inspectorTitleLabel.text =
                    stateObject != null ? $"Inspector — {stateObject.name}" : "Inspector";
            }

            if (stateObject == null)
            {
                return;
            }

            try
            {
                _currentInspectorEditor = Editor.CreateEditor(stateObject);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, stateObject);
                _currentInspectorEditor = null;
            }
            if (_inspectorGuiContainer != null)
            {
                _inspectorGuiContainer.MarkDirtyRepaint();
            }
        }

        private void HandleTransitionSelection(StateStackGraphView.StateEdge edge)
        {
            if (edge == null || !edge.HasMetadata)
            {
                _selectedTransitionIndex = -1;
                if (_currentInspectorEditor == null && _inspectorTitleLabel != null)
                {
                    _inspectorTitleLabel.text = "Inspector";
                }
                if (_inspectorGuiContainer != null)
                {
                    _inspectorGuiContainer.MarkDirtyRepaint();
                }
                return;
            }

            _selectedTransitionIndex = edge.MetadataIndex;
            DisposeInspectorEditor();
            if (_inspectorTitleLabel != null)
            {
                string fromLabel = edge.From != null ? edge.From.StateName : "<from>";
                string toLabel = edge.To != null ? edge.To.StateName : "<to>";
                _inspectorTitleLabel.text = $"Transition — {fromLabel} → {toLabel}";
            }

            if (_inspectorGuiContainer != null)
            {
                _inspectorGuiContainer.MarkDirtyRepaint();
            }
        }

        private void DisposeInspectorEditor()
        {
            if (_currentInspectorEditor != null)
            {
                UnityEngine.Object.DestroyImmediate(_currentInspectorEditor);
                _currentInspectorEditor = null;
            }

            _currentInspectorTarget = null;

            if (_inspectorGuiContainer != null)
            {
                _inspectorGuiContainer.MarkDirtyRepaint();
            }

            if (_inspectorTitleLabel != null)
            {
                _inspectorTitleLabel.text = "Inspector";
            }
        }

        private void DrawInspector()
        {
            if (_currentInspectorEditor != null && _currentInspectorTarget != null)
            {
                _currentInspectorEditor.OnInspectorGUI();
                return;
            }

            if (_selectedTransitionIndex >= 0)
            {
                DrawTransitionInspector();
                return;
            }

            GUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Select a state node or transition to inspect details.",
                MessageType.Info
            );
        }

        private void DrawTransitionInspector()
        {
            SerializedProperty entry = GetTransitionProperty(_selectedTransitionIndex);
            if (entry == null)
            {
                GUILayout.Space(4f);
                EditorGUILayout.HelpBox("Transition metadata unavailable.", MessageType.Info);
                return;
            }

            _graphSerialized?.Update();
            SerializedProperty labelProperty = entry.FindPropertyRelative("_label");
            SerializedProperty tooltipProperty = entry.FindPropertyRelative("_tooltip");

            EditorGUILayout.LabelField("Transition Metadata", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            string newLabel = EditorGUILayout.TextField(
                "Label",
                labelProperty != null ? labelProperty.stringValue : string.Empty
            );
            string newTooltip = EditorGUILayout.TextField(
                "Tooltip",
                tooltipProperty != null ? tooltipProperty.stringValue : string.Empty
            );
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(_graphAsset, "Edit Transition Metadata");
                if (labelProperty != null)
                {
                    labelProperty.stringValue = newLabel;
                }
                if (tooltipProperty != null)
                {
                    tooltipProperty.stringValue = newTooltip;
                }

                _graphSerialized?.ApplyModifiedProperties();
                _graphView?.RefreshEdgeMetadata(_selectedTransitionIndex);
            }
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
            if (_graphView != null)
            {
                _graphView.Populate(_graphSerialized, stackProperty, _graphAsset, configuration);
                _graphView.ClearSelection();
            }

            HandleStateSelection(null);
            HandleTransitionSelection(null);
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

        private SerializedProperty GetTransitionProperty(int index)
        {
            if (index < 0)
            {
                return null;
            }

            _graphSerialized?.Update();
            SerializedProperty stackProperty = FindStackProperty(_stackName);
            if (stackProperty == null)
            {
                return null;
            }

            SerializedProperty transitionsProperty = stackProperty.FindPropertyRelative(
                "_transitions"
            );
            if (transitionsProperty == null || index >= transitionsProperty.arraySize)
            {
                return null;
            }

            return transitionsProperty.GetArrayElementAtIndex(index);
        }

        private static StateStackConfiguration ResolveConfiguration(
            StateGraph graph,
            string stackName
        )
        {
            if (graph == null)
            {
                return null;
            }

            if (
                !string.IsNullOrEmpty(stackName)
                && graph.TryGetStack(stackName, out StateStackConfiguration configuration)
            )
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
            private readonly List<StateEdge> _edges;
            private readonly Dictionary<string, StateMetricsSnapshot> _metricsCache;
            private readonly Dictionary<string, StateMetricsAccumulator> _metricsAccumulators;
            private SerializedProperty _transitionsProperty;

            public Action GraphModified { get; set; }
            public Action<UnityEngine.Object> StateSelected { get; set; }
            public Action<StateEdge> TransitionSelected { get; set; }

            public StateStackGraphView()
            {
                _nodes = new List<StateNode>();
                _edges = new List<StateEdge>();
                _metricsCache = new Dictionary<string, StateMetricsSnapshot>(
                    StringComparer.Ordinal
                );
                _metricsAccumulators = new Dictionary<string, StateMetricsAccumulator>(
                    StringComparer.Ordinal
                );
                this.SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);
                this.AddManipulator(new ContentDragger());
                this.AddManipulator(new SelectionDragger());
                this.AddManipulator(new RectangleSelector());

                GridBackground grid = new GridBackground();
                Insert(0, grid);
                grid.StretchToParentSize();

                graphViewChanged += OnGraphViewChanged;
                RegisterCallback<DragUpdatedEvent>(OnDragUpdated);
                RegisterCallback<DragPerformEvent>(OnDragPerform);
                RegisterCallback<DragLeaveEvent>(OnDragLeave);
            }

            public void DisposeView()
            {
                ClearSelection();
                graphViewChanged -= OnGraphViewChanged;
                UnregisterCallback<DragUpdatedEvent>(OnDragUpdated);
                UnregisterCallback<DragPerformEvent>(OnDragPerform);
                UnregisterCallback<DragLeaveEvent>(OnDragLeave);
                DeleteElements(graphElements.ToList());
                _nodes.Clear();
                _edges.Clear();
                _serializedGraph = null;
                _stackProperty = null;
                _graphAsset = null;
                _configuration = null;
                _currentManager = null;
                StateSelected?.Invoke(null);
                TransitionSelected?.Invoke(null);
                StateSelected = null;
                TransitionSelected = null;
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
                _transitionsProperty =
                    stackProperty != null
                        ? stackProperty.FindPropertyRelative("_transitions")
                        : null;

                ClearSelection();
                StateSelected?.Invoke(null);
                TransitionSelected?.Invoke(null);
                DeleteElements(graphElements.ToList());
                _nodes.Clear();
                _edges.Clear();

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
                    SerializedProperty stateProperty = referenceProperty.FindPropertyRelative(
                        "_state"
                    );
                    SerializedProperty initialProperty = referenceProperty.FindPropertyRelative(
                        "_setAsInitial"
                    );

                    UnityEngine.Object stateObject =
                        stateProperty != null ? stateProperty.objectReferenceValue : null;
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

                CreateEdges();
                Highlight(_currentManager);
            }

            private void OnDragUpdated(DragUpdatedEvent evt)
            {
                List<UnityEngine.Object> candidateStates;
                if (!TryCollectDraggedStates(out candidateStates))
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    return;
                }

                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
                evt.StopPropagation();
            }

            private void OnDragPerform(DragPerformEvent evt)
            {
                if (
                    !TryCollectDraggedStates(out List<UnityEngine.Object> draggedStates)
                    || draggedStates.Count == 0
                )
                {
                    DragAndDrop.visualMode = DragAndDropVisualMode.Rejected;
                    return;
                }

                DragAndDrop.AcceptDrag();

                for (int i = 0; i < draggedStates.Count; i++)
                {
                    AddStateReference(draggedStates[i]);
                }

                evt.StopPropagation();
            }

            private void OnDragLeave(DragLeaveEvent evt)
            {
                DragAndDrop.visualMode = DragAndDropVisualMode.None;
            }

            private bool TryCollectDraggedStates(out List<UnityEngine.Object> states)
            {
                states = new List<UnityEngine.Object>();

                UnityEngine.Object[] draggedObjects = DragAndDrop.objectReferences;
                if (draggedObjects == null || draggedObjects.Length == 0)
                {
                    return false;
                }

                for (int i = 0; i < draggedObjects.Length; i++)
                {
                    UnityEngine.Object candidate = draggedObjects[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate is IState)
                    {
                        states.Add(candidate);
                        continue;
                    }

                    Component componentCandidate = candidate as Component;
                    if (componentCandidate != null && componentCandidate is IState)
                    {
                        states.Add(componentCandidate);
                        continue;
                    }

                    GameObject gameObject = candidate as GameObject;
                    if (gameObject != null)
                    {
                        Component[] components = gameObject.GetComponents<Component>();
                        for (int c = 0; c < components.Length; c++)
                        {
                            Component component = components[c];
                            if (component is IState)
                            {
                                states.Add(component);
                            }
                        }
                    }
                }

                return states.Count > 0;
            }

            public void Highlight(StateStackManager manager)
            {
                _currentManager = manager;
                UpdateMetrics(manager?.Diagnostics);

                foreach (StateNode node in _nodes)
                {
                    node.SetActiveState(false, false);
                }

                if (manager == null)
                {
                    UpdateActiveConnections(null, null);
                    return;
                }

                IReadOnlyList<IState> stack = manager.Stack;
                if (stack == null)
                {
                    UpdateActiveConnections(null, null);
                    return;
                }

                IState current = stack.Count > 0 ? stack[stack.Count - 1] : null;
                IState previous = manager.PreviousState;
                StateNode currentNode = null;
                StateNode previousNode = null;
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
                        if (node.MatchesState(state))
                        {
                            node.SetActiveState(true, ReferenceEquals(state, current));
                            if (ReferenceEquals(state, current))
                            {
                                currentNode = node;
                            }

                            if (ReferenceEquals(state, previous))
                            {
                                previousNode = node;
                            }
                        }
                    }
                }

                UpdateActiveConnections(previousNode, currentNode);
            }

            public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
            {
                base.BuildContextualMenu(evt);
                UnityEngine.Object selected = Selection.activeObject;
                DropdownMenuAction.Status status =
                    selected is IState
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

                List<StateEdge> edgesToRemove = selection.OfType<StateEdge>().ToList();
                if (edgesToRemove.Count > 0)
                {
                    RemoveTransitions(edgesToRemove);
                    return EventPropagation.Stop;
                }

                return base.DeleteSelection();
            }

            private GraphViewChange OnGraphViewChanged(GraphViewChange change)
            {
                if (change.edgesToCreate != null && change.edgesToCreate.Count > 0)
                {
                    CreateMetadataFromEdges(change.edgesToCreate);
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

            private void CreateEdges()
            {
                DeleteElements(graphElements.OfType<StateEdge>().ToList());
                _edges.Clear();

                bool created = CreateEdgesFromMetadata();
                if (!created)
                {
                    CreateDefaultSequentialEdges();
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

            private bool CreateEdgesFromMetadata()
            {
                if (_transitionsProperty == null || _transitionsProperty.arraySize == 0)
                {
                    return false;
                }

                bool created = false;
                for (int i = 0; i < _transitionsProperty.arraySize; i++)
                {
                    SerializedProperty entry = _transitionsProperty.GetArrayElementAtIndex(i);
                    int fromIndex = SafeGetInt(entry, "_fromIndex");
                    int toIndex = SafeGetInt(entry, "_toIndex");
                    StateNode fromNode = FindNodeByIndex(fromIndex);
                    StateNode toNode = FindNodeByIndex(toIndex);
                    if (fromNode == null || toNode == null)
                    {
                        continue;
                    }

                    StateEdge edge = ConnectNodes(fromNode, toNode);
                    edge.MetadataIndex = i;
                    ApplyEdgeMetadata(edge, i);
                    created = true;
                }

                return created;
            }

            private void CreateDefaultSequentialEdges()
            {
                for (int i = 0; i < _nodes.Count - 1; i++)
                {
                    StateNode from = _nodes[i];
                    StateNode to = _nodes[i + 1];
                    StateEdge edge = ConnectNodes(from, to);
                    edge.ClearMetadata();
                }
            }

            private StateEdge ConnectNodes(StateNode from, StateNode to)
            {
                StateEdge edge = new StateEdge(this, from, to)
                {
                    output = from.Output,
                    input = to.Input,
                };
                AddElement(edge);
                from.RefreshPorts();
                to.RefreshPorts();
                _edges.Add(edge);
                return edge;
            }

            private void ApplyEdgeMetadata(StateEdge edge, int metadataIndex)
            {
                if (
                    _transitionsProperty == null
                    || metadataIndex < 0
                    || metadataIndex >= _transitionsProperty.arraySize
                )
                {
                    edge.ClearMetadata();
                    return;
                }

                SerializedProperty entry = _transitionsProperty.GetArrayElementAtIndex(
                    metadataIndex
                );
                string label = SafeGetString(entry, "_label");
                string tooltip = SafeGetString(entry, "_tooltip");
                edge.ApplyMetadata(label, tooltip);
            }

            private void RemoveTransitions(List<StateEdge> edgesToRemove)
            {
                if (_transitionsProperty == null)
                {
                    for (int i = 0; i < edgesToRemove.Count; i++)
                    {
                        StateEdge edge = edgesToRemove[i];
                        RemoveElement(edge);
                        _edges.Remove(edge);
                    }
                    return;
                }

                Undo.RecordObject(_graphAsset, "Remove Transition Metadata");
                for (int i = 0; i < edgesToRemove.Count; i++)
                {
                    StateEdge edge = edgesToRemove[i];
                    if (
                        edge.MetadataIndex >= 0
                        && edge.MetadataIndex < _transitionsProperty.arraySize
                    )
                    {
                        _transitionsProperty.DeleteArrayElementAtIndex(edge.MetadataIndex);
                    }
                }

                ApplySerializedChanges();
                Populate(_serializedGraph, _stackProperty, _graphAsset, _configuration);
                Highlight(_currentManager);
            }

            private void CreateMetadataFromEdges(List<Edge> edgesToCreate)
            {
                if (_transitionsProperty == null)
                {
                    return;
                }

                bool addedMetadata = false;
                for (int i = 0; i < edgesToCreate.Count; i++)
                {
                    Edge edge = edgesToCreate[i];
                    StateNode fromNode = edge.output?.node as StateNode;
                    StateNode toNode = edge.input?.node as StateNode;
                    if (fromNode == null || toNode == null)
                    {
                        continue;
                    }

                    int fromIndex = fromNode.ArrayIndex;
                    int toIndex = toNode.ArrayIndex;
                    if (fromIndex < 0 || toIndex < 0)
                    {
                        continue;
                    }

                    Undo.RecordObject(_graphAsset, "Add Transition Metadata");
                    int newIndex = _transitionsProperty.arraySize;
                    _transitionsProperty.arraySize++;
                    SerializedProperty entry = _transitionsProperty.GetArrayElementAtIndex(
                        newIndex
                    );
                    entry.FindPropertyRelative("_fromIndex").intValue = fromIndex;
                    entry.FindPropertyRelative("_toIndex").intValue = toIndex;
                    entry.FindPropertyRelative("_label").stringValue = string.Empty;
                    entry.FindPropertyRelative("_tooltip").stringValue = string.Empty;
                    addedMetadata = true;
                    RemoveElement(edge);
                }

                if (addedMetadata)
                {
                    ApplySerializedChanges();
                    Populate(_serializedGraph, _stackProperty, _graphAsset, _configuration);
                    Highlight(_currentManager);
                }
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
                    UpdateTransitionsAfterRemoval(index);
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

                Dictionary<int, int> remap = new Dictionary<int, int>(_nodes.Count);
                for (int i = 0; i < orderedNodes.Count; i++)
                {
                    StateNode node = orderedNodes[i];
                    int previousIndex = node.ArrayIndex;
                    node.ArrayIndex = i;
                    SerializedProperty entry = statesProperty.GetArrayElementAtIndex(i);
                    SerializedProperty stateProperty = entry.FindPropertyRelative("_state");
                    SerializedProperty initialProperty = entry.FindPropertyRelative(
                        "_setAsInitial"
                    );
                    if (stateProperty != null)
                    {
                        stateProperty.objectReferenceValue = node.StateObject;
                    }
                    if (initialProperty != null)
                    {
                        initialProperty.boolValue = node.IsInitial;
                    }

                    remap[previousIndex] = i;
                }

                _nodes.Clear();
                _nodes.AddRange(orderedNodes);

                ApplySerializedChanges();
                RemapTransitions(remap);
                Populate(_serializedGraph, _stackProperty, _graphAsset, _configuration);
                Highlight(_currentManager);
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

            public sealed class StateNode : Node
            {
                private readonly StateStackGraphView _owner;
                private readonly Label _descriptionLabel;
                private readonly Label _metricsLabel;
                private readonly Color _baseColor;
                private readonly Color _initialColor = new Color(0.2f, 0.5f, 0.3f, 1f);
                private readonly Color _missingColor = new Color(0.6f, 0.2f, 0.2f, 1f);
                private readonly Color _activeColor = new Color(0.35f, 0.45f, 0.75f, 1f);
                private readonly Color _currentColor = new Color(0.4f, 0.7f, 0.95f, 1f);

                public UnityEngine.Object StateObject { get; set; }
                public IState ResolvedState { get; }
                public string StateName { get; }
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
                    _metricsLabel = new Label();
                    StateName = DetermineStateName(stateObject, resolvedState);

                    this.title =
                        stateObject != null
                            ? stateObject.name
                            : (resolvedState != null ? resolvedState.Name : "<missing>");

                    _descriptionLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    mainContainer.Add(_descriptionLabel);

                    _metricsLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
                    _metricsLabel.style.fontSize = 10;
                    _metricsLabel.style.whiteSpace = WhiteSpace.Normal;
                    _metricsLabel.style.color = new Color(0.82f, 0.82f, 0.82f, 1f);
                    mainContainer.Add(_metricsLabel);

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

                public override void OnSelected()
                {
                    base.OnSelected();
                    _owner.NotifySelectionChange();
                }

                public override void OnUnselected()
                {
                    base.OnUnselected();
                    _owner.NotifySelectionChange();
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
                        _metricsLabel.text = string.Empty;
                    }
                    else
                    {
                        string description =
                            ResolvedState != null
                                ? ResolvedState.GetType().Name
                                : StateObject.GetType().Name;
                        _descriptionLabel.text = description;
                        _descriptionLabel.style.color = Color.white;
                        _metricsLabel.text = string.Empty;
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

                public void ApplyMetrics(StateMetricsSnapshot metrics)
                {
                    if (metrics.HasData)
                    {
                        string lastTriggered = metrics.LastTriggeredUtc.HasValue
                            ? metrics.LastTriggeredUtc.Value.ToLocalTime().ToString("HH:mm:ss")
                            : "--:--:--";
                        string averageLabel =
                            metrics.AverageDurationSeconds > 0f
                                ? metrics.AverageDurationSeconds.ToString("F2")
                                : "0.00";
                        _metricsLabel.text =
                            $"Transitions: {metrics.TransitionCount}\nAvg: {averageLabel}s\nLast: {lastTriggered}";
                    }
                    else
                    {
                        _metricsLabel.text = "No telemetry";
                    }
                }

                public bool MatchesState(IState state)
                {
                    if (state == null)
                    {
                        return false;
                    }

                    if (StateObject is IState nodeState && ReferenceEquals(nodeState, state))
                    {
                        return true;
                    }

                    if (ResolvedState != null && ReferenceEquals(ResolvedState, state))
                    {
                        return true;
                    }

                    return false;
                }

                private static string DetermineStateName(
                    UnityEngine.Object stateObject,
                    IState resolvedState
                )
                {
                    if (stateObject != null)
                    {
                        return stateObject.name;
                    }

                    return resolvedState != null ? resolvedState.Name : string.Empty;
                }
            }

            private void UpdateTransitionsAfterRemoval(int removedIndex)
            {
                if (_transitionsProperty == null)
                {
                    return;
                }

                bool modified = false;
                for (int i = _transitionsProperty.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty entry = _transitionsProperty.GetArrayElementAtIndex(i);
                    int fromIndex = SafeGetInt(entry, "_fromIndex");
                    int toIndex = SafeGetInt(entry, "_toIndex");
                    if (fromIndex == removedIndex || toIndex == removedIndex)
                    {
                        _transitionsProperty.DeleteArrayElementAtIndex(i);
                        modified = true;
                        continue;
                    }

                    if (fromIndex > removedIndex)
                    {
                        entry.FindPropertyRelative("_fromIndex").intValue = fromIndex - 1;
                        modified = true;
                    }

                    if (toIndex > removedIndex)
                    {
                        entry.FindPropertyRelative("_toIndex").intValue = toIndex - 1;
                        modified = true;
                    }
                }

                if (modified)
                {
                    ApplySerializedChanges();
                }
            }

            private void RemapTransitions(Dictionary<int, int> remap)
            {
                if (_transitionsProperty == null || remap == null || remap.Count == 0)
                {
                    return;
                }

                bool modified = false;
                for (int i = _transitionsProperty.arraySize - 1; i >= 0; i--)
                {
                    SerializedProperty entry = _transitionsProperty.GetArrayElementAtIndex(i);
                    int fromIndex = SafeGetInt(entry, "_fromIndex");
                    int toIndex = SafeGetInt(entry, "_toIndex");
                    if (
                        !remap.TryGetValue(fromIndex, out int newFrom)
                        || !remap.TryGetValue(toIndex, out int newTo)
                    )
                    {
                        _transitionsProperty.DeleteArrayElementAtIndex(i);
                        modified = true;
                        continue;
                    }

                    if (newFrom != fromIndex)
                    {
                        entry.FindPropertyRelative("_fromIndex").intValue = newFrom;
                        modified = true;
                    }

                    if (newTo != toIndex)
                    {
                        entry.FindPropertyRelative("_toIndex").intValue = newTo;
                        modified = true;
                    }
                }

                if (modified)
                {
                    ApplySerializedChanges();
                }
            }

            private StateNode FindNodeByIndex(int index)
            {
                if (index < 0)
                {
                    return null;
                }

                for (int i = 0; i < _nodes.Count; i++)
                {
                    StateNode node = _nodes[i];
                    if (node.ArrayIndex == index)
                    {
                        return node;
                    }
                }

                return null;
            }

            public void NotifySelectionChange()
            {
                UnityEngine.Object selectedObject = null;
                StateEdge selectedEdge = null;

                foreach (ISelectable selectable in selection)
                {
                    if (selectedObject == null && selectable is StateNode stateNode)
                    {
                        selectedObject = stateNode.StateObject;
                    }

                    if (selectedEdge == null && selectable is StateEdge stateEdge)
                    {
                        selectedEdge = stateEdge;
                    }
                }

                StateSelected?.Invoke(selectedObject);
                TransitionSelected?.Invoke(selectedEdge);
            }

            public void RefreshEdgeMetadata(int metadataIndex)
            {
                for (int i = 0; i < _edges.Count; i++)
                {
                    StateEdge edge = _edges[i];
                    if (edge.MetadataIndex == metadataIndex)
                    {
                        ApplyEdgeMetadata(edge, metadataIndex);
                        break;
                    }
                }
            }

            private static int SafeGetInt(SerializedProperty property, string relativeName)
            {
                SerializedProperty relative = property.FindPropertyRelative(relativeName);
                return relative != null ? relative.intValue : -1;
            }

            private static string SafeGetString(SerializedProperty property, string relativeName)
            {
                SerializedProperty relative = property.FindPropertyRelative(relativeName);
                return relative != null ? relative.stringValue : string.Empty;
            }

            private void UpdateMetrics(StateStackDiagnostics diagnostics)
            {
                _metricsAccumulators.Clear();
                if (diagnostics == null)
                {
                    ApplyMetricsToNodes();
                    return;
                }

                IReadOnlyList<StateStackDiagnosticEvent> events = diagnostics.Events;
                if (events == null || events.Count == 0)
                {
                    ApplyMetricsToNodes();
                    return;
                }

                Dictionary<string, DateTime> transitionStarts = new Dictionary<string, DateTime>(
                    StringComparer.Ordinal
                );

                for (int i = 0; i < events.Count; i++)
                {
                    StateStackDiagnosticEvent entry = events[i];
                    string currentStateName = string.IsNullOrEmpty(entry.CurrentState)
                        ? "<null>"
                        : entry.CurrentState;

                    switch (entry.EventType)
                    {
                        case StateStackDiagnosticEventType.TransitionStart:
                            transitionStarts[currentStateName] = entry.TimestampUtc;
                            break;
                        case StateStackDiagnosticEventType.TransitionComplete:
                            RecordMetricsSample(
                                currentStateName,
                                entry.TimestampUtc,
                                transitionStarts
                            );
                            break;
                        case StateStackDiagnosticEventType.StatePushed:
                        case StateStackDiagnosticEventType.StatePopped:
                        case StateStackDiagnosticEventType.StateFlattened:
                        case StateStackDiagnosticEventType.StateRemoved:
                            UpdateLastTriggered(currentStateName, entry.TimestampUtc);
                            break;
                    }
                }

                ApplyMetricsToNodes();
            }

            private void RecordMetricsSample(
                string stateName,
                DateTime timestampUtc,
                Dictionary<string, DateTime> transitionStarts
            )
            {
                if (string.IsNullOrEmpty(stateName))
                {
                    return;
                }

                if (
                    !_metricsAccumulators.TryGetValue(
                        stateName,
                        out StateMetricsAccumulator accumulator
                    )
                )
                {
                    accumulator = new StateMetricsAccumulator();
                    _metricsAccumulators[stateName] = accumulator;
                }

                accumulator.TransitionCount++;
                accumulator.LastTriggeredUtc = timestampUtc;

                if (transitionStarts.TryGetValue(stateName, out DateTime startTimeUtc))
                {
                    float duration = Mathf.Max(
                        0f,
                        (float)(timestampUtc - startTimeUtc).TotalSeconds
                    );
                    accumulator.TotalDurationSeconds += duration;
                    accumulator.DurationSamples++;
                }
            }

            private void UpdateLastTriggered(string stateName, DateTime timestampUtc)
            {
                if (string.IsNullOrEmpty(stateName))
                {
                    return;
                }

                if (
                    !_metricsAccumulators.TryGetValue(
                        stateName,
                        out StateMetricsAccumulator accumulator
                    )
                )
                {
                    accumulator = new StateMetricsAccumulator();
                    _metricsAccumulators[stateName] = accumulator;
                }

                if (
                    !accumulator.LastTriggeredUtc.HasValue
                    || accumulator.LastTriggeredUtc < timestampUtc
                )
                {
                    accumulator.LastTriggeredUtc = timestampUtc;
                }
            }

            private void ApplyMetricsToNodes()
            {
                _metricsCache.Clear();
                foreach (
                    KeyValuePair<string, StateMetricsAccumulator> entry in _metricsAccumulators
                )
                {
                    StateMetricsAccumulator accumulator = entry.Value;
                    float average =
                        accumulator.DurationSamples > 0
                            ? accumulator.TotalDurationSeconds / accumulator.DurationSamples
                            : 0f;
                    _metricsCache[entry.Key] = new StateMetricsSnapshot(
                        accumulator.TransitionCount,
                        average,
                        accumulator.LastTriggeredUtc
                    );
                }

                for (int i = 0; i < _nodes.Count; i++)
                {
                    StateNode node = _nodes[i];
                    if (string.IsNullOrEmpty(node.StateName))
                    {
                        node.ApplyMetrics(StateMetricsSnapshot.Empty);
                        continue;
                    }

                    if (_metricsCache.TryGetValue(node.StateName, out StateMetricsSnapshot metrics))
                    {
                        node.ApplyMetrics(metrics);
                    }
                    else
                    {
                        node.ApplyMetrics(StateMetricsSnapshot.Empty);
                    }
                }
            }

            private void UpdateActiveConnections(StateNode previous, StateNode current)
            {
                for (int i = 0; i < _edges.Count; i++)
                {
                    StateEdge edge = _edges[i];
                    bool isActive =
                        previous != null
                        && current != null
                        && ReferenceEquals(edge.From, previous)
                        && ReferenceEquals(edge.To, current);
                    edge.RefreshAppearance(isActive);
                }
            }

            private sealed class StateMetricsAccumulator
            {
                public int TransitionCount;
                public int DurationSamples;
                public float TotalDurationSeconds;
                public DateTime? LastTriggeredUtc;
            }

            public readonly struct StateMetricsSnapshot
            {
                public StateMetricsSnapshot(
                    int transitionCount,
                    float averageDurationSeconds,
                    DateTime? lastTriggeredUtc
                )
                {
                    TransitionCount = transitionCount;
                    AverageDurationSeconds = averageDurationSeconds;
                    LastTriggeredUtc = lastTriggeredUtc;
                }

                public int TransitionCount { get; }
                public float AverageDurationSeconds { get; }
                public DateTime? LastTriggeredUtc { get; }
                public bool HasData => TransitionCount > 0 || LastTriggeredUtc.HasValue;

                public static StateMetricsSnapshot Empty => new StateMetricsSnapshot(0, 0f, null);
            }

            public sealed class StateEdge : Edge
            {
                private static readonly Color ActiveColor = new Color(0.3f, 0.85f, 1f, 1f);
                private static readonly Color IdleColor = new Color(0.25f, 0.25f, 0.25f, 0.85f);

                private readonly StateStackGraphView _owner;
                private readonly Label _label;

                public StateEdge(StateStackGraphView owner, StateNode from, StateNode to)
                {
                    _owner = owner;
                    From = from;
                    To = to;
                    _label = new Label();
                    _label.AddToClassList("state-graph-edge-label");
                    _label.pickingMode = PickingMode.Ignore;
                    _label.style.unityTextAlign = TextAnchor.MiddleCenter;
                    _label.style.fontSize = 10;
                    _label.style.paddingLeft = 4f;
                    _label.style.paddingRight = 4f;
                    _label.style.paddingTop = 2f;
                    _label.style.paddingBottom = 2f;
                    _label.style.backgroundColor = new Color(0f, 0f, 0f, 0.6f);
                    _label.style.color = Color.white;
                    _label.style.borderTopLeftRadius = 4f;
                    _label.style.borderTopRightRadius = 4f;
                    _label.style.borderBottomLeftRadius = 4f;
                    _label.style.borderBottomRightRadius = 4f;
                    Add(_label);
                }

                public StateNode From { get; }
                public StateNode To { get; }
                public int MetadataIndex { get; set; } = -1;

                public bool HasMetadata => MetadataIndex >= 0;

                public override void OnSelected()
                {
                    base.OnSelected();
                    _owner.NotifySelectionChange();
                }

                public override void OnUnselected()
                {
                    base.OnUnselected();
                    _owner.NotifySelectionChange();
                }

                public void ApplyMetadata(string label, string tooltip)
                {
                    _label.text = string.IsNullOrWhiteSpace(label) ? "<transition>" : label;
                    this.tooltip = string.IsNullOrEmpty(tooltip) ? null : tooltip;
                }

                public void ClearMetadata()
                {
                    _label.text = string.Empty;
                    tooltip = null;
                }

                public void RefreshAppearance(bool isActive)
                {
                    EdgeControl edgeControl = this.Q<EdgeControl>();
                    if (edgeControl == null)
                    {
                        return;
                    }

                    Color color = isActive ? ActiveColor : IdleColor;
                    edgeControl.edgeWidth = isActive ? 4 : 2;
                    edgeControl.inputColor = color;
                    edgeControl.outputColor = color;
                    edgeControl.MarkDirtyRepaint();
                }
            }
        }
    }
}
#endif
