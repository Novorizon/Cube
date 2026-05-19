using System;
using TMPro;
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
        private GameObject selectedFrame;

        [SerializeField]
        private TMP_Text nameText;

        [SerializeField]
        private TMP_Text costText;

        public int TowerId { get; private set; }

        private Action<int> clickedCallback;

        public void Init(int towerId, string towerName, int cost, Action<int> clickedCallback)
        {
            TowerId = towerId;
            this.clickedCallback = clickedCallback;

            if (nameText != null)
            {
                nameText.text = towerName;
            }

            if (costText != null)
            {
                costText.text = cost.ToString();
            }

            if (button != null)
            {
                button.onClick.RemoveListener(OnButtonClicked);
                button.onClick.AddListener(OnButtonClicked);
            }
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
            if (selectedFrame != null)
            {
                selectedFrame.SetActive(selected);
            }
        }

        private void OnDestroy()
        {
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
    }
}