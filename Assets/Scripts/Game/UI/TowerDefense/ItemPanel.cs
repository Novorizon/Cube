using Game.Framework;
using System;
using System.Collections.Generic;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class ItemPanel : UIPanel
    {
        private const string ItemContentPrefabPath = "Assets/Arts/UI/TowerDefense/Prefabs/Item.prefab";

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private CommonSlotView slotPrefab;

        private readonly Dictionary<int, CommonSlotView> slots = new Dictionary<int, CommonSlotView>();
        private readonly List<CommonSlotView> slotPool = new List<CommonSlotView>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();
        private GameObject itemContentPrefab;
        private int usedSlotCount;

        public event Action<int> ItemClicked;

        protected override void OnCreate()
        {
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.OnItemChanged += OnItemChanged;
            }

            Initialize();
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

        public void Initialize()
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
            if (slots.TryGetValue(itemId, out CommonSlotView slot))
            {
                slot.SetCount(count);
                return;
            }

            if (count > 0)
            {
                CreateOrUpdateSlot(itemId, count);
            }
        }

        public bool TryGetSlotTransform(int itemId, out RectTransform target)
        {
            target = null;

            if (!slots.TryGetValue(itemId, out CommonSlotView slot) || slot == null)
            {
                return false;
            }

            target = slot.transform as RectTransform;
            return target != null;
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

            if (slots.TryGetValue(itemId, out CommonSlotView existingSlot))
            {
                existingSlot.SetCount(count);
                return;
            }

            GameObject contentPrefab = GetItemContentPrefab();
            if (contentPrefab == null)
            {
                return;
            }

            CommonSlotView slot = AcquireSlot();
            slot.Init(config.Id, config.Name, count, LoadIcon(config.IconLocation), contentPrefab, OnItemClicked);
            slots[itemId] = slot;
        }

        private void OnItemClicked(int itemId)
        {
            ItemClicked?.Invoke(itemId);
        }

        private void Clear()
        {
            RefreshSlotPool();

            foreach (CommonSlotView slot in slotPool)
            {
                if (slot != null)
                {
                    slot.gameObject.SetActive(true);
                    slot.ClearContent();
                }
            }

            slots.Clear();
            usedSlotCount = 0;
        }

        private CommonSlotView AcquireSlot()
        {
            RefreshSlotPool();

            if (usedSlotCount < slotPool.Count)
            {
                CommonSlotView slot = slotPool[usedSlotCount];
                usedSlotCount++;
                slot.gameObject.SetActive(true);
                return slot;
            }

            CommonSlotView instance = Instantiate(slotPrefab, contentRoot);
            slotPool.Add(instance);
            usedSlotCount++;
            return instance;
        }

        private void RefreshSlotPool()
        {
            if (contentRoot == null)
            {
                slotPool.Clear();
                return;
            }

            slotPool.Clear();
            contentRoot.GetComponentsInChildren(true, slotPool);
            slotPool.Sort(CompareSlotOrder);
        }

        private static int CompareSlotOrder(CommonSlotView left, CommonSlotView right)
        {
            if (left == right)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex());
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

        private GameObject GetItemContentPrefab()
        {
            if (itemContentPrefab != null)
            {
                return itemContentPrefab;
            }

            itemContentPrefab = ResourceManager.Instance.LoadGameObject(ItemContentPrefabPath);
            if (itemContentPrefab == null)
            {
                Debug.LogWarning($"Item content prefab load failed. location: {ItemContentPrefabPath}");
            }

            return itemContentPrefab;
        }
    }
}
