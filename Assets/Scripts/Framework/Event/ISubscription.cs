///------------------------------------
/// Author：Novorizon
/// Mail：novorizon@hotmail.com
/// Date：2023-04-11
/// Description：订阅句柄，返回ISubscription，调用 Dispose() 即可取消订阅
///------------------------------------
using System;

namespace Game.Framework.Event
{
    public interface ISubscription : IDisposable { }

    // 取消订阅封装成 IDisposable
    sealed class Subscription : ISubscription
    {
        private Action dispose;

        public Subscription(Action dispose)
        {
            this.dispose = dispose;
        }

        //幂等，dispose 置空, 重复 Dispose 不会重复执行取消逻辑
        public void Dispose()
        {
            // 避免并发/重复 Dispose 时多次执行
            Action d = dispose;
            if (d == null)
            {
                return;
            }

            dispose = null;

            d();
        }
    }
}
