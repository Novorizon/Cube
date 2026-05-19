using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class SkillPanel : MonoBehaviour
    {
        [SerializeField] private RectTransform contentRoot;
        [SerializeField] private SkillSlotView slotPrefab;

        private readonly Dictionary<int, SkillSlotView> slots = new Dictionary<int, SkillSlotView>();

        public event Action<int> SkillClicked;

        public void Build(IReadOnlyList<TdSkillUiConfig> configs)
        {
            Clear();
            if (contentRoot == null || slotPrefab == null || configs == null)
            {
                return;
            }
            for (int i = 0; i < configs.Count; i++)
            {
                SkillSlotView slot = Instantiate(slotPrefab, contentRoot);
                slot.Init(configs[i], OnSkillClicked);
                slots[configs[i].Id] = slot;
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
