using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public enum ItemUseState
    {
        Failed = 0,
        Selected = 1,
        PendingTarget = 2,
        Completed = 3,
    }

    public enum ItemUseFailure
    {
        None = 0,
        InvalidItem = 1,
        NotOwned = 2,
        MissingConfig = 3,
        SelectionFailed = 4,
        Unsupported = 5,
    }

    public readonly struct ItemUseResult
    {
        private ItemUseResult(int itemId, ItemUseState state, ItemUseFailure failure)
        {
            ItemId = itemId;
            State = state;
            Failure = failure;
        }

        public int ItemId { get; }
        public ItemUseState State { get; }
        public ItemUseFailure Failure { get; }
        public bool Succeeded => State != ItemUseState.Failed;

        public static ItemUseResult Selected(int itemId)
        {
            return new ItemUseResult(itemId, ItemUseState.Selected, ItemUseFailure.None);
        }

        public static ItemUseResult Failed(int itemId, ItemUseFailure failure)
        {
            return new ItemUseResult(itemId, ItemUseState.Failed, failure);
        }
    }

    public sealed class ItemManager
    {
        public static ItemManager Instance { get; } = new ItemManager();

        private readonly Dictionary<int, ItemStack> itemMap = new Dictionary<int, ItemStack>();
        private bool loading;

        private ItemManager()
        {
        }

        public int GetCount(int itemId)
        {
            if (itemMap.TryGetValue(itemId, out ItemStack item))
            {
                return item.Count;
            }

            return 0;
        }

        public ItemUseResult Use(int itemId)
        {
            if (itemId <= 0)
            {
                return ItemUseResult.Failed(itemId, ItemUseFailure.InvalidItem);
            }

            if (GetCount(itemId) <= 0)
            {
                return ItemUseResult.Failed(itemId, ItemUseFailure.NotOwned);
            }

            if (ToolKitDefinitions.TryGetTool(itemId, out _))
            {
                if (ToolKitManager.Instance.TrySelectToolItem(itemId))
                {
                    return ItemUseResult.Selected(itemId);
                }

                Debug.Log($"Select tool failed. Toolkit has no available slot. itemId: {itemId}");
                return ItemUseResult.Failed(itemId, ItemUseFailure.SelectionFailed);
            }

            if (DataManager.Instance.Item == null ||
                !DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) ||
                config == null)
            {
                return ItemUseResult.Failed(itemId, ItemUseFailure.MissingConfig);
            }

            Debug.Log($"Item use is not configured. itemId: {itemId}, itemType: {config.ItemType}");
            return ItemUseResult.Failed(itemId, ItemUseFailure.Unsupported);
        }

        public void NotifyUseCompleted(int itemId, int count = 1)
        {
            if (itemId <= 0 || count <= 0 || loading)
            {
                return;
            }

            QuestManager.Instance.NotifyEvent(QuestEventType.UseItem, itemId, count);
        }

        public bool HasItem(int itemId, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            return GetCount(itemId) >= count;
        }

        public bool HasItems(IReadOnlyList<ItemStack> costs)
        {
            Dictionary<int, int> requiredCounts = BuildRequiredCounts(costs);
            foreach (KeyValuePair<int, int> pair in requiredCounts)
            {
                if (!HasItem(pair.Key, pair.Value))
                {
                    return false;
                }
            }

            return true;
        }

        public void AddItem(int itemId, int count)
        {
            if (itemId <= 0)
            {
                Debug.LogWarning($"Item add failed. Invalid item id: {itemId}");
                return;
            }

            if (count <= 0)
            {
                return;
            }

            int nextCount = GetCount(itemId) + count;
            SetItemCount(itemId, nextCount, count);
        }

        public void AddItems(IReadOnlyList<ItemStack> items)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                ItemStack item = items[i];
                if (item == null)
                {
                    continue;
                }

                AddItem(item.ItemId, item.Count);
            }
        }

        public bool TryConsumeItem(int itemId, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            if (itemId <= 0)
            {
                Debug.LogWarning($"Item consume failed. Invalid item id: {itemId}");
                return false;
            }

            int current = GetCount(itemId);
            if (current < count)
            {
                Debug.Log($"Item not enough. itemId: {itemId}, need: {count}, current: {current}");
                return false;
            }

            SetItemCount(itemId, current - count, -count);
            return true;
        }

        public bool TryConsumeItems(IReadOnlyList<ItemStack> costs)
        {
            Dictionary<int, int> requiredCounts = BuildRequiredCounts(costs);
            foreach (KeyValuePair<int, int> pair in requiredCounts)
            {
                if (!HasItem(pair.Key, pair.Value))
                {
                    return false;
                }
            }

            foreach (KeyValuePair<int, int> pair in requiredCounts)
            {
                TryConsumeItem(pair.Key, pair.Value);
            }

            return true;
        }

        public IReadOnlyDictionary<int, ItemStack> GetAllItems()
        {
            return itemMap;
        }

        public void Clear()
        {
            itemMap.Clear();
        }

        private void SetItemCount(int itemId, int count, int delta)
        {
            int nextCount = count > 0 ? count : 0;
            if (nextCount <= 0)
            {
                itemMap.Remove(itemId);
            }
            else if (itemMap.TryGetValue(itemId, out ItemStack item))
            {
                item.SetCount(nextCount);
            }
            else
            {
                itemMap.Add(itemId, new ItemStack(itemId, nextCount));
            }

            Messager.Instance.Notify(WorldMessageTopic.ItemChanged, new ItemChangedMessage
            {
                ItemId = itemId,
                Count = nextCount,
                Delta = delta,
            });

            if (delta > 0 && !loading)
            {
                QuestManager.Instance.NotifyEvent(QuestEventType.GainItem, itemId, delta);
            }

            if (!loading)
            {
                StorageManager.Instance.MarkDirty();
            }
        }

        public SaveWorldItemData[] CreateSaveData()
        {
            List<SaveWorldItemData> items = new List<SaveWorldItemData>();
            foreach (KeyValuePair<int, ItemStack> pair in itemMap)
            {
                ItemStack item = pair.Value;
                if (item == null || item.ItemId <= 0 || item.Count <= 0)
                {
                    continue;
                }

                items.Add(new SaveWorldItemData
                {
                    ItemId = item.ItemId,
                    Count = item.Count,
                });
            }

            return items.ToArray();
        }

        public void LoadSaveData(IReadOnlyList<SaveWorldItemData> items)
        {
            loading = true;
            itemMap.Clear();

            if (items != null)
            {
                for (int i = 0; i < items.Count; i++)
                {
                    SaveWorldItemData item = items[i];
                    if (item == null || item.ItemId <= 0 || item.Count <= 0)
                    {
                        continue;
                    }

                    SetItemCount(item.ItemId, item.Count, item.Count);
                }
            }

            loading = false;
        }

        private Dictionary<int, int> BuildRequiredCounts(IReadOnlyList<ItemStack> costs)
        {
            Dictionary<int, int> requiredCounts = new Dictionary<int, int>();
            if (costs == null)
            {
                return requiredCounts;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                ItemStack cost = costs[i];
                if (cost == null || cost.ItemId <= 0 || cost.Count <= 0)
                {
                    continue;
                }

                if (requiredCounts.TryGetValue(cost.ItemId, out int current))
                {
                    requiredCounts[cost.ItemId] = current + cost.Count;
                }
                else
                {
                    requiredCounts.Add(cost.ItemId, cost.Count);
                }
            }

            return requiredCounts;
        }
    }
}
