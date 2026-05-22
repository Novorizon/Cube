using Game.Framework;
using System;
using System.Collections.Generic;
using UI;
using UnityEngine;
using UnityEngine.U2D;

namespace Game
{
    public sealed class ItemPanel : UIPanel
    {
        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private ItemSlotView slotPrefab;

        [SerializeField]
        private string atlasLocation = "Assets/Arts/UI/Atlas/TowerDefense.spriteatlasv2";

        private readonly Dictionary<int, ItemSlotView> slots = new Dictionary<int, ItemSlotView>();

        private SpriteAtlas atlas;

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

            atlas = ResourceManager.Instance.LoadSpriteAtlas(atlasLocation);

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

            Sprite icon = null;

            if (atlas != null && !string.IsNullOrEmpty(config.IconLocation))
            {
                icon = atlas.GetSprite(config.IconLocation);
            }

            ItemSlotView slot = Instantiate(slotPrefab, contentRoot);
            slot.Init(config, count, icon, OnItemClicked);
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
    }
}