#if UNITY_EDITOR
namespace WallstopStudios.DxState.Editor.State
{
    using System;
    using UnityEditor;
    using UnityEngine;

    [FilePath("ProjectSettings/WallstopStudios/DxStateStateMachineDiagnosticsSettings.asset", FilePathAttribute.Location.ProjectFolder)]
    public sealed class StateMachineDiagnosticsSettings : ScriptableSingleton<StateMachineDiagnosticsSettings>
    {
        private const int MinRecentEventLimit = 1;
        private const int MaxRecentEventLimit = 128;

        [SerializeField]
        private int _recentEventLimit = 5;

        [SerializeField]
        private bool _autoRefresh = true;

        public event Action SettingsChanged;

        public int RecentEventLimit
        {
            get => Mathf.Clamp(_recentEventLimit, MinRecentEventLimit, MaxRecentEventLimit);
            set
            {
                int clampedValue = Mathf.Clamp(value, MinRecentEventLimit, MaxRecentEventLimit);
                if (_recentEventLimit == clampedValue)
                {
                    return;
                }

                _recentEventLimit = clampedValue;
                SaveSettings();
            }
        }

        public bool AutoRefresh
        {
            get => _autoRefresh;
            set
            {
                if (_autoRefresh == value)
                {
                    return;
                }

                _autoRefresh = value;
                SaveSettings();
            }
        }

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            SettingsProvider provider = new SettingsProvider("Project/Wallstop Studios/DxState/Diagnostics", SettingsScope.Project)
            {
                label = "DxState Diagnostics",
                guiHandler = searchContext =>
                {
                    StateMachineDiagnosticsSettings settings = instance;
                    EditorGUI.BeginChangeCheck();
                    bool autoRefreshValue = EditorGUILayout.Toggle("Auto Refresh", settings.AutoRefresh);
                    int recentLimitValue = EditorGUILayout.IntSlider(
                        "Recent Event Limit",
                        settings.RecentEventLimit,
                        MinRecentEventLimit,
                        MaxRecentEventLimit
                    );
                    if (EditorGUI.EndChangeCheck())
                    {
                        settings.AutoRefresh = autoRefreshValue;
                        settings.RecentEventLimit = recentLimitValue;
                    }
                },
            };

            return provider;
        }

        private void SaveSettings()
        {
            Save(true);
            SettingsChanged?.Invoke();
        }
    }
}
#endif
