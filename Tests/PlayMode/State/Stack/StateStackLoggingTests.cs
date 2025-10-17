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
            try
            {
                StateStackManager manager = host.AddComponent<StateStackManager>();
                StateStackLoggingProfile profile =
                    ScriptableObject.CreateInstance<StateStackLoggingProfile>();
                typeof(StateStackManager)
                    .GetField(
                        "_loggingProfile",
                        System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.NonPublic
                    )
                    ?.SetValue(manager, profile);

                yield return null;

                TestState first = new TestState("First");
                TestState second = new TestState("Second");

                LogAssert.Expect(LogType.Log, new Regex(".*Transition complete.*<none>.*First.*"));
                LogAssert.Expect(LogType.Log, new Regex(".*Transition complete.*First.*Second.*"));

                yield return WaitForValueTask(manager.PushAsync(first));
                yield return WaitForValueTask(manager.PushAsync(second));
                yield return WaitForValueTask(manager.WaitForTransitionCompletionAsync());
                yield return null;
            }
            finally
            {
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
    }
}
