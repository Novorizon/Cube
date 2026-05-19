using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class TowerInfoPanel : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Image towerIconImage;
        [SerializeField] private TMP_Text towerNameText;
        [SerializeField] private TMP_Text levelText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private TMP_Text attackAddText;
        [SerializeField] private TMP_Text rangeText;
        [SerializeField] private TMP_Text speedText;
        [SerializeField] private TMP_Text upgradeCostText;
        [SerializeField] private TMP_Text sellGoldText;
        [SerializeField] private Button upgradeButton;
        [SerializeField] private Button sellButton;

        private int selectedTowerId;

        public event Action<int> UpgradeClicked;
        public event Action<int> SellClicked;

        private void Awake()
        {
            if (upgradeButton != null)
            {
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }
            if (sellButton != null)
            {
                sellButton.onClick.AddListener(OnSellClicked);
            }
            Hide();
        }

        public void Show(TdTowerRuntimeInfo info)
        {
            selectedTowerId = info.TowerId;
            if (towerIconImage != null)
            {
                towerIconImage.sprite = info.Icon;
            }
            if (towerNameText != null)
            {
                towerNameText.text = info.Name;
            }
            if (levelText != null)
            {
                levelText.text = $"等级 {info.Level}";
            }
            if (attackText != null)
            {
                attackText.text = info.Attack.ToString();
            }
            if (attackAddText != null)
            {
                attackAddText.text = info.AttackAdd > 0 ? $"+{info.AttackAdd}" : string.Empty;
            }
            if (rangeText != null)
            {
                rangeText.text = $"{info.Range:0.#} 格";
            }
            if (speedText != null)
            {
                speedText.text = $"{info.AttackInterval:0.#} 秒";
            }
            if (upgradeCostText != null)
            {
                upgradeCostText.text = info.UpgradeCost.ToString();
            }
            if (sellGoldText != null)
            {
                sellGoldText.text = info.SellGold.ToString();
            }
            if (upgradeButton != null)
            {
                upgradeButton.interactable = info.CanUpgrade;
            }
            SetVisible(true);
        }

        public void Hide()
        {
            selectedTowerId = 0;
            SetVisible(false);
        }

        private void SetVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            else
            {
                gameObject.SetActive(visible);
            }
        }

        private void OnUpgradeClicked()
        {
            if (selectedTowerId != 0)
            {
                UpgradeClicked?.Invoke(selectedTowerId);
            }
        }

        private void OnSellClicked()
        {
            if (selectedTowerId != 0)
            {
                SellClicked?.Invoke(selectedTowerId);
            }
        }
    }
}
