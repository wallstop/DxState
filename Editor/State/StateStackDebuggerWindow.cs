namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using DxState.State.Stack.Diagnostics;
    using UnityEditor;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;

    public sealed class StateStackDebuggerWindow : EditorWindow
    {
        private StateStackManager _manager;
        private string _pushStateName = string.Empty;
        private Vector2 _stackScroll;
        private Vector2 _diagnosticsScroll;

        [MenuItem("Window/Wallstop Studios/DxState/State Stack Debugger")]
        private static void Open()
        {
            StateStackDebuggerWindow window = GetWindow<StateStackDebuggerWindow>(
                "State Stack Debugger"
            );
            window.minSize = new Vector2(350f, 300f);
        }

        private void OnEnable()
        {
            EditorApplication.update += Repaint;
        }

        private void OnDisable()
        {
            EditorApplication.update -= Repaint;
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                _manager = (StateStackManager)
                    EditorGUILayout.ObjectField(
                        "Manager",
                        _manager,
                        typeof(StateStackManager),
                        true
                    );

                if (GUILayout.Button("Find", GUILayout.Width(50f)))
                {
                    _manager = FindObjectOfType<StateStackManager>();
                }
            }

            if (_manager == null)
            {
                EditorGUILayout.HelpBox(
                    "Assign a StateStackManager from the active scene or click 'Find' while in Play Mode.",
                    MessageType.Info
                );
                return;
            }

            EditorGUILayout.Space();
            DrawRuntimeSnapshot();
            EditorGUILayout.Space();
            DrawStackSection();
            EditorGUILayout.Space();
            DrawRegisteredStatesSection();
            EditorGUILayout.Space();
            DrawDiagnosticsSection();
        }

        private void DrawRuntimeSnapshot()
        {
            EditorGUILayout.LabelField("Runtime Snapshot", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.Toggle("Is Transitioning", _manager.IsTransitioning);
                EditorGUILayout.ObjectField(
                    "Current State",
                    _manager.CurrentState as UnityEngine.Object,
                    typeof(UnityEngine.Object),
                    true
                );
                EditorGUILayout.ObjectField(
                    "Previous State",
                    _manager.PreviousState as UnityEngine.Object,
                    typeof(UnityEngine.Object),
                    true
                );
                EditorGUILayout.FloatField("Progress", _manager.Progress);
            }
        }

        private void DrawStackSection()
        {
            IReadOnlyList<IState> stack = _manager.Stack;
            EditorGUILayout.LabelField($"Stack ({stack.Count})", EditorStyles.boldLabel);

            using (
                var scroll = new EditorGUILayout.ScrollViewScope(
                    _stackScroll,
                    GUILayout.Height(120f)
                )
            )
            {
                _stackScroll = scroll.scrollPosition;
                if (stack.Count == 0)
                {
                    EditorGUILayout.LabelField("<empty>");
                }
                else
                {
                    for (int i = stack.Count - 1; i >= 0; i--)
                    {
                        IState state = stack[i];
                        string title = state != null ? state.Name : "<null>";
                        using (new EditorGUILayout.HorizontalScope())
                        {
                            EditorGUILayout.LabelField(
                                $"[{stack.Count - 1 - i}] {title}",
                                state != null ? state.GetType().Name : string.Empty
                            );

                            if (Application.isPlaying && state != null)
                            {
                                using (new EditorGUI.DisabledScope(_manager.IsTransitioning))
                                {
                                    if (i == stack.Count - 1)
                                    {
                                        if (GUILayout.Button("Pop", GUILayout.Width(60f)))
                                        {
                                            _ = _manager.PopAsync();
                                        }
                                    }
                                    else
                                    {
                                        if (GUILayout.Button("Flatten", GUILayout.Width(70f)))
                                        {
                                            _ = _manager.FlattenAsync(state);
                                        }
                                        if (GUILayout.Button("Remove", GUILayout.Width(70f)))
                                        {
                                            _ = _manager.RemoveAsync(state);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            if (Application.isPlaying)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    _pushStateName = EditorGUILayout.TextField("Push by Name", _pushStateName);
                    using (
                        new EditorGUI.DisabledScope(
                            _manager.IsTransitioning || string.IsNullOrWhiteSpace(_pushStateName)
                        )
                    )
                    {
                        if (GUILayout.Button("Push", GUILayout.Width(60f)))
                        {
                            _ = _manager.PushAsync(_pushStateName);
                        }
                    }
                }

                using (new EditorGUI.DisabledScope(_manager.IsTransitioning))
                {
                    if (GUILayout.Button("Clear Stack"))
                    {
                        _ = _manager.ClearAsync();
                    }
                }
            }
        }

        private void DrawRegisteredStatesSection()
        {
            IReadOnlyDictionary<string, IState> registry = _manager.RegisteredStates;
            EditorGUILayout.LabelField(
                $"Registered States ({registry.Count})",
                EditorStyles.boldLabel
            );

            using (new EditorGUI.IndentLevelScope())
            {
                foreach (KeyValuePair<string, IState> entry in registry.OrderBy(pair => pair.Key))
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        IState state = entry.Value;
                        EditorGUILayout.LabelField(
                            entry.Key,
                            state != null ? state.GetType().Name : "<null>"
                        );

                        if (Application.isPlaying && state != null)
                        {
                            using (new EditorGUI.DisabledScope(_manager.IsTransitioning))
                            {
                                if (GUILayout.Button("Push", GUILayout.Width(60f)))
                                {
                                    _ = _manager.PushAsync(state);
                                }
                                if (GUILayout.Button("Flatten", GUILayout.Width(70f)))
                                {
                                    _ = _manager.FlattenAsync(state);
                                }
                            }
                        }
                    }
                }
            }
        }

        private void DrawDiagnosticsSection()
        {
            StateStackDiagnostics diagnostics = _manager.Diagnostics;
            if (diagnostics == null)
            {
                return;
            }

            EditorGUILayout.LabelField(
                $"Diagnostics (Last {diagnostics.Events.Count})",
                EditorStyles.boldLabel
            );

            using (
                var scroll = new EditorGUILayout.ScrollViewScope(
                    _diagnosticsScroll,
                    GUILayout.Height(150f)
                )
            )
            {
                _diagnosticsScroll = scroll.scrollPosition;
                if (diagnostics.Events.Count == 0)
                {
                    EditorGUILayout.LabelField("No events recorded yet.");
                }
                else
                {
                    foreach (StateStackDiagnosticEvent entry in diagnostics.Events)
                    {
                        EditorGUILayout.LabelField(
                            entry.TimestampUtc.ToString("HH:mm:ss"),
                            $"{entry.EventType} » {entry.CurrentState} (Depth {entry.StackDepth})"
                        );
                    }
                }
            }
        }
    }
}
