namespace WallstopStudios.DxState.Tests.PlayMode.State.Stack
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Text.RegularExpressions;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;
    using WallstopStudios.DxState.State.Stack.Diagnostics;
    using Object = UnityEngine.Object;

    public sealed class StateStackLoggingTests
    {
        [UnityTest]
        public IEnumerator LogsTransitionMessagesWhenProfileEnabled()
        {
            GameObject host = new GameObject("StateStackHost");
            List<string> capturedLogs = new List<string>();
            Application.LogCallback logHandler = (condition, _, type) =>
            {
                if (type == LogType.Log)
                {
                    capturedLogs.Add(condition);
                }
            };

            Application.logMessageReceived += logHandler;
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackLoggingProfile profile =
                    ScriptableObject.CreateInstance<StateStackLoggingProfile>();
                manager.SetLoggingProfile(profile);

                yield return null;

                TestState first = new TestState("First");
                TestState second = new TestState("Second");

                yield return WaitForValueTask(manager.PushAsync(first));
                yield return WaitForValueTask(manager.PushAsync(second));
                yield return WaitForValueTask(manager.WaitForTransitionCompletionAsync());
                yield return null;

                Assert.IsTrue(
                    ContainsTransitionLog(capturedLogs, "<none>", first.Name),
                    "Expected logging profile to record transition into the first state."
                );
                Assert.IsTrue(
                    ContainsTransitionLog(capturedLogs, first.Name, second.Name),
                    "Expected logging profile to record transition into the second state."
                );
            }
            finally
            {
                Application.logMessageReceived -= logHandler;

                foreach (
                    StateStackLoggingProfile existing in Resources.FindObjectsOfTypeAll<StateStackLoggingProfile>()
                )
                {
                    ScriptableObject.DestroyImmediate(existing);
                }
                Object.DestroyImmediate(host);
            }
        }

        private static IEnumerator WaitForValueTask(ValueTask valueTask)
        {
            Task awaitedTask = valueTask.AsTask();
            while (!awaitedTask.IsCompleted)
            {
                yield return null;
            }

            if (awaitedTask.IsFaulted)
            {
                Exception exception = awaitedTask.Exception;
                Exception inner = exception != null ? exception.InnerException : null;
                throw inner ?? exception;
            }

            if (awaitedTask.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }

        private sealed class TestState : IState
        {
            private readonly string _name;

            public TestState(string name)
            {
                _name = name;
            }

            public string Name => _name;

            public TickMode TickMode => TickMode.None;

            public bool TickWhenInactive => false;

            public float? TimeInState => null;

            public ValueTask Enter<TProgress>(
                IState previousState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return default;
            }

            public void Tick(TickMode mode, float delta) { }

            public ValueTask Exit<TProgress>(
                IState nextState,
                TProgress progress,
                StateDirection direction
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return default;
            }

            public ValueTask Remove<TProgress>(
                System.Collections.Generic.IReadOnlyList<IState> previousStatesInStack,
                System.Collections.Generic.IReadOnlyList<IState> nextStatesInStack,
                TProgress progress
            )
                where TProgress : IProgress<float>
            {
                progress.Report(1f);
                return default;
            }
        }

        private static bool ContainsTransitionLog(
            IReadOnlyList<string> logs,
            string previous,
            string current
        )
        {
            Regex pattern = new Regex(
                $"\\[[^\\]]+\\] Transition complete: {Regex.Escape(previous)} -> {Regex.Escape(current)}"
            );
            foreach (string entry in logs)
            {
                if (pattern.IsMatch(entry))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
