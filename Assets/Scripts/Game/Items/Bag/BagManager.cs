using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class BagManager
    {
        public const int QuickSlotCount = 10;
        public const int BagSlotCount = 60;
        public const int TotalSlotCount = QuickSlotCount + BagSlotCount;

        public static BagManager Instance { get; } = new BagManager();

        private readonly List<BagSlot> slots = new List<BagSlot>(TotalSlotCount);
        private readonly HashSet<int> loadingItems = new HashSet<int>();
        private ISubscription itemChangedSubscription;
        private bool loading;

        private BagManager()
        {
        }

        public IReadOnlyList<BagSlot> Slots => slots;

        public static bool IsBagItem(int itemId)
        {
            if (itemId <= 0)
            {
                return false;
            }

            if (itemId == ItemIds.Food)
            {
                return true;
            }

            if (ToolKitDefinitions.TryGetTool(itemId, out _))
            {
                return true;
            }

            if (itemId >= ItemIds.TdConsumableMin && itemId <= ItemIds.TdConsumableMax)
            {
                return true;
            }

            if (itemId >= ItemIds.BlueprintMin && itemId <= ItemIds.BlueprintMax)
            {
                return true;
            }

            if (itemId >= ItemIds.SeedMin && itemId <= ItemIds.SeedMax)
            {
                return true;
            }

            if (itemId >= ItemIds.MaterialMin && itemId <= ItemIds.MaterialMax)
            {
                return false;
            }

            if (itemId >= ItemIds.CropProductMin && itemId <= ItemIds.CropProductMax)
            {
                return false;
            }

            if (itemId >= ItemIds.BasicResourceMin && itemId <= ItemIds.BasicResourceMax)
            {
                return false;
            }

            if (DataManager.Instance.Item == null ||
                !DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) ||
                config == null)
            {
                return false;
            }

            return config.ItemType == (int)global::ItemType.Consumable ||
                   config.ItemType == (int)global::ItemType.Seed ||
                   config.ItemType == (int)global::ItemType.Tool ||
                   config.ItemType == (int)global::ItemType.Blueprint;
        }

        public void Initialize()
        {
            loading = true;
            slots.Clear();
            EnsureSlotCount();
            loading = false;

            itemChangedSubscription?.Dispose();
            itemChangedSubscription = Messager.Instance.Subscribe<WorldMessageTopic, ItemChangedMessage>(
                WorldMessageTopic.ItemChanged,
                OnItemChanged);
        }

        public bool TryAddItem(int itemId, int count)
        {
            if (itemId <= 0 || count <= 0)
            {
                return false;
            }

            if (IsBagItem(itemId) && !CanAddItem(itemId))
            {
                Debug.Log($"Bag add item failed. Bag is full. itemId: {itemId}, count: {count}");
                return false;
            }

            if (IsBagItem(itemId))
            {
                EnsureItemSlot(itemId);
            }

            ItemManager.Instance.AddItem(itemId, count);
            TryAutoEquipTool(itemId);
            NotifyFullRefresh();
            return true;
        }

        public bool TryAddItems(IReadOnlyList<ItemStack> items)
        {
            if (items == null || items.Count == 0)
            {
                return false;
            }

            if (!CanAddItems(items))
            {
                Debug.Log("Bag add items failed. Bag is full.");
                return false;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ItemStack item = items[i];
                if (item == null || item.ItemId <= 0 || item.Count <= 0)
                {
                    continue;
                }

                if (IsBagItem(item.ItemId))
                {
                    EnsureItemSlot(item.ItemId);
                }

                ItemManager.Instance.AddItem(item.ItemId, item.Count);
                TryAutoEquipTool(item.ItemId);
            }

            NotifyFullRefresh();
            return true;
        }

        public bool TryUseSlot(int slotIndex)
        {
            if (!TryGetSlot(slotIndex, out BagSlot slot) || slot == null || slot.IsEmpty)
            {
                return false;
            }

            int count = ItemManager.Instance.GetCount(slot.ItemId);
            if (count <= 0)
            {
                slot.Clear();
                NotifySlotChanged(slotIndex);
                return false;
            }

            return ItemManager.Instance.Use(slot.ItemId).Succeeded;
        }

        public bool TryMoveOrSwapSlot(int fromSlotIndex, int toSlotIndex)
        {
            EnsureSlotCount();
            if (fromSlotIndex < 0 ||
                fromSlotIndex >= slots.Count ||
                toSlotIndex < 0 ||
                toSlotIndex >= slots.Count ||
                fromSlotIndex == toSlotIndex)
            {
                return false;
            }

            BagSlot fromSlot = slots[fromSlotIndex];
            BagSlot toSlot = slots[toSlotIndex];
            if (fromSlot == null || toSlot == null || fromSlot.IsEmpty)
            {
                return false;
            }

            int fromItemId = fromSlot.ItemId;
            int toItemId = toSlot.ItemId;
            toSlot.SetItem(fromItemId);
            if (toItemId > 0)
            {
                fromSlot.SetItem(toItemId);
            }
            else
            {
                fromSlot.Clear();
            }

            MarkDirtyIfReady();
            NotifyFullRefresh();
            return true;
        }

        public bool TryGetSlot(int slotIndex, out BagSlot slot)
        {
            EnsureSlotCount();
            if (slotIndex < 0 || slotIndex >= slots.Count)
            {
                slot = null;
                return false;
            }

            slot = slots[slotIndex];
            return true;
        }

        public int GetSlotItemCount(int slotIndex)
        {
            return TryGetSlot(slotIndex, out BagSlot slot) && slot != null && !slot.IsEmpty
                ? ItemManager.Instance.GetCount(slot.ItemId)
                : 0;
        }

        public SaveBagData CreateSaveData()
        {
            EnsureSlotCount();
            List<SaveBagSlotData> savedSlots = new List<SaveBagSlotData>();
            for (int i = 0; i < slots.Count; i++)
            {
                BagSlot slot = slots[i];
                if (slot == null || slot.IsEmpty)
                {
                    continue;
                }

                savedSlots.Add(new SaveBagSlotData
                {
                    SlotIndex = i,
                    ItemId = slot.ItemId,
                });
            }

            return new SaveBagData
            {
                SlotItemIds = savedSlots.ToArray(),
            };
        }

        public void LoadSaveData(SaveBagData data)
        {
            loading = true;
            slots.Clear();
            EnsureSlotCount();

            if (data == null || data.SlotItemIds == null || data.SlotItemIds.Length == 0)
            {
                FillSlotsFromWorldItems();
                loading = false;
                TryAutoEquipOwnedTools();
                NotifyFullRefresh();
                return;
            }

            loadingItems.Clear();
            for (int i = 0; i < data.SlotItemIds.Length; i++)
            {
                SaveBagSlotData saved = data.SlotItemIds[i];
                if (saved == null ||
                    saved.SlotIndex < 0 ||
                    saved.SlotIndex >= TotalSlotCount ||
                    saved.ItemId <= 0 ||
                    !IsBagItem(saved.ItemId) ||
                    loadingItems.Contains(saved.ItemId))
                {
                    continue;
                }

                slots[saved.SlotIndex].SetItem(saved.ItemId);
                loadingItems.Add(saved.ItemId);
            }

            loadingItems.Clear();
            loading = false;
            RemoveZeroCountItems();
            TryAutoEquipOwnedTools();
            NotifyFullRefresh();
        }

        public void Release()
        {
            itemChangedSubscription?.Dispose();
            itemChangedSubscription = null;
        }

        private bool CanAddItem(int itemId)
        {
            return HasItemSlot(itemId) || FindEmptySlotIndex() >= 0;
        }

        private bool CanAddItems(IReadOnlyList<ItemStack> items)
        {
            int emptySlotCount = CountEmptySlots();
            HashSet<int> newItemIds = new HashSet<int>();

            for (int i = 0; i < items.Count; i++)
            {
                ItemStack item = items[i];
                if (item == null ||
                    item.ItemId <= 0 ||
                    item.Count <= 0 ||
                    !IsBagItem(item.ItemId) ||
                    HasItemSlot(item.ItemId))
                {
                    continue;
                }

                newItemIds.Add(item.ItemId);
            }

            return newItemIds.Count <= emptySlotCount;
        }

        private void EnsureItemSlot(int itemId)
        {
            if (itemId <= 0 || HasItemSlot(itemId))
            {
                return;
            }

            int emptyIndex = FindEmptySlotIndex();
            if (emptyIndex < 0)
            {
                return;
            }

            slots[emptyIndex].SetItem(itemId);
            MarkDirtyIfReady();
        }

        private bool HasItemSlot(int itemId)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].ItemId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        private int FindEmptySlotIndex()
        {
            EnsureSlotCount();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    return i;
                }
            }

            return -1;
        }

        private int CountEmptySlots()
        {
            int count = 0;
            EnsureSlotCount();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].IsEmpty)
                {
                    count++;
                }
            }

            return count;
        }

        private void EnsureSlotCount()
        {
            while (slots.Count < TotalSlotCount)
            {
                slots.Add(new BagSlot(slots.Count, 0));
            }

            while (slots.Count > TotalSlotCount)
            {
                slots.RemoveAt(slots.Count - 1);
            }
        }

        private void FillSlotsFromWorldItems()
        {
            IReadOnlyDictionary<int, ItemStack> items = ItemManager.Instance.GetAllItems();
            if (items == null)
            {
                return;
            }

            foreach (KeyValuePair<int, ItemStack> pair in items)
            {
                ItemStack item = pair.Value;
                if (item == null || item.ItemId <= 0 || item.Count <= 0 || !IsBagItem(item.ItemId))
                {
                    continue;
                }

                EnsureItemSlot(item.ItemId);
            }
        }

        private void RemoveZeroCountItems()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                BagSlot slot = slots[i];
                if (slot != null && !slot.IsEmpty && ItemManager.Instance.GetCount(slot.ItemId) <= 0)
                {
                    slot.Clear();
                }
            }
        }

        private void OnItemChanged(ItemChangedMessage message)
        {
            if (message == null)
            {
                return;
            }

            if (message.Count <= 0)
            {
                bool changed = false;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i].ItemId == message.ItemId)
                    {
                        slots[i].Clear();
                        changed = true;
                    }
                }

                if (changed)
                {
                    NotifyFullRefresh();
                    MarkDirtyIfReady();
                    return;
                }
            }

            NotifyItemChanged(message.ItemId);
        }

        private void NotifyItemChanged(int itemId)
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i].ItemId == itemId)
                {
                    NotifySlotChanged(i);
                }
            }
        }

        private void NotifySlotChanged(int slotIndex)
        {
            BagSlot slot = slotIndex >= 0 && slotIndex < slots.Count ? slots[slotIndex] : null;
            Messager.Instance.Notify(WorldMessageTopic.BagChanged, new BagChangedMessage
            {
                SlotIndex = slotIndex,
                ItemId = slot != null ? slot.ItemId : 0,
                Count = slot != null && !slot.IsEmpty ? ItemManager.Instance.GetCount(slot.ItemId) : 0,
                FullRefresh = false,
            });
        }

        private void NotifyFullRefresh()
        {
            Messager.Instance.Notify(WorldMessageTopic.BagChanged, new BagChangedMessage
            {
                SlotIndex = -1,
                ItemId = 0,
                Count = 0,
                FullRefresh = true,
            });
        }

        private static void TryAutoEquipTool(int itemId)
        {
            if (ToolKitDefinitions.TryGetTool(itemId, out _))
            {
                ToolKitManager.Instance.TrySelectToolItem(itemId);
            }
        }

        private static void TryAutoEquipOwnedTools()
        {
            IReadOnlyDictionary<int, ItemStack> items = ItemManager.Instance.GetAllItems();
            if (items == null)
            {
                return;
            }

            foreach (KeyValuePair<int, ItemStack> pair in items)
            {
                ItemStack item = pair.Value;
                if (item == null || item.ItemId <= 0 || item.Count <= 0)
                {
                    continue;
                }

                TryAutoEquipTool(item.ItemId);
            }
        }

        private void MarkDirtyIfReady()
        {
            if (!loading)
            {
                StorageManager.Instance.MarkDirty();
            }
        }
    }
}
