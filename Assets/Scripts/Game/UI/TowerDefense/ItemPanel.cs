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
        private SkillSlotView slotPrefab;

        private readonly Dictionary<int, SkillSlotView> slots = new Dictionary<int, SkillSlotView>();

        public event Action<int> SkillClicked;

        protected override void OnDestroyed()
        {
            Clear();
            SkillClicked = null;
        }

        public void Build(IReadOnlyList<TdSkillUiConfig> configs)
        {
            Clear();

            if (contentRoot == null || slotPrefab == null || configs == null)
            {
                return;
            }

            for (int i = 0; i < configs.Count; i++)
            {
                TdSkillUiConfig config = configs[i];
                if (config == null)
                {
                    continue;
                }

                SkillSlotView slot = Instantiate(slotPrefab, contentRoot);
                slot.Init(config, OnSkillClicked);
                slots[config.Id] = slot;
            }
        }

        public void SetSkillCount(int skillId, int count)
        {
            if (slots.TryGetValue(skillId, out SkillSlotView slot))
            {
                slot.SetCount(count);
            }
        }

        private void OnSkillClicked(int skillId)
        {
            SkillClicked?.Invoke(skillId);
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
        }
    }
}
