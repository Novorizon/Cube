///------------------------------------
/// Author：guanjinbiao
/// Mail：novogooglor@gmail.com
/// Date：2026-02-05
/// Description：主线程派发器
///------------------------------------
using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace Game.Framework
{
    public partial class MessageDispatcher : MonoSingleton<MessageDispatcher>
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
