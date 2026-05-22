using System;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    /// <summary>
    /// 常驻目标信息面板。
    /// 有目标时显示目标信息；没有目标时显示空状态，但面板本身不隐藏。
    /// </summary>
    public sealed class InfoPanel : UIPanel
    {
        private const string EmptyName = "未选择目标";
        private const string EmptyValue = "--";

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private Image towerIconImage;

        [SerializeField]
        private TMP_Text towerNameText;

        [SerializeField]
        private TMP_Text levelText;

        [SerializeField]
        private TMP_Text attackText;

        [SerializeField]
        private TMP_Text attackAddText;

        [SerializeField]
        private TMP_Text rangeText;

        [SerializeField]
        private TMP_Text speedText;

        [SerializeField]
        private TMP_Text upgradeCostText;

        [SerializeField]
        private TMP_Text sellGoldText;

        [SerializeField]
        private Button upgradeButton;

        [SerializeField]
        private Button sellButton;

        private int selectedTowerId;
        private bool initialized;

        public event Action<int> UpgradeClicked;
        public event Action<int> SellClicked;

        protected override void OnCreate()
        {
            Initialize();
        }

        private void Start()
        {
            Initialize();
        }

        protected override void OnDestroyed()
        {
            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
            }

            if (sellButton != null)
            {
                sellButton.onClick.RemoveListener(OnSellClicked);
            }

            UpgradeClicked = null;
            SellClicked = null;
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;

            if (upgradeButton != null)
            {
                upgradeButton.onClick.RemoveListener(OnUpgradeClicked);
                upgradeButton.onClick.AddListener(OnUpgradeClicked);
            }

            if (sellButton != null)
            {
                sellButton.onClick.RemoveListener(OnSellClicked);
                sellButton.onClick.AddListener(OnSellClicked);
            }

            SetPanelVisible(true);
            ClearInfo();
        }

        public void Show(TdTowerRuntimeInfo info)
        {
            Initialize();

            selectedTowerId = info.TowerId;

            if (towerIconImage != null)
            {
                towerIconImage.sprite = info.Icon;
                towerIconImage.enabled = info.Icon != null;
            }

            if (towerNameText != null)
            {
                towerNameText.text = string.IsNullOrEmpty(info.Name) ? EmptyName : info.Name;
            }

            if (levelText != null)
            {
                levelText.text = $"Lv {info.Level}";
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
                rangeText.text = $"{info.Range:0.#}";
            }

            if (speedText != null)
            {
                speedText.text = $"{info.AttackInterval:0.#}s";
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
                upgradeButton.interactable = selectedTowerId > 0 && info.CanUpgrade;
            }

            if (sellButton != null)
            {
                sellButton.interactable = selectedTowerId > 0;
            }

            SetPanelVisible(true);
        }

        /// <summary>
        /// 兼容 BattleHudController.HideTowerInfo 的旧调用。
        /// InfoPanel 是常驻面板，所以这里只清空内容，不隐藏 GameObject / CanvasGroup。
        /// </summary>
        public void Hide()
        {
            Initialize();
            ClearInfo();
        }

        public void ClearInfo()
        {
            selectedTowerId = 0;

            if (towerIconImage != null)
            {
                towerIconImage.sprite = null;
                towerIconImage.enabled = false;
            }

            if (towerNameText != null)
            {
                towerNameText.text = EmptyName;
            }

            if (levelText != null)
            {
                levelText.text = EmptyValue;
            }

            if (attackText != null)
            {
                attackText.text = EmptyValue;
            }

            if (attackAddText != null)
            {
                attackAddText.text = string.Empty;
            }

            if (rangeText != null)
            {
                rangeText.text = EmptyValue;
            }

            if (speedText != null)
            {
                speedText.text = EmptyValue;
            }

            if (upgradeCostText != null)
            {
                upgradeCostText.text = EmptyValue;
            }

            if (sellGoldText != null)
            {
                sellGoldText.text = EmptyValue;
            }

            if (upgradeButton != null)
            {
                upgradeButton.interactable = false;
            }

            if (sellButton != null)
            {
                sellButton.interactable = false;
            }

            SetPanelVisible(true);
        }

        private void SetPanelVisible(bool visible)
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = visible ? 1f : 0f;
                canvasGroup.interactable = visible;
                canvasGroup.blocksRaycasts = visible;
                return;
            }

            gameObject.SetActive(visible);
        }

        private void OnUpgradeClicked()
        {
            if (selectedTowerId <= 0)
            {
                return;
            }

            UpgradeClicked?.Invoke(selectedTowerId);
        }

        private void OnSellClicked()
        {
            if (selectedTowerId <= 0)
            {
                return;
            }

            SellClicked?.Invoke(selectedTowerId);
        }
    }
}
