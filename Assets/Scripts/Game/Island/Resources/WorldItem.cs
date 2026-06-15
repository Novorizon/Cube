namespace Game
{
    public sealed class WorldItem
    {
        public int ItemId { get; }
        public int Count { get; private set; }

        public WorldItem(int itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public void SetCount(int count)
        {
            Count = count > 0 ? count : 0;
        }
    }
}
