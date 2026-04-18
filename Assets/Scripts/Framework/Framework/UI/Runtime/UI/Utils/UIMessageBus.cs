using System;
using System.Collections.Generic;

namespace UI
{
    public sealed class UIMessageBus
    {
        readonly Dictionary<Type, List<Delegate>> handlers = new();

        public void Subscribe<T>(Action<T> handler)
        {
            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out List<Delegate>? list))
            {
                list = new List<Delegate>();
                handlers.Add(type, list);
            }

            if (!list.Contains(handler))
            {
                list.Add(handler);
            }
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out List<Delegate>? list))
            {
                return;
            }

            list.Remove(handler);
        }

        public void Publish<T>(T message)
        {
            Type type = typeof(T);
            if (!handlers.TryGetValue(type, out List<Delegate>? list))
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
    }
}
