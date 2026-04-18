///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2023-04-11
/// Description：主线程派发器
///------------------------------------
using Game.Framework;
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Game.Framework
{
    public partial class Dispatcher : MonoSingleton<Dispatcher>
    {
        private static readonly ConcurrentQueue<Action> queues = new ConcurrentQueue<Action>();

        public static void Post(Action action)
        {
            if (action == null) 
                return;

            queues.Enqueue(action);
        }

        private void Update()
        {
            while (queues.TryDequeue(out var action))
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
        }

    }
}
