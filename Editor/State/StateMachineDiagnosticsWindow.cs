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

        private readonly List<StateMachineDiagnosticsEntry> _entries;
        private readonly List<StateMachineStateMetricsRecord> _metricsBuffer;
        private readonly List<StateMachineDiagnosticEventRecord> _eventsBuffer;

        private Vector2 _scrollPosition;
        private bool _entriesNeedRefresh;
        private bool _isSubscribedToUpdate;

        public StateMachineDiagnosticsWindow()
        {
            _entries = new List<StateMachineDiagnosticsEntry>();
            _metricsBuffer = new List<StateMachineStateMetricsRecord>();
            _eventsBuffer = new List<StateMachineDiagnosticEventRecord>();
            _entriesNeedRefresh = true;
        }

        [MenuItem("Window/Wallstop Studios/DxState/State Machine Diagnostics")]
        private static void Open()
        {
            StateMachineDiagnosticsWindow window = GetWindow<StateMachineDiagnosticsWindow>(
                "State Machine Diagnostics"
            );
            window.minSize = new Vector2(360f, 240f);
        }

        private StateMachineDiagnosticsSettings Settings => StateMachineDiagnosticsSettings.instance;

        private void OnEnable()
        {
            Settings.SettingsChanged += HandleSettingsChanged;
            _entriesNeedRefresh = true;
            UpdateSubscription();
            RefreshEntries();
        }

        private void OnDisable()
        {
            Settings.SettingsChanged -= HandleSettingsChanged;
            if (_isSubscribedToUpdate)
            {
                EditorApplication.update -= OnEditorUpdate;
                _isSubscribedToUpdate = false;
            }
        }

        private void OnGUI()
        {
            EnsureEntries();

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUILayout.LabelField(
                Settings.AutoRefresh ? "Auto Refresh: On" : "Auto Refresh: Off",
                EditorStyles.toolbarButton,
                GUILayout.Width(140f)
            );
            if (!Settings.AutoRefresh)
            {
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                {
                    RefreshEntries();
                    Repaint();
                }
            }
            if (GUILayout.Button("Settings", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            {
                SettingsService.OpenProjectSettings("Project/Wallstop Studios/DxState/Diagnostics");
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            if (_entries.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No state machine diagnostics registered. Call AttachDiagnostics on your state machines at runtime to populate this view.",
                    MessageType.Info
                );
                return;
            }

            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);
            for (int i = 0; i < _entries.Count; i++)
            {
                DrawEntry(_entries[i]);
                if (i < _entries.Count - 1)
                {
                    EditorGUILayout.Space();
                }
            }

            EditorGUILayout.EndScrollView();
        }

        private void DrawEntry(StateMachineDiagnosticsEntry entry)
        {
            IStateMachineDiagnosticsView diagnostics = entry.Diagnostics;
            int transitionCount = diagnostics.TransitionCount;
            int deferredCount = diagnostics.DeferredTransitionCount;
            DateTime? lastTransitionUtc = diagnostics.LastTransitionUtc;

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
            EditorGUILayout.LabelField(
                "Pending Queue",
                string.Format(
                    "{0} (max {1}, avg {2:F2})",
                    diagnostics.PendingTransitionQueueDepth,
                    diagnostics.MaxPendingTransitionQueueDepth,
                    diagnostics.AveragePendingTransitionQueueDepth
                )
            );

            diagnostics.CopyStateMetrics(_metricsBuffer);
            DrawStateMetrics();

            DrawCauseBreakdown(diagnostics);

            diagnostics.CopyRecentEvents(_eventsBuffer, Settings.RecentEventLimit);
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

        private void DrawCauseBreakdown(IStateMachineDiagnosticsView diagnostics)
        {
            bool headerWritten = false;
            for (int i = 0; i < Causes.Length; i++)
            {
                TransitionCause cause = Causes[i];
                int count = diagnostics.GetTransitionCauseCount(cause);
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

        private void EnsureEntries()
        {
            if (_entriesNeedRefresh)
            {
                RefreshEntries();
            }
        }

        private void RefreshEntries()
        {
            StateMachineDiagnosticsRegistry.FillEntries(_entries);
            _entriesNeedRefresh = false;
        }

        private void HandleSettingsChanged()
        {
            UpdateSubscription();
            _entriesNeedRefresh = true;
            Repaint();
        }

        private void UpdateSubscription()
        {
            bool shouldSubscribe = Settings.AutoRefresh;
            if (shouldSubscribe && !_isSubscribedToUpdate)
            {
                EditorApplication.update += OnEditorUpdate;
                _isSubscribedToUpdate = true;
            }
            else if (!shouldSubscribe && _isSubscribedToUpdate)
            {
                EditorApplication.update -= OnEditorUpdate;
                _isSubscribedToUpdate = false;
            }
        }

        private void OnEditorUpdate()
        {
            _entriesNeedRefresh = true;
            Repaint();
        }
    }
}
#endif
