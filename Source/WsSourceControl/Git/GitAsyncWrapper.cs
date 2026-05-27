using System;
using System.Collections.Concurrent;
using System.Threading;
using FlaxEditor;
using FlaxEngine;

namespace WsSourceControl.Git
{
    public class GitAsyncWrapper
    {
        private readonly ConcurrentQueue<Action> _callbackQueue = new ConcurrentQueue<Action>();
        private int _isProcessing;
        private Action<string> _onStatusChanged;
        private Action<string> _onError;

        public bool IsBusy => Interlocked.CompareExchange(ref _isProcessing, 0, 0) != 0;

        public GitAsyncWrapper(Action<string> onStatusChanged, Action<string> onError)
        {
            _onStatusChanged = onStatusChanged;
            _onError = onError;
            Editor.Instance.EditorUpdate += ProcessCallbacks;
        }

        public void Dispose()
        {
            Editor.Instance.EditorUpdate -= ProcessCallbacks;
        }

        public void RunAsync(Func<GitResult> operation, Action<GitResult> onComplete, string statusText = "Processing...")
        {
            if (Interlocked.CompareExchange(ref _isProcessing, 1, 0) != 0)
                return;

            _onStatusChanged?.Invoke(statusText);

            var thread = new Thread(() =>
            {
                GitResult result;
                try
                {
                    result = operation();
                }
                catch (Exception ex)
                {
                    result = GitResult.Fail(ex.Message);
                }

                _callbackQueue.Enqueue(() =>
                {
                    Interlocked.Exchange(ref _isProcessing, 0);
                    _onStatusChanged?.Invoke("Ready");

                    if (!result.Success && !string.IsNullOrEmpty(result.Error))
                        _onError?.Invoke(result.Error);

                    onComplete?.Invoke(result);
                });
            })
            {
                IsBackground = true
            };
            thread.Start();
        }

        public void RunAsync(Action action, Action onComplete, string statusText = "Processing...")
        {
            RunAsync(() =>
            {
                try
                {
                    action();
                    return GitResult.Ok(string.Empty, string.Empty, 0);
                }
                catch (Exception ex)
                {
                    return GitResult.Fail(ex.Message);
                }
            }, _ => onComplete?.Invoke(), statusText);
        }

        private void ProcessCallbacks()
        {
            while (_callbackQueue.TryDequeue(out var callback))
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    FlaxEngine.Debug.LogError($"GitAsync callback error: {ex.Message}");
                }
            }
        }
    }
}
