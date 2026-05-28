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
        private SkillSlotView slotPrefab;

        private readonly Dictionary<int, SkillSlotView> slots = new Dictionary<int, SkillSlotView>();
        private readonly Dictionary<int, SkillConfig> slotConfigs = new Dictionary<int, SkillConfig>();
        private readonly HashSet<string> missingIconWarnings = new HashSet<string>();

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

        public void Build()
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

                SkillSlotView slot = Instantiate(slotPrefab, contentRoot);
                slot.Init(config, LoadIcon(config.IconLocation), GetAvailableCastCount(config), OnSkillClicked);
                slots[config.Id] = slot;
                slotConfigs[config.Id] = config;
            }
        }

        public void SetSkillCount(int skillId, int count)
        {
            if (slots.TryGetValue(skillId, out SkillSlotView slot))
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

                if (!slots.TryGetValue(pair.Key, out SkillSlotView slot) || slot == null)
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
                if (config != null && config.CostResourceId == itemId && slots.TryGetValue(pair.Key, out SkillSlotView slot))
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
            foreach (SkillSlotView slot in slots.Values)
            {
                if (slot != null)
                {
                    Destroy(slot.gameObject);
                }
            }

            slots.Clear();
            slotConfigs.Clear();
        }
    }
}
