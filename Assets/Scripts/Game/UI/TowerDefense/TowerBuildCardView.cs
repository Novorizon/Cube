using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class TowerBuildCardView : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private Image normalFrame;

        [SerializeField]
        private Image selectedFrame;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        private TMP_Text costText;

        [SerializeField]
        private TMP_Text damageValueText;

        [SerializeField]
        private GameObject[] skillSlots;

        [SerializeField]
        private BattleSlotContentView[] skillViews;

        [SerializeField]
        private TooltipTrigger tooltipTrigger;

        public int TowerId { get; private set; }

        private Action<int> clickedCallback;
        private bool affordable = true;

        public void Init(
            int towerId,
            string towerName,
            string description,
            int cost,
            int damage,
            Action<int> clickedCallback)
        {
            TowerId = towerId;
            this.clickedCallback = clickedCallback;

            if (nameText != null)
            {
                nameText.text = towerName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = description;
            }

            if (costText != null)
            {
                costText.text = cost.ToString();
            }

            if (damageValueText != null)
            {
                damageValueText.text = damage.ToString();
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
                button.onClick.AddListener(OnButtonClicked);
            }

            tooltipTrigger?.Bind(CreateTooltipData);
        }

        public void SetIcon(Sprite sprite)
        {
            if (iconImage != null)
            {
                iconImage.sprite = sprite;
                iconImage.enabled = sprite != null;
            }
        }

        public void SetSelected(bool selected)
        {
            if (normalFrame != null)
            {
                normalFrame.gameObject.SetActive(!selected);
            }

            if (selectedFrame != null)
            {
                selectedFrame.gameObject.SetActive(selected);
            }
        }

        public void SetAffordable(bool value)
        {
            affordable = value;
            if (button != null)
            {
                button.interactable = affordable;
            }
        }

        public void SetSkills(IReadOnlyList<SkillConfig> skills, Func<string, Sprite> loadIcon)
        {
            int slotCount = Mathf.Max(skillSlots?.Length ?? 0, skillViews?.Length ?? 0);
            for (int i = 0; i < slotCount; i++)
            {
                SkillConfig config = skills != null && i < skills.Count ? skills[i] : null;
                BattleSlotContentView contentView = skillViews != null && i < skillViews.Length
                    ? skillViews[i]
                    : null;
                GameObject slot = skillSlots != null && i < skillSlots.Length
                    ? skillSlots[i]
                    : contentView != null ? contentView.gameObject : null;

                if (contentView != null)
                {
                    contentView.SetIcon(config != null ? loadIcon?.Invoke(config.IconLocation) : null);
                }

                if (slot != null)
                {
                    slot.SetActive(config != null);
                }
            }

            if (skills != null && skills.Count > slotCount)
            {
                Debug.LogWarning(
                    $"Tower card has {slotCount} authored skill slots, so only the first {slotCount} of {skills.Count} skills are shown.",
                    this);
            }
        }

        private void OnDestroy()
        {
            tooltipTrigger?.ClearBinding();

            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }

            clickedCallback = null;
        }

        private void OnButtonClicked()
        {
            clickedCallback?.Invoke(TowerId);
        }

        private TooltipData CreateTooltipData()
        {
            return new TooltipData
            {
                Title = LocalizedConfigText.TowerName(TowerId),
                Description = LocalizedConfigText.TowerDescription(TowerId),
                Icon = iconImage != null ? iconImage.sprite : null,
            };
        }
    }
}
