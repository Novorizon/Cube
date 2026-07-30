using System;
using System.Collections.Generic;
using Game.Framework;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class SkillPanel : UIPanel
    {
        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private CommonSlotView slotPrefab;

        [SerializeField]
        private CommonSlotView[] initialSlots;

        [Header("HUD Layout")]
        [SerializeField]
        private int featuredSkillId = 50000001;

        [SerializeField, Min(0)]
        private int maxVisibleSlots = 5;

        private readonly Dictionary<int, CommonSlotView> slots = new Dictionary<int, CommonSlotView>();
        private readonly Dictionary<int, SkillConfig> slotConfigs = new Dictionary<int, SkillConfig>();
        private readonly List<CommonSlotView> slotPool = new List<CommonSlotView>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();
        private int usedSlotCount;
        private bool subscribed;

        public event Action<int> SkillClicked;

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
            SkillClicked = null;
        }

        public void Initialize()
        {
            Clear();

            if (contentRoot == null || slotPrefab == null || DataManager.Instance.Skill == null || DataManager.Instance.Skill.GetAll() == null)
            {
                return;
            }

            List<SkillConfig> visibleSkills = new List<SkillConfig>();
            foreach (KeyValuePair<int, SkillConfig> pair in DataManager.Instance.Skill.GetAll())
            {
                if (ShouldShowInHud(pair.Value))
                {
                    visibleSkills.Add(pair.Value);
                }
            }

            visibleSkills.Sort(CompareHudSkills);
            int visibleCount = maxVisibleSlots > 0
                ? Mathf.Min(maxVisibleSlots, visibleSkills.Count)
                : visibleSkills.Count;

            if (visibleCount < visibleSkills.Count)
            {
                Debug.LogWarning(
                    $"[{nameof(SkillPanel)}] {visibleSkills.Count} skills are eligible for the HUD, but only {visibleCount} slots are configured.",
                    this);
            }

            for (int i = 0; i < visibleCount; i++)
            {
                SkillConfig config = visibleSkills[i];
                CommonSlotView slot = AcquireSlot();
                slot.Init(config.Id, LocalizedConfigText.SkillName(config.Id), GetAvailableCastCount(config), LoadIcon(config.IconLocation), OnSkillClicked);
                slots[config.Id] = slot;
                slotConfigs[config.Id] = config;
            }
        }

        public void SetSkillCount(int skillId, int count)
        {
            if (slots.TryGetValue(skillId, out CommonSlotView slot))
            {
                slot.SetCount(count);
            }
        }

        public bool TryGetTargetForItem(int itemId, out RectTransform target)
        {
            target = null;

            foreach (KeyValuePair<int, SkillConfig> pair in slotConfigs)
            {
                SkillConfig config = pair.Value;
                if (config == null || config.CostResourceId != itemId)
                {
                    continue;
                }

                if (!slots.TryGetValue(pair.Key, out CommonSlotView slot) || slot == null)
                {
                    continue;
                }

                target = slot.transform as RectTransform;
                return target != null;
            }

            return false;
        }

        public bool UsesItem(int itemId)
        {
            foreach (KeyValuePair<int, SkillConfig> pair in slotConfigs)
            {
                SkillConfig config = pair.Value;
                if (config != null && config.CostResourceId == itemId)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnSkillClicked(int skillId)
        {
            SkillClicked?.Invoke(skillId);
        }

        private void OnItemChanged(int itemId, int count)
        {
            foreach (KeyValuePair<int, SkillConfig> pair in slotConfigs)
            {
                SkillConfig config = pair.Value;
                if (config != null && config.CostResourceId == itemId && slots.TryGetValue(pair.Key, out CommonSlotView slot))
                {
                    slot.SetCount(GetAvailableCastCount(config));
                }
            }
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            foreach (KeyValuePair<int, CommonSlotView> pair in slots)
            {
                if (pair.Value == null)
                {
                    continue;
                }

                if (AbilityManager.Instance.TryGetBaseAbilityCooldown(pair.Key, out float remaining, out float duration))
                {
                    pair.Value.SetCooldown(remaining, duration);
                }
            }
        }

        private static bool ShouldShowInHud(SkillConfig config)
        {
            if (config == null || !config.Enable || config.AbilityActionGroupId <= 0 || config.CostResourceId <= 0)
            {
                return false;
            }

            return (config.Behavior & 8) == 0;
        }

        private int CompareHudSkills(SkillConfig left, SkillConfig right)
        {
            if (ReferenceEquals(left, right))
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

            bool leftFeatured = featuredSkillId > 0 && left.Id == featuredSkillId;
            bool rightFeatured = featuredSkillId > 0 && right.Id == featuredSkillId;
            if (leftFeatured != rightFeatured)
            {
                return leftFeatured ? -1 : 1;
            }

            return left.Id.CompareTo(right.Id);
        }

        private static int GetAvailableCastCount(SkillConfig config)
        {
            if (config == null)
            {
                return 0;
            }

            if (config.CostResourceId <= 0 || config.CostCount <= 0)
            {
                return 1;
            }

            return BattleItemManager.Instance.GetCount(config.CostResourceId) / config.CostCount;
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
                    Debug.LogWarning($"Skill icon location must be a full asset path. location: {location}");
                }

                return null;
            }

            Sprite sprite = ResourceManager.Instance.LoadAsset<Sprite>(location);
            if (sprite == null && missingIconWarnings.Add(location))
            {
                Debug.LogWarning($"Skill icon load failed. location: {location}");
            }

            return sprite;
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
            slotConfigs.Clear();
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
