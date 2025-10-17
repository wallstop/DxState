namespace WallstopStudios.DxState.Tests.EditMode.State.Diagnostics
{
    using NUnit.Framework;
#if UNITY_EDITOR
    using UnityEditor;
#endif
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack.Diagnostics;

    public sealed class DiagnosticsPreferencesTests
    {
#if UNITY_EDITOR
        [Test]
        public void InstancePersistsChangesAcrossSaves()
        {
            bool assetExisted = AssetDatabase.LoadAssetAtPath<DiagnosticsPreferences>(DiagnosticsPreferences.AssetPath) != null;

            DiagnosticsPreferences prefs = DiagnosticsPreferences.Instance;
            bool originalAutoExpand = prefs.AutoExpandEvents;
            int originalHistory = prefs.EventHistoryLimit;
            float originalTimeline = prefs.TimelineDurationSeconds;

            prefs.AutoExpandEvents = !originalAutoExpand;
            prefs.EventHistoryLimit = originalHistory + 1;
            prefs.TimelineDurationSeconds = originalTimeline + 5f;
            prefs.Save();
            AssetDatabase.Refresh();

            DiagnosticsPreferences reloaded = AssetDatabase.LoadAssetAtPath<DiagnosticsPreferences>(DiagnosticsPreferences.AssetPath);
            Assert.IsNotNull(reloaded, "Expected diagnostics preferences asset to exist.");
            Assert.AreEqual(!originalAutoExpand, reloaded.AutoExpandEvents);
            Assert.AreEqual(originalHistory + 1, reloaded.EventHistoryLimit);
            Assert.AreEqual(originalTimeline + 5f, reloaded.TimelineDurationSeconds, 0.001f);

            // Restore original values to avoid polluting editor state.
            reloaded.AutoExpandEvents = originalAutoExpand;
            reloaded.EventHistoryLimit = originalHistory;
            reloaded.TimelineDurationSeconds = originalTimeline;
            reloaded.Save();
            AssetDatabase.Refresh();

            if (!assetExisted)
            {
                AssetDatabase.DeleteAsset(DiagnosticsPreferences.AssetPath);
                AssetDatabase.Refresh();
            }
        }
#else
        [Test]
        public void RuntimeInstanceDoesNotThrow()
        {
            DiagnosticsPreferences prefs = DiagnosticsPreferences.Instance;
            Assert.IsNotNull(prefs);
        }
#endif
    }
}
