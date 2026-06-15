using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class WorldItemManager
    {
        public static WorldItemManager Instance { get; } = new WorldItemManager();

        private readonly Dictionary<int, WorldItem> itemMap = new Dictionary<int, WorldItem>();
        private bool loading;

        private WorldItemManager()
        {
        }

        public int GetCount(int itemId)
        {
            if (itemMap.TryGetValue(itemId, out WorldItem item))
            {
                return item.Count;
            }

            return 0;
        }

        public bool HasItem(int itemId, int count)
        {
            if (count <= 0)
            {
                return true;
            }

            return GetCount(itemId) >= count;
        }

        public bool HasItems(IReadOnlyList<WorldItem> costs)
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
                Debug.LogWarning($"World item add failed. Invalid item id: {itemId}");
                return;
            }

            if (count <= 0)
            {
                return;
            }

            int nextCount = GetCount(itemId) + count;
            SetItemCount(itemId, nextCount, count);
        }

        public void AddItems(IReadOnlyList<WorldItem> items)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                WorldItem item = items[i];
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
                Debug.LogWarning($"World item consume failed. Invalid item id: {itemId}");
                return false;
            }

            int current = GetCount(itemId);
            if (current < count)
            {
                Debug.Log($"World item not enough. itemId: {itemId}, need: {count}, current: {current}");
                return false;
            }

            SetItemCount(itemId, current - count, -count);
            return true;
        }

        public bool TryConsumeItems(IReadOnlyList<WorldItem> costs)
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

        public IReadOnlyDictionary<int, WorldItem> GetAllItems()
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
            else if (itemMap.TryGetValue(itemId, out WorldItem item))
            {
                item.SetCount(nextCount);
            }
            else
            {
                itemMap.Add(itemId, new WorldItem(itemId, nextCount));
            }

            Messager.Instance.Notify(WorldMessageTopic.ItemChanged, new WorldItemChangedMessage
            {
                ItemId = itemId,
                Count = nextCount,
                Delta = delta,
            });

            if (!loading)
            {
                StorageManager.Instance.MarkDirty();
            }
        }

        public SaveWorldItemData[] CreateSaveData()
        {
            List<SaveWorldItemData> items = new List<SaveWorldItemData>();
            foreach (KeyValuePair<int, WorldItem> pair in itemMap)
            {
                WorldItem item = pair.Value;
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

        private Dictionary<int, int> BuildRequiredCounts(IReadOnlyList<WorldItem> costs)
        {
            Dictionary<int, int> requiredCounts = new Dictionary<int, int>();
            if (costs == null)
            {
                return requiredCounts;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                WorldItem cost = costs[i];
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
