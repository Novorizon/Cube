using System;
using System.Collections.Generic;
using Game.Framework;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class SkillPanel : UIPanel
    {
        private const string SkillContentPrefabPath = "Assets/Arts/UI/TowerDefense/Prefabs/Skill.prefab";

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private CommonSlotView slotPrefab;

        private readonly Dictionary<int, CommonSlotView> slots = new Dictionary<int, CommonSlotView>();
        private readonly Dictionary<int, SkillConfig> slotConfigs = new Dictionary<int, SkillConfig>();
        private readonly List<CommonSlotView> slotPool = new List<CommonSlotView>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();
        private GameObject skillContentPrefab;
        private int usedSlotCount;

        public event Action<int> SkillClicked;

        protected override void OnCreate()
        {
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.OnItemChanged += OnItemChanged;
            }
        }

        protected override void OnDestroyed()
        {
            if (ItemManager.Instance != null)
            {
                ItemManager.Instance.OnItemChanged -= OnItemChanged;
            }

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

            foreach (KeyValuePair<int, SkillConfig> pair in DataManager.Instance.Skill.GetAll())
            {
                SkillConfig config = pair.Value;
                if (!ShouldShowInHud(config))
                {
                    continue;
                }

                GameObject contentPrefab = GetSkillContentPrefab();
                if (contentPrefab == null)
                {
                    return;
                }

                CommonSlotView slot = AcquireSlot();
                slot.Init(config.Id, LocalizedConfigText.SkillName(config.Id), GetAvailableCastCount(config), LoadIcon(config.IconLocation), contentPrefab, OnSkillClicked);
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

        private static bool ShouldShowInHud(SkillConfig config)
        {
            if (config == null || !config.Enable || config.AbilityActionGroupId <= 0 || config.CostResourceId <= 0)
            {
                return false;
            }

            return (config.Behavior & 8) == 0;
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

            return ItemManager.Instance.GetCount(config.CostResourceId) / config.CostCount;
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
            slotConfigs.Clear();
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

        private GameObject GetSkillContentPrefab()
        {
            if (skillContentPrefab != null)
            {
                return skillContentPrefab;
            }

            skillContentPrefab = ResourceManager.Instance.LoadGameObject(SkillContentPrefabPath);
            if (skillContentPrefab == null)
            {
                Debug.LogWarning($"Skill content prefab load failed. location: {SkillContentPrefabPath}");
            }

            return skillContentPrefab;
        }
    }
}
