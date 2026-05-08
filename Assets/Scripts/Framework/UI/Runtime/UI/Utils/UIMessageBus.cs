using System;
using System.Collections.Generic;

namespace UI
{
    public sealed class UIMessageBus
    {
        readonly Dictionary<Type, List<Delegate>> handlers = new Dictionary<Type, List<Delegate>>();

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out List<Delegate> list))
            {
                list = new List<Delegate>();
                handlers.Add(type, list);
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }

            return new Subscription<T>(this, handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out List<Delegate> list))
            {
                return;
            }

            list.Remove(handler);
            if (list.Count == 0)
            {
                handlers.Remove(type);
            }
        }

        public void Publish<T>(T message)
        {
            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out List<Delegate> list))
            {
                return;
            }

            Delegate[] snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] is Action<T> cb)
                {
                    cb.Invoke(message);
                }
            }
        }

        public void Clear()
        {
            handlers.Clear();
        }

        sealed class Subscription<T> : IDisposable
        {
            readonly UIMessageBus bus;
            Action<T> handler;

            public Subscription(UIMessageBus bus, Action<T> handler)
            {
                this.bus = bus;
                this.handler = handler;
            }

            public void Dispose()
            {
                if (handler == null)
                {
                    return;
                }

                bus.Unsubscribe(handler);
                handler = null;
            }
        }
    }
}
