namespace WallstopStudios.DxState.State.Stack.Components
{
    using System.Collections.Generic;
    using System.Text;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    [RequireComponent(typeof(StateStackManager))]
    public sealed class StateStackDiagnosticsOverlay : MonoBehaviour
    {
        [SerializeField]
        private KeyCode _toggleKey = KeyCode.F9;

        [SerializeField]
        private bool _startVisible;

        [SerializeField]
        [Min(1)]
        private int _eventsToDisplay = 8;

        private enum OverlayTab
        {
            Stack = 0,
            Events = 1,
            Progress = 2,
            Metrics = 3,
        }

        private StateStackManager _stateStackManager;
        private bool _isVisible;
        private Rect _windowRect = new Rect(16f, 16f, 360f, 260f);
        private OverlayTab _selectedTab;
        private Vector2 _scrollPosition;

        public void Configure(KeyCode toggleKey, bool startVisible, int eventsToDisplay)
        {
            _toggleKey = toggleKey;
            _startVisible = startVisible;
            _eventsToDisplay = Mathf.Max(1, eventsToDisplay);
            _isVisible = startVisible;
        }

        private void Awake()
        {
            _stateStackManager = GetComponent<StateStackManager>();
            _isVisible = _startVisible;
        }

        private void Update()
        {
            if (_toggleKey == KeyCode.None)
            {
                return;
            }

            if (Input.GetKeyDown(_toggleKey))
            {
                _isVisible = !_isVisible;
            }
        }

        private void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }

            if (_stateStackManager == null)
            {
                return;
            }

            if (_stateStackManager.Diagnostics == null)
            {
                return;
            }

