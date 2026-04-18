///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2022-10-11
/// Description：unity 线程
///------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Game.Framework
{
    public static class UnityThread
    {
        private static SynchronizationContext ctx;
        private static int mainThreadId;

        public static void Initialize()
        {
            ctx = SynchronizationContext.Current;
            mainThreadId = Thread.CurrentThread.ManagedThreadId;
        }

        public static bool IsMainThread => Thread.CurrentThread.ManagedThreadId == mainThreadId;

        public static Task SwitchToMainThread()
        {
            if (ctx == null || IsMainThread)
                return Task.CompletedTask;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ctx.Post(_ => tcs.TrySetResult(true), null);
            return tcs.Task;
        }

        public static async Task<bool> SwitchToMainThread(int ms)
        {
            if (ctx == null || IsMainThread)
                return true;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            ctx.Post(_ => tcs.TrySetResult(true), null);

            Task done = await Task.WhenAny(tcs.Task, Task.Delay(ms));
            return done == tcs.Task;
        }

        public static void Post(Action action)
        {
            if (ctx == null)
            {
                // 可能不在主线程
                action();
                return;
            }

            ctx.Post(_ => action(), null);
        }
    }
}