namespace Game
{
    public sealed class ItemStack
    {
        public int ItemId { get; }
        public int Count { get; private set; }

        public ItemStack(int itemId, int count)
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
