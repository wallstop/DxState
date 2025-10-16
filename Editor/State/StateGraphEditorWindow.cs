#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
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
        private Texture2D _highlightTexture;
        private SearchField _searchField;
        private string _stackSearchTerm = string.Empty;

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
                EditorGUILayout.HelpBox("StateGraphAsset is missing the _stacks field.", MessageType.Error);
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

                bool highlight = !string.IsNullOrEmpty(selectedStackName)
                    && string.Equals(displayName, selectedStackName, StringComparison.Ordinal);

                DrawStackDefinition(stackProperty, displayName, i, stacksProperty, highlight);
            }
            EditorGUILayout.EndScrollView();

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

            StateGraphAsset selected = EditorGUILayout.ObjectField(
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
                Rect searchRect = GUILayoutUtility.GetRect(200f, EditorGUIUtility.singleLineHeight, GUIStyle.none);
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
            using (EditorGUILayout.VerticalScope verticalScope = new EditorGUILayout.VerticalScope(containerStyle))
            {
            EditorGUILayout.BeginHorizontal();
            bool expanded = EditorGUILayout.Foldout(stackProperty.isExpanded, displayName, true);
            if (expanded != stackProperty.isExpanded)
            {
                stackProperty.isExpanded = expanded;
            }

            if (GUILayout.Button("Remove", EditorStyles.miniButton, GUILayout.Width(70)) &&
                EditorUtility.DisplayDialog(
                    "Remove Stack",
                    $"Remove '{displayName}' from the graph?",
                    "Remove",
                    "Cancel"
                ))
            {
                stacksCollection.DeleteArrayElementAtIndex(index);
                EditorGUILayout.EndHorizontal();
                return;
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
                SerializedProperty newReference = statesProperty.GetArrayElementAtIndex(statesProperty.arraySize - 1);
                SerializedProperty setAsInitial = newReference.FindPropertyRelative("_setAsInitial");
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
            SerializedProperty initialProperty = stateReference.FindPropertyRelative("_setAsInitial");

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(stateProperty, GUIContent.none);

            bool isInitial = initialProperty != null && initialProperty.boolValue;
            bool newIsInitial = EditorGUILayout.ToggleLeft("Initial", isInitial, GUILayout.Width(70));
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

            if (GUILayout.Button("▼", GUILayout.Width(24)) && index < statesCollection.arraySize - 1)
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
                SerializedProperty newStack = stacksProperty.GetArrayElementAtIndex(stacksProperty.arraySize - 1);
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

        private void DrawManagerPanel()
        {
            EditorGUILayout.LabelField("Runtime Debugging", EditorStyles.boldLabel);
            _targetManager = EditorGUILayout.ObjectField(
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
                configuration.ApplyAsync(
                    stateStack,
                    _applyForceRegister,
                    _applyEnsureInitialActive
                ).GetAwaiter().GetResult();
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

            string stackName = _stackNames[Mathf.Clamp(_selectedStackIndex, 0, _stackNames.Length - 1)];
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

        private void DrawStatusBadge(string text, Color color)
        {
            Rect rect = GUILayoutUtility.GetRect(80f, EditorGUIUtility.singleLineHeight + 6f, _badgeStyle, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(rect, color);
            EditorGUI.LabelField(rect, text, _badgeStyle);
        }

        private void EnsureStyles()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(EditorStyles.boldLabel)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold
                };
            }

            if (_stateLabelStyle == null)
            {
                _stateLabelStyle = new GUIStyle(EditorStyles.label)
                {
                    fontStyle = FontStyle.Italic
                };
            }

            if (_badgeStyle == null)
            {
                _badgeStyle = new GUIStyle(EditorStyles.miniBoldLabel)
                {
                    alignment = TextAnchor.MiddleCenter,
                    padding = new RectOffset(6, 6, 2, 2)
                };
                _badgeStyle.normal.textColor = Color.white;
            }

            if (_highlightStackStyle == null)
            {
                _highlightTexture = CreateSolidTexture(new Color(0.2f, 0.55f, 0.95f, 0.18f));
                _highlightStackStyle = new GUIStyle(EditorStyles.helpBox);
                _highlightStackStyle.normal.background = _highlightTexture;
            }

            if (_searchField == null)
            {
                _searchField = new SearchField();
            }
        }

        private Texture2D CreateSolidTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static void EnsureSingleInitialState(SerializedProperty statesProperty, int selectedIndex)
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
    }
}
#endif
