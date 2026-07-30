namespace Game.Framework
{
    /// <summary>
    /// Lightweight playback handle. One-shot callers may ignore it; looping or
    /// pending asynchronous playback can be stopped through the handle.
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

        public bool IsValid => Owner != null && Owner.IsHandleActive(this);

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
