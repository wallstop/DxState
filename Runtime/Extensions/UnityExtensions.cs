namespace WallstopStudios.DxState.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;

    public static class UnityExtensions
    {
        public static ValueTask AwaitWithProgress<TProgress>(
            this UnityEngine.AsyncOperation operation,
            TProgress progressReporter,
            float total = 1.0f,
            CancellationToken cancellationToken = default
        )
            where TProgress : IProgress<float>
        {
            if (operation == null)
            {
                progressReporter?.Report(1f);
                return default;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask(Task.FromCanceled(cancellationToken));
            }

            if (operation.isDone)
            {
                progressReporter?.Report(operation.progress / total);
                return default;
            }

            AsyncOperationAwaiter awaiter = new AsyncOperationAwaiter(
                operation,
                progressReporter,
                total,
                cancellationToken
            );
            return awaiter.AwaitAsync();
        }

        private sealed class AsyncOperationAwaiter : IDisposable
        {
            private readonly AsyncOperation _operation;
            private readonly IProgress<float> _progressReporter;
            private readonly float _total;
            private readonly CancellationToken _cancellationToken;
            private readonly TaskCompletionSource<bool> _completion;
            private CancellationTokenRegistration _cancellationRegistration;
            private bool _completed;

            public AsyncOperationAwaiter(
                AsyncOperation operation,
                IProgress<float> progressReporter,
                float total,
                CancellationToken cancellationToken
            )
            {
                _operation = operation;
                _progressReporter = progressReporter;
                _total = Math.Abs(total) < float.Epsilon ? 1f : total;
                _cancellationToken = cancellationToken;
                _completion = new TaskCompletionSource<bool>(
                    TaskCreationOptions.RunContinuationsAsynchronously
                );
            }

            public ValueTask AwaitAsync()
            {
                ReportProgress();

                AsyncOperationProgressDriver.Instance.Register(this);
                _operation.completed += OnCompleted;

                if (_cancellationToken.CanBeCanceled)
                {
                    _cancellationRegistration = _cancellationToken.Register(OnCanceled);
                }

                return new ValueTask(_completion.Task);
            }

            public void Update()
            {
                if (_completed)
                {
                    return;
                }

                ReportProgress();
            }

            private void OnCompleted(AsyncOperation obj)
            {
                if (_completed)
                {
                    return;
                }

                _completed = true;
                ReportProgress();
                Cleanup();
                _completion.TrySetResult(true);
            }

            private void OnCanceled()
            {
                if (_completed)
                {
                    return;
                }

                _completed = true;
                Cleanup();
                _completion.TrySetCanceled(_cancellationToken);
            }

            private void Cleanup()
            {
                _operation.completed -= OnCompleted;
                AsyncOperationProgressDriver.Instance.Unregister(this);
                _cancellationRegistration.Dispose();
            }

            private void ReportProgress()
            {
                _progressReporter?.Report(_operation.progress / _total);
            }

            public void Dispose()
            {
                Cleanup();
            }
        }

        private sealed class AsyncOperationProgressDriver : MonoBehaviour
        {
            private static AsyncOperationProgressDriver _instance;
            private readonly List<AsyncOperationAwaiter> _watchers =
                new List<AsyncOperationAwaiter>(4);

            public static AsyncOperationProgressDriver Instance
            {
                get
                {
                    if (_instance == null)
                    {
                        CreateDriver();
                    }

                    return _instance;
                }
            }

            private static void CreateDriver()
            {
                GameObject host = new GameObject("DxState.AsyncOperationDriver")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<AsyncOperationProgressDriver>();
            }

            public void Register(AsyncOperationAwaiter awaiter)
            {
                if (!_watchers.Contains(awaiter))
                {
                    _watchers.Add(awaiter);
                }
            }

            public void Unregister(AsyncOperationAwaiter awaiter)
            {
                _watchers.Remove(awaiter);
            }

            private void Update()
            {
                for (int i = 0; i < _watchers.Count; i++)
                {
                    _watchers[i].Update();
                }
            }

            private void OnDestroy()
            {
                _watchers.Clear();
            }
        }
    }
}
