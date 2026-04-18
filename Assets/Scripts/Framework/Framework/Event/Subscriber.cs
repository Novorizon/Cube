///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2023-04-11
/// Description：订阅集合
///------------------------------------
using System;
using System.Collections.Generic;

namespace Game.Framework.Event
{
    public sealed class Subscriber : IDisposable
    {
        private readonly List<IDisposable> list = new List<IDisposable>(8);

        public void Add(IDisposable sub)
        {
            if (sub != null)
            {
                list.Add(sub);
            }
        }

        public void Clear()
        {
            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    list[i].Dispose();
                }
                catch
                {
                }
            }

            list.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
