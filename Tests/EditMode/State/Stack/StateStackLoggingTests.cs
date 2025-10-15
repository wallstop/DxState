namespace WallstopStudios.DxState.Tests.EditMode.State.Stack
{
    using System;
    using System.Collections.Generic;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.Components;
    using WallstopStudios.DxState.State.Stack.Diagnostics;
    using Object = UnityEngine.Object;

    public sealed class StateStackLoggingTests
    {
        [Test]
        public void LogsTransitionMessagesWhenProfileEnabled()
        {
            GameObject host = new GameObject("StateStackHost");
            ScriptableObject.CreateInstance<StateStackLoggingProfile>();
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

                manager.SendMessage("Awake", SendMessageOptions.DontRequireReceiver);

                TestState first = new TestState("First");
                TestState second = new TestState("Second");

                LogAssert.Expect(LogType.Log, new Regex(".*Transition complete.*<none>.*First.*"));
                LogAssert.Expect(LogType.Log, new Regex(".*Transition complete.*First.*Second.*"));

                Task.Run(async () =>
                    {
                        await manager.PushAsync(first);
                        await manager.PushAsync(second);
                    })
                    .GetAwaiter()
                    .GetResult();
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
