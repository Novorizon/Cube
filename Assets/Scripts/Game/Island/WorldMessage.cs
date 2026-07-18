using Game.Framework;

namespace Game
{
    public enum WorldMessageTopic
    {
        None = 0,
        ItemChanged = 1,
        BagChanged = 2,
        TechChanged = 3,
        QuestChanged = 4,
        QuestCompleted = 5,
        QuestAccepted = 6,
    }

    public sealed class ItemChangedMessage : IMessage
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

    public sealed class QuestChangedMessage : IMessage
    {
        public int QuestId;
        public bool FullRefresh;
    }

    public sealed class QuestCompletedMessage : IMessage
    {
        public int QuestId;
        public string QuestName;
    }

    public sealed class QuestAcceptedMessage : IMessage
    {
        public int QuestId;
        public string QuestName;
    }
}
