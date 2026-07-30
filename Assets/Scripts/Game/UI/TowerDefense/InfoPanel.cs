using Game.Framework;
using System;
using System.Collections.Generic;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Events;
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
        private TMP_Text levelText;

        [SerializeField]
        private TMP_Text descriptionText;

        [SerializeField]
        private GameObject statusRoot;

        [Header("Dynamic Info Slots")]
        [SerializeField]
        private RectTransform contentRoot;

        [SerializeField]
        private InfoSlotView infoSlotPrefab;

        [SerializeField]
        private InfoSlotView[] initialSlots;

        [Header("Target Actions")]
        [SerializeField]
        private GameObject actionRoot;

        [SerializeField]
        private Button[] actionButtons;

        private readonly Dictionary<string, InfoSlotView> slots = new Dictionary<string, InfoSlotView>();
        private readonly List<InfoSlotView> slotPool = new List<InfoSlotView>();
        private readonly List<TdTargetActionData> visibleActions = new List<TdTargetActionData>();
        private readonly Dictionary<string, Sprite> actionIconCache = new Dictionary<string, Sprite>();
        private static readonly Dictionary<TdTargetInfoType, Sprite> fallbackIconCache = new Dictionary<TdTargetInfoType, Sprite>();

        private TdTargetInfoType selectedTargetType;
        private int selectedTargetId;
        private TdTargetRuntimeInfo selectedInfo;
        private UnityAction[] actionListeners;
        private TargetModelPreview modelPreview;
        private Subscriber subscriber;

        public event Action<TdTargetActionRequest> TargetActionClicked;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.None;

        protected override void OnCreate()
        {
            ResolveAuthoredReferences();
            RegisterInitialSlots();
            EnsureModelPreview();
            BindButtons();
        }

        protected override void OnOpen(object args)
        {
            RegisterMessageHandlers();
            ClearInfo();
        }

        protected override void OnClose()
        {
            ClearSubscriptions();
            modelPreview?.Clear();
        }

        protected override void OnDestroyed()
        {
            ClearSubscriptions();
            UnbindButtons();

            if (modelPreview != null)
            {
                modelPreview.Dispose();
                modelPreview = null;
            }

            DestroyInfoSlots();
            actionIconCache.Clear();
            TargetActionClicked = null;
        }

        [Obsolete("Embedded panel lifecycle initializes InfoPanel automatically.")]
        public void Initialize()
        {
            ClearInfo();
        }

        public void Show(TdTargetRuntimeInfo info)
        {
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

            if (levelText != null)
            {
                bool showLevel = info.Level > 0;
                levelText.gameObject.SetActive(showLevel);
                levelText.text = showLevel
                    ? $"{LocalizationManager.Get("ui.td.info.level")} {info.Level}"
                    : string.Empty;
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(info.Description) ? string.Empty : info.Description;
            }

            statusRoot?.SetActive(false);

            SetInfoSlots(GetInfoSlots(info));
            UpdateActionButtons(info.Actions);
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
            ReleaseInfoSlots();

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

                InfoSlotView slot = AcquireInfoSlot();
                slot.Init(data);
                slots[data.Key] = slot;
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

            if (levelText != null)
            {
                levelText.text = string.Empty;
                levelText.gameObject.SetActive(false);
            }

            if (descriptionText != null)
            {
                descriptionText.text = string.Empty;
            }

            statusRoot?.SetActive(false);

            ReleaseInfoSlots();
            UpdateActionButtons(null);
            // The center information frame is part of the persistent battle HUD.
            // Clearing a selection removes only its content; it must not hide the
            // whole panel/background.
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

        private void ResolveAuthoredReferences()
        {
            if (levelText != null && statusRoot != null)
            {
                return;
            }

            Transform[] descendants = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < descendants.Length; i++)
            {
                Transform descendant = descendants[i];
                if (descendant == null)
                {
                    continue;
                }

                if (levelText == null && string.Equals(descendant.name, "Level", StringComparison.OrdinalIgnoreCase))
                {
                    levelText = descendant.GetComponent<TMP_Text>();
                }

                if (statusRoot == null && string.Equals(descendant.name, "Status", StringComparison.OrdinalIgnoreCase))
                {
                    statusRoot = descendant.gameObject;
                }
            }
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

        private InfoSlotView AcquireInfoSlot()
        {
            for (int i = 0; i < slotPool.Count; i++)
            {
                InfoSlotView pooledSlot = slotPool[i];
                if (pooledSlot != null && !pooledSlot.gameObject.activeSelf)
                {
                    pooledSlot.gameObject.SetActive(true);
                    pooledSlot.transform.SetAsLastSibling();
                    return pooledSlot;
                }
            }

            InfoSlotView instance = Instantiate(infoSlotPrefab, contentRoot);
            slotPool.Add(instance);
            return instance;
        }

        private void RegisterInitialSlots()
        {
            slotPool.Clear();

            if (initialSlots != null)
            {
                for (int i = 0; i < initialSlots.Length; i++)
                {
                    RegisterInitialSlot(initialSlots[i]);
                }
            }

            if (contentRoot != null)
            {
                InfoSlotView[] authoredSlots = contentRoot.GetComponentsInChildren<InfoSlotView>(true);
                for (int i = 0; i < authoredSlots.Length; i++)
                {
                    RegisterInitialSlot(authoredSlots[i]);
                }
            }
        }

        private void RegisterInitialSlot(InfoSlotView slot)
        {
            if (slot != null && !slotPool.Contains(slot))
            {
                slotPool.Add(slot);
            }
        }

        private void ReleaseInfoSlots()
        {
            slots.Clear();
            for (int i = 0; i < slotPool.Count; i++)
            {
                InfoSlotView slot = slotPool[i];
                if (slot != null)
                {
                    slot.Clear();
                }
            }
        }

        private void DestroyInfoSlots()
        {
            slots.Clear();
            for (int i = slotPool.Count - 1; i >= 0; i--)
            {
                if (slotPool[i] != null)
                {
                    Destroy(slotPool[i].gameObject);
                }
            }

            slotPool.Clear();
        }

        private void UpdateActionButtons(IReadOnlyList<TdTargetActionData> actions)
        {
            visibleActions.Clear();
            int buttonCount = actionButtons != null ? actionButtons.Length : 0;
            int actionCount = actions != null ? Mathf.Min(actions.Count, buttonCount) : 0;

            for (int i = 0; i < buttonCount; i++)
            {
                Button button = actionButtons[i];
                if (button == null)
                {
                    continue;
                }

                bool visible = i < actionCount;
                button.gameObject.SetActive(visible);
                if (!visible)
                {
                    continue;
                }

                TdTargetActionData action = actions[i];
                visibleActions.Add(action);
                button.interactable = action.Interactable;
                ApplyActionIcon(button, action.IconLocation);
            }

            if (actions != null && actions.Count > buttonCount)
            {
                Debug.LogWarning($"[{nameof(InfoPanel)}] Action count exceeds authored slots. actions: {actions.Count}, slots: {buttonCount}", this);
            }

            actionRoot?.SetActive(actionCount > 0);
        }

        private void ApplyActionIcon(Button button, string iconLocation)
        {
            if (!(button.targetGraphic is Image image) || string.IsNullOrWhiteSpace(iconLocation))
            {
                return;
            }

            if (!actionIconCache.TryGetValue(iconLocation, out Sprite icon))
            {
                icon = ResourceManager.Instance.LoadAsset<Sprite>(iconLocation);
                if (icon != null)
                {
                    actionIconCache[iconLocation] = icon;
                }
            }

            if (icon != null)
            {
                image.sprite = icon;
                image.enabled = true;
            }
        }

        private void RegisterMessageHandlers()
        {
            ClearSubscriptions();
            subscriber = new Subscriber();

            ISubscription targetInfoChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, TargetInfoMessage>(BattleMessageTopic.TargetInfoChanged, OnTargetInfoMessage);
            ISubscription targetInfoClearedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, TargetInfoClearMessage>(BattleMessageTopic.TargetInfoCleared, OnTargetInfoClearMessage);

            subscriber.Add(targetInfoChangedSubscription);
            subscriber.Add(targetInfoClearedSubscription);
        }

        private void ClearSubscriptions()
        {
            if (subscriber != null)
            {
                subscriber.Clear();
                subscriber = null;
            }
        }

        private void BindButtons()
        {
            UnbindButtons();
            int count = actionButtons != null ? actionButtons.Length : 0;
            actionListeners = new UnityAction[count];
            for (int i = 0; i < count; i++)
            {
                Button button = actionButtons[i];
                if (button == null)
                {
                    continue;
                }

                if (button.targetGraphic != null)
                {
                    button.targetGraphic.raycastTarget = true;
                }

                int buttonIndex = i;
                actionListeners[i] = () => OnActionClicked(buttonIndex);
                button.onClick.AddListener(actionListeners[i]);
            }
        }

        private void UnbindButtons()
        {
            if (actionButtons != null && actionListeners != null)
            {
                int count = Mathf.Min(actionButtons.Length, actionListeners.Length);
                for (int i = 0; i < count; i++)
                {
                    if (actionButtons[i] != null && actionListeners[i] != null)
                    {
                        actionButtons[i].onClick.RemoveListener(actionListeners[i]);
                    }
                }
            }

            actionListeners = null;
        }

        private void OnTargetInfoMessage(TargetInfoMessage message)
        {
            Show(message.Info);
        }

        private void OnTargetInfoClearMessage(TargetInfoClearMessage message)
        {
            ClearInfo();
        }

        private void OnActionClicked(int buttonIndex)
        {
            if (selectedTargetType == TdTargetInfoType.None || selectedTargetId <= 0 ||
                buttonIndex < 0 || buttonIndex >= visibleActions.Count)
            {
                return;
            }

            TdTargetActionData action = visibleActions[buttonIndex];
            if (!action.Interactable)
            {
                return;
            }

            TargetActionClicked?.Invoke(new TdTargetActionRequest
            {
                Action = action,
                Target = selectedInfo
            });
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
