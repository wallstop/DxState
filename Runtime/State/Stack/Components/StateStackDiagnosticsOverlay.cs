namespace WallstopStudios.DxState.State.Stack.Components
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using UnityEditor;
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

        [SerializeField]
        private bool _showTransitionStart = true;

        [SerializeField]
        private bool _showTransitionComplete = true;

        [SerializeField]
        private bool _showStatePushed = true;

        [SerializeField]
        private bool _showStatePopped = true;

        [SerializeField]
        private OverlayThemePreset _themePreset = OverlayThemePreset.Dark;

        [SerializeField]
        private Color _customWindowColor = new Color(0.10f, 0.10f, 0.10f, 0.94f);

        [SerializeField]
        private Color _customPanelColor = new Color(0.16f, 0.16f, 0.16f, 0.94f);

        [SerializeField]
        private Color _customTextColor = new Color(0.92f, 0.94f, 0.96f, 1f);

        [SerializeField]
        private Color _customAccentColor = new Color(0.38f, 0.68f, 1f, 1f);

        [SerializeField]
        [Range(0.6f, 2.5f)]
        private float _fontScale = 1f;

        private enum OverlayTab
        {
            Stack = 0,
            Events = 1,
            Progress = 2,
            Metrics = 3,
            Timeline = 4,
        }

        private enum OverlayThemePreset
        {
            Dark = 0,
            Light = 1,
            HighContrast = 2,
            Custom = 3,
        }

        [Serializable]
        private struct OverlayThemePalette
        {
            public Color WindowColor;
            public Color PanelColor;
            public Color TextColor;
            public Color AccentColor;

            public static OverlayThemePalette Dark => new OverlayThemePalette
            {
                WindowColor = new Color(0.10f, 0.10f, 0.10f, 0.94f),
                PanelColor = new Color(0.16f, 0.16f, 0.16f, 0.94f),
                TextColor = new Color(0.92f, 0.94f, 0.96f, 1f),
                AccentColor = new Color(0.38f, 0.68f, 1f, 1f),
            };

            public static OverlayThemePalette Light => new OverlayThemePalette
            {
                WindowColor = new Color(0.94f, 0.94f, 0.96f, 0.94f),
                PanelColor = new Color(0.97f, 0.97f, 0.99f, 0.94f),
                TextColor = new Color(0.12f, 0.12f, 0.14f, 1f),
                AccentColor = new Color(0.12f, 0.45f, 0.90f, 1f),
            };

            public static OverlayThemePalette HighContrast => new OverlayThemePalette
            {
                WindowColor = new Color(0f, 0f, 0f, 0.96f),
                PanelColor = new Color(0.08f, 0.08f, 0.08f, 0.96f),
                TextColor = new Color(1f, 1f, 1f, 1f),
                AccentColor = new Color(1f, 0.85f, 0f, 1f),
            };

            public static OverlayThemePalette Create(
                Color windowColor,
                Color panelColor,
                Color textColor,
                Color accentColor
            )
            {
                OverlayThemePalette palette = new OverlayThemePalette();
                palette.WindowColor = windowColor;
                palette.PanelColor = panelColor;
                palette.TextColor = textColor;
                palette.AccentColor = accentColor;
                return palette;
            }
        }

        private static readonly GUIContent[] TabLabels =
        {
            new GUIContent("Stack"),
            new GUIContent("Events"),
            new GUIContent("Progress"),
            new GUIContent("Metrics"),
            new GUIContent("Timeline"),
        };

        private StateStackManager _stateStackManager;
        private bool _isVisible;
        private Rect _windowRect = new Rect(16f, 16f, 360f, 260f);
        private OverlayTab _selectedTab;
        private Vector2 _scrollPosition;
        private OverlayLayout _appliedLayout;
        private bool _isPaused;
        private readonly List<StateStackDiagnosticEvent> _pausedEvents =
            new List<StateStackDiagnosticEvent>();
        private readonly List<IState> _pausedStack = new List<IState>();
        private readonly Dictionary<string, float> _pausedProgress =
            new Dictionary<string, float>(StringComparer.Ordinal);
        private StateStackMetricSnapshot _pausedMetrics;
        private int _pausedEventCursor;
        private readonly List<StateStackDiagnosticEvent> _timelineBuffer =
            new List<StateStackDiagnosticEvent>();
        private readonly HashSet<string> _pinnedStates =
            new HashSet<string>(StringComparer.Ordinal);
        private OverlayThemePalette _activeTheme;
        private OverlayThemePalette _appliedTheme;
        private float _appliedFontScale = -1f;
        private bool _themeInitialized;
        private Color _originalGuiColor;
        private Color _originalBackgroundColor;
        private Color _originalContentColor;
        private Texture2D _windowTexture;
        private Texture2D _panelTexture;
        private Texture2D _accentTexture;
        private GUIStyle _windowStyle;
        private GUIStyle _boxStyle;
        private GUIStyle _labelStyle;
        private GUIStyle _boldLabelStyle;
        private GUIStyle _miniLabelStyle;
        private GUIStyle _buttonStyle;
        private GUIStyle _toggleStyle;
        private GUIStyle _toolbarStyle;
        private GUIStyle _centeredLabelStyle;

        public void Configure(KeyCode toggleKey, bool startVisible, int eventsToDisplay)
        {
            _toggleKey = toggleKey;
            _startVisible = startVisible;
            _eventsToDisplay = Mathf.Max(1, eventsToDisplay);
            _isVisible = startVisible;
        }

        private void Reset()
        {
            OverlayThemePalette palette = OverlayThemePalette.Dark;
            _customWindowColor = palette.WindowColor;
            _customPanelColor = palette.PanelColor;
            _customTextColor = palette.TextColor;
            _customAccentColor = palette.AccentColor;
            _fontScale = 1f;
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

            EnsureThemeResources();
            ApplyGuiTheme();

            _windowRect = GUILayout.Window(
                GetInstanceID(),
                _windowRect,
                DrawWindowContents,
                "DxState Diagnostics",
                _windowStyle
            );
            RestoreGuiTheme();
        }

        private void EnsureThemeResources()
        {
            OverlayThemePalette theme = ResolveThemePalette();
            bool themeChanged =
                !_themeInitialized
                || _appliedTheme.WindowColor != theme.WindowColor
                || _appliedTheme.PanelColor != theme.PanelColor
                || _appliedTheme.TextColor != theme.TextColor
                || _appliedTheme.AccentColor != theme.AccentColor
                || !Mathf.Approximately(_appliedFontScale, _fontScale);

            if (!themeChanged)
            {
                _activeTheme = theme;
                return;
            }

            DisposeThemeTextures();

            _windowTexture = CreateSolidColorTexture(theme.WindowColor);
            _panelTexture = CreateSolidColorTexture(theme.PanelColor);
            _accentTexture = CreateSolidColorTexture(theme.AccentColor);

            BuildStyles(theme);

            _appliedTheme = theme;
            _activeTheme = theme;
            _appliedFontScale = _fontScale;
            _themeInitialized = true;
        }

        private OverlayThemePalette ResolveThemePalette()
        {
            switch (_themePreset)
            {
                case OverlayThemePreset.Dark:
                    return OverlayThemePalette.Dark;
                case OverlayThemePreset.Light:
                    return OverlayThemePalette.Light;
                case OverlayThemePreset.HighContrast:
                    return OverlayThemePalette.HighContrast;
                case OverlayThemePreset.Custom:
                default:
                    return OverlayThemePalette.Create(
                        _customWindowColor,
                        _customPanelColor,
                        _customTextColor,
                        _customAccentColor
                    );
            }
        }

        private void ApplyGuiTheme()
        {
            _originalGuiColor = GUI.color;
            _originalBackgroundColor = GUI.backgroundColor;
            _originalContentColor = GUI.contentColor;

            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            GUI.contentColor = _activeTheme.TextColor;
        }

        private void RestoreGuiTheme()
        {
            GUI.color = _originalGuiColor;
            GUI.backgroundColor = _originalBackgroundColor;
            GUI.contentColor = _originalContentColor;
        }

        private void BuildStyles(OverlayThemePalette theme)
        {
            GUISkin skin = GUI.skin;

            _windowStyle = new GUIStyle(skin.window);
            _windowStyle.normal.background = _windowTexture;
            _windowStyle.onNormal.background = _windowTexture;
            _windowStyle.active.background = _windowTexture;
            _windowStyle.onActive.background = _windowTexture;

            _boxStyle = new GUIStyle(skin.box);
            _boxStyle.normal.background = _panelTexture;
            _boxStyle.onNormal.background = _panelTexture;
            SetTextColor(_boxStyle, theme.TextColor);
            _boxStyle.fontSize = ResolveFontSize(_boxStyle.fontSize, 1f);

            _labelStyle = CreateLabelStyle(skin.label, theme.TextColor, FontStyle.Normal, 1f);
            _boldLabelStyle = CreateLabelStyle(skin.label, theme.TextColor, FontStyle.Bold, 1f);
            _miniLabelStyle = CreateLabelStyle(skin.label, theme.TextColor, FontStyle.Normal, 0.85f);

            _buttonStyle = CreateButtonStyle(skin.button, theme);
            _toggleStyle = CreateToggleStyle(skin.toggle, theme);
            _toolbarStyle = CreateToolbarStyle(skin.button, theme);

            _centeredLabelStyle = new GUIStyle(_labelStyle);
            _centeredLabelStyle.alignment = TextAnchor.MiddleCenter;
        }

        private GUIStyle CreateLabelStyle(
            GUIStyle source,
            Color textColor,
            FontStyle fontStyle,
            float scaleMultiplier
        )
        {
            GUIStyle style = new GUIStyle(source);
            style.fontStyle = fontStyle;
            SetTextColor(style, textColor);
            style.wordWrap = true;
            style.fontSize = ResolveFontSize(style.fontSize, scaleMultiplier);
            return style;
        }

        private GUIStyle CreateButtonStyle(GUIStyle source, OverlayThemePalette theme)
        {
            GUIStyle style = new GUIStyle(source);
            style.fontSize = ResolveFontSize(style.fontSize, 1f);
            SetTextColor(style, theme.TextColor);
            style.normal.background = _panelTexture;
            style.hover.background = _panelTexture;
            style.active.background = _accentTexture;
            style.focused.background = _accentTexture;
            style.onNormal.background = _accentTexture;
            style.onHover.background = _accentTexture;
            style.onActive.background = _accentTexture;
            style.onFocused.background = _accentTexture;
            return style;
        }

        private GUIStyle CreateToggleStyle(GUIStyle source, OverlayThemePalette theme)
        {
            GUIStyle style = new GUIStyle(source);
            style.fontSize = ResolveFontSize(style.fontSize, 1f);
            SetTextColor(style, theme.TextColor);
            style.normal.background = _panelTexture;
            style.hover.background = _panelTexture;
            style.active.background = _accentTexture;
            style.focused.background = _accentTexture;
            style.onNormal.background = _accentTexture;
            style.onHover.background = _accentTexture;
            style.onActive.background = _accentTexture;
            style.onFocused.background = _accentTexture;
            return style;
        }

        private GUIStyle CreateToolbarStyle(GUIStyle source, OverlayThemePalette theme)
        {
            GUIStyle style = CreateButtonStyle(source, theme);
            style.fontStyle = FontStyle.Bold;
            float clampedScale = Mathf.Clamp(_fontScale, 0.6f, 2.5f);
            style.fixedHeight = Mathf.RoundToInt(22f * clampedScale);
            style.stretchWidth = true;
            return style;
        }

        private int ResolveFontSize(int baseSize, float scaleMultiplier)
        {
            float clampedScale = Mathf.Clamp(_fontScale, 0.6f, 2.5f);
            int sourceSize = baseSize > 0 ? baseSize : 12;
            float scaled = sourceSize * clampedScale * Mathf.Max(0.4f, scaleMultiplier);
            return Mathf.Max(8, Mathf.RoundToInt(scaled));
        }

        private static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }

        private static Texture2D CreateSolidColorTexture(Color color)
        {
            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private void DisposeThemeTextures()
        {
            if (_windowTexture != null)
            {
                Destroy(_windowTexture);
                _windowTexture = null;
            }

            if (_panelTexture != null)
            {
                Destroy(_panelTexture);
                _panelTexture = null;
            }

            if (_accentTexture != null)
            {
                Destroy(_accentTexture);
                _accentTexture = null;
            }
        }

        private void DrawWindowContents(int windowId)
        {
            DrawSummaryBar();

            OverlayTab newTab = (OverlayTab)
                GUILayout.Toolbar((int)_selectedTab, TabLabels, _toolbarStyle);
            if (newTab != _selectedTab)
            {
                _selectedTab = newTab;
                _scrollPosition = Vector2.zero;
            }

            _scrollPosition = GUILayout.BeginScrollView(
                _scrollPosition,
                GUILayout.ExpandHeight(true)
            );
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
            GUILayout.BeginHorizontal(_boxStyle);
            StateStackDiagnostics diagnostics = _stateStackManager.Diagnostics;
            GUILayout.Label(
                $"Queue: {diagnostics.TransitionQueueDepth}",
                _labelStyle,
                GUILayout.ExpandWidth(false)
            );
            GUILayout.Label(
                $"Deferred (P/L): {diagnostics.PendingDeferredTransitions}/{diagnostics.LifetimeDeferredTransitions}",
                _labelStyle,
                GUILayout.ExpandWidth(false)
            );
            float progress = Mathf.Clamp01(_stateStackManager.Progress);
            GUILayout.Label(
                $"Progress {progress:P0}",
                _labelStyle,
                GUILayout.ExpandWidth(false)
            );
            if (_pinnedStates.Count > 0)
            {
                GUILayout.Label("Pinned:", _boldLabelStyle, GUILayout.Width(50f));
                foreach (string pinned in _pinnedStates)
                {
                    float pinnedProgress = 0f;
                    diagnostics.LatestProgress.TryGetValue(pinned, out pinnedProgress);
                    GUILayout.Label(
                        $"{pinned} ({pinnedProgress:P0})",
                        _miniLabelStyle,
                        GUILayout.ExpandWidth(false)
                    );
                }
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button(_layoutMode.ToString(), _buttonStyle, GUILayout.Width(100f)))
            {
                CycleLayout();
            }
            bool newLock = GUILayout.Toggle(
                _lockWindow,
                "Lock",
                _toggleStyle,
                GUILayout.Width(60f)
            );
            if (newLock != _lockWindow)
            {
                _lockWindow = newLock;
            }
            if (
                GUILayout.Button(
                    _isPaused ? "Resume" : "Pause",
                    _buttonStyle,
                    GUILayout.Width(70f)
                )
            )
            {
                TogglePause();
            }
            bool previousEnabled = GUI.enabled;
            GUI.enabled = _isPaused;
            if (
                GUILayout.Button("Step", _buttonStyle, GUILayout.Width(60f))
                && _isPaused
            )
            {
                StepForward();
            }
            GUI.enabled = previousEnabled;
            if (GUILayout.Button("Copy", _buttonStyle, GUILayout.Width(60f)))
            {
                CopyDiagnosticsToClipboard();
            }
            GUILayout.EndHorizontal();
        }

        private void DrawStack()
        {
            IReadOnlyList<IState> stackSnapshot = GetStackSource();
            GUILayout.Label($"Stack Depth: {stackSnapshot.Count}", _labelStyle);
            GUILayout.Label(
                $"Current State: {FormatStateName(_stateStackManager.CurrentState)}",
                _labelStyle
            );
            if (_pinnedStates.Count > 0)
            {
                GUILayout.Label("Pinned States:", _boldLabelStyle);
                foreach (string pinned in _pinnedStates)
                {
                    GUILayout.Label($"• {pinned}", _miniLabelStyle);
                }
            }
            GUILayout.Space(4f);

            for (int i = stackSnapshot.Count - 1; i >= 0; i--)
            {
                IState state = stackSnapshot[i];
                string formattedName = FormatStateName(state);
                GUILayout.BeginHorizontal();
                GUILayout.Label(
                    $"[{stackSnapshot.Count - 1 - i}] {formattedName}",
                    _labelStyle,
                    GUILayout.ExpandWidth(true)
                );
                bool isPinned = !string.IsNullOrEmpty(formattedName)
                    && _pinnedStates.Contains(formattedName);
                string pinLabel = isPinned ? "★" : "☆";
                if (GUILayout.Button(pinLabel, _buttonStyle, GUILayout.Width(24f)))
                {
                    TogglePinState(formattedName, isPinned);
                }
                GUILayout.EndHorizontal();
            }
        }

        private void DrawEvents()
        {
            IReadOnlyList<StateStackDiagnosticEvent> events = GetEventSource();
            int eventsToShow = Mathf.Min(_eventsToDisplay, events.Count);
            if (eventsToShow == 0)
            {
                GUILayout.Label("No events recorded yet.", _labelStyle);
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("Events to display", _labelStyle, GUILayout.Width(140f));
            Color previousColor = GUI.color;
            GUI.color = _activeTheme.AccentColor;
            float slider = GUILayout.HorizontalSlider(_eventsToDisplay, 1, 32);
            GUI.color = previousColor;
            _eventsToDisplay = Mathf.Clamp(Mathf.RoundToInt(slider), 1, 32);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            _showTransitionStart = GUILayout.Toggle(
                _showTransitionStart,
                "Start",
                _toggleStyle,
                GUILayout.Width(70f)
            );
            _showTransitionComplete = GUILayout.Toggle(
                _showTransitionComplete,
                "Complete",
                _toggleStyle,
                GUILayout.Width(90f)
            );
            _showStatePushed = GUILayout.Toggle(
                _showStatePushed,
                "Pushed",
                _toggleStyle,
                GUILayout.Width(80f)
            );
            _showStatePopped = GUILayout.Toggle(
                _showStatePopped,
                "Popped",
                _toggleStyle,
                GUILayout.Width(80f)
            );
            GUILayout.EndHorizontal();

            int displayed = 0;
            for (int i = events.Count - 1; i >= events.Count - eventsToShow; i--)
            {
                StateStackDiagnosticEvent entry = events[i];
                if (!IsEventVisible(entry.EventType))
                {
                    continue;
                }

                Color previous = GUI.color;
                GUI.color = GetEventColor(entry.EventType);
                GUILayout.Label(
                    $"[{entry.TimestampUtc.ToLocalTime():HH:mm:ss.fff}] {entry.EventType} » {entry.CurrentState}",
                    _labelStyle
                );
                GUI.color = previous;
                displayed++;
            }

            if (displayed == 0)
            {
                GUILayout.Label("No events match current filters.", _labelStyle);
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
                    newRect = new Rect(screenWidth * 0.5f - 160f, Margin, 320f, 120f);
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
            IReadOnlyDictionary<string, float> progress = GetProgressSource();
            if (progress.Count == 0)
            {
                GUILayout.Label("No progress reported yet.", _labelStyle);
                return;
            }

            foreach (KeyValuePair<string, float> entry in progress)
            {
                DrawProgressBar(entry.Key, entry.Value);
            }
        }

        private void DrawTimeline()
        {
            IReadOnlyList<StateStackDiagnosticEvent> events = GetTimelineEvents();
            if (events.Count == 0)
            {
                GUILayout.Label("No events recorded yet.", _labelStyle);
                return;
            }

            Rect rect = GUILayoutUtility.GetRect(320f, 80f, GUILayout.ExpandWidth(true));
            if (Event.current.type == EventType.Repaint)
            {
                DrawTimelineChart(rect, events);
            }

            GUILayout.Space(4f);
            GUILayout.Label("Recent Events", _boldLabelStyle);
            int displayed = 0;
            for (int i = events.Count - 1; i >= 0 && displayed < 5; i--)
            {
                StateStackDiagnosticEvent entry = events[i];
                Color previous = GUI.color;
                GUI.color = GetEventColor(entry.EventType);
                GUILayout.Label(
                    $"[{entry.TimestampUtc:HH:mm:ss}] {entry.EventType} → {entry.CurrentState}",
                    _miniLabelStyle
                );
                GUI.color = previous;
                displayed++;
            }
        }

        private void DrawMetrics()
        {
            StateStackMetricSnapshot metrics = _stateStackManager.Diagnostics.GetMetricsSnapshot();
            GUILayout.Label($"Transitions: {metrics.TransitionCount}", _labelStyle);
            GUILayout.Label(
                $"Average Duration: {metrics.AverageTransitionDurationSeconds:0.000}s",
                _labelStyle
            );
            GUILayout.Label(
                $"Longest Duration: {metrics.LongestTransitionDurationSeconds:0.000}s",
                _labelStyle
            );
        }

        private void DrawProgressBar(string label, float value)
        {
            GUILayout.Label(label, _labelStyle);
            Rect rect = GUILayoutUtility.GetRect(200f, 18f, GUILayout.ExpandWidth(true));
            Color backgroundColor = new Color(
                _activeTheme.PanelColor.r,
                _activeTheme.PanelColor.g,
                _activeTheme.PanelColor.b,
                0.6f
            );
            EditorGUI.DrawRect(rect, backgroundColor);
            Rect fill = rect;
            fill.width *= Mathf.Clamp01(value);
            Color accentColor = new Color(
                _activeTheme.AccentColor.r,
                _activeTheme.AccentColor.g,
                _activeTheme.AccentColor.b,
                0.9f
            );
            EditorGUI.DrawRect(fill, accentColor);
            GUI.Label(rect, $"{value:P0}", _centeredLabelStyle);
        }

        private void DrawTimelineChart(
            Rect rect,
            IReadOnlyList<StateStackDiagnosticEvent> events
        )
        {
            const int maxSamples = 64;
            int sampleCount = Mathf.Min(maxSamples, events.Count);
            Color outerColor = new Color(
                _activeTheme.PanelColor.r,
                _activeTheme.PanelColor.g,
                _activeTheme.PanelColor.b,
                0.6f
            );
            EditorGUI.DrawRect(rect, outerColor);

            if (sampleCount <= 1)
            {
                return;
            }

            float padding = 6f;
            Rect inner = new Rect(
                rect.x + padding,
                rect.y + padding,
                rect.width - padding * 2f,
                rect.height - padding * 2f
            );

            Color innerColor = new Color(
                _activeTheme.WindowColor.r,
                _activeTheme.WindowColor.g,
                _activeTheme.WindowColor.b,
                0.35f
            );
            EditorGUI.DrawRect(inner, innerColor);

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
                Color barColor = GetEventColor(entry.EventType);
                EditorGUI.DrawRect(bar, barColor);
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

        private bool IsEventVisible(StateStackDiagnosticEventType eventType)
        {
            switch (eventType)
            {
                case StateStackDiagnosticEventType.TransitionStart:
                    return _showTransitionStart;
                case StateStackDiagnosticEventType.TransitionComplete:
                    return _showTransitionComplete;
                case StateStackDiagnosticEventType.StatePushed:
                    return _showStatePushed;
                case StateStackDiagnosticEventType.StatePopped:
                    return _showStatePopped;
                default:
                    return true;
            }
        }

        private IReadOnlyList<StateStackDiagnosticEvent> GetEventSource()
        {
            return _isPaused ? _pausedEvents : _stateStackManager.Diagnostics.Events;
        }

        private IReadOnlyList<StateStackDiagnosticEvent> GetTimelineEvents()
        {
            IReadOnlyList<StateStackDiagnosticEvent> source = GetEventSource();
            if (
                _showTransitionStart
                && _showTransitionComplete
                && _showStatePushed
                && _showStatePopped
            )
            {
                return source;
            }

            _timelineBuffer.Clear();
            for (int i = 0; i < source.Count; i++)
            {
                StateStackDiagnosticEvent entry = source[i];
                if (IsEventVisible(entry.EventType))
                {
                    _timelineBuffer.Add(entry);
                }
            }

            return _timelineBuffer;
        }

        private IReadOnlyList<IState> GetStackSource()
        {
            return _isPaused ? _pausedStack : _stateStackManager.Stack;
        }

        private IReadOnlyDictionary<string, float> GetProgressSource()
        {
            return _isPaused
                ? (IReadOnlyDictionary<string, float>)_pausedProgress
                : _stateStackManager.Diagnostics.LatestProgress;
        }

        private StateStackMetricSnapshot GetMetricsSource()
        {
            return _isPaused
                ? _pausedMetrics
                : _stateStackManager.Diagnostics.GetMetricsSnapshot();
        }

        private void TogglePause()
        {
            if (_isPaused)
            {
                _isPaused = false;
                return;
            }

            if (CaptureSnapshot())
            {
                _isPaused = true;
            }
        }

        private void StepForward()
        {
            if (!_isPaused)
            {
                return;
            }

            StateStackDiagnostics diagnostics = _stateStackManager.Diagnostics;
            if (diagnostics == null)
            {
                return;
            }
            IReadOnlyList<StateStackDiagnosticEvent> events = diagnostics.Events;
            if (_pausedEventCursor < events.Count)
            {
                _pausedEvents.Add(events[_pausedEventCursor]);
                _pausedEventCursor++;
                CaptureDynamicSnapshots(diagnostics);
            }
        }

        private bool CaptureSnapshot()
        {
            StateStackDiagnostics diagnostics = _stateStackManager.Diagnostics;
            if (diagnostics == null)
            {
                return false;
            }

            _pausedEvents.Clear();
            IReadOnlyList<StateStackDiagnosticEvent> events = diagnostics.Events;
            for (int i = 0; i < events.Count; i++)
            {
                _pausedEvents.Add(events[i]);
            }
            _pausedEventCursor = _pausedEvents.Count;

            CaptureDynamicSnapshots(diagnostics);
            return true;
        }

        private void CaptureDynamicSnapshots(StateStackDiagnostics diagnostics)
        {
            if (diagnostics == null)
            {
                return;
            }

            _pausedStack.Clear();
            IReadOnlyList<IState> stack = _stateStackManager.Stack;
            for (int i = 0; i < stack.Count; i++)
            {
                _pausedStack.Add(stack[i]);
            }

            _pausedProgress.Clear();
            IReadOnlyDictionary<string, float> progress = diagnostics.LatestProgress;
            foreach (KeyValuePair<string, float> entry in progress)
            {
                _pausedProgress[entry.Key] = entry.Value;
            }

            _pausedMetrics = diagnostics.GetMetricsSnapshot();
        }

        private void TogglePinState(string stateName, bool currentlyPinned)
        {
            if (string.IsNullOrEmpty(stateName) || stateName == "<none>")
            {
                return;
            }

            if (currentlyPinned)
            {
                _pinnedStates.Remove(stateName);
            }
            else
            {
                _pinnedStates.Add(stateName);
            }
        }

        private void OnDestroy()
        {
            DisposeThemeTextures();
        }

        private void CopyDiagnosticsToClipboard()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine($"DxState Diagnostics ({_stateStackManager.name})");
            builder.AppendLine($"Current: {FormatStateName(_stateStackManager.CurrentState)}");
            builder.AppendLine($"Previous: {FormatStateName(_stateStackManager.PreviousState)}");
            builder.AppendLine($"Stack Depth: {_stateStackManager.Stack.Count}");
            builder.AppendLine(
                $"Queue Depth: {_stateStackManager.Diagnostics.TransitionQueueDepth}"
            );
            builder.AppendLine(
                $"Deferred Pending/Lifetime: {_stateStackManager.Diagnostics.PendingDeferredTransitions}/{_stateStackManager.Diagnostics.LifetimeDeferredTransitions}"
            );
            builder.AppendLine($"Progress: {Mathf.Clamp01(_stateStackManager.Progress):P0}");

            IReadOnlyDictionary<string, float> progress = _stateStackManager
                .Diagnostics
                .LatestProgress;
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
