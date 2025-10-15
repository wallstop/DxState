namespace WallstopStudios.DxState.State.Stack.Components
{
    using System.Collections.Generic;
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

        private StateStackManager _stateStackManager;
        private bool _isVisible;
        private Rect _windowRect = new Rect(16f, 16f, 360f, 240f);

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
            IReadOnlyList<IState> stackSnapshot = _stateStackManager.Stack;
            GUILayout.Label($"Stack Depth: {stackSnapshot.Count}");
            GUILayout.Label($"Current State: {FormatStateName(_stateStackManager.CurrentState)}");
            GUILayout.Space(4f);

            GUILayout.Label("Active Stack:");
            for (int i = stackSnapshot.Count - 1; i >= 0; i--)
            {
                IState state = stackSnapshot[i];
                GUILayout.Label($"  • {FormatStateName(state)}");
            }

            GUILayout.Space(4f);
            GUILayout.Label("Recent Events:");

            IReadOnlyList<StateStackDiagnosticEvent> events = _stateStackManager.Diagnostics.Events;
            int eventsToShow = Mathf.Min(_eventsToDisplay, events.Count);
            for (int i = events.Count - 1; i >= events.Count - eventsToShow; i--)
            {
                StateStackDiagnosticEvent entry = events[i];
                GUILayout.Label(
                    $"  [{entry.TimestampUtc.ToLocalTime():HH:mm:ss.fff}] {entry.EventType} (prev: {entry.PreviousState}, current: {entry.CurrentState})"
                );
            }

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private static string FormatStateName(IState state)
        {
            return state != null ? state.Name : "<none>";
        }
    }
}
