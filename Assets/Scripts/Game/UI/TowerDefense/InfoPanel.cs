using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class InfoPanel : UIPanel
    {
        private const string EmptyName = "";
        private const string EmptyValue = "--";

        [SerializeField]
        private CanvasGroup canvasGroup;

        [SerializeField]
        private Image targetIconImage;

        [SerializeField]
        private TMP_Text targetNameText;

        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        private TMP_Text levelText;

        [SerializeField]
        private TMP_Text hpText;

        [SerializeField]
        private UIProgressBar hpFill;

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

        private TdTargetInfoType selectedTargetType;
        private int selectedTargetId;
        private bool initialized;
        private Subscriber subscriber;

        public event Action<int> UpgradeClicked;
        public event Action<int> SellClicked;

        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private TowerBuildCardView cardPrefab;

        private readonly List<TowerBuildCardView> cards = new List<TowerBuildCardView>();
        private int selectedTowerConfigId;

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
            if (subscriber != null)
            {
                subscriber.Clear();
                subscriber = null;
            }

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
            RegisterMessageHandlers();

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

        public void Show(TdTargetRuntimeInfo info)
        {
            Initialize();

            selectedTargetType = info.Type;
            selectedTargetId = info.TargetId;

            if (targetIconImage != null)
            {
                targetIconImage.sprite = info.Icon;
                targetIconImage.enabled = info.Icon != null;
            }

            if (targetNameText != null)
            {
                targetNameText.text = string.IsNullOrEmpty(info.Name) ? EmptyName : info.Name;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;
            }

            if (levelText != null)
            {
                levelText.text = info.Level > 0 ? $"Lv {info.Level}" : EmptyValue;
            }

            if (hpText != null)
            {
                hpText.text = info.MaxHp > 0 ? $"{Mathf.Max(0, info.CurrentHp)}/{info.MaxHp}" : EmptyValue;
            }

            if (hpFill != null)
            {
                hpFill.SetValue(Mathf.Max(0, info.CurrentHp), Mathf.Max(0, info.MaxHp));
            }

            if (attackText != null)
            {
                attackText.text = info.Attack > 0 ? info.Attack.ToString() : EmptyValue;
            }

            if (attackAddText != null)
            {
                attackAddText.text = info.AttackAdd > 0 ? $"+{info.AttackAdd}" : string.Empty;
            }

            if (rangeText != null)
            {
                rangeText.text = info.Range > 0f ? $"{info.Range:0.#}" : EmptyValue;
            }

            if (speedText != null)
            {
                speedText.text = info.AttackInterval > 0f ? $"{info.AttackInterval:0.#}s" : EmptyValue;
            }

            if (upgradeCostText != null)
            {
                upgradeCostText.text = info.CanUpgrade ? info.UpgradeCost.ToString() : EmptyValue;
            }

            if (sellGoldText != null)
            {
                sellGoldText.text = info.CanSell ? info.SellGold.ToString() : EmptyValue;
            }

            if (upgradeButton != null)
            {
                upgradeButton.interactable = info.Type == TdTargetInfoType.Tower && info.TargetId > 0 && info.CanUpgrade;
            }

            if (sellButton != null)
            {
                sellButton.interactable = info.Type == TdTargetInfoType.Tower && info.TargetId > 0 && info.CanSell;
            }

            SetPanelVisible(true);
        }

        public void ShowTower(TdTowerRuntimeInfo info)
        {
            TdTargetRuntimeInfo targetInfo = new TdTargetRuntimeInfo
            {
                Type = TdTargetInfoType.Tower,
                TargetId = info.TowerId,
                Name = info.Name,
                Icon = info.Icon,
                Level = info.Level,
                Attack = info.Attack,
                AttackAdd = info.AttackAdd,
                Range = info.Range,
                AttackInterval = info.AttackInterval,
                UpgradeCost = info.UpgradeCost,
                SellGold = info.SellGold,
                CanUpgrade = info.CanUpgrade,
                CanSell = true
            };

            Show(targetInfo);
        }

        public void ClearInfo()
        {
            selectedTargetType = TdTargetInfoType.None;
            selectedTargetId = 0;

            if (targetIconImage != null)
            {
                targetIconImage.sprite = null;
                targetIconImage.enabled = false;
            }

            if (targetNameText != null)
            {
                targetNameText.text = EmptyName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.Empty;
            }

            if (levelText != null)
            {
                levelText.text = EmptyValue;
            }

            if (hpText != null)
            {
                hpText.text = EmptyValue;
            }

            if (hpFill != null)
            {
                hpFill.SetValue(0, 1);
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

        protected void SetPanelVisible(bool visible)
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

        private void RegisterMessageHandlers()
        {
            subscriber = new Subscriber();

            ISubscription targetInfoChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, TargetInfoMessage>(BattleMessageTopic.TargetInfoChanged, OnTargetInfoMessage);
            ISubscription targetInfoClearedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, TargetInfoClearMessage>(BattleMessageTopic.TargetInfoCleared, OnTargetInfoClearMessage);

            subscriber.Add(targetInfoChangedSubscription);
            subscriber.Add(targetInfoClearedSubscription);
        }

        private void OnTargetInfoMessage(TargetInfoMessage message)
        {
            Show(message.Info);
        }

        private void OnTargetInfoClearMessage(TargetInfoClearMessage message)
        {
            ClearInfo();
        }

        private void OnUpgradeClicked()
        {
            if (selectedTargetType != TdTargetInfoType.Tower || selectedTargetId <= 0)
            {
                return;
            }

            UpgradeClicked?.Invoke(selectedTargetId);
        }

        private void OnSellClicked()
        {
            if (selectedTargetType != TdTargetInfoType.Tower || selectedTargetId <= 0)
            {
                return;
            }

            SellClicked?.Invoke(selectedTargetId);
        }
    }
}
