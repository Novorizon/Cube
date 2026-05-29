using System;
using System.Collections;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BattleHudController : UIPanel
    {
        private const string DefaultFontAssetPath = "Assets/Arts/Font/NotoSansSC-Regular SDF.asset";
        private const string GoldIconAssetPath = "Assets/Arts/UI/Icons/Item/Gold.png";

        private static TMP_FontAsset defaultFontAsset;
        private static Sprite goldIconSprite;

        [SerializeField]
        private StatusPanel statusPanel;

        [SerializeField]
        private BuildTowerPanel buildTowerPanel;

        [SerializeField]
        private ItemPanel itemPanel;

        [SerializeField]
        private InfoPanel targetInfoPanel;

        [SerializeField]
        private SkillPanel skillPanel;

        [SerializeField]
        private BattleControlPanel battleControlPanel;

        [SerializeField]
        private MiniMapPanel miniMapPanel;

        [SerializeField]
        private GameObject settingsDialog;

        [SerializeField]
        private Button settingsLanguageButton;

        [SerializeField]
        private Button settingsSoundButton;

        [SerializeField]
        private Button settingsRestartButton;

        [SerializeField]
        private Button settingsMainMenuButton;

        [SerializeField]
        private Button settingsCloseButton;

        [SerializeField]
        private Button settingsBlockerButton;

        [SerializeField]
        private TMP_Text settingsSoundText;

        private Subscriber subscriber;
        private GameObject battleResultDialog;
        private TMP_Text battleResultTitleText;
        private TMP_Text battleResultMapText;
        private TMP_Text battleResultReasonText;
        private TMP_Text battleResultRewardText;
        private Button battleResultNextButton;
        private Button battleResultRestartButton;
        private Button battleResultMenuButton;
        private bool soundMuted;

        public event Action<int> TowerBuildClicked;
        public event Action<int> SkillClicked;
        public event Action<int> TowerUpgradeClicked;
        public event Action<int> TowerSellClicked;
        public event Action<TdTargetRuntimeInfo> TowerUpgradeTargetClicked;
        public event Action<TdTargetRuntimeInfo> TowerSellTargetClicked;
        public event Action<float> SpeedChanged;
        public event Action<bool> AutoNextWaveChanged;
        public event Action<int> ItemClicked;

        protected override void OnCreate()
        {
            ResolveMissingReferences();
            InitializePanels();
            RegisterEvents();
        }

        private void ResolveMissingReferences()
        {
            if (statusPanel == null)
            {
                statusPanel = GetComponentInChildren<StatusPanel>(true);
            }

            if (buildTowerPanel == null)
            {
                buildTowerPanel = GetComponentInChildren<BuildTowerPanel>(true);
            }

            if (itemPanel == null)
            {
                itemPanel = GetComponentInChildren<ItemPanel>(true);
            }

            if (targetInfoPanel == null)
            {
                targetInfoPanel = GetComponentInChildren<InfoPanel>(true);
            }

            if (skillPanel == null)
            {
                skillPanel = GetComponentInChildren<SkillPanel>(true);
            }

            if (battleControlPanel == null)
            {
                battleControlPanel = GetComponentInChildren<BattleControlPanel>(true);
            }

            if (miniMapPanel == null)
            {
                miniMapPanel = GetComponentInChildren<MiniMapPanel>(true);
            }

            ResolveSettingsDialogReferences();
        }

        protected override void OnDestroyed()
        {
            UnregisterEvents();
        }

        public void InitializePanels()
        {
            if (statusPanel != null)
            {
                statusPanel.RefreshAll();
            }

            if (buildTowerPanel != null)
            {
                buildTowerPanel.Initialize();
            }

            if (skillPanel != null)
            {
                skillPanel.Initialize();
            }

            if (itemPanel != null)
            {
                itemPanel.Initialize();
            }

            if (targetInfoPanel != null)
            {
                targetInfoPanel.Initialize();
            }

            HideSettingsDialog();
        }

        private void ResolveSettingsDialogReferences()
        {
            if (settingsDialog == null)
            {
                Transform dialog = FindChildByName(transform, "SettingsDialog");
                if (dialog != null)
                {
                    settingsDialog = dialog.gameObject;
                }
            }

            if (settingsDialog == null)
            {
                return;
            }

            Transform root = settingsDialog.transform;

            if (settingsLanguageButton == null)
            {
                settingsLanguageButton = FindButton(root, "LanguageButton");
            }

            if (settingsSoundButton == null)
            {
                settingsSoundButton = FindButton(root, "SoundButton");
            }

            if (settingsRestartButton == null)
            {
                settingsRestartButton = FindButton(root, "RestartButton");
            }

            if (settingsRestartButton == null)
            {
                settingsRestartButton = FindButton(root, "EndBattleButton");
            }

            if (settingsMainMenuButton == null)
            {
                settingsMainMenuButton = FindButton(root, "MainMenuButton");
            }

            if (settingsCloseButton == null)
            {
                settingsCloseButton = FindButton(root, "CloseButton");
            }

            if (settingsBlockerButton == null)
            {
                settingsBlockerButton = settingsDialog.GetComponent<Button>();
            }

            if (settingsSoundText == null && settingsSoundButton != null)
            {
                settingsSoundText = settingsSoundButton.GetComponentInChildren<TMP_Text>(true);
            }
        }

        private void RegisterSettingsEvents()
        {
            ResolveSettingsDialogReferences();
            UnregisterSettingsEvents();

            if (settingsLanguageButton != null)
            {
                settingsLanguageButton.onClick.AddListener(OnSettingsLanguageClicked);
            }

            if (settingsSoundButton != null)
            {
                settingsSoundButton.onClick.AddListener(OnSettingsSoundClicked);
            }

            if (settingsRestartButton != null)
            {
                settingsRestartButton.onClick.AddListener(OnSettingsRestartClicked);
            }

            if (settingsMainMenuButton != null)
            {
                settingsMainMenuButton.onClick.AddListener(OnSettingsMainMenuClicked);
            }

            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.AddListener(HideSettingsDialog);
            }

            if (settingsBlockerButton != null)
            {
                settingsBlockerButton.onClick.AddListener(HideSettingsDialog);
            }
        }

        private void UnregisterSettingsEvents()
        {
            if (settingsLanguageButton != null)
            {
                settingsLanguageButton.onClick.RemoveListener(OnSettingsLanguageClicked);
            }

            if (settingsSoundButton != null)
            {
                settingsSoundButton.onClick.RemoveListener(OnSettingsSoundClicked);
            }

            if (settingsRestartButton != null)
            {
                settingsRestartButton.onClick.RemoveListener(OnSettingsRestartClicked);
            }

            if (settingsMainMenuButton != null)
            {
                settingsMainMenuButton.onClick.RemoveListener(OnSettingsMainMenuClicked);
            }

            if (settingsCloseButton != null)
            {
                settingsCloseButton.onClick.RemoveListener(HideSettingsDialog);
            }

            if (settingsBlockerButton != null)
            {
                settingsBlockerButton.onClick.RemoveListener(HideSettingsDialog);
            }
        }

        private void RegisterEvents()
        {
            if (buildTowerPanel != null)
            {
                buildTowerPanel.TowerClicked += OnTowerBuildClicked;
            }

            if (skillPanel != null)
            {
                skillPanel.SkillClicked += OnSkillClicked;
            }

            if (targetInfoPanel != null)
            {
                targetInfoPanel.UpgradeClicked += OnTowerUpgradeClicked;
                targetInfoPanel.SellClicked += OnTowerSellClicked;
                targetInfoPanel.UpgradeTargetClicked += OnTowerUpgradeTargetClicked;
                targetInfoPanel.SellTargetClicked += OnTowerSellTargetClicked;
            }

            if (battleControlPanel != null)
            {
                battleControlPanel.SpeedChanged += OnSpeedChanged;
                battleControlPanel.AutoNextWaveChanged += OnAutoNextWaveChanged;
                battleControlPanel.SettingClicked += OnSettingClicked;
            }

            if (itemPanel != null)
            {
                itemPanel.ItemClicked += OnItemClicked;
            }

            RegisterSettingsEvents();

            subscriber?.Clear();
            subscriber = new Subscriber();
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, GoldsMessage>(BattleMessageTopic.GoldChanged, OnGoldChanged));
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, BaseLifeMessage>(BattleMessageTopic.BaseLifeChanged, OnBaseLifeChanged));
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, WaveMessage>(BattleMessageTopic.WaveChanged, OnWaveChanged));
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, BattleEndedMessage>(BattleMessageTopic.BattleEnded, OnBattleEnded));
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, GoldFlyMessage>(BattleMessageTopic.GoldFlyRequested, OnGoldFlyRequested));
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, ItemFlyMessage>(BattleMessageTopic.ItemFlyRequested, OnItemFlyRequested));
        }

        private void UnregisterEvents()
        {
            if (buildTowerPanel != null)
            {
                buildTowerPanel.TowerClicked -= OnTowerBuildClicked;
            }

            if (skillPanel != null)
            {
                skillPanel.SkillClicked -= OnSkillClicked;
            }

            if (targetInfoPanel != null)
            {
                targetInfoPanel.UpgradeClicked -= OnTowerUpgradeClicked;
                targetInfoPanel.SellClicked -= OnTowerSellClicked;
                targetInfoPanel.UpgradeTargetClicked -= OnTowerUpgradeTargetClicked;
                targetInfoPanel.SellTargetClicked -= OnTowerSellTargetClicked;
            }

            if (battleControlPanel != null)
            {
                battleControlPanel.SpeedChanged -= OnSpeedChanged;
                battleControlPanel.AutoNextWaveChanged -= OnAutoNextWaveChanged;
                battleControlPanel.SettingClicked -= OnSettingClicked;
            }

            if (itemPanel != null)
            {
                itemPanel.ItemClicked -= OnItemClicked;
            }

            UnregisterSettingsEvents();

            if (subscriber != null)
            {
                subscriber.Clear();
                subscriber = null;
            }

            TowerBuildClicked = null;
            SkillClicked = null;
            TowerUpgradeClicked = null;
            TowerSellClicked = null;
            TowerUpgradeTargetClicked = null;
            TowerSellTargetClicked = null;
            SpeedChanged = null;
            AutoNextWaveChanged = null;
            ItemClicked = null;
        }

        public void SetBaseLife(int current, int max)
        {
            statusPanel?.SetBaseLife(current, max);
        }

        public void SetGold(int gold)
        {
            statusPanel?.SetGold(gold);
        }

        public void SetWave(int currentWave, int totalWave)
        {
            statusPanel?.SetWave(currentWave, totalWave);
        }

        public void SetEnemyCount(int alive, int total)
        {
            statusPanel?.SetEnemyCount(alive, total);
        }

        public void ShowTargetInfo(TdTargetRuntimeInfo info)
        {
            targetInfoPanel?.Show(info);
        }

        public void ClearTargetInfo()
        {
            targetInfoPanel?.ClearInfo();
        }

        public void ShowTowerInfo(TdTowerRuntimeInfo info)
        {
            targetInfoPanel?.ShowTower(info);
        }

        public void HideTowerInfo()
        {
            ClearTargetInfo();
        }

        public void SetSkillCount(int skillId, int count)
        {
            skillPanel?.SetSkillCount(skillId, count);
        }

        public void SetItemCount(int itemId, int count)
        {
            itemPanel?.SetItemCount(itemId, count);
        }

        public void SetMiniMapBounds(Vector2 min, Vector2 max)
        {
            miniMapPanel?.SetMapBounds(min, max);
        }

        public void ClearMiniMap()
        {
            miniMapPanel?.Clear();
        }

        public void AddMiniMapIcon(Vector2 mapPosition, MiniMapIconType type)
        {
            miniMapPanel?.AddIcon(mapPosition, type);
        }

        private void OnTowerBuildClicked(int towerId)
        {
            TowerBuildClicked?.Invoke(towerId);
        }

        private void OnSkillClicked(int skillId)
        {
            SkillClicked?.Invoke(skillId);
        }

        private void OnTowerUpgradeClicked(int towerId)
        {
            TowerUpgradeClicked?.Invoke(towerId);
        }

        private void OnTowerSellClicked(int towerId)
        {
            TowerSellClicked?.Invoke(towerId);
        }

        private void OnTowerUpgradeTargetClicked(TdTargetRuntimeInfo info)
        {
            TowerUpgradeTargetClicked?.Invoke(info);
        }

        private void OnTowerSellTargetClicked(TdTargetRuntimeInfo info)
        {
            TowerSellTargetClicked?.Invoke(info);
        }

        private void OnSpeedChanged(float speed)
        {
            SpeedChanged?.Invoke(speed);
        }

        private void OnAutoNextWaveChanged(bool value)
        {
            AutoNextWaveChanged?.Invoke(value);
        }

        private void OnSettingClicked()
        {
            ShowSettingsDialog();
        }

        private void OnItemClicked(int itemId)
        {
            ItemClicked?.Invoke(itemId);
        }

        private void OnGoldChanged(GoldsMessage message)
        {
            if (message == null)
            {
                return;
            }

            statusPanel?.SetGold(message.Gold);
        }

        private void OnBaseLifeChanged(BaseLifeMessage message)
        {
            if (message == null)
            {
                return;
            }

            statusPanel?.SetBaseLife(message.CurrentLife, message.MaxLife);
        }

        private void OnWaveChanged(WaveMessage message)
        {
            statusPanel?.SetWaveState(message);
        }

        private void OnBattleEnded(BattleEndedMessage message)
        {
            ShowBattleResultDialog(message);
        }

        private void OnGoldFlyRequested(GoldFlyMessage message)
        {
            if (message == null || message.Count <= 0 || statusPanel == null)
            {
                return;
            }

            RectTransform root = transform as RectTransform;
            RectTransform target = statusPanel.GoldAnchor;
            if (root == null || target == null)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera uiCamera = GetCanvasCamera(canvas);
            Camera worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }

            Vector2 startScreenPosition = worldCamera.WorldToScreenPoint(message.WorldPosition);
            Vector2 endScreenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, startScreenPosition, uiCamera, out Vector2 startLocalPosition))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, endScreenPosition, uiCamera, out Vector2 endLocalPosition))
            {
                return;
            }

            StartCoroutine(PlayGoldFlyAsync(root, startLocalPosition, endLocalPosition, message.Count));
        }

        private void OnItemFlyRequested(ItemFlyMessage message)
        {
            if (message == null || message.ItemId <= 0 || message.Count <= 0)
            {
                return;
            }

            if (message.ItemId == ItemIds.Gold)
            {
                OnGoldFlyRequested(new GoldFlyMessage
                {
                    WorldPosition = message.WorldPosition,
                    Count = message.Count
                });
                return;
            }

            RectTransform root = transform as RectTransform;
            RectTransform target = GetItemFlyTarget(message.ItemId);
            if (root == null || target == null)
            {
                return;
            }

            Canvas canvas = GetComponentInParent<Canvas>();
            Camera uiCamera = GetCanvasCamera(canvas);
            Camera worldCamera = Camera.main;
            if (worldCamera == null)
            {
                return;
            }

            Vector2 startScreenPosition = worldCamera.WorldToScreenPoint(message.WorldPosition);
            Vector2 endScreenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, startScreenPosition, uiCamera, out Vector2 startLocalPosition))
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, endScreenPosition, uiCamera, out Vector2 endLocalPosition))
            {
                return;
            }

            Sprite icon = GetItemIconSprite(message.ItemId);
            StartCoroutine(PlayItemFlyAsync(root, startLocalPosition, endLocalPosition, message.Count, icon, target));
        }

        private IEnumerator PlayGoldFlyAsync(RectTransform root, Vector2 startPosition, Vector2 endPosition, int count)
        {
            GameObject iconObject = CreateRectObject("GoldFlyIcon", root);
            RectTransform iconRect = iconObject.transform as RectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(34f, 34f);
            iconRect.anchoredPosition = startPosition;

            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.sprite = GetGoldIconSprite();
            iconImage.color = new Color(1f, 0.93f, 0.32f, 1f);

            TMP_Text countText = null;
            if (count > 1)
            {
                countText = CreateText("Count", iconObject.transform, 18, FontStyles.Bold, TextAlignmentOptions.Center);
                countText.text = $"+{count}";
                countText.raycastTarget = false;
                countText.color = new Color(1f, 0.96f, 0.55f, 1f);
                SetRect(countText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -15f), new Vector2(80f, 24f));
            }

            Vector2 controlPosition = (startPosition + endPosition) * 0.5f + new Vector2(0f, 95f);
            float duration = 0.62f;
            float elapsed = 0f;

            while (elapsed < duration && iconRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                iconRect.anchoredPosition = EvaluateQuadraticBezier(startPosition, controlPosition, endPosition, eased);
                iconRect.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.72f, eased);

                Color color = iconImage.color;
                color.a = Mathf.Lerp(1f, 0.15f, Mathf.Clamp01((t - 0.72f) / 0.28f));
                iconImage.color = color;
                if (countText != null)
                {
                    countText.alpha = color.a;
                }

                yield return null;
            }

            Destroy(iconObject);
            StartCoroutine(PunchGoldAnchorAsync());
        }

        private IEnumerator PlayItemFlyAsync(RectTransform root, Vector2 startPosition, Vector2 endPosition, int count, Sprite icon, RectTransform target)
        {
            GameObject iconObject = CreateRectObject("ItemFlyIcon", root);
            RectTransform iconRect = iconObject.transform as RectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(34f, 34f);
            iconRect.anchoredPosition = startPosition;

            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.sprite = icon;
            iconImage.color = icon == null ? new Color(0.72f, 0.92f, 1f, 1f) : Color.white;

            TMP_Text countText = null;
            if (count > 1)
            {
                countText = CreateText("Count", iconObject.transform, 18, FontStyles.Bold, TextAlignmentOptions.Center);
                countText.text = $"+{count}";
                countText.raycastTarget = false;
                countText.color = new Color(0.85f, 0.96f, 1f, 1f);
                SetRect(countText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -15f), new Vector2(80f, 24f));
            }

            Vector2 controlPosition = (startPosition + endPosition) * 0.5f + new Vector2(0f, 95f);
            float duration = 0.62f;
            float elapsed = 0f;

            while (elapsed < duration && iconRect != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                iconRect.anchoredPosition = EvaluateQuadraticBezier(startPosition, controlPosition, endPosition, eased);
                iconRect.localScale = Vector3.one * Mathf.Lerp(1.15f, 0.72f, eased);

                Color color = iconImage.color;
                color.a = Mathf.Lerp(1f, 0.15f, Mathf.Clamp01((t - 0.72f) / 0.28f));
                iconImage.color = color;
                if (countText != null)
                {
                    countText.alpha = color.a;
                }

                yield return null;
            }

            Destroy(iconObject);
            StartCoroutine(PunchRectAsync(target));
        }

        private IEnumerator PunchGoldAnchorAsync()
        {
            RectTransform target = statusPanel != null ? statusPanel.GoldAnchor : null;
            if (target == null)
            {
                yield break;
            }

            Vector3 originalScale = target.localScale;
            float duration = 0.16f;
            float elapsed = 0f;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                target.localScale = originalScale * scale;
                yield return null;
            }

            if (target != null)
            {
                target.localScale = originalScale;
            }
        }

        private IEnumerator PunchRectAsync(RectTransform target)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 originalScale = target.localScale;
            float duration = 0.16f;
            float elapsed = 0f;

            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
                target.localScale = originalScale * scale;
                yield return null;
            }

            if (target != null)
            {
                target.localScale = originalScale;
            }
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * end;
        }

        private static Camera GetCanvasCamera(Canvas canvas)
        {
            if (canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return null;
            }

            return canvas.worldCamera;
        }

        private static Sprite GetGoldIconSprite()
        {
            if (goldIconSprite != null)
            {
                return goldIconSprite;
            }

            goldIconSprite = ResourceManager.Instance.LoadAsset<Sprite>(GoldIconAssetPath);
            return goldIconSprite;
        }

        private RectTransform GetItemFlyTarget(int itemId)
        {
            if (skillPanel != null && skillPanel.TryGetTargetForItem(itemId, out RectTransform skillTarget))
            {
                return skillTarget;
            }

            if (itemPanel != null && itemPanel.TryGetSlotTransform(itemId, out RectTransform itemTarget))
            {
                return itemTarget;
            }

            return null;
        }

        private Sprite GetItemIconSprite(int itemId)
        {
            if (DataManager.Instance.Item == null || !DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) || config == null)
            {
                return null;
            }

            if (string.IsNullOrWhiteSpace(config.IconLocation) || !config.IconLocation.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return null;
            }

            return ResourceManager.Instance.LoadAsset<Sprite>(config.IconLocation);
        }

        private void ShowBattleResultDialog(BattleEndedMessage message)
        {
            EnsureBattleResultDialog();

            if (battleResultDialog == null)
            {
                return;
            }

            bool victory = message != null && message.Victory;
            battleResultTitleText.text = victory ? "战斗胜利" : "战斗失败";
            battleResultMapText.text = message != null && !string.IsNullOrWhiteSpace(message.MapName) ? message.MapName : "当前关卡";
            battleResultReasonText.text = message != null && !string.IsNullOrWhiteSpace(message.Reason) ? message.Reason : string.Empty;
            battleResultRewardText.text = BuildRewardText(message);
            RefreshBattleResultButtons(message, victory);
            battleResultDialog.SetActive(true);
        }

        private void RefreshBattleResultButtons(BattleEndedMessage message, bool victory)
        {
            int mapId = message != null ? message.MapId : 0;
            bool hasNextMap = victory && MapManager.Instance.HasNextMap(mapId);

            if (battleResultNextButton != null)
            {
                battleResultNextButton.gameObject.SetActive(hasNextMap);
            }

            if (battleResultRestartButton != null)
            {
                RectTransform restartRect = battleResultRestartButton.GetComponent<RectTransform>();
                restartRect.anchoredPosition = hasNextMap ? new Vector2(0f, 34f) : new Vector2(-86f, 34f);
            }

            if (battleResultMenuButton != null)
            {
                RectTransform menuRect = battleResultMenuButton.GetComponent<RectTransform>();
                menuRect.anchoredPosition = hasNextMap ? new Vector2(152f, 34f) : new Vector2(86f, 34f);
            }
        }
        private string BuildRewardText(BattleEndedMessage message)
        {
            if (message == null || !message.Victory)
            {
                return "奖励：无";
            }

            if (message.Reward == null || message.Reward.OuterItemMap == null || message.Reward.OuterItemMap.Count == 0)
            {
                return "奖励：无";
            }

            System.Text.StringBuilder builder = new System.Text.StringBuilder("奖励：");
            bool first = true;

            foreach (System.Collections.Generic.KeyValuePair<int, int> pair in message.Reward.OuterItemMap)
            {
                if (!first)
                {
                    builder.Append("，");
                }

                builder.Append(pair.Key);
                builder.Append(" x");
                builder.Append(pair.Value);
                first = false;
            }

            return builder.ToString();
        }

        private void EnsureBattleResultDialog()
        {
            if (battleResultDialog != null)
            {
                return;
            }

            RectTransform parent = transform as RectTransform;
            battleResultDialog = CreateRectObject("BattleResultDialog", parent);
            RectTransform dialogRect = battleResultDialog.transform as RectTransform;
            Stretch(dialogRect);

            Image blockerImage = battleResultDialog.AddComponent<Image>();
            blockerImage.color = new Color(0f, 0f, 0f, 0.55f);

            GameObject panel = CreateRectObject("Panel", dialogRect);
            RectTransform panelRect = panel.transform as RectTransform;
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(460f, 300f);
            panelRect.anchoredPosition = Vector2.zero;

            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.98f, 0.9f, 0.72f, 0.98f);

            battleResultTitleText = CreateText("Title", panelRect, 34, FontStyles.Bold, TextAlignmentOptions.Center);
            SetRect(battleResultTitleText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -42f), new Vector2(-40f, 54f));

            battleResultMapText = CreateText("Map", panelRect, 22, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRect(battleResultMapText.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -88f), new Vector2(-48f, 34f));

            battleResultReasonText = CreateText("Reason", panelRect, 20, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRect(battleResultReasonText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(-56f, 70f));

            battleResultRewardText = CreateText("Reward", panelRect, 20, FontStyles.Normal, TextAlignmentOptions.Center);
            SetRect(battleResultRewardText.rectTransform, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -54f), new Vector2(-56f, 44f));

            battleResultNextButton = CreateButton("NextButton", panelRect, "下一关");
            SetRect(battleResultNextButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-152f, 34f), new Vector2(132f, 46f));
            battleResultNextButton.onClick.AddListener(OnBattleResultNextClicked);

            battleResultRestartButton = CreateButton("RestartButton", panelRect, "重新开始");
            SetRect(battleResultRestartButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(132f, 46f));
            battleResultRestartButton.onClick.AddListener(OnBattleResultRestartClicked);

            battleResultMenuButton = CreateButton("MenuButton", panelRect, "主菜单");
            SetRect(battleResultMenuButton.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(152f, 34f), new Vector2(132f, 46f));
            battleResultMenuButton.onClick.AddListener(OnBattleResultMenuClicked);

            battleResultDialog.SetActive(false);
        }

        private void OnBattleResultNextClicked()
        {
            BattleEndedMessage message = BattleFlowManager.Instance.LastEndMessage;
            int mapId = message != null ? message.MapId : 0;
            MapManager.Instance.LoadNextMap(mapId);
        }

        private void OnBattleResultRestartClicked()
        {
            MapManager.Instance.RestartCurrentMap();
        }

        private void OnBattleResultMenuClicked()
        {
            MapManager.Instance.ReturnToMainMenu();
        }

        private void ShowSettingsDialog()
        {
            ResolveSettingsDialogReferences();
            if (settingsDialog != null)
            {
                RefreshSettingsSoundText();
                settingsDialog.transform.SetAsLastSibling();
                settingsDialog.SetActive(true);
            }
        }

        private void HideSettingsDialog()
        {
            if (settingsDialog != null)
            {
                settingsDialog.SetActive(false);
            }
        }

        private void OnSettingsLanguageClicked()
        {
            Toast.Info("语言设置暂未开放");
        }

        private void OnSettingsSoundClicked()
        {
            soundMuted = !soundMuted;
            AudioListener.volume = soundMuted ? 0f : 1f;
            RefreshSettingsSoundText();
        }

        private void RefreshSettingsSoundText()
        {
            soundMuted = AudioListener.volume <= 0.01f;
            if (settingsSoundText != null)
            {
                settingsSoundText.text = soundMuted ? "声音：关" : "声音：开";
            }
        }

        private void OnSettingsRestartClicked()
        {
            HideSettingsDialog();
            if (MapManager.IsCreated)
            {
                MapManager.Instance.RestartCurrentMap();
            }
        }

        private void OnSettingsMainMenuClicked()
        {
            HideSettingsDialog();
            if (MapManager.IsCreated)
            {
                MapManager.Instance.ReturnToMainMenu();
            }
        }

        private void HideBattleResultDialog()
        {
            if (battleResultDialog != null)
            {
                battleResultDialog.SetActive(false);
            }
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static Button FindButton(Transform root, string name)
        {
            Transform child = FindChildByName(root, name);
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static Transform FindChildByName(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform match = FindChildByName(root.GetChild(i), name);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }

        private static TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject instance = CreateRectObject(name, parent);
            TextMeshProUGUI text = instance.AddComponent<TextMeshProUGUI>();
            TMP_FontAsset fontAsset = GetDefaultFontAsset();
            if (fontAsset != null)
            {
                text.font = fontAsset;
            }

            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = new Color(0.25f, 0.13f, 0.05f, 1f);
            text.enableWordWrapping = true;
            return text;
        }

        private static TMP_FontAsset GetDefaultFontAsset()
        {
            if (defaultFontAsset != null)
            {
                return defaultFontAsset;
            }

            defaultFontAsset = ResourceManager.Instance.LoadAsset<TMP_FontAsset>(DefaultFontAssetPath);
            return defaultFontAsset;
        }

        private static Button CreateButton(string name, Transform parent, string label)
        {
            GameObject instance = CreateRectObject(name, parent);
            Image image = instance.AddComponent<Image>();
            image.color = new Color(0.64f, 0.24f, 0.08f, 1f);

            Button button = instance.AddComponent<Button>();
            button.targetGraphic = image;

            TMP_Text text = CreateText("Text", instance.transform, 22, FontStyles.Bold, TextAlignmentOptions.Center);
            text.text = label;
            text.color = Color.white;
            Stretch(text.rectTransform);

            return button;
        }

        private static void Stretch(RectTransform rectTransform)
        {
            SetRect(rectTransform, Vector2.zero, Vector2.one, new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
        }

        private static void SetRect(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.pivot = pivot;
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = sizeDelta;
        }
    }
}
