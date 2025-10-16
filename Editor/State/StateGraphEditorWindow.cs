#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Text;
    using UnityEditor;
    using UnityEditor.IMGUI.Controls;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Builder;
    using WallstopStudios.DxState.State.Stack.Components;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public sealed class StateGraphEditorWindow : EditorWindow
    {
        private StateGraphAsset _graphAsset;
        private SerializedObject _serializedGraph;
        private Vector2 _stackScrollPosition;
        private StateStackManager _targetManager;
        private int _selectedStackIndex;
        private string[] _stackNames = Array.Empty<string>();
        private bool _applyForceRegister = true;
        private bool _applyEnsureInitialActive = true;
        private GUIStyle _headerStyle;
        private GUIStyle _badgeStyle;
        private GUIStyle _stateLabelStyle;
        private GUIStyle _highlightStackStyle;
        private GUIStyle _graphStackHeaderStyle;
        private GUIStyle _graphNodeLabelStyle;
        private Texture2D _highlightTexture;
        private SearchField _searchField;
        private string _stackSearchTerm = string.Empty;
        private readonly List<NodeHitTarget> _graphHitTargets = new List<NodeHitTarget>();

        [MenuItem("Window/Wallstop Studios/State Graph Editor", priority = 2000)]
        private static void Open()
        {
            StateGraphEditorWindow window = GetWindow<StateGraphEditorWindow>();
            window.titleContent = new GUIContent("State Graph Editor");
            window.Show();
        }

        private void OnEnable()
        {
            if (_graphAsset != null)
            {
                _serializedGraph = new SerializedObject(_graphAsset);
                RefreshStackNames();
            }

            if (_searchField == null)
            {
                _searchField = new SearchField();
            }
        }

        private void OnDisable()
        {
            if (_highlightTexture != null)
            {
                DestroyImmediate(_highlightTexture);
                _highlightTexture = null;
            }

            _highlightStackStyle = null;
        }

        private void OnGUI()
        {
            EnsureStyles();

            DrawToolbar();

            if (_graphAsset == null)
            {
                DrawEmptyState();
                return;
            }

            if (_serializedGraph == null)
            {
                _serializedGraph = new SerializedObject(_graphAsset);
            }

            _serializedGraph.UpdateIfRequiredOrScript();

            SerializedProperty stacksProperty = _serializedGraph.FindProperty("_stacks");
            if (stacksProperty == null)
            {
                EditorGUILayout.HelpBox(
                    "StateGraphAsset is missing the _stacks field.",
                    MessageType.Error
                );
                return;
            }

            EditorGUILayout.Space();
            DrawStacksHeader();

            string selectedStackName = GetSelectedStackName();
            _stackScrollPosition = EditorGUILayout.BeginScrollView(_stackScrollPosition);
            for (int i = 0; i < stacksProperty.arraySize; i++)
            {
                SerializedProperty stackProperty = stacksProperty.GetArrayElementAtIndex(i);
                string displayName = ResolveStackDisplayName(stackProperty, i);
                if (!ShouldDisplayStack(displayName))
                {
                    continue;
                }

                bool highlight =
                    !string.IsNullOrEmpty(selectedStackName)
                    && string.Equals(displayName, selectedStackName, StringComparison.Ordinal);

                DrawStackDefinition(stackProperty, displayName, i, stacksProperty, highlight);
            }
            EditorGUILayout.EndScrollView();

            DrawGraphPreview(stacksProperty);

            EditorGUILayout.Space();
            DrawStackFooter(stacksProperty);

            EditorGUILayout.Space();
            DrawManagerPanel();

            if (_serializedGraph.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(_graphAsset);
                RefreshStackNames();
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            StateGraphAsset selected =
                EditorGUILayout.ObjectField(
                    "Graph Asset",
                    _graphAsset,
                    typeof(StateGraphAsset),
                    false
                ) as StateGraphAsset;

            if (selected != _graphAsset)
            {
                _graphAsset = selected;
                _serializedGraph = _graphAsset != null ? new SerializedObject(_graphAsset) : null;
                RefreshStackNames();
            }

            if (GUILayout.Button("Load Selection", EditorStyles.toolbarButton))
            {
                StateGraphAsset candidate = Selection.activeObject as StateGraphAsset;
                if (candidate != null)
                {
                    _graphAsset = candidate;
                    _serializedGraph = new SerializedObject(_graphAsset);
                    RefreshStackNames();
                }
            }

            GUILayout.FlexibleSpace();

            if (_graphAsset != null && GUILayout.Button("Ping", EditorStyles.toolbarButton))
            {
                EditorGUIUtility.PingObject(_graphAsset);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStacksHeader()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Stacks", _headerStyle);
            GUILayout.FlexibleSpace();
            if (_searchField != null)
            {
                Rect searchRect = GUILayoutUtility.GetRect(
                    200f,
                    EditorGUIUtility.singleLineHeight,
                    GUIStyle.none
                );
                _stackSearchTerm = _searchField.OnGUI(searchRect, _stackSearchTerm);
            }
            EditorGUILayout.EndHorizontal();
        }

        private static void DrawEmptyState()
        {
            EditorGUILayout.HelpBox(
                "Assign a StateGraphAsset to begin editing stacks and state references.",
                MessageType.Info
            );
        }

        private void DrawStackDefinition(
            SerializedProperty stackProperty,
            string displayName,
            int index,
            SerializedProperty stacksCollection,
            bool highlight
        )
        {
            SerializedProperty nameProperty = stackProperty.FindPropertyRelative("_name");
            SerializedProperty statesProperty = stackProperty.FindPropertyRelative("_states");

            GUIStyle containerStyle = highlight ? _highlightStackStyle : EditorStyles.helpBox;
            using (
                EditorGUILayout.VerticalScope verticalScope = new EditorGUILayout.VerticalScope(
                    containerStyle
                )
            )
            {
                EditorGUILayout.BeginHorizontal();
                bool hasIssues = HasStateReferenceIssues(statesProperty);
                GUIContent headerContent = hasIssues
                    ? BuildHeaderContent(
                        displayName,
                        "Stack contains missing or invalid state references.",
                        "console.erroricon.sml"
                    )
                    : new GUIContent(displayName);
                bool expanded = EditorGUILayout.Foldout(
                    stackProperty.isExpanded,
                    headerContent,
                    true
                );
                if (expanded != stackProperty.isExpanded)
                {
                    stackProperty.isExpanded = expanded;
                }

                if (
                    GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(70))
                    && EditorUtility.DisplayDialog(
                        "Remove Stack",
                        $"Remove '{displayName}' from the graph?",
                        "Remove",
                        "Cancel"
                    )
                )
                {
                    stacksCollection.DeleteArrayElementAtIndex(index);
                    EditorGUILayout.EndHorizontal();
                    return;
                }

                using (new EditorGUI.DisabledScope(_graphAsset == null))
                {
                    if (GUILayout.Button("Graph", EditorStyles.miniButton, GUILayout.Width(60)))
                    {
                        string stackKey = nameProperty != null ? nameProperty.stringValue : null;
                        if (string.IsNullOrWhiteSpace(stackKey))
                        {
                            stackKey = null;
                        }
                        StateGraphViewWindow.Open(_graphAsset, stackKey);
                    }
                }

                EditorGUILayout.EndHorizontal();

                if (!stackProperty.isExpanded)
                {
                    return;
                }

                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(nameProperty, new GUIContent("Name"));
                EditorGUILayout.Space();

                EditorGUILayout.LabelField("States", EditorStyles.boldLabel);
                for (int i = 0; i < statesProperty.arraySize; i++)
                {
                    SerializedProperty stateReference = statesProperty.GetArrayElementAtIndex(i);
                    DrawStateReference(stateReference, statesProperty, i);
                }

                if (GUILayout.Button("Add State Reference"))
                {
                    statesProperty.arraySize++;
                    SerializedProperty newReference = statesProperty.GetArrayElementAtIndex(
                        statesProperty.arraySize - 1
                    );
                    SerializedProperty setAsInitial = newReference.FindPropertyRelative(
                        "_setAsInitial"
                    );
                    if (setAsInitial != null)
                    {
                        setAsInitial.boolValue = statesProperty.arraySize == 1;
                    }
                }

                EditorGUI.indentLevel--;
            }
        }

        private void DrawStateReference(
            SerializedProperty stateReference,
            SerializedProperty statesCollection,
            int index
        )
        {
            SerializedProperty stateProperty = stateReference.FindPropertyRelative("_state");
            SerializedProperty initialProperty = stateReference.FindPropertyRelative(
                "_setAsInitial"
            );

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(stateProperty, GUIContent.none);

            if (!IsValidStateReference(stateProperty) || stateProperty.objectReferenceValue == null)
            {
                GUILayout.Label(
                    EditorGUIUtility.IconContent("console.warnicon.sml"),
                    GUILayout.Width(20f)
                );
            }

            UnityEngine.Object stateObject = stateProperty != null
                ? stateProperty.objectReferenceValue
                : null;

            using (new EditorGUI.DisabledScope(stateObject == null))
            {
                if (GUILayout.Button(EditorGUIUtility.IconContent("d_ViewToolZoom"), GUILayout.Width(22f)))
                {
                    EditorGUIUtility.PingObject(stateObject);
                }

                if (GUILayout.Button(EditorGUIUtility.IconContent("d_UnityEditor.InspectorWindow"), GUILayout.Width(22f)))
                {
                    Selection.activeObject = stateObject;
                }
            }

            bool isInitial = initialProperty != null && initialProperty.boolValue;
            bool newIsInitial = EditorGUILayout.ToggleLeft(
                "Initial",
                isInitial,
                GUILayout.Width(70)
            );
            if (initialProperty != null && newIsInitial != isInitial)
            {
                initialProperty.boolValue = newIsInitial;
                if (newIsInitial)
                {
                    EnsureSingleInitialState(statesCollection, index);
                }
                else
                {
                    EnsureAtLeastOneInitialState(statesCollection);
                }
            }

            if (GUILayout.Button("▲", GUILayout.Width(24)) && index > 0)
            {
                statesCollection.MoveArrayElement(index, index - 1);
            }

            if (
                GUILayout.Button("▼", GUILayout.Width(24))
                && index < statesCollection.arraySize - 1
            )
            {
                statesCollection.MoveArrayElement(index, index + 1);
            }

            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                bool removedWasInitial = initialProperty != null && initialProperty.boolValue;
                statesCollection.DeleteArrayElementAtIndex(index);
                if (removedWasInitial)
                {
                    EnsureAtLeastOneInitialState(statesCollection);
                }
                EditorGUILayout.EndHorizontal();
                return;
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawStackFooter(SerializedProperty stacksProperty)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Add Stack", GUILayout.Width(120)))
            {
                stacksProperty.arraySize++;
                SerializedProperty newStack = stacksProperty.GetArrayElementAtIndex(
                    stacksProperty.arraySize - 1
                );
                SerializedProperty nameProperty = newStack.FindPropertyRelative("_name");
                SerializedProperty statesProperty = newStack.FindPropertyRelative("_states");
                if (nameProperty != null)
                {
                    nameProperty.stringValue = "New Stack";
                }
                if (statesProperty != null)
                {
                    statesProperty.arraySize = 0;
                }
            }
            EditorGUILayout.EndHorizontal();
        }

        private void DrawGraphPreview(SerializedProperty stacksProperty)
        {
            const float previewHeight = 240f;
            Rect previewRect = GUILayoutUtility.GetRect(
                0f,
                previewHeight,
                GUILayout.ExpandWidth(true)
            );
            if (Event.current.type == EventType.Repaint)
            {
                EditorGUI.DrawRect(previewRect, new Color(0.12f, 0.12f, 0.16f, 1f));
            }

            Rect paddingRect = new Rect(
                previewRect.x + 8f,
                previewRect.y + 8f,
                previewRect.width - 16f,
                previewRect.height - 16f
            );

            Rect headerRect = new Rect(paddingRect.x, paddingRect.y - 22f, paddingRect.width, 20f);
            GUI.Label(headerRect, "Graph Preview", _headerStyle);
            _graphHitTargets.Clear();

            List<(SerializedProperty stackProperty, string displayName)> visibleStacks =
                new List<(SerializedProperty, string)>();
            for (int i = 0; i < stacksProperty.arraySize; i++)
            {
                SerializedProperty stackProperty = stacksProperty.GetArrayElementAtIndex(i);
                string displayName = ResolveStackDisplayName(stackProperty, i);
                if (!ShouldDisplayStack(displayName))
                {
                    continue;
                }

                visibleStacks.Add((stackProperty, displayName));
            }

            if (visibleStacks.Count == 0)
            {
                Rect emptyRect = new Rect(
                    paddingRect.x,
                    paddingRect.center.y - 10f,
                    paddingRect.width,
                    20f
                );
                GUI.Label(emptyRect, "No stacks to preview", _stateLabelStyle);
                return;
            }

            float columnWidth = Mathf.Max(160f, paddingRect.width / visibleStacks.Count);
            float totalWidth = columnWidth * visibleStacks.Count;
            float offsetX = paddingRect.x + Mathf.Max(0f, (paddingRect.width - totalWidth) * 0.5f);
            float columnHeight = paddingRect.height;

            Handles.BeginGUI();
            Handles.color = new Color(1f, 1f, 1f, 0.2f);

            for (int columnIndex = 0; columnIndex < visibleStacks.Count; columnIndex++)
            {
                SerializedProperty stackProperty = visibleStacks[columnIndex].stackProperty;
                string displayName = visibleStacks[columnIndex].displayName;

                Rect columnRect = new Rect(
                    offsetX + columnIndex * columnWidth,
                    paddingRect.y,
                    columnWidth,
                    columnHeight
                );

                Rect localHeaderRect = new Rect(columnRect.x, columnRect.y, columnRect.width, 22f);
                EditorGUI.DrawRect(localHeaderRect, new Color(0.18f, 0.22f, 0.28f, 0.95f));
                GUI.Label(localHeaderRect, displayName, _graphStackHeaderStyle);

                SerializedProperty statesProperty = stackProperty.FindPropertyRelative("_states");
                if (statesProperty == null || statesProperty.arraySize == 0)
                {
                    Rect emptyStateRect = new Rect(
                        columnRect.x + 12f,
                        localHeaderRect.yMax + 8f,
                        columnRect.width - 24f,
                        20f
                    );
                    EditorGUI.DrawRect(emptyStateRect, new Color(0.25f, 0.25f, 0.28f, 1f));
                    GUI.Label(emptyStateRect, "(empty)", _graphNodeLabelStyle);
                    continue;
                }

                float availableHeight = columnRect.height - localHeaderRect.height - 14f;
                float nodeHeight = 26f;
                float spacing = ResolveVerticalSpacing(
                    statesProperty.arraySize,
                    availableHeight,
                    nodeHeight
                );
                float startY = localHeaderRect.yMax + 8f;

                for (int i = 0; i < statesProperty.arraySize; i++)
                {
                    SerializedProperty reference = statesProperty.GetArrayElementAtIndex(i);
                    SerializedProperty stateProperty = reference.FindPropertyRelative("_state");
                    SerializedProperty initialProperty = reference.FindPropertyRelative(
                        "_setAsInitial"
                    );
                    bool isInitial = initialProperty != null && initialProperty.boolValue;

                    UnityEngine.Object stateObject =
                        stateProperty != null ? stateProperty.objectReferenceValue : null;
                    string label = stateObject != null ? stateObject.name : "<missing>";

                    float y = startY + i * (nodeHeight + spacing);
                    Rect nodeRect = new Rect(
                        columnRect.x + 12f,
                        y,
                        columnRect.width - 24f,
                        nodeHeight
                    );

                    Color nodeColor = ResolveNodeColor(stateObject, isInitial);
                    EditorGUI.DrawRect(nodeRect, nodeColor);
                    GUI.Label(nodeRect, label, _graphNodeLabelStyle);

                    if (stateObject != null)
                    {
                        EditorGUIUtility.AddCursorRect(nodeRect, MouseCursor.Link);
                        _graphHitTargets.Add(
                            new NodeHitTarget { Rect = nodeRect, State = stateObject }
                        );
                    }

                    if (i < statesProperty.arraySize - 1)
                    {
                        SerializedProperty nextReference = statesProperty.GetArrayElementAtIndex(
                            i + 1
                        );
                        SerializedProperty nextStateProperty = nextReference.FindPropertyRelative(
                            "_state"
                        );
                        UnityEngine.Object nextStateObject =
                            nextStateProperty != null
                                ? nextStateProperty.objectReferenceValue
                                : null;
                        if (stateObject != null || nextStateObject != null)
                        {
                            Rect nextRect = new Rect(
                                columnRect.x + 12f,
                                startY + (i + 1) * (nodeHeight + spacing),
                                columnRect.width - 24f,
                                nodeHeight
                            );
                            DrawConnector(nodeRect, nextRect, i % 2 == 0);
                        }
                    }
                }
            }

            Handles.EndGUI();
            HandleGraphPreviewEvents();
        }

        private void HandleGraphPreviewEvents()
        {
            Event evt = Event.current;
            if (evt.type != EventType.MouseDown || evt.button != 0)
            {
                return;
            }

            for (int i = 0; i < _graphHitTargets.Count; i++)
            {
                NodeHitTarget target = _graphHitTargets[i];
                if (target.State == null)
                {
                    continue;
                }

                if (!target.Rect.Contains(evt.mousePosition))
                {
                    continue;
                }

                EditorGUIUtility.PingObject(target.State);
                evt.Use();
                break;
            }
        }

        private void DrawManagerPanel()
        {
            EditorGUILayout.LabelField("Runtime Debugging", EditorStyles.boldLabel);
            _targetManager =
                EditorGUILayout.ObjectField(
                    "State Stack Manager",
                    _targetManager,
                    typeof(StateStackManager),
                    true
                ) as StateStackManager;

            if (_targetManager == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign an active StateStackManager to apply stacks and inspect the live state.",
                    MessageType.Info
                );
                return;
            }

            if (_stackNames.Length == 0)
            {
                EditorGUILayout.HelpBox(
                    "The selected StateGraphAsset does not contain any stacks.",
                    MessageType.Warning
                );
            }
            else
            {
                if (_selectedStackIndex >= _stackNames.Length)
                {
                    _selectedStackIndex = 0;
                }

                _selectedStackIndex = EditorGUILayout.Popup(
                    "Stack",
                    _selectedStackIndex,
                    _stackNames
                );
            }

            _applyForceRegister = EditorGUILayout.Toggle(
                new GUIContent("Force Register States"),
                _applyForceRegister
            );
            _applyEnsureInitialActive = EditorGUILayout.Toggle(
                new GUIContent("Ensure Initial Active"),
                _applyEnsureInitialActive
            );

            StateStackDiagnosticsOverlay overlayComponent =
                _targetManager.GetComponent<StateStackDiagnosticsOverlay>();
            using (new EditorGUILayout.HorizontalScope())
            {
                bool overlayEnabled = overlayComponent != null;
                bool newOverlayEnabled = EditorGUILayout.Toggle(
                    new GUIContent("Diagnostics Overlay"),
                    overlayEnabled
                );

                if (newOverlayEnabled != overlayEnabled)
                {
                    SetDiagnosticsOverlayEnabled(newOverlayEnabled);
                    overlayComponent = _targetManager.GetComponent<StateStackDiagnosticsOverlay>();
                }

                using (new EditorGUI.DisabledScope(!overlayEnabled))
                {
                    if (GUILayout.Button("Focus Overlay", GUILayout.Width(120)))
                    {
                        EditorGUIUtility.PingObject(overlayComponent);
                    }
                }
            }

            if (overlayComponent != null)
            {
                DrawOverlayControls(overlayComponent);
            }

            EditorGUILayout.Space();
            DrawStatusSummary();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                using (new EditorGUI.DisabledScope(_graphAsset == null))
                {
                    if (GUILayout.Button("Apply To Manager", GUILayout.Width(160)))
                    {
                        ApplyGraphToManager();
                    }
                }
            }

            EditorGUILayout.Space();
            DrawLiveStackView();
            EditorGUILayout.Space();
            DrawRecentEvents();
        }

        private void DrawLiveStackView()
        {
            IReadOnlyList<IState> activeStack = _targetManager.Stack;
            EditorGUILayout.LabelField("Active Stack", EditorStyles.boldLabel);

            if (activeStack == null || activeStack.Count == 0)
            {
                EditorGUILayout.LabelField("(empty)");
                return;
            }

            EditorGUI.indentLevel++;
            for (int i = activeStack.Count - 1; i >= 0; i--)
            {
                IState state = activeStack[i];
                string stateName = state != null ? state.Name : "<null>";
                EditorGUILayout.LabelField(stateName);
            }
            EditorGUI.indentLevel--;
        }

        private void DrawRecentEvents()
        {
            StateStackDiagnostics diagnostics = _targetManager.Diagnostics;
            if (diagnostics == null)
            {
                return;
            }

            IReadOnlyList<StateStackDiagnosticEvent> events = diagnostics.Events;
            if (events == null || events.Count == 0)
            {
                EditorGUILayout.LabelField("No recent events.");
                return;
            }

            EditorGUILayout.LabelField("Recent Events", EditorStyles.boldLabel);
            int displayed = 0;
            EditorGUI.indentLevel++;
            for (int i = events.Count - 1; i >= 0 && displayed < 8; i--)
            {
                StateStackDiagnosticEvent entry = events[i];
                string label =
                    $"[{entry.TimestampUtc:HH:mm:ss}] {entry.EventType} → {entry.CurrentState} (prev: {entry.PreviousState})";
                EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                displayed++;
            }
            EditorGUI.indentLevel--;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Copy Diagnostics", GUILayout.Width(150)))
                {
                    GUIUtility.systemCopyBuffer = BuildDiagnosticsClipboard();
                    ShowNotification(new GUIContent("Diagnostics copied"));
                }
            }
        }

        private void ApplyGraphToManager()
        {
            if (_graphAsset == null || _targetManager == null)
            {
                return;
            }

            StateGraph graph;
            try
            {
                graph = _graphAsset.BuildGraph();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                return;
            }

            StateStackConfiguration configuration = ResolveConfiguration(graph);
            if (configuration == null)
            {
                Debug.LogError("Unable to resolve selected stack from the graph asset.");
                return;
            }

            StateStack stateStack = GetUnderlyingStateStack(_targetManager);
            if (stateStack == null)
            {
                Debug.LogError("Unable to access StateStack on the selected manager.");
                return;
            }

            try
            {
                configuration
                    .ApplyAsync(stateStack, _applyForceRegister, _applyEnsureInitialActive)
                    .GetAwaiter()
                    .GetResult();
                EditorUtility.SetDirty(_targetManager);
                Repaint();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, _targetManager);
            }
        }

        private StateStackConfiguration ResolveConfiguration(StateGraph graph)
        {
            if (graph == null)
            {
                return null;
            }

            if (_stackNames.Length == 0)
            {
                foreach (KeyValuePair<string, StateStackConfiguration> entry in graph.Stacks)
                {
                    return entry.Value;
                }

                return null;
            }

            string stackName = _stackNames[
                Mathf.Clamp(_selectedStackIndex, 0, _stackNames.Length - 1)
            ];
            if (graph.TryGetStack(stackName, out StateStackConfiguration configuration))
            {
                return configuration;
            }

            return null;
        }

        private static StateStack GetUnderlyingStateStack(StateStackManager manager)
        {
            if (manager == null)
            {
                return null;
            }

            FieldInfo field = typeof(StateStackManager).GetField(
                "_stateStack",
                BindingFlags.Instance | BindingFlags.NonPublic
            );
            return field?.GetValue(manager) as StateStack;
        }

        private void RefreshStackNames()
        {
            if (_graphAsset == null)
            {
                _stackNames = Array.Empty<string>();
                _selectedStackIndex = 0;
                return;
            }

            try
            {
                StateGraph graph = _graphAsset.BuildGraph();
                List<string> names = new List<string>();
                foreach (KeyValuePair<string, StateStackConfiguration> entry in graph.Stacks)
                {
                    if (!string.IsNullOrWhiteSpace(entry.Key))
                    {
                        names.Add(entry.Key);
                    }
                }

                _stackNames = names.Count > 0 ? names.ToArray() : Array.Empty<string>();
                if (_selectedStackIndex >= _stackNames.Length)
                {
                    _selectedStackIndex = Mathf.Max(0, _stackNames.Length - 1);
                }
            }
            catch
            {
                _stackNames = Array.Empty<string>();
                _selectedStackIndex = 0;
            }
        }

        private bool ShouldDisplayStack(string displayName)
        {
            if (string.IsNullOrEmpty(_stackSearchTerm))
            {
                return true;
            }

            if (string.IsNullOrEmpty(displayName))
            {
                return false;
            }

            return displayName.IndexOf(_stackSearchTerm, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private string ResolveStackDisplayName(SerializedProperty stackProperty, int index)
        {
            SerializedProperty nameProperty = stackProperty.FindPropertyRelative("_name");
            string displayName = nameProperty != null ? nameProperty.stringValue : null;
            if (string.IsNullOrWhiteSpace(displayName))
            {
                displayName = $"Stack {index + 1}";
            }

            return displayName;
        }

        private string GetSelectedStackName()
        {
            if (_stackNames == null || _stackNames.Length == 0)
            {
                return null;
            }

            int index = Mathf.Clamp(_selectedStackIndex, 0, _stackNames.Length - 1);
            return _stackNames[index];
        }

        private void DrawStatusSummary()
        {
            if (_targetManager == null)
            {
                return;
            }

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    bool isTransitioning = _targetManager.IsTransitioning;
                    string statusLabel = isTransitioning ? "Transitioning" : "Idle";
                    Color statusColor = isTransitioning
                        ? new Color(0.85f, 0.4f, 0.2f)
                        : new Color(0.2f, 0.6f, 0.25f);
                    DrawStatusBadge(statusLabel, statusColor);

                    StateStackDiagnostics diagnostics = _targetManager.Diagnostics;
                    if (diagnostics != null && diagnostics.TransitionQueueDepth > 0)
                    {
                        DrawStatusBadge(
                            $"Queue {diagnostics.TransitionQueueDepth}",
                            new Color(0.95f, 0.65f, 0.2f)
                        );
                    }

                    GUILayout.FlexibleSpace();
                    float progress = Mathf.Clamp01(_targetManager.Progress);
                    EditorGUILayout.LabelField(
                        $"Progress {Mathf.RoundToInt(progress * 100f)}%",
                        _stateLabelStyle,
                        GUILayout.Width(140f)
                    );
                }

                EditorGUILayout.Space(2f);
                EditorGUILayout.LabelField(
                    $"Current: {FormatStateName(_targetManager.CurrentState)}",
                    _stateLabelStyle
                );
                EditorGUILayout.LabelField(
                    $"Previous: {FormatStateName(_targetManager.PreviousState)}",
                    _stateLabelStyle
                );

                StateStackDiagnostics diagnosticsSnapshot = _targetManager.Diagnostics;
                if (diagnosticsSnapshot != null)
                {
                    EditorGUILayout.Space(2f);
                    EditorGUILayout.LabelField(
                        $"Transitions: {diagnosticsSnapshot.TransitionCount}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Average Duration: {diagnosticsSnapshot.AverageTransitionDuration:F2}s",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Longest Duration: {diagnosticsSnapshot.LongestTransitionDuration:F2}s",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Deferred Pending: {diagnosticsSnapshot.PendingDeferredTransitions}",
                        EditorStyles.miniLabel
                    );
                    EditorGUILayout.LabelField(
                        $"Deferred Lifetime: {diagnosticsSnapshot.LifetimeDeferredTransitions}",
                        EditorStyles.miniLabel
                    );

                    IReadOnlyList<StateStackDiagnosticEvent> events = diagnosticsSnapshot.Events;
                    if (events != null && events.Count > 0)
                    {
                        EditorGUILayout.Space(4f);
                        EditorGUILayout.LabelField("Recent Events", EditorStyles.boldLabel);
                        int displayed = 0;
                        for (int i = events.Count - 1; i >= 0 && displayed < 5; i--)
                        {
                            StateStackDiagnosticEvent diagnosticEvent = events[i];
                            string eventLabel =
                                $"{diagnosticEvent.TimestampUtc:HH:mm:ss} {diagnosticEvent.EventType} → {diagnosticEvent.CurrentState}";
                            EditorGUILayout.LabelField(eventLabel, EditorStyles.miniLabel);
                            displayed++;
                        }
                    }
                }
            }
        }

        private string FormatStateName(IState state)
        {
            return state != null ? state.Name : "<none>";
        }

        private string BuildDiagnosticsClipboard()
        {
            StringBuilder builder = new StringBuilder();
            builder.AppendLine($"StateStack Diagnostics Snapshot ({_targetManager.name})");
            builder.AppendLine($"Current: {FormatStateName(_targetManager.CurrentState)}");
            builder.AppendLine($"Previous: {FormatStateName(_targetManager.PreviousState)}");
            builder.AppendLine($"Stack Depth: {_targetManager.Stack.Count}");

            StateStackDiagnostics diagnostics = _targetManager.Diagnostics;
            if (diagnostics != null)
            {
                builder.AppendLine(
                    $"Queue Depth: {diagnostics.TransitionQueueDepth}, Deferred Pending: {diagnostics.PendingDeferredTransitions}, Deferred Lifetime: {diagnostics.LifetimeDeferredTransitions}"
                );
                builder.AppendLine(
                    $"Average Duration: {diagnostics.AverageTransitionDuration:F3}s, Longest: {diagnostics.LongestTransitionDuration:F3}s"
                );

                IReadOnlyList<StateStackDiagnosticEvent> events = diagnostics.Events;
                int count = Mathf.Min(events?.Count ?? 0, 10);
                if (count > 0)
                {
                    builder.AppendLine("Recent Events:");
                    for (int i = events.Count - count; i < events.Count; i++)
                    {
                        StateStackDiagnosticEvent evt = events[i];
                        builder.AppendLine(
                            $"  [{evt.TimestampUtc:O}] {evt.EventType} → {evt.CurrentState} (prev: {evt.PreviousState}, depth: {evt.StackDepth})"
                        );
                    }
                }
            }

            return builder.ToString();
        }

        private void DrawOverlayControls(StateStackDiagnosticsOverlay overlay)
        {
            SerializedObject overlaySerialized = new SerializedObject(overlay);
            overlaySerialized.Update();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Overlay Settings", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(
                overlaySerialized.FindProperty("_toggleKey"),
                new GUIContent("Toggle Key")
            );
            EditorGUILayout.PropertyField(
                overlaySerialized.FindProperty("_startVisible"),
                new GUIContent("Start Visible")
            );
            EditorGUILayout.PropertyField(
                overlaySerialized.FindProperty("_eventsToDisplay"),
                new GUIContent("Events To Display")
            );
            EditorGUI.indentLevel--;

            if (overlaySerialized.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(overlay);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Dump Diagnostics", GUILayout.Width(140)))
                {
                    DumpDiagnosticsToConsole();
                }
            }
        }

        private void DrawStatusBadge(string text, Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(
                80f,
                EditorGUIUtility.singleLineHeight + 6f,
                _badgeStyle,
                GUILayout.ExpandWidth(false)
            );
            EditorGUI.DrawRect(rect, color);
            EditorGUI.LabelField(rect, text, _badgeStyle);
        }

        private void DumpDiagnosticsToConsole()
        {
            if (_targetManager == null)
            {
                return;
            }

            StateStackDiagnostics diagnostics = _targetManager.Diagnostics;
            if (diagnostics == null)
            {
                Debug.LogWarning(
                    "StateStackManager does not have diagnostics enabled.",
                    _targetManager
                );
                return;
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine($"StateStack Diagnostics ({_targetManager.name})");
            builder.AppendLine($"Current: {FormatStateName(_targetManager.CurrentState)}");
            builder.AppendLine($"Previous: {FormatStateName(_targetManager.PreviousState)}");
            builder.AppendLine($"Stack Depth: {_targetManager.Stack.Count}");
            builder.AppendLine(
                $"Queue Depth: {diagnostics.TransitionQueueDepth}, Deferred Pending: {diagnostics.PendingDeferredTransitions}, Deferred Lifetime: {diagnostics.LifetimeDeferredTransitions}"
            );
            builder.AppendLine(
                $"Average Duration: {diagnostics.AverageTransitionDuration:F3}s, Longest: {diagnostics.LongestTransitionDuration:F3}s"
            );

            IReadOnlyList<StateStackDiagnosticEvent> events = diagnostics.Events;
            int count = Mathf.Min(events.Count, 10);
            if (count > 0)
            {
                builder.AppendLine("Recent Events:");
                for (int i = events.Count - count; i < events.Count; i++)
                {
                    StateStackDiagnosticEvent evt = events[i];
                    builder.AppendLine(
                        $"  [{evt.TimestampUtc:O}] {evt.EventType} → {evt.CurrentState} (prev: {evt.PreviousState}, depth: {evt.StackDepth})"
                    );
                }
            }

            Debug.Log(builder.ToString(), _targetManager);
        }

        private Color ResolveNodeColor(UnityEngine.Object stateObject, bool isInitial)
        {
            Color baseColor = isInitial
                ? new Color(0.25f, 0.6f, 0.35f, 0.95f)
                : new Color(0.27f, 0.3f, 0.36f, 0.95f);

            if (_targetManager == null || stateObject == null)
            {
                return baseColor;
            }

            if (stateObject is IState state)
            {
                IReadOnlyList<IState> activeStack = _targetManager.Stack;
                for (int i = 0; i < activeStack.Count; i++)
                {
                    IState activeState = activeStack[i];
                    if (ReferenceEquals(activeState, state))
                    {
                        if (i == activeStack.Count - 1)
                        {
                            return new Color(0.45f, 0.75f, 0.95f, 0.95f);
                        }

                        return new Color(0.35f, 0.45f, 0.75f, 0.95f);
                    }
                }
            }

            return baseColor;
        }

        private float ResolveVerticalSpacing(int count, float availableHeight, float nodeHeight)
        {
            if (count <= 1)
            {
                return 0f;
            }

            float spacing = (availableHeight - count * nodeHeight) / (count - 1);
            return Mathf.Max(6f, spacing);
        }

        private void DrawConnector(Rect fromRect, Rect toRect, bool alternate)
        {
            Vector3 start = new Vector2(fromRect.center.x, fromRect.yMax);
            Vector3 end = new Vector2(toRect.center.x, toRect.yMin);
            float verticalDistance = Mathf.Abs(end.y - start.y);
            float controlOffset = Mathf.Max(18f, verticalDistance * 0.4f);
            float horizontalOffset = alternate ? 12f : -12f;
            Vector3 startTangent = start + new Vector3(horizontalOffset, controlOffset);
            Vector3 endTangent = end - new Vector3(horizontalOffset, controlOffset);
            Handles.DrawBezier(
                start,
                end,
                startTangent,
                endTangent,
                new Color(1f, 1f, 1f, 0.35f),
                null,
                2f
            );
            Vector3 arrowHead = end + new Vector3(0f, 4f, 0f);
            Handles.DrawAAPolyLine(2f, end, arrowHead + new Vector3(-4f, -8f, 0f));
            Handles.DrawAAPolyLine(2f, end, arrowHead + new Vector3(4f, -8f, 0f));
        }

        private void SetDiagnosticsOverlayEnabled(bool enable)
        {
            if (_targetManager == null)
            {
                return;
            }

            StateStackDiagnosticsOverlay overlay =
                _targetManager.GetComponent<StateStackDiagnosticsOverlay>();
            if (enable)
            {
                if (overlay != null)
                {
                    return;
                }

                Undo.RecordObject(_targetManager.gameObject, "Enable Diagnostics Overlay");
                overlay = Undo.AddComponent<StateStackDiagnosticsOverlay>(
                    _targetManager.gameObject
                );
                EditorUtility.SetDirty(_targetManager.gameObject);
                return;
            }

            if (overlay == null)
            {
                return;
            }

            Undo.DestroyObjectImmediate(overlay);
        }

        private void EnsureStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                };
            }

            if (_stateLabelStyle == null)
            {
                _stateLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Italic,
                };
            }

            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(6, 6, 2, 2),
                };
                _badgeStyle.normal.textColor = Color.white;
            }

            if (_highlightStackStyle == null)
            {
                _highlightTexture = CreateSolidTexture(new Color(0.2f, 0.55f, 0.95f, 0.18f));
                _highlightStackStyle = new GUIStyle(EditorStyles.helpBox);
                _highlightStackStyle.normal.background = _highlightTexture;
            }

            if (_graphStackHeaderStyle == null)
            {
                _graphStackHeaderStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontSize = 12,
                };
            }

            if (_graphNodeLabelStyle == null)
            {
                _graphNodeLabelStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                };
                _graphNodeLabelStyle.normal.textColor = Color.white;
            }

            if (_searchField == null)
            {
                _searchField = new SearchField();
            }
        }

        private Texture2D CreateSolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void EnsureSingleInitialState(
            SerializedProperty statesProperty,
            int selectedIndex
        )
        {
            for (int i = 0; i < statesProperty.arraySize; i++)
            {
                if (i == selectedIndex)
                {
                    continue;
                }

                SerializedProperty other = statesProperty.GetArrayElementAtIndex(i);
                SerializedProperty otherInitial = other.FindPropertyRelative("_setAsInitial");
                if (otherInitial != null)
                {
                    otherInitial.boolValue = false;
                }
            }
        }

        private static void EnsureAtLeastOneInitialState(SerializedProperty statesProperty)
        {
            if (statesProperty.arraySize == 0)
            {
                return;
            }

            for (int i = 0; i < statesProperty.arraySize; i++)
            {
                SerializedProperty candidate = statesProperty.GetArrayElementAtIndex(i);
                SerializedProperty initial = candidate.FindPropertyRelative("_setAsInitial");
                if (initial != null && initial.boolValue)
                {
                    return;
                }
            }

            SerializedProperty fallback = statesProperty.GetArrayElementAtIndex(0);
            SerializedProperty fallbackFlag = fallback.FindPropertyRelative("_setAsInitial");
            if (fallbackFlag != null)
            {
                fallbackFlag.boolValue = true;
            }
        }

        private static bool IsValidStateReference(SerializedProperty stateProperty)
        {
            if (stateProperty == null)
            {
                return false;
            }

            UnityEngine.Object obj = stateProperty.objectReferenceValue;
            return obj == null || obj is IState;
        }

        private static bool HasStateReferenceIssues(SerializedProperty statesProperty)
        {
            if (statesProperty == null)
            {
                return false;
            }

            for (int i = 0; i < statesProperty.arraySize; i++)
            {
                SerializedProperty reference = statesProperty.GetArrayElementAtIndex(i);
                SerializedProperty stateProperty = reference.FindPropertyRelative("_state");
                if (!IsValidStateReference(stateProperty) || stateProperty.objectReferenceValue == null)
                {
                    return true;
                }
            }

            return false;
        }

        private static GUIContent BuildHeaderContent(string title, string tooltip, string iconName)
        {
            GUIContent icon = EditorGUIUtility.IconContent(iconName);
            return icon != null
                ? new GUIContent(title, icon.image, tooltip)
                : new GUIContent(title, tooltip);
        }

        private struct NodeHitTarget
        {
            public Rect Rect;
            public UnityEngine.Object State;
        }
    }
}
#endif
