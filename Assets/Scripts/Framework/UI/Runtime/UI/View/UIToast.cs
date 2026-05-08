using System;
using System.Threading;
using System.Threading.Tasks;

namespace UI
{
    public abstract class UIToast : UIView
    {
        TaskCompletionSource<bool> completion;

        public virtual float Duration => 2.0f;

        internal async Task WaitForCompleteAsync(CancellationToken cancellationToken)
        {
            completion = new TaskCompletionSource<bool>();

            using CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, OpenCancellationToken);

            Task completeTask = completion.Task;
            Task delayTask = Duration > 0f ? Task.Delay(TimeSpan.FromSeconds(Duration), linked.Token) : Task.Delay(-1, linked.Token);
            Task finishedTask = await Task.WhenAny(completeTask, delayTask);

            if (finishedTask == delayTask && !linked.IsCancellationRequested)
            {
                Complete();
            }
        }

        protected void Complete()
        {
            completion?.TrySetResult(true);
        }

        protected override void OnClose()
        {
            completion?.TrySetCanceled();
            completion = null;
        }
    }
}
