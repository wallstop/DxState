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

    public sealed class StateStackDiagnosticsRuntimeTests
    {
        [UnityTest]
        public IEnumerator RecordsPushPopEventsInOrder()
        {
            GameObject host = new GameObject("Diagnostics_Order");
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackDiagnostics diagnostics = manager.Diagnostics;

                TestState first = host.AddComponent<TestState>();
                first.Initialize("First");
                TestState second = host.AddComponent<TestState>();
                second.Initialize("Second");

                yield return manager.PushAsync(first).AsCoroutine();
                yield return manager.PushAsync(second).AsCoroutine();
                yield return manager.PopAsync().AsCoroutine();
                yield return manager.WaitForTransitionCompletionAsync().AsCoroutine();
                yield return WaitForFrames(1);

                IReadOnlyList<StateStackDiagnosticEvent> events = diagnostics.Events;
                int pushIndex = IndexOfEvent(events, StateStackDiagnosticEventType.StatePushed);
                int completeIndex = IndexOfEvent(
                    events,
                    StateStackDiagnosticEventType.TransitionComplete
                );
                int popIndex = IndexOfEvent(events, StateStackDiagnosticEventType.StatePopped);

                Assert.GreaterOrEqual(pushIndex, 0, "Expected push event recorded");
                Assert.GreaterOrEqual(
                    completeIndex,
                    0,
                    "Expected transition complete event recorded"
                );
                Assert.GreaterOrEqual(popIndex, 0, "Expected pop event recorded");
                Assert.Less(pushIndex, completeIndex, "Push should be logged before completion.");
                Assert.Less(pushIndex, popIndex, "Push should occur before pop.");
            }
            finally
            {
                Object.DestroyImmediate(host);
            }
        }

        [UnityTest]
        public IEnumerator EmitsConsoleLogsWhenLoggingEnabled()
        {
            GameObject host = new GameObject("Diagnostics_Logging");
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
                using StateStackDiagnostics diagnostics = new StateStackDiagnostics(
                    manager.StateStack,
                    16,
                    logEvents: true
                );

                TestState state = host.AddComponent<TestState>();
                state.Initialize("Logged");

                yield return manager.PushAsync(state).AsCoroutine();
                yield return manager.WaitForTransitionCompletionAsync().AsCoroutine();
                yield return WaitForFrames(1);

                Assert.IsTrue(
                    ContainsLogEntry(capturedLogs, "Logged"),
                    "Expected diagnostics to emit console log when logging enabled."
                );
            }
            finally
            {
                Application.logMessageReceived -= handler;
                Object.DestroyImmediate(host);
            }
        }

        private static int IndexOfEvent(
            IReadOnlyList<StateStackDiagnosticEvent> events,
            StateStackDiagnosticEventType type
        )
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].EventType == type)
                {
                    return i;
                }
            }

            return -1;
        }

        private static bool ContainsLogEntry(IEnumerable<string> logs, string stateName)
        {
            foreach (string entry in logs)
            {
                if (entry.Contains(stateName) && entry.Contains("Transition complete"))
                {
                    return true;
                }
            }

            return false;
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
