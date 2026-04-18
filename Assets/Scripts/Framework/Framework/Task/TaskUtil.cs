using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Framework
{
    public static class TaskUtil
    {
        public static async Task WaitAsync(Task task, CancellationToken ct)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (task.IsCompleted)
            {
                await task;
                return;
            }

            Task delay = Task.Delay(Timeout.Infinite, ct);
            Task finished = await Task.WhenAny(task, delay);
            if (finished == delay)
            {
                throw new OperationCanceledException(ct);
            }

            await task;
        }

        public static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken ct)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (task.IsCompleted)
            {
                return await task;
            }

            Task delay = Task.Delay(Timeout.Infinite, ct);
            Task finished = await Task.WhenAny(task, delay);
            if (finished == delay)
            {
                throw new OperationCanceledException(ct);
            }

            return await task;
        }
    }
}
