///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2023-04-11
/// Description：消息系统
///------------------------------------
using Game.Framework;
using System;
using System.Collections.Generic;

namespace Game.Framework.Event
{
    public partial class EventManager
    {
        private readonly Dictionary<(Type topicType, ulong topicValue), Dictionary<Type, List<Delegate>>> messageHandlers = new Dictionary<(Type, ulong), Dictionary<Type, List<Delegate>>>(64);


        /// <summary>
        /// 订阅事件。如果使用匿名函数，且需要一对一取消订阅，必须使用ISubscription.Dispose
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <typeparam name="TMsg"></typeparam>
        /// <param name="topic"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ISubscription Subscribe<TTopic, TMsg>(TTopic topic, Action<TMsg> handler) where TTopic : Enum where TMsg : IMessage
        {
            if (handler == null)
            {
                return null;
            }

            var topicKey = (typeof(TTopic), ToUInt64(topic));
            var msgType = typeof(TMsg);

            lock (gate)
            {
                if (!messageHandlers.TryGetValue(topicKey, out var byMsgType))
                {
                    byMsgType = new Dictionary<Type, List<Delegate>>(16);
                    messageHandlers[topicKey] = byMsgType;
                }

                if (!byMsgType.TryGetValue(msgType, out var list))
                {
                    list = new List<Delegate>(8);
                    byMsgType[msgType] = list;
                }

                list.Add(handler);
            }

            return new Subscription(() => Unsubscribe(topicKey, msgType, handler));
        }

        /// <summary>
        /// 条件订阅，一对一取消订阅时，必须使用ISubscription.Dispose
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <typeparam name="TMsg"></typeparam>
        /// <param name="topic"></param>
        /// <param name="filter"></param>
        /// <param name="handler"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        public ISubscription Subscribe<TTopic, TMsg>(TTopic topic, Func<TMsg, bool> filter, Action<TMsg> handler) where TTopic : Enum where TMsg : IMessage
        {
            if (filter == null)
            {
                throw new ArgumentNullException(nameof(filter));
            }

            if (handler == null)
            {
                throw new ArgumentNullException(nameof(handler));
            }

            Action<TMsg> wrapped = msg =>
            {
                if (filter(msg))
                {
                    handler(msg);
                }
            };

            return Subscribe<TTopic, TMsg>(topic, wrapped);
        }


        /// <summary>
        /// 发送消息通知
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <param name="topic"></param>
        public void Notify<TTopic, TMsg>(TTopic topic, TMsg message) where TTopic : Enum where TMsg : IMessage
        {
            if (message == null)
            {
                return;
            }

            var topicKey = (typeof(TTopic), ToUInt64(topic));
            var msgType = typeof(TMsg);

            Delegate[] snapshot;

            lock (gate)
            {
                if (!messageHandlers.TryGetValue(topicKey, out var byMsgType))
                {
                    return;
                }

                if (!byMsgType.TryGetValue(msgType, out var list) || list.Count == 0)
                {
                    return;
                }

                snapshot = list.ToArray();
            }

            if (UnityThread.IsMainThread)
            {
                Dispatch(snapshot, message);
                return;
            }

            UnityThread.Post(() => Dispatch(snapshot, message));
        }


        /// <summary>
        /// 一对一取消订阅
        /// </summary>
        /// <typeparam name="TTopic"></typeparam>
        /// <param name="topic"></param>
        /// <param name="handler"></param>
        public void Unsubscribe<TTopic, TMsg>(TTopic topic, Action<TMsg> handler) where TTopic : Enum where TMsg : IMessage
        {
            if (handler == null)
            {
                return;
            }

            var topicKey = (typeof(TTopic), ToUInt64(topic));
            var msgType = typeof(TMsg);

            Unsubscribe(topicKey, msgType, handler);
        }



        private void Unsubscribe((Type topicType, ulong topicValue) topicKey, Type msgType, Delegate handler)
        {
            lock (gate)
            {
                if (!messageHandlers.TryGetValue(topicKey, out var byMsgType))
                {
                    return;
                }

                if (!byMsgType.TryGetValue(msgType, out var list))
                {
                    return;
                }

                list.Remove(handler);

                if (list.Count == 0)
                {
                    byMsgType.Remove(msgType);

                    if (byMsgType.Count == 0)
                    {
                        messageHandlers.Remove(topicKey);
                    }
                }
            }
        }

        private static void Dispatch<T>(Delegate[] snapshot, T message) where T : IMessage
        {
            for (int i = 0; i < snapshot.Length; i++)
            {
                try
                {
                    if (snapshot[i] is Action<T> action)
                    {
                        action(message);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogException(ex);
                }
            }
        }

        private static ulong ToUInt64<TTopic>(TTopic value) where TTopic : Enum
        {
            return Convert.ToUInt64(value);
        }
    }
}