            _windowRect = GUILayout.Window(
                GetInstanceID(),
                _windowRect,
                DrawWindowContents,
                "DxState Diagnostics"
            );
        }

        private void DrawWindowContents(int windowId)
        {
            DrawSummaryBar();

            OverlayTab newTab = (OverlayTab)GUILayout.Toolbar(
                (int)_selectedTab,
                new[] { "Stack", "Events", "Progress", "Metrics" }
            );
            if (newTab != _selectedTab)
            {
                _selectedTab = newTab;
                _scrollPosition = Vector2.zero;
            }

            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition, GUILayout.ExpandHeight(true));
            switch (_selectedTab)
            {
                case OverlayTab.Stack:
                {
                    DrawStack();
                    break;
                }
                case OverlayTab.Events:
                {
                    DrawEvents();
                    break;
                }
                case OverlayTab.Progress:
                {
                    DrawProgress();
                    break;
                }
                case OverlayTab.Metrics:
                {
                    DrawMetrics();
                    break;
                }
            }
            GUILayout.EndScrollView();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private static string FormatStateName(IState state)
        {
            return state != null ? state.Name : "<none>";
        }

        private void DrawSummaryBar()
        {
            GUILayout.BeginHorizontal(GUI.skin.box);
            StateStackDiagnostics diagnostics = _stateStackManager.Diagnostics;
            GUILayout.Label(
                $"Queue: {diagnostics.TransitionQueueDepth}",
                GUILayout.ExpandWidth(false)
            );
            GUILayout.Label(
                $"Deferred (P/L): {diagnostics.PendingDeferredTransitions}/{diagnostics.LifetimeDeferredTransitions}",
                GUILayout.ExpandWidth(false)
            );
            float progress = Mathf.Clamp01(_stateStackManager.Progress);
            GUILayout.Label(
                $"Progress {progress:P0}",
                GUILayout.ExpandWidth(false)
            );
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Copy", GUILayout.Width(60f)))
            {
                CopyDiagnosticsToClipboard();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawStack()
        {
            IReadOnlyList<IState> stackSnapshot = _stateStackManager.Stack;
            GUILayout.Label($"Stack Depth: {stackSnapshot.Count}");
            GUILayout.Label($"Current State: {FormatStateName(_stateStackManager.CurrentState)}");
            GUILayout.Space(4f);

            for (int i = stackSnapshot.Count - 1; i >= 0; i--)
            {
                IState state = stackSnapshot[i];
                GUILayout.Label($"[{stackSnapshot.Count - 1 - i}] {FormatStateName(state)}");
            }
        }

        private void DrawEvents()
        {
            IReadOnlyList<StateStackDiagnosticEvent> events = _stateStackManager.Diagnostics.Events;
            int eventsToShow = Mathf.Min(_eventsToDisplay, events.Count);
            if (eventsToShow == 0)
            {
                GUILayout.Label("No events recorded yet.");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Events to display", GUILayout.Width(140f));
            float slider = GUILayout.HorizontalSlider(_eventsToDisplay, 1, 32);
            _eventsToDisplay = Mathf.Clamp(Mathf.RoundToInt(slider), 1, 32);
            GUILayout.EndHorizontal();

            for (int i = events.Count - 1; i >= events.Count - eventsToShow; i--)
            {
                StateStackDiagnosticEvent entry = events[i];
                GUILayout.Label(
                    $"[{entry.TimestampUtc.ToLocalTime():HH:mm:ss.fff}] {entry.EventType} » {entry.CurrentState}"
                );
            }
        }

        private void DrawProgress()
        {
            IReadOnlyDictionary<string, float> progress = _stateStackManager.Diagnostics.LatestProgress;
            if (progress.Count == 0)
            {
                GUILayout.Label("No progress reported yet.");
                return;
            }

            foreach (KeyValuePair<string, float> entry in progress)
            {
                DrawProgressBar(entry.Key, entry.Value);
            }
        }

        private void DrawMetrics()
        {
            StateStackMetricSnapshot metrics = _stateStackManager.Diagnostics.GetMetricsSnapshot();
            GUILayout.Label($"Transitions: {metrics.TransitionCount}");
            GUILayout.Label($"Average Duration: {metrics.AverageTransitionDurationSeconds:0.000}s");
            GUILayout.Label($"Longest Duration: {metrics.LongestTransitionDurationSeconds:0.000}s");
        }

        private void DrawProgressBar(string label, float value)
        {
            GUILayout.Label(label);
            Rect rect = GUILayoutUtility.GetRect(200f, 18f);
            GUI.Box(rect, GUIContent.none);
            Rect fill = rect;
            fill.width *= Mathf.Clamp01(value);
            GUI.backgroundColor = new Color(0.2f, 0.6f, 0.9f);
            GUI.Box(fill, GUIContent.none);
            GUI.backgroundColor = Color.white;
            GUI.Label(rect, $"{value:P0}", new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter });
        }

        private void CopyDiagnosticsToClipboard()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine($"DxState Diagnostics ({_stateStackManager.name})");
            builder.AppendLine($"Current: {FormatStateName(_stateStackManager.CurrentState)}");
            builder.AppendLine($"Previous: {FormatStateName(_stateStackManager.PreviousState)}");
            builder.AppendLine($"Stack Depth: {_stateStackManager.Stack.Count}");
            builder.AppendLine($"Queue Depth: {_stateStackManager.Diagnostics.TransitionQueueDepth}");
            builder.AppendLine(
                $"Deferred Pending/Lifetime: {_stateStackManager.Diagnostics.PendingDeferredTransitions}/{_stateStackManager.Diagnostics.LifetimeDeferredTransitions}"
            );
            builder.AppendLine(
                $"Progress: {Mathf.Clamp01(_stateStackManager.Progress):P0}"
            );

            IReadOnlyDictionary<string, float> progress = _stateStackManager.Diagnostics.LatestProgress;
            if (progress.Count > 0)
            {
                builder.AppendLine("Progress:");
                foreach (KeyValuePair<string, float> entry in progress)
                {
                    builder.AppendLine($"  {entry.Key}: {entry.Value:P0}");
                }
            }

            IReadOnlyList<StateStackDiagnosticEvent> events = _stateStackManager.Diagnostics.Events;
            int count = Mathf.Min(events.Count, _eventsToDisplay);
            if (count > 0)
            {
                builder.AppendLine("Recent Events:");
                for (int i = events.Count - count; i < events.Count; i++)
                {
                    StateStackDiagnosticEvent entry = events[i];
                    builder.AppendLine(
                        $"  [{entry.TimestampUtc:O}] {entry.EventType} -> {entry.CurrentState} (prev: {entry.PreviousState}, depth: {entry.StackDepth})"
                    );
                }
            }

            GUIUtility.systemCopyBuffer = builder.ToString();
        }
    }
}
