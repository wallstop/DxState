namespace WallstopStudios.DxState.Tests.PlayMode.Runtime
{
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;
    using WallstopStudios.DxState.State.Stack.Diagnostics;
    using WallstopStudios.UnityHelpers.Core.Extension;
    using static WallstopStudios.DxState.Tests.PlayMode.Runtime.CoroutineTestUtilities;

    public sealed class StateStackLoggingRuntimeTests
    {
        [UnityTest]
        public IEnumerator LogsProgressEntriesWhenConfigured()
        {
            GameObject host = new GameObject("Logging_Profile_ProgressOnly");
            List<string> capturedLogs = new List<string>();
            Application.LogCallback handler = (condition, _, type) =>
            {
                if (type == LogType.Log)
                {
                    capturedLogs.Add(condition);
                }
            };

            Application.logMessageReceived += handler;
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackLoggingProfile profile =
                    ScriptableObject.CreateInstance<StateStackLoggingProfile>();
                profile.Configure(logTransitions: false, logProgress: true, category: "DxState");
                manager.SetLoggingProfile(profile);

                ProgressReportingState state = host.AddComponent<ProgressReportingState>();
                state.Initialize("Progressive");

                yield return WaitForFrames(1);

                yield return manager.PushAsync(state).AsCoroutine();
                yield return manager.WaitForTransitionCompletionAsync().AsCoroutine();
                yield return WaitForFrames(1);

                Assert.IsTrue(
                    ContainsEntry(capturedLogs, "Progressive"),
                    "Expected progress log entry for progressive state."
                );
                Assert.IsFalse(
                    ContainsTransitionEntry(capturedLogs, "<none>", "Progressive"),
                    "Did not expect transition logs when disabled."
                );
            }
            finally
            {
                Application.logMessageReceived -= handler;
                Object.DestroyImmediate(host);
                foreach (
                    StateStackLoggingProfile existing in Resources.FindObjectsOfTypeAll<StateStackLoggingProfile>()
                )
                {
                    ScriptableObject.DestroyImmediate(existing);
                }
            }
        }

        [UnityTest]
        public IEnumerator StopsLoggingAfterProfileCleared()
        {
            GameObject host = new GameObject("Logging_Profile_Toggle");
            List<string> capturedLogs = new List<string>();
            Application.LogCallback handler = (condition, _, type) =>
            {
                if (type == LogType.Log)
                {
                    capturedLogs.Add(condition);
                }
            };

            Application.logMessageReceived += handler;
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackLoggingProfile profile =
                    ScriptableObject.CreateInstance<StateStackLoggingProfile>();
                profile.Configure(logTransitions: true, logProgress: false, category: "DxState");
                manager.SetLoggingProfile(profile);

                TestState first = host.AddComponent<TestState>();
                first.Initialize("First");

                yield return manager.PushAsync(first).AsCoroutine();
                yield return manager.WaitForTransitionCompletionAsync().AsCoroutine();

                Assert.IsTrue(ContainsTransitionEntry(capturedLogs, "<none>", "First"));

                capturedLogs.Clear();
                manager.SetLoggingProfile(null);

                TestState second = host.AddComponent<TestState>();
                second.Initialize("Second");

                yield return manager.PushAsync(second).AsCoroutine();
                yield return manager.WaitForTransitionCompletionAsync().AsCoroutine();

                Assert.IsEmpty(capturedLogs, "Expected no logging after profile cleared.");
            }
            finally
            {
                Application.logMessageReceived -= handler;
                Object.DestroyImmediate(host);
                foreach (
                    StateStackLoggingProfile existing in Resources.FindObjectsOfTypeAll<StateStackLoggingProfile>()
                )
                {
                    ScriptableObject.DestroyImmediate(existing);
                }
            }
        }

        private static bool ContainsEntry(IEnumerable<string> logs, string stateName)
        {
            foreach (string entry in logs)
            {
                if (entry.Contains(stateName) && entry.Contains("Progress"))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsTransitionEntry(
            IEnumerable<string> logs,
            string previous,
            string current
        )
        {
            string expected = $"Transition complete: {previous} -> {current}";
            foreach (string entry in logs)
            {
                if (entry.Contains(expected))
                {
                    return true;
                }
            }

            return false;
        }

        private sealed class ProgressReportingState : GameState
        {
            public void Initialize(string stateName)
            {
                _name = stateName;
            }

            public override ValueTask Enter<TProgress>(
                IState previousState,
                TProgress progress,
                StateDirection direction
            )
            {
                progress.Report(0.25f);
                progress.Report(1f);
                return base.Enter(previousState, progress, direction);
            }
        }

        private sealed class TestState : GameState
        {
            public void Initialize(string stateName)
            {
                _name = stateName;
            }
        }
    }
}
