namespace WallstopStudios.DxState.Editor.State
{
    using System.Collections.Generic;
    using DxState.State.Stack.Diagnostics;
    using UnityEditor;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;

    [CustomEditor(typeof(StateStackManager))]
    public sealed class StateStackManagerEditor : UnityEditor.Editor
    {
        private bool _showStack = true;
        private bool _showRegisteredStates;
        private bool _showDiagnostics;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            StateStackManager manager = (StateStackManager)target;
            if (manager == null)
            {
                return;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Runtime Snapshot", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Is Transitioning", manager.IsTransitioning);
                EditorGUILayout.ObjectField(
                    "Current State",
                    manager.CurrentState as Object,
                    typeof(Object),
                    true
                );
                EditorGUILayout.ObjectField(
                    "Previous State",
                    manager.PreviousState as Object,
                    typeof(Object),
                    true
                );
                EditorGUILayout.FloatField("Progress", manager.Progress);
            }

            IState current = manager.CurrentState;
            if (current != null)
            {
                EditorGUILayout.LabelField(
                    "Active State",
                    $"{current.Name} ({current.GetType().Name})"
                );
            }

            IReadOnlyList<IState> stack = manager.Stack;
            _showStack = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showStack,
                $"Stack ({stack.Count})"
            );
            if (_showStack)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    for (int i = stack.Count - 1; i >= 0; i--)
                    {
                        IState state = stack[i];
                        string displayName = state != null ? state.Name : "<null>";
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                $"[{stack.Count - 1 - i}] {displayName}",
                                state != null ? state.GetType().Name : string.Empty
                            );

                            if (Application.isPlaying && state != null)
                            {
                                using (new EditorGUI.DisabledScope(manager.IsTransitioning))
                                {
                                    if (i == stack.Count - 1)
                                    {
                                        if (GUILayout.Button("Pop", GUILayout.Width(60f)))
                                        {
                                            _ = manager.PopAsync();
                                        }
                                    }
                                    else
                                    {
                                        if (GUILayout.Button("Flatten", GUILayout.Width(70f)))
                                        {
                                            _ = manager.FlattenAsync(state);
                                        }
                                        if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                                        {
                                            _ = manager.RemoveAsync(state);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            IReadOnlyDictionary<string, IState> registry = manager.RegisteredStates;
            _showRegisteredStates = EditorGUILayout.BeginFoldoutHeaderGroup(
                _showRegisteredStates,
                $"Registered States ({registry.Count})"
            );
            if (_showRegisteredStates)
            {
                using (new EditorGUI.IndentLevelScope())
                {
                    foreach (KeyValuePair<string, IState> entry in registry)
                    {
                        IState state = entry.Value;
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                entry.Key,
                                state != null ? state.GetType().Name : "<null>"
                            );

                            if (Application.isPlaying && state != null)
                            {
                                using (new EditorGUI.DisabledScope(manager.IsTransitioning))
                                {
                                    if (GUILayout.Button("Push", GUILayout.Width(60f)))
                                    {
                                        _ = manager.PushAsync(state);
                                    }
                                    if (GUILayout.Button("Flatten", GUILayout.Width(70f)))
                                    {
                                        _ = manager.FlattenAsync(state);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            EditorGUILayout.EndFoldoutHeaderGroup();

            StateStackDiagnostics diagnostics = manager.Diagnostics;
            if (diagnostics != null)
            {
                _showDiagnostics = EditorGUILayout.BeginFoldoutHeaderGroup(
                    _showDiagnostics,
                    $"Diagnostics ({diagnostics.Events.Count})"
                );
                if (_showDiagnostics)
                {
                    using (new EditorGUI.IndentLevelScope())
                    {
                        EditorGUILayout.LabelField(
                            "Logging Enabled",
                            diagnostics.LoggingEnabled ? "Yes" : "No"
                        );
                        foreach (StateStackDiagnosticEvent diagnosticEvent in diagnostics.Events)
                        {
                            EditorGUILayout.LabelField(
                                diagnosticEvent.TimestampUtc.ToString("HH:mm:ss"),
                                $"{diagnosticEvent.EventType} » {diagnosticEvent.CurrentState}"
                            );
                        }
                    }
                }
                EditorGUILayout.EndFoldoutHeaderGroup();
            }

            if (Application.isPlaying)
            {
                EditorGUILayout.Space();
                if (GUILayout.Button("Clear Stack"))
                {
                    _ = manager.ClearAsync();
                }
            }
        }
    }
}
