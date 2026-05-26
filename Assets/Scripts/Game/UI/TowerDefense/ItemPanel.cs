using Game.Framework;
using System;
using System.Collections.Generic;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class ItemPanel : UIPanel
    {
        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private ItemSlotView slotPrefab;

        private readonly Dictionary<int, ItemSlotView> slots = new Dictionary<int, ItemSlotView>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();

        public event Action<int> ItemClicked;

        protected override void OnCreate()
        {
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.OnItemChanged += OnItemChanged;
            }

            Build();
        }

        protected override void OnDestroyed()
        {
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.OnItemChanged -= OnItemChanged;
            }

            Clear();
            ItemClicked = null;
        }

        public void Build()
        {
            Clear();

            if (contentRoot == null || slotPrefab == null)
            {
                return;
            }

            IReadOnlyDictionary<int, ItemData> items = ItemManager.Instance.GetAllItems();

            foreach (KeyValuePair<int, ItemData> pair in items)
            {
                CreateOrUpdateSlot(pair.Key, pair.Value.Count);
            }
        }

        public void SetItemCount(int itemId, int count)
        {
            if (slots.TryGetValue(itemId, out ItemSlotView slot))
            {
                slot.SetCount(count);
                return;
            }

            if (count > 0)
            {
                CreateOrUpdateSlot(itemId, count);
            }
        }

        private void OnItemChanged(int itemId, int count)
        {
            SetItemCount(itemId, count);
        }

        private void CreateOrUpdateSlot(int itemId, int count)
        {
            if (itemId == ItemIds.Gold)
            {
                return;
            }

            ItemConfig config = DataManager.Instance.Item.Get(itemId);

            if (config == null)
            {
                return;
            }

            if (slots.TryGetValue(itemId, out ItemSlotView existingSlot))
            {
                existingSlot.SetCount(count);
                return;
            }

            ItemSlotView slot = Instantiate(slotPrefab, contentRoot);
            slot.Init(config, count, LoadIcon(config.IconLocation), OnItemClicked);
            slots[itemId] = slot;
        }

        private void OnItemClicked(int itemId)
        {
            ItemClicked?.Invoke(itemId);
        }

        private void Clear()
        {
            foreach (ItemSlotView slot in slots.Values)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }

            slots.Clear();
        }

        private Sprite LoadIcon(string location)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                return null;
            }

            if (!location.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (missingIconWarnings.Add(location))
                {
                    Debug.LogWarning($"Item icon location must be a full asset path. location: {location}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(location);
            if (sprite == null && missingIconWarnings.Add(location))
            {
                Debug.LogWarning($"Item icon load failed. location: {location}");
            }

            return sprite;
        }
    }
}
