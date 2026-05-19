using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class SkillSlotView : MonoBehaviour
    {
        [SerializeField] private Button button;
        [SerializeField] private Image iconImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text countText;
        [SerializeField] private CanvasGroup canvasGroup;

        private int skillId;
        private Action<int> clicked;

        public void Init(TdSkillUiConfig config, Action<int> onClicked)
        {
            skillId = config.Id;
            clicked = onClicked;
            if (iconImage != null)
            {
                iconImage.sprite = config.Icon;
            }
            if (nameText != null)
            {
                nameText.text = config.Name;
            }
            SetCount(config.Count);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnClick);
            }
        }

        public void SetCount(int count)
        {
            if (countText != null)
            {
                countText.text = count.ToString();
            }
            bool available = count > 0;
            if (button != null)
            {
                button.interactable = available;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = available ? 1f : 0.45f;
            }
        }

        private void OnClick()
        {
            clicked?.Invoke(skillId);
        }
    }
}
