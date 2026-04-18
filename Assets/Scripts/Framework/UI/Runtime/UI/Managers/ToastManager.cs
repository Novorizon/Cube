using System.Collections.Generic;
using System.Threading.Tasks;

namespace UI
{
    public sealed class ToastOptions
    {
        public string? MergeKey { get; set; }
    }

    public sealed class ToastManager
    {
        readonly UIInstanceFactory factory;

        readonly Queue<(string path, object? args, ToastOptions? options)> queue = new();

        UIHandle current;
        bool isRunning;

        public ToastManager(UIInstanceFactory factory)
        {
            this.factory = factory;
        }

        public void Enqueue(string prefabPath, object? args = null, ToastOptions? options = null)
        {
            queue.Enqueue((prefabPath, args, options));

            if (!isRunning)
            {
                isRunning = true;
                _ = RunAsync();
            }
        }

        async Task RunAsync()
        {
            while (queue.Count > 0)
            {
                var item = queue.Dequeue();

                current = await factory.OpenAsync(UIKind.Toast, UILayer.Toast, item.path, item.args, true, true, null);

                if (current.View is UIToast toast)
                {
                    toast.Bind(this);
                }
                else
                {
                    factory.Close(current, false, true);
                    current = default;
                    continue;
                }

                while (current.IsValid)
                {
                    await Task.Yield();
                }
            }

            isRunning = false;
        }

        internal void NotifyToastCompleted(UIToast toast)
        {
            if (!current.IsValid || current.View != toast)
            {
                return;
            }

            factory.Close(current, false, true);
            current = default;
        }
    }
}
