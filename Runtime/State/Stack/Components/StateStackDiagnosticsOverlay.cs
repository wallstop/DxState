namespace WallstopStudios.DxState.State.Stack.Components
{
    using System;
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

        [SerializeField]
        private OverlayLayout _layoutMode = OverlayLayout.Floating;

        [SerializeField]
        private bool _lockWindow;

        private enum OverlayTab
        {
            Stack = 0,
            Events = 1,
            Progress = 2,
            Metrics = 3,
            Timeline = 4,
        }

        private StateStackManager _stateStackManager;
        private bool _isVisible;
        private Rect _windowRect = new Rect(16f, 16f, 360f, 260f);
        private OverlayTab _selectedTab;
        private Vector2 _scrollPosition;
        private OverlayLayout _appliedLayout;

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
            _appliedLayout = OverlayLayout.Floating;
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

            ApplyLayoutIfNecessary();

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
                new[] { "Stack", "Events", "Progress", "Metrics", "Timeline" }
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
                case OverlayTab.Timeline:
                {
                    DrawTimeline();
                    break;
                }
            }
            GUILayout.EndScrollView();

            if (!_lockWindow && _layoutMode == OverlayLayout.Floating)
            {
                GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
            }
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
            if (GUILayout.Button(_layoutMode.ToString(), GUILayout.Width(100f)))
            {
                CycleLayout();
            }
            bool newLock = GUILayout.Toggle(
                _lockWindow,
                "Lock",
                GUILayout.Width(60f)
            );
            if (newLock != _lockWindow)
            {
                _lockWindow = newLock;
            }
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

        private void ApplyLayoutIfNecessary()
        {
            if (_layoutMode == OverlayLayout.Floating)
            {
                _appliedLayout = OverlayLayout.Floating;
                return;
            }

            if (_appliedLayout == _layoutMode)
            {
                return;
            }

            const float Margin = 16f;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            Rect newRect = _windowRect;
            switch (_layoutMode)
            {
                case OverlayLayout.TopLeft:
                    newRect = new Rect(Margin, Margin, 360f, 260f);
                    break;
                case OverlayLayout.TopRight:
                    newRect = new Rect(
                        Mathf.Max(Margin, screenWidth - 360f - Margin),
                        Margin,
                        360f,
                        260f
                    );
                    break;
                case OverlayLayout.BottomLeft:
                    newRect = new Rect(
                        Margin,
                        Mathf.Max(Margin, screenHeight - 260f - Margin),
                        360f,
                        260f
                    );
                    break;
                case OverlayLayout.BottomRight:
                    newRect = new Rect(
                        Mathf.Max(Margin, screenWidth - 360f - Margin),
                        Mathf.Max(Margin, screenHeight - 260f - Margin),
                        360f,
                        260f
                    );
                    break;
                case OverlayLayout.DockedTop:
                    newRect = new Rect(0f, 0f, screenWidth, Mathf.Min(180f, screenHeight * 0.35f));
                    break;
                case OverlayLayout.DockedBottom:
                    newRect = new Rect(
                        0f,
                        Mathf.Max(0f, screenHeight - Mathf.Min(180f, screenHeight * 0.35f)),
                        screenWidth,
                        Mathf.Min(180f, screenHeight * 0.35f)
                    );
                    break;
                case OverlayLayout.CompactHud:
                    newRect = new Rect(
                        screenWidth * 0.5f - 160f,
                        Margin,
                        320f,
                        120f
                    );
                    break;
            }

            _windowRect = newRect;
            _appliedLayout = _layoutMode;
        }

        private void CycleLayout()
        {
            Array values = Enum.GetValues(typeof(OverlayLayout));
            int index = Array.IndexOf(values, _layoutMode);
            index = (index + 1) % values.Length;
            _layoutMode = (OverlayLayout)values.GetValue(index);
            _appliedLayout = OverlayLayout.Floating;
        }

        [Serializable]
        private enum OverlayLayout
        {
            Floating = 0,
            TopLeft = 1,
            TopRight = 2,
            BottomLeft = 3,
            BottomRight = 4,
            DockedTop = 5,
            DockedBottom = 6,
            CompactHud = 7,
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

        private void DrawTimeline()
        {
            IReadOnlyList<StateStackDiagnosticEvent> events =
                _stateStackManager.Diagnostics.Events;
            if (events.Count == 0)
            {
                GUILayout.Label("No events recorded yet.");
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(GUILayout.ExpandWidth(true), GUILayout.Height(80f));
            if (Event.current.type == EventType.Repaint)
            {
                DrawTimelineChart(rect, events);
            }

            GUILayout.Space(4f);
            GUILayout.Label("Recent Events", EditorStyles.boldLabel);
            int displayed = 0;
            for (int i = events.Count - 1; i >= 0 && displayed < 5; i--)
            {
                StateStackDiagnosticEvent entry = events[i];
                GUILayout.Label(
                    $"[{entry.TimestampUtc:HH:mm:ss}] {entry.EventType} → {entry.CurrentState}",
                    EditorStyles.miniLabel
                );
                displayed++;
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

        private static void DrawTimelineChart(
            Rect rect,
            IReadOnlyList<StateStackDiagnosticEvent> events
        )
        {
            const int maxSamples = 64;
            int sampleCount = Mathf.Min(maxSamples, events.Count);
            if (sampleCount <= 1)
            {
                EditorGUI.DrawRect(rect, new Color(0f, 0f, 0f, 0.3f));
                return;
            }

            float padding = 6f;
            Rect inner = new Rect(
                rect.x + padding,
                rect.y + padding,
                rect.width - padding * 2f,
                rect.height - padding * 2f
            );

            EditorGUI.DrawRect(inner, new Color(0f, 0f, 0f, 0.25f));

            int startIndex = events.Count - sampleCount;
            float step = inner.width / Mathf.Max(1, sampleCount - 1);
            float barWidth = Mathf.Max(2f, step * 0.6f);

            for (int i = 0; i < sampleCount; i++)
            {
                StateStackDiagnosticEvent entry = events[startIndex + i];
                float x = inner.x + i * step - barWidth * 0.5f;
                float intensity = Mathf.Clamp01((float)(i + 1) / sampleCount);
                float height = Mathf.Lerp(4f, inner.height, intensity);
                Rect bar = new Rect(x, inner.yMax - height, barWidth, height);
                EditorGUI.DrawRect(bar, GetEventColor(entry.EventType));
            }
        }

        private static Color GetEventColor(StateStackDiagnosticEventType eventType)
        {
            switch (eventType)
            {
                case StateStackDiagnosticEventType.TransitionStart:
                    return new Color(0.3f, 0.7f, 1f, 0.9f);
                case StateStackDiagnosticEventType.TransitionComplete:
                    return new Color(0.2f, 0.9f, 0.5f, 0.9f);
                case StateStackDiagnosticEventType.StatePushed:
                    return new Color(0.85f, 0.7f, 0.2f, 0.9f);
                case StateStackDiagnosticEventType.StatePopped:
                    return new Color(0.95f, 0.4f, 0.3f, 0.9f);
                default:
                    return new Color(0.6f, 0.6f, 0.6f, 0.9f);
            }
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
