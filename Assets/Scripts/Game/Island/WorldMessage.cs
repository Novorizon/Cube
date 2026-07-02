using Game.Framework;

namespace Game
{
    public enum WorldMessageTopic
    {
        None = 0,
        ItemChanged = 1,
        BagChanged = 2,
        TechChanged = 3,
    }

    public sealed class WorldItemChangedMessage : IMessage
    {
        public int ItemId;
        public int Count;
        public int Delta;
    }

    public sealed class BagChangedMessage : IMessage
    {
        public int SlotIndex;
        public int ItemId;
        public int Count;
        public bool FullRefresh;
    }

    public sealed class TechChangedMessage : IMessage
    {
        public int TechId;
        public bool FullRefresh;
    }
}
