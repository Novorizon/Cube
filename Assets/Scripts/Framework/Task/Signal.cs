///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2022-10-11
/// Description：Task扩展
///------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

public class Signal
{
    private readonly TaskCompletionSource<bool> tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync()
    {
        return tcs.Task;
    }

    public void SetTrue()
    {
        tcs.TrySetResult(true);
    }

    public async Task<bool> WaitSignalAsync(CancellationToken cancellationToken = default)
    {
        Task delayTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
        Task completed = await Task.WhenAny(delayTask, tcs.Task);

        return completed == tcs.Task;
    }
}