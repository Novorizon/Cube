///------------------------------------
/// Author：guanjinbiao
/// Mail：novogooglor@gmail.com
/// Date：2026-02-05
/// Description：订阅集合
///------------------------------------
using System;
using System.Collections.Generic;

namespace Game.Framework
{
    public sealed class Subscriber : IDisposable
    {
        readonly List<IDisposable> list = new List<IDisposable>(8);

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
