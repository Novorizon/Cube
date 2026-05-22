using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class ItemSlotView : MonoBehaviour
    {
        [SerializeField]
        private Button button;

        [SerializeField]
        private Image iconImage;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text countText;

        [SerializeField]
        private CanvasGroup canvasGroup;

        private int itemId;
        private Action<int> clicked;

        public void Init(ItemConfig config, int count, Sprite icon, Action<int> onClicked)
        {
            itemId = config.Id;
            clicked = onClicked;

            if (iconImage != null)
            {
                iconImage.sprite = icon;
                iconImage.enabled = icon != null;
            }

            if (nameText != null)
            {
                nameText.text = config.Name;
            }

            SetCount(count);

            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
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

        private void OnDestroy()
        {
            if (button != null)
            {
                button.onClick.RemoveListener(OnClick);
            }

            clicked = null;
        }

        private void OnClick()
        {
            clicked?.Invoke(itemId);
        }
    }
}