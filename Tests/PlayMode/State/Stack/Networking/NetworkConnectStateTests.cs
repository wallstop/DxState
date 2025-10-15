namespace WallstopStudios.DxState.Tests.Runtime.State.Stack.Networking
{
    using System;
    using System.Collections;
    using System.Threading;
    using System.Threading.Tasks;
    using NUnit.Framework;
    using UnityEngine.TestTools;
    using WallstopStudios.DxState.State.Stack;
    using WallstopStudios.DxState.State.Stack.States.Scenarios;
    using WallstopStudios.DxState.Tests.Runtime.TestSupport;

    public sealed class NetworkConnectStateTests
    {
        [UnityTest]
        public IEnumerator ConnectsSuccessfully()
        {
            TestConnector connector = new TestConnector();
            NetworkConnectState state = new NetworkConnectState(
                "Network",
                connector,
                TimeSpan.FromSeconds(5)
            );

            System.Threading.Tasks.ValueTask enterTask = state.Enter(
                null,
                new ProgressCollector(),
                StateDirection.Forward
            );

            Assert.IsFalse(enterTask.IsCompleted);
            connector.CompleteConnection();

            yield return ValueTaskTestHelpers.WaitForValueTask(enterTask);
            Assert.IsTrue(connector.IsConnected);
        }

        [UnityTest]
        public IEnumerator TimesOutWhenNoCompletion()
        {
            TestConnector connector = new TestConnector();
            NetworkConnectState state = new NetworkConnectState(
                "Network",
                connector,
                TimeSpan.FromMilliseconds(50)
            );

            System.Threading.Tasks.ValueTask enterTask = state.Enter(
                null,
                new ProgressCollector(),
                StateDirection.Forward
            );

            yield return ValueTaskTestHelpers.ExpectFaulted(
                enterTask,
                exception => Assert.IsInstanceOf<TimeoutException>(exception)
            );
        }

        private sealed class ProgressCollector : IProgress<float>
        {
            public float Value { get; private set; }

            public void Report(float value)
            {
                Value = value;
            }
        }

        private sealed class TestConnector : INetworkConnector
        {
            private readonly TaskCompletionSource<bool> _connectCompletion;

            public TestConnector()
            {
                _connectCompletion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
            }

            public bool IsConnected { get; private set; }

            public ValueTask ConnectAsync(
                IProgress<float> progress,
                CancellationToken cancellationToken
            )
            {
                cancellationToken.Register(() =>
                    _connectCompletion.TrySetCanceled(cancellationToken)
                );
                return new ValueTask(WaitAsync(progress));
            }

            public ValueTask DisconnectAsync(CancellationToken cancellationToken)
            {
                IsConnected = false;
                return default;
            }

            public void CompleteConnection()
            {
                IsConnected = true;
                _connectCompletion.TrySetResult(true);
            }

            private async Task WaitAsync(IProgress<float> progress)
            {
                await _connectCompletion.Task;
                progress?.Report(1f);
            }
        }
    }
}
