#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using System.Collections.Generic;
    using UnityEditor;
    using UnityEngine;
    using WallstopStudios.DxState.State.Machine;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public sealed class StateMachineDiagnosticsWindow : EditorWindow
    {
        private static readonly TransitionCause[] Causes =
            (TransitionCause[])Enum.GetValues(typeof(TransitionCause));

        private readonly List<StateMachineStateMetricsRecord> _metricsBuffer;
        private readonly List<StateMachineDiagnosticEventRecord> _eventsBuffer;

        private Vector2 _scrollPosition;

        public StateMachineDiagnosticsWindow()
        {
            _metricsBuffer = new List<StateMachineStateMetricsRecord>();
            _eventsBuffer = new List<StateMachineDiagnosticEventRecord>();
        }

        [MenuItem("Window/Wallstop Studios/DxState/State Machine Diagnostics")]
        private static void Open()
        {
            StateMachineDiagnosticsWindow window = GetWindow<StateMachineDiagnosticsWindow>(
                "State Machine Diagnostics"
            );
            window.minSize = new Vector2(360f, 240f);
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
            IReadOnlyList<StateMachineDiagnosticsEntry> entries =
                StateMachineDiagnosticsRegistry.GetEntries();

            if (entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No state machine diagnostics registered. Call AttachDiagnostics on your state machines at runtime to populate this view.",
                    MessageType.Info
                );
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < entries.Count; i++)
            {
                DrawEntry(entries[i]);
                if (i < entries.Count - 1)
                {
                    EditorGUILayout.Space();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(StateMachineDiagnosticsEntry entry)
        {
            object diagnostics = entry.Diagnostics;
            if (diagnostics == null)
            {
                return;
            }

            Type diagnosticsType = diagnostics.GetType();
            int transitionCount = (int)diagnosticsType
                .GetProperty(nameof(StateMachineDiagnostics<object>.TransitionCount))
                .GetValue(diagnostics);
            int deferredCount = (int)diagnosticsType
                .GetProperty(nameof(StateMachineDiagnostics<object>.DeferredTransitionCount))
                .GetValue(diagnostics);
            DateTime? lastTransitionUtc = (DateTime?)diagnosticsType
                .GetProperty(nameof(StateMachineDiagnostics<object>.LastTransitionUtc))
                .GetValue(diagnostics);

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(entry.StateType.Name, EditorStyles.boldLabel);

            if (entry.Machine is UnityEngine.Object unityOwner)
            {
                EditorGUILayout.ObjectField("Owner", unityOwner, typeof(UnityEngine.Object), true);
            }
            else if (entry.Machine != null)
            {
                EditorGUILayout.LabelField("Owner", entry.Machine.ToString());
            }
            else
            {
                EditorGUILayout.LabelField("Owner", "<unknown>");
            }

            EditorGUILayout.LabelField("Transitions", transitionCount.ToString());
            EditorGUILayout.LabelField("Deferred", deferredCount.ToString());
            EditorGUILayout.LabelField(
                "Last Transition",
                lastTransitionUtc.HasValue
                    ? lastTransitionUtc.Value.ToLocalTime().ToString("HH:mm:ss")
                    : "<never>"
            );

            MethodInfoHelper.CopyMetrics(diagnosticsType, diagnostics, _metricsBuffer);
            DrawStateMetrics();

            MethodInfoHelper.DrawCauseBreakdown(diagnosticsType, diagnostics, Causes);

            MethodInfoHelper.CopyEvents(diagnosticsType, diagnostics, _eventsBuffer, 5);
            DrawRecentEvents();

            EditorGUILayout.EndVertical();
        }

        private void DrawStateMetrics()
        {
            if (_metricsBuffer.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("State Metrics", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < _metricsBuffer.Count; i++)
                {
                    StateMachineStateMetricsRecord record = _metricsBuffer[i];
                    StateMachineStateMetrics metrics = record.Metrics;
                    string label = record.State != null ? record.State.ToString() : "<null>";
                    EditorGUILayout.LabelField(
                        label,
                        $"Enter {metrics.EnterCount} | Exit {metrics.ExitCount}"
                    );
                }
            }
        }

        private void DrawRecentEvents()
        {
            if (_eventsBuffer.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField("Recent Events", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                for (int i = 0; i < _eventsBuffer.Count; i++)
                {
                    StateMachineDiagnosticEventRecord record = _eventsBuffer[i];
                    string label =
                        $"{record.TimestampUtc.ToLocalTime():HH:mm:ss} {record.EventType} → {FormatState(record.RequestedState)}";
                    EditorGUILayout.LabelField(label, EditorStyles.miniLabel);
                }
            }
        }

        private static string FormatState(object state)
        {
            if (state == null)
            {
                return "<null>";
            }

            if (state is UnityEngine.Object unityObject)
            {
                return unityObject.name;
            }

            return state.ToString();
        }

        private static class MethodInfoHelper
        {
            public static void CopyMetrics(
                Type diagnosticsType,
                object diagnostics,
                List<StateMachineStateMetricsRecord> buffer
            )
            {
                System.Reflection.MethodInfo method = diagnosticsType.GetMethod(
                    nameof(StateMachineDiagnostics<object>.CopyStateMetrics)
                );
                method?.Invoke(diagnostics, new object[] { buffer });
            }

            public static void CopyEvents(
                Type diagnosticsType,
                object diagnostics,
                List<StateMachineDiagnosticEventRecord> buffer,
                int maxCount
            )
            {
                System.Reflection.MethodInfo method = diagnosticsType.GetMethod(
                    nameof(StateMachineDiagnostics<object>.CopyRecentEvents)
                );
                method?.Invoke(diagnostics, new object[] { buffer, maxCount });
            }

            public static void DrawCauseBreakdown(
                Type diagnosticsType,
                object diagnostics,
                TransitionCause[] causes
            )
            {
                System.Reflection.MethodInfo method = diagnosticsType.GetMethod(
                    nameof(StateMachineDiagnostics<object>.GetTransitionCauseCount)
                );
                if (method == null)
                {
                    return;
                }

                bool headerWritten = false;
                for (int i = 0; i < causes.Length; i++)
                {
                    TransitionCause cause = causes[i];
                    int count = (int)method.Invoke(diagnostics, new object[] { cause });
                    if (count <= 0)
                    {
                        continue;
                    }

                    if (!headerWritten)
                    {
                        EditorGUILayout.LabelField("Causes", EditorStyles.boldLabel);
                        EditorGUI.indentLevel++;
                        headerWritten = true;
                    }

                    EditorGUILayout.LabelField($"{cause}: {count}");
                }

                if (headerWritten)
                {
                    EditorGUI.indentLevel--;
                }
            }
        }
    }
}
#endif
