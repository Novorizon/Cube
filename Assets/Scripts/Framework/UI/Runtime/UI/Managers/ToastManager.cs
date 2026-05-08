using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace UI
{
    public sealed class ToastOptions
    {
        public string MergeKey { get; set; }
    }

    public sealed class ToastManager
    {
        readonly UIInstanceFactory factory;
        readonly Queue<(string path, object args, ToastOptions options)> queue = new Queue<(string path, object args, ToastOptions options)>();
        readonly HashSet<string> mergeKeys = new HashSet<string>();

        UIHandle current;
        bool isRunning;
        CancellationTokenSource runCancellation;

        public ToastManager(UIInstanceFactory factory)
        {
            this.factory = factory;
        }

        public void Enqueue(string prefabPath, object args = null, ToastOptions options = null)
        {
            if (!string.IsNullOrEmpty(options?.MergeKey) && mergeKeys.Contains(options.MergeKey))
            {
                return;
            }

            if (!string.IsNullOrEmpty(options?.MergeKey))
            {
                mergeKeys.Add(options.MergeKey);
            }

            queue.Enqueue((prefabPath, args, options));

            if (!isRunning)
            {
                runCancellation = new CancellationTokenSource();
                isRunning = true;
                _ = RunAsync(runCancellation.Token);
            }
        }

        public void Clear(bool closeCurrent = true)
        {
            queue.Clear();
            mergeKeys.Clear();
            runCancellation?.Cancel();

            if (closeCurrent && current.IsValid)
            {
                factory.Close(current, true, false);
                current = default;
            }

            isRunning = false;
        }

        async Task RunAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (queue.Count > 0 && !cancellationToken.IsCancellationRequested)
                {
                    var item = queue.Dequeue();
                    if (!string.IsNullOrEmpty(item.options?.MergeKey))
                    {
                        mergeKeys.Remove(item.options.MergeKey);
                    }

                    current = await factory.OpenAsync(UIKind.Toast, UILayer.Toast, item.path, item.args, true, false, null);
                    if (!current.IsValid)
                    {
                        current = default;
                        continue;
                    }

                    if (current.View is UIToast toast)
                    {
                        try
                        {
                            await toast.WaitForCompleteAsync(cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }

                    if (current.IsValid)
                    {
                        factory.Close(current, true, false);
                    }

                    current = default;
                }
            }
            finally
            {
                isRunning = false;
                runCancellation?.Dispose();
                runCancellation = null;
            }
        }
    }
}
