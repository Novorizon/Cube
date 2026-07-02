using UnityEngine;

namespace Game
{
    public sealed class ItemUseManager
    {
        public static ItemUseManager Instance { get; } = new ItemUseManager();

        private ItemUseManager()
        {
        }

        public bool TryUseItem(int itemId)
        {
            if (itemId <= 0 || WorldItemManager.Instance.GetCount(itemId) <= 0)
            {
                return false;
            }

            if (ToolKitDefinitions.TryGetTool(itemId, out _))
            {
                bool selected = ToolKitManager.Instance.TrySelectToolItem(itemId);
                if (!selected)
                {
                    Debug.Log($"Use tool failed. ToolKit has no free slot. itemId: {itemId}");
                }

                return selected;
            }

            if (itemId >= ItemIds.SeedMin && itemId <= ItemIds.SeedMax)
            {
                Debug.Log($"Seed selected from bag. itemId: {itemId}");
                return true;
            }

            if (DataManager.Instance.Item != null &&
                DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) &&
                config != null &&
                config.ItemType == (int)global::ItemType.Consumable)
            {
                return WorldItemManager.Instance.TryConsumeItem(itemId, 1);
            }

            Debug.Log($"Item selected. No use action configured. itemId: {itemId}");
            return true;
        }
    }
}
