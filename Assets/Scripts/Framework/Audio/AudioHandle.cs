namespace Game.Framework
{
    /// <summary>
    /// 播放句柄。一次性音效可以忽略；循环播放或仍在异步加载的播放可用它停止。
    /// </summary>
    public sealed class AudioHandle
    {
        internal AudioHandle(AudioManager owner, int id)
        {
            Owner = owner;
            Id = id;
        }

        internal AudioManager Owner { get; private set; }
        internal int Id { get; }

        /// <summary>
        /// 当前请求是否仍在等待加载或正在播放。
        /// 播放结束、加载失败、被停止或声源被复用后均为 false。
        /// </summary>
        public bool IsValid => Owner != null && Owner.IsHandleActive(this);

        /// <summary>
        /// 停止这次播放。重复调用或对已失效句柄调用不会产生副作用。
        /// </summary>
        public void Stop()
        {
            Owner?.Stop(this);
        }

        internal void Invalidate()
        {
            Owner = null;
        }
    }
}
