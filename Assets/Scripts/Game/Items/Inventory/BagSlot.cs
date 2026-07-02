namespace Game
{
    public sealed class BagSlot
    {
        public BagSlot(int slotIndex, int itemId)
        {
            SlotIndex = slotIndex;
            ItemId = itemId;
        }

        public int SlotIndex { get; }
        public int ItemId { get; private set; }
        public bool IsEmpty => ItemId <= 0;

        public void SetItem(int itemId)
        {
            ItemId = itemId > 0 ? itemId : 0;
        }

        public void Clear()
        {
            ItemId = 0;
        }
    }
}
