namespace WallstopStudios.DxState.Extensions
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using UnityEngine;
    using WallstopStudios.DxState.State.Stack.Internal;
#if UNITY_EDITOR
    using UnityEditor;
#endif

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
                ReportProgress(progressReporter, 1f);
                return default;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return new ValueTask(Task.FromCanceled(cancellationToken));
            }

            if (operation.isDone)
            {
                ReportProgress(
                    progressReporter,
                    ResolveNormalizedProgress(operation.progress, total)
                );
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

        internal static void ReportProgress(IProgress<float> reporter, float value)
        {
            if (reporter == null)
            {
                return;
            }

#if UNITY_EDITOR
            if (!Application.isPlaying && reporter is Progress<float> progress)
            {
                if (ProgressInvoker.TryInvokeInline(progress, value))
                {
                    return;
                }
            }
#endif

            reporter.Report(value);
        }

        private static float ResolveNormalizedProgress(float progress, float total)
        {
            float safeTotal = Mathf.Approximately(total, 0f) ? 1f : total;
            return Mathf.Clamp01(progress / safeTotal);
        }

        private sealed class AsyncOperationAwaiter : IDisposable
        {
            private readonly AsyncOperation _operation;
            private readonly IProgress<float> _progressReporter;
            private readonly float _total;
            private readonly CancellationToken _cancellationToken;
            private TransitionCompletionSource _completionSource;
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

                _completionSource = TransitionCompletionSource.Rent();
                return _completionSource.AsValueTask();
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
                SignalCompletion(null);
            }

            private void OnCanceled()
            {
                if (_completed)
                {
                    return;
                }

                _completed = true;
                Cleanup();
                SignalCompletion(new OperationCanceledException(_cancellationToken));
            }

            private void Cleanup()
            {
                _operation.completed -= OnCompleted;
                AsyncOperationProgressDriver.Instance.Unregister(this);
                _cancellationRegistration.Dispose();
            }

            private void ReportProgress()
            {
                float normalized = ResolveNormalizedProgress(_operation.progress, _total);
                UnityExtensions.ReportProgress(_progressReporter, normalized);
            }

            public void Dispose()
            {
                Cleanup();
                if (_completed)
                {
                    return;
                }

                _completed = true;
                SignalCompletion(new ObjectDisposedException(nameof(AsyncOperationAwaiter)));
            }

            private void SignalCompletion(Exception exception)
            {
                TransitionCompletionSource completion = _completionSource;
                if (completion == null)
                {
                    return;
                }

                _completionSource = null;

                if (exception == null)
                {
                    completion.SetResult();
                    return;
                }

                completion.SetException(exception);
            }
        }

#if UNITY_EDITOR
        private static class ProgressInvoker
        {
            private static readonly FieldInfo HandlerField = typeof(Progress<float>).GetField(
                "_handler",
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            public static bool TryInvokeInline(Progress<float> progress, float value)
            {
                if (HandlerField == null)
                {
                    return false;
                }

                if (HandlerField.GetValue(progress) is Action<float> handler)
                {
                    handler.Invoke(value);
                    return true;
                }

                return false;
            }
        }
#endif

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
                if (Application.isPlaying)
                {
                    DontDestroyOnLoad(host);
                }
                _instance = host.AddComponent<AsyncOperationProgressDriver>();
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    EditorApplication.update += _instance.EditorUpdate;
                }
#endif
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
                TickWatchers();
            }

#if UNITY_EDITOR
            private void EditorUpdate()
            {
                if (_instance == null)
                {
                    EditorApplication.update -= EditorUpdate;
                    return;
                }

                TickWatchers();

                if (Application.isPlaying)
                {
                    EditorApplication.update -= EditorUpdate;
                }
            }
#endif

            private void TickWatchers()
            {
                for (int i = 0; i < _watchers.Count; i++)
                {
                    _watchers[i].Update();
                }
            }

            private void OnDestroy()
            {
#if UNITY_EDITOR
                EditorApplication.update -= EditorUpdate;
#endif
                _watchers.Clear();
            }
        }
    }
}
