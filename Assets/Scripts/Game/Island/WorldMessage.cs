using Game.Framework;

namespace Game
{
    public enum WorldMessageTopic
    {
        None = 0,
        ItemChanged = 1,
    }

    public sealed class WorldItemChangedMessage : IMessage
    {
        public int ItemId;
        public int Count;
        public int Delta;
    }
}
