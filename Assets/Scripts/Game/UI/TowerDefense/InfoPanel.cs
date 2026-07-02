using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Serialization;
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
        [FormerlySerializedAs("towerIconImage")]
        private Image targetIconImage;

        [SerializeField]
        [FormerlySerializedAs("towerNameText")]
        private TMP_Text targetNameText;

        [SerializeField]
        private TMP_Text descriptionText;

        [Header("Legacy Texts")]
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

        [Header("Dynamic Info Slots")]
        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private InfoSlotView infoSlotPrefab;

        [Header("Tower Actions")]
        [SerializeField]
        private Button upgradeButton;

        [SerializeField]
        private Button sellButton;

        private readonly Dictionary<string, InfoSlotView> slots = new Dictionary<string, InfoSlotView>();
        private static readonly Dictionary<TdTargetInfoType, Sprite> fallbackIconCache = new Dictionary<TdTargetInfoType, Sprite>();

        private TdTargetInfoType selectedTargetType;
        private int selectedTargetId;
        private TdTargetRuntimeInfo selectedInfo;
        private bool initialized;
        private TargetModelPreview modelPreview;
        private Subscriber subscriber;

        public event Action<int> UpgradeClicked;
        public event Action<int> SellClicked;
        public event Action<TdTargetRuntimeInfo> UpgradeTargetClicked;
        public event Action<TdTargetRuntimeInfo> SellTargetClicked;

        protected override void OnCreate()
        {
            Initialize();
        }

        private void Start()
        {
            //Initialize();
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

            if (modelPreview != null)
            {
                modelPreview.Dispose();
                modelPreview = null;
            }

            ClearInfoSlots();

            UpgradeClicked = null;
            SellClicked = null;
            UpgradeTargetClicked = null;
            SellTargetClicked = null;
        }

        public void Initialize()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            RegisterMessageHandlers();
            EnsureModelPreview();

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
            if (!initialized)
            {
                Initialize();
            }

            selectedTargetType = info.Type;
            selectedTargetId = info.TargetId;
            selectedInfo = info;

            if (targetIconImage != null)
            {
                targetIconImage.sprite = info.Icon != null ? info.Icon : GetFallbackIcon(info.Type);
                targetIconImage.enabled = targetIconImage.sprite != null;
            }

            bool previewVisible = modelPreview != null && modelPreview.Show(info.PreviewPrefabLocation);
            if (previewVisible && targetIconImage != null)
            {
                targetIconImage.enabled = false;
            }

            if (targetNameText != null)
            {
                targetNameText.text = string.IsNullOrEmpty(info.Name) ? EmptyName : info.Name;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;
            }

            UpdateLegacyTexts(info);
            SetInfoSlots(GetInfoSlots(info));
            UpdateActionButtons(info);
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
                PreviewPrefabLocation = info.PreviewPrefabLocation,
                Level = info.Level,
                Attack = info.Attack,
                AttackAdd = info.AttackAdd,
                Range = info.Range,
                AttackInterval = info.AttackInterval,
                UpgradeCost = info.UpgradeCost,
                SellGold = info.SellGold,
                CanUpgrade = info.CanUpgrade,
                CanSell = true,
                InfoSlots = info.InfoSlots
            };

            Show(targetInfo);
        }

        public void SetInfoSlots(IReadOnlyList<TdInfoSlotData> slotDataList)
        {
            ClearInfoSlots();

            if (contentRoot == null || infoSlotPrefab == null)
            {
                return;
            }

            if (slotDataList == null)
            {
                return;
            }

            for (int i = 0; i < slotDataList.Count; i++)
            {
                TdInfoSlotData data = slotDataList[i];

                if (string.IsNullOrEmpty(data.Key))
                {
                    data.Key = string.IsNullOrEmpty(data.Name) ? i.ToString() : data.Name;
                }

                InfoSlotView slot = Instantiate(infoSlotPrefab, contentRoot);
                slot.Init(data);
                slots[data.Key] = slot;
                slot.transform.SetAsLastSibling();
            }
        }

        public void ClearInfo()
        {
            selectedTargetType = TdTargetInfoType.None;
            selectedTargetId = 0;
            selectedInfo = default;

            if (targetIconImage != null)
            {
                targetIconImage.sprite = null;
                targetIconImage.enabled = false;
            }

            if (modelPreview != null)
            {
                modelPreview.Clear();
            }

            if (targetNameText != null)
            {
                targetNameText.text = EmptyName;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.Empty;
            }

            ClearLegacyTexts();
            ClearInfoSlots();
            UpdateActionButtons(default);
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

        private void EnsureModelPreview()
        {
            if (targetIconImage == null || modelPreview != null)
            {
                return;
            }

            modelPreview = new TargetModelPreview();
            modelPreview.Initialize(targetIconImage);
        }

        private IReadOnlyList<TdInfoSlotData> GetInfoSlots(TdTargetRuntimeInfo info)
        {
            if (info.InfoSlots != null && info.InfoSlots.Count > 0)
            {
                return info.InfoSlots;
            }

            return BuildDefaultInfoSlots(info);
        }

        private List<TdInfoSlotData> BuildDefaultInfoSlots(TdTargetRuntimeInfo info)
        {
            List<TdInfoSlotData> result = new List<TdInfoSlotData>();

            AddInfoSlot(result, "level", LocalizationManager.Get("ui.td.info.level"), info.Level > 0 ? info.Level.ToString() : EmptyValue);
            AddInfoSlot(result, "hp", LocalizationManager.Get("ui.td.info.hp"), info.MaxHp > 0 ? $"{Mathf.Max(0, info.CurrentHp)}/{info.MaxHp}" : EmptyValue);
            AddInfoSlot(result, "attack", LocalizationManager.Get("ui.td.info.attack"), info.Attack > 0 ? info.Attack.ToString() : EmptyValue, info.AttackAdd > 0 ? $"+{info.AttackAdd}" : string.Empty);
            AddInfoSlot(result, "range", LocalizationManager.Get("ui.td.info.range"), info.Range > 0f ? $"{info.Range:0.#}" : EmptyValue);
            AddInfoSlot(result, "speed", LocalizationManager.Get("ui.td.info.attack_speed"), info.AttackInterval > 0f ? $"{info.AttackInterval:0.#}s" : EmptyValue);
            AddInfoSlot(result, "upgradeCost", LocalizationManager.Get("ui.td.info.upgrade"), info.CanUpgrade ? info.UpgradeCost.ToString() : EmptyValue);
            AddInfoSlot(result, "sellGold", LocalizationManager.Get("ui.td.info.sell"), info.CanSell ? info.SellGold.ToString() : EmptyValue);

            return result;
        }

        private void AddInfoSlot(List<TdInfoSlotData> result, string key, string name, string value, string addValue = null)
        {
            if (value == EmptyValue && string.IsNullOrEmpty(addValue))
            {
                return;
            }

            result.Add(new TdInfoSlotData(key, name, value, addValue));
        }

        private void ClearInfoSlots()
        {
            slots.Clear();

            if (contentRoot == null)
            {
                return;
            }

            for (int i = contentRoot.childCount - 1; i >= 0; i--)
            {
                Transform child = contentRoot.GetChild(i);
                if (child != null && child.GetComponent<InfoSlotView>() != null)
                {
                    child.gameObject.SetActive(false);
                    Destroy(child.gameObject);
                }
            }
        }

        private void UpdateLegacyTexts(TdTargetRuntimeInfo info)
        {
            if (levelText != null)
            {
                levelText.text = info.Level > 0 ? info.Level.ToString() : EmptyValue;
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
        }

        private void ClearLegacyTexts()
        {
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
        }

        private void UpdateActionButtons(TdTargetRuntimeInfo info)
        {
            bool isTower = info.Type == TdTargetInfoType.Tower && info.TargetId > 0;

            if (upgradeButton != null)
            {
                upgradeButton.gameObject.SetActive(isTower);
                upgradeButton.interactable = isTower && info.CanUpgrade;
            }

            if (sellButton != null)
            {
                sellButton.gameObject.SetActive(isTower);
                sellButton.interactable = isTower && info.CanSell;
            }
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
            UpgradeTargetClicked?.Invoke(selectedInfo);
        }

        private void OnSellClicked()
        {
            if (selectedTargetType != TdTargetInfoType.Tower || selectedTargetId <= 0)
            {
                return;
            }

            SellClicked?.Invoke(selectedTargetId);
            SellTargetClicked?.Invoke(selectedInfo);
        }

        private static Sprite GetFallbackIcon(TdTargetInfoType type)
        {
            if (type == TdTargetInfoType.None)
            {
                return null;
            }

            if (fallbackIconCache.TryGetValue(type, out Sprite sprite))
            {
                return sprite;
            }

            Color color;
            switch (type)
            {
                case TdTargetInfoType.Tower:
                    color = new Color(0.95f, 0.64f, 0.18f, 1f);
                    break;
                case TdTargetInfoType.Npc:
                    color = new Color(0.82f, 0.24f, 0.18f, 1f);
                    break;
                case TdTargetInfoType.Base:
                    color = new Color(0.18f, 0.54f, 0.95f, 1f);
                    break;
                default:
                    color = new Color(0.7f, 0.7f, 0.7f, 1f);
                    break;
            }

            Texture2D texture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            texture.name = $"TargetFallbackIcon_{type}";
            Color clear = new Color(0f, 0f, 0f, 0f);
            Vector2 center = new Vector2(15.5f, 15.5f);

            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    texture.SetPixel(x, y, distance <= 14f ? color : clear);
                }
            }

            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 32f);
            fallbackIconCache[type] = sprite;
            return sprite;
        }
    }
}
