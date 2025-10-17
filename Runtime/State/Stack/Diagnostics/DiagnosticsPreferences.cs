namespace WallstopStudios.DxState.State.Stack.Diagnostics
{
    using UnityEngine;
#if UNITY_EDITOR
    using UnityEditor;
#endif

    /// <summary>
    /// Stores editor diagnostics preferences so tooling can persist view state across sessions.
    /// </summary>
    public sealed class DiagnosticsPreferences : ScriptableObject
    {
        internal const string ResourcePath = "WallstopStudios.DxState/DiagnosticsPreferences";
        internal const string AssetPath = "Assets/Resources/WallstopStudios.DxState/DiagnosticsPreferences.asset";

        private static DiagnosticsPreferences _instance;

        [SerializeField]
        private bool _autoExpandEvents = true;

        [SerializeField]
        private int _eventHistoryLimit = 16;

        [SerializeField]
        private float _timelineDurationSeconds = 30f;

        public static DiagnosticsPreferences Instance => _instance ??= LoadOrCreate();

        public bool AutoExpandEvents
        {
            get => _autoExpandEvents;
            set => _autoExpandEvents = value;
        }

        public int EventHistoryLimit
        {
            get => _eventHistoryLimit;
            set => _eventHistoryLimit = Mathf.Max(1, value);
        }

        public float TimelineDurationSeconds
        {
            get => _timelineDurationSeconds;
            set => _timelineDurationSeconds = Mathf.Max(1f, value);
        }

#if UNITY_EDITOR
        public void Save()
        {
            if (this == null)
            {
                return;
            }

            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
        }
#else
        public void Save() { }
#endif

        private static DiagnosticsPreferences LoadOrCreate()
        {
            DiagnosticsPreferences preferences = Resources.Load<DiagnosticsPreferences>(ResourcePath);
#if UNITY_EDITOR
            if (preferences == null)
            {
                preferences = CreateInstance<DiagnosticsPreferences>();
                EnsureAssetDirectory();
                AssetDatabase.CreateAsset(preferences, AssetPath);
                AssetDatabase.SaveAssets();
            }
#else
            if (preferences == null)
            {
                preferences = CreateInstance<DiagnosticsPreferences>();
            }
#endif
            return preferences;
        }

#if UNITY_EDITOR
        private static void EnsureAssetDirectory()
        {
            string resourcesDirectory = "Assets/Resources";
            if (!AssetDatabase.IsValidFolder(resourcesDirectory))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            string dxStateDirectory = "Assets/Resources/WallstopStudios.DxState";
            if (!AssetDatabase.IsValidFolder(dxStateDirectory))
            {
                AssetDatabase.CreateFolder("Assets/Resources", "WallstopStudios.DxState");
            }
        }
#endif
    }
}
