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
        private CommonSlotView slotPrefab;

        [SerializeField]
        private CommonSlotView[] initialSlots;

        [SerializeField]
        private GameObject itemContentPrefab;

        private readonly Dictionary<int, CommonSlotView> slots = new Dictionary<int, CommonSlotView>();
        private readonly List<CommonSlotView> slotPool = new List<CommonSlotView>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();
        private int usedSlotCount;
        private bool subscribed;

        public event Action<int> ItemClicked;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.None;

        protected override void OnCreate()
        {
            RegisterInitialSlots();
        }

        protected override void OnOpen(object args)
        {
            Subscribe();
            Initialize();
        }

        protected override void OnClose()
        {
            Unsubscribe();
        }

        protected override void OnDestroyed()
        {
            Unsubscribe();
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

            IReadOnlyDictionary<int, BattleItemData> items = BattleItemManager.Instance.GetAllItems();

            foreach (KeyValuePair<int, BattleItemData> pair in items)
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

            if (itemContentPrefab == null)
            {
                Debug.LogError($"[{nameof(ItemPanel)}] itemContentPrefab is not assigned.", this);
                return;
            }

            CommonSlotView slot = AcquireSlot();
            slot.Init(config.Id, LocalizedConfigText.ItemName(config.Id), count, LoadIcon(config.IconLocation), itemContentPrefab, OnItemClicked);
            slots[itemId] = slot;
        }

        private void OnItemClicked(int itemId)
        {
            ItemClicked?.Invoke(itemId);
        }

        private void Clear()
        {
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

        private void RegisterInitialSlots()
        {
            slotPool.Clear();
            if (initialSlots == null)
            {
                return;
            }

            for (int i = 0; i < initialSlots.Length; i++)
            {
                CommonSlotView slot = initialSlots[i];
                if (slot != null && !slotPool.Contains(slot))
                {
                    slotPool.Add(slot);
                }
            }
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

        private void Subscribe()
        {
            if (subscribed)
            {
                return;
            }

            BattleItemManager.Instance.OnItemChanged += OnItemChanged;
            subscribed = true;
        }

        private void Unsubscribe()
        {
            if (!subscribed)
            {
                return;
            }

            BattleItemManager.Instance.OnItemChanged -= OnItemChanged;
            subscribed = false;
        }
    }
}
