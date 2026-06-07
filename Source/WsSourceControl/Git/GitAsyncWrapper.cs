using System;
using System.Collections.Concurrent;
using System.Threading;
using FlaxEditor;
using FlaxEngine;

namespace WsSourceControl.Git
{
    public sealed class GitOperationQueue : IDisposable
    {
        private readonly ConcurrentQueue<Action> _mainThreadCallbacks = new ConcurrentQueue<Action>();
        private readonly Action<string, bool> _onStatusChanged;
        private readonly Action<GitOperationResult> _onError;
        private CancellationTokenSource _currentCancellation;
        private int _isBusy;

        public bool IsBusy => Interlocked.CompareExchange(ref _isBusy, 0, 0) != 0;

        public GitOperationQueue(Action<string, bool> onStatusChanged, Action<GitOperationResult> onError)
        {
            _onStatusChanged = onStatusChanged;
            _onError = onError;
            Editor.Instance.EditorUpdate += ProcessCallbacks;
        }

        public void Dispose()
        {
            Cancel();
            Editor.Instance.EditorUpdate -= ProcessCallbacks;
        }

        public bool Enqueue(Func<CancellationToken, GitOperationResult> operation, Action<GitOperationResult> onComplete, string statusText)
        {
            if (Interlocked.CompareExchange(ref _isBusy, 1, 0) != 0)
                return false;

            _currentCancellation = new CancellationTokenSource();
            _onStatusChanged?.Invoke(statusText, true);
            var token = _currentCancellation.Token;

            var thread = new Thread(() =>
            {
                GitOperationResult result;
                try
                {
                    result = token.IsCancellationRequested
                        ? GitOperationResult.Fail("Operation cancelled.")
                        : operation(token);
                }
                catch (Exception ex)
                {
                    result = GitOperationResult.Fail("Git operation failed.", ex);
                }

                _mainThreadCallbacks.Enqueue(() =>
                {
                    _currentCancellation?.Dispose();
                    _currentCancellation = null;
                    Interlocked.Exchange(ref _isBusy, 0);
                    _onStatusChanged?.Invoke("Ready", false);
                    if (!result.Success)
                        _onError?.Invoke(result);
                    onComplete?.Invoke(result);
                });
            })
            {
                IsBackground = true,
                Name = "WsSourceControl Git Operation"
            };
            thread.Start();
            return true;
        }

        public void Cancel()
        {
            _currentCancellation?.Cancel();
        }

        private void ProcessCallbacks()
        {
            while (_mainThreadCallbacks.TryDequeue(out var callback))
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Git operation callback error: {ex.Message}");
                }
            }
        }
    }

    public sealed class GitAsyncWrapper : IDisposable
    {
        private readonly GitOperationQueue _queue;

        public bool IsBusy => _queue.IsBusy;

        public GitAsyncWrapper(Action<string> onStatusChanged, Action<string> onError)
        {
            _queue = new GitOperationQueue(
                (text, busy) => onStatusChanged?.Invoke(busy ? text : "Ready"),
                result => onError?.Invoke(result.Error));
        }

        public void Dispose()
        {
            _queue.Dispose();
        }

        public void RunAsync(Func<GitResult> operation, Action<GitResult> onComplete, string statusText = "Processing")
        {
            _queue.Enqueue(_ =>
            {
                try
                {
                    var result = operation();
                    return result.Success ? GitOperationResult.Ok(result.Output) : GitOperationResult.Fail(result.Error);
                }
                catch (Exception ex)
                {
                    return GitOperationResult.Fail("Git operation failed.", ex);
                }
            }, result => onComplete?.Invoke(GitResult.FromOperation(result)), statusText);
        }

        public void RunAsync(Action action, Action onComplete, string statusText = "Processing")
        {
            _queue.Enqueue(_ =>
            {
                action();
                return GitOperationResult.Ok();
            }, _ => onComplete?.Invoke(), statusText);
        }
    }
}
