///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2023-04-11
/// Description：消息系统，任意线程调用 Notify，回调会回到 Unity 主线程执行
///------------------------------------
using System;
using System.Collections.Generic;

namespace Game.Framework.Event
{
    public partial class EventManager : Singleton<EventManager>
    {
        private readonly object gate = new object();

        private readonly Dictionary<(Type topicType, ulong topicValue), List<Action>> handlers = new Dictionary<(Type, ulong), List<Action>>(64);
        /// <summary>
        /// 订阅事件。如果使用匿名函数，且需要一对一取消订阅，必须使用ISubscription.Dispose
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <param name="topic"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
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

        /// <summary>
        /// 条件订阅，一对一取消订阅时，必须使用ISubscription.Dispose
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <param name="topic"></param>
        /// <param name="filter"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ISubscription Subscribe<TTopic>(TTopic topic, Func<bool> filter, Action handler) where TTopic : Enum
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Action wrapped = () =>
            {
                if (filter())
                {
                    handler();
                }
            };

            return Subscribe(topic, wrapped);
        }


        /// <summary>
        /// 一对一取消订阅
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <param name="topic"></param>
        /// <param name="handler"></param>
        public void Unsubscribe<TTopic>(TTopic topic, Action handler) where TTopic : Enum
        {
            if (handler == null)
            {
                return;
            }

            var key = (typeof(TTopic), ToUInt64(topic));

            lock (gate)
            {
                if (!handlers.TryGetValue(key, out var list))
                {
                    return;
                }

                list.Remove(handler);

                if (list.Count == 0)
                {
                    handlers.Remove(key);
                }
            }
        }

        /// <summary>
        /// 取消所有相同TTopic的订阅
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <param name="topic"></param>
        public void Unsubscribe<TTopic>(TTopic topic) where TTopic : Enum
        {
            var key = (typeof(TTopic), ToUInt64(topic));
            lock (gate)
            {
                handlers.Remove(key);
                messageHandlers.Remove(key);
            }
        }

        public void Clear()
        {
            lock (gate)
            {
                handlers.Clear();
                messageHandlers.Clear();
            }
        }

        /// <summary>
        /// 发送消息通知
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <param name="topic"></param>
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
