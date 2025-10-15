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

    public sealed class NetworkDisconnectStateTests
    {
        [UnityTest]
        public IEnumerator DisconnectsSuccessfully()
        {
            DisconnectConnector connector = new DisconnectConnector
            {
                IsConnected = true,
            };
            NetworkDisconnectState state = new NetworkDisconnectState(
                "Disconnect",
                connector,
                TimeSpan.FromSeconds(5)
            );

            System.Threading.Tasks.ValueTask exitTask = state.Exit(
                null,
                new ProgressCollector(),
                StateDirection.Forward
            );

            Assert.IsFalse(exitTask.IsCompleted);
            connector.Complete();

            yield return ValueTaskTestHelpers.WaitForValueTask(exitTask);
            Assert.IsFalse(connector.IsConnected);
        }

        [UnityTest]
        public IEnumerator TimeoutTriggersException()
        {
            DisconnectConnector connector = new DisconnectConnector
            {
                IsConnected = true,
            };
            NetworkDisconnectState state = new NetworkDisconnectState(
                "Disconnect",
                connector,
                TimeSpan.FromMilliseconds(50)
            );

            System.Threading.Tasks.ValueTask exitTask = state.Exit(
                null,
                new ProgressCollector(),
                StateDirection.Forward
            );

            yield return ValueTaskTestHelpers.ExpectFaulted(
                exitTask,
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

        private sealed class DisconnectConnector : INetworkDisconnector
        {
            private readonly TaskCompletionSource<bool> _completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            public bool IsConnected { get; set; }

            public ValueTask DisconnectAsync(IProgress<float> progress, CancellationToken cancellationToken)
            {
                cancellationToken.Register(() => _completion.TrySetCanceled(cancellationToken));
                return new ValueTask(WaitAsync(progress));
            }

            public void Complete()
            {
                IsConnected = false;
                _completion.TrySetResult(true);
            }

            private async Task WaitAsync(IProgress<float> progress)
            {
                await _completion.Task;
                progress?.Report(1f);
            }
        }
    }
}
