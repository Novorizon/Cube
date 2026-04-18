///------------------------------------
/// Author：guanjinbiao
/// Mail：novogooglor@gmail.com
/// Date：2026-02-05
/// Description：消息系统，任意线程调用 Notify，回调会回到 Unity 主线程执行
///------------------------------------
using Game.Framework;
using System;
using System.Collections.Generic;

namespace Game.Framework
{
    public partial class Messager : Singleton<Messager>
    {
        private readonly object gate = new object();

        private readonly Dictionary<(Type topicType, ulong topicValue), List<Action>> handlers = new Dictionary<(Type, ulong), List<Action>>(64);
        public ISubscription Subscribe<TTopic>(TTopic topic, Action handler) where TTopic : Enum
        {
            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            var topicKey = (typeof(TTopic), ToUInt64(topic));

            lock (gate)
            {
                if (!handlers.TryGetValue(topicKey, out var list))
                {
                    list = new List<Action>(8);
                    handlers[topicKey] = list;
                }

                list.Add(handler);
            }

            return new Subscription(() => Unsubscribe(topic, handler));
        }

        public void Unsubscribe<TTopic>(TTopic topic, Action handler) where TTopic : Enum
        {
            if (handler == null)
            {
                return;
            }

            var topicKey = (typeof(TTopic), ToUInt64(topic));

            lock (gate)
            {
                if (!handlers.TryGetValue(topicKey, out var list))
                {
                    return;
                }

                list.Remove(handler);

                if (list.Count == 0)
                {
                    handlers.Remove(topicKey);
                }
            }
        }

        public void Notify<TTopic>(TTopic topic) where TTopic : Enum
        {
            var topicKey = (typeof(TTopic), ToUInt64(topic));

            Action[] snapshot;

            lock (gate)
            {
                if (!handlers.TryGetValue(topicKey, out var list) || list.Count == 0)
                {
                    return;
                }

                snapshot = list.ToArray();
            }


            if (UnityThread.IsMainThread)
            {
                Dispatch(snapshot);
                return;
            }

            UnityThread.Post(() => Dispatch(snapshot));
        }


        private static void Dispatch(Action[] snapshot)
        {
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    snapshot[i]();
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }


    }
}
