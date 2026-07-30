using System;
using System.Collections;
using System.Threading.Tasks;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace Game
{
    public sealed class BattlePage : UIPage
    {
        private const string DefaultFontAssetPath = "Assets/Arts/Font/NotoSansSC-Regular SDF.asset";
        private const string GoldIconAssetPath = "Assets/Arts/UI/Icons/Item/Gold.png";

        private static TMP_FontAsset defaultFontAsset;
        private static Sprite goldIconSprite;

        [SerializeField]
        [FormerlySerializedAs("statusPanel")]
        private TopPanel topPanel;
        [SerializeField] private BuildTowerPanel buildTowerPanel;
        [SerializeField] private ItemPanel itemPanel;
        [SerializeField] private InfoPanel targetInfoPanel;
        [SerializeField] private SkillPanel skillPanel;
        [SerializeField] private BattleControlPanel battleControlPanel;
        [SerializeField] private MiniMapPanel miniMapPanel;

        private UIEmbeddedPanelGroup embeddedPanels;
        private Subscriber subscriber;

        public event Action<int> TowerBuildClicked;
        public event Action<int> SkillClicked;
        public event Action<TdTargetActionRequest> TargetActionClicked;
        public event Action<float> SpeedChanged;
        public event Action<bool> AutoNextWaveChanged;
        public event Action<int> ItemClicked;

        public bool AutoNextWaveEnabled => battleControlPanel != null && battleControlPanel.AutoNextWaveEnabled;

        protected override void OnCreate()
        {
            ValidateReferences();
            embeddedPanels = new UIEmbeddedPanelGroup(
                topPanel,
                buildTowerPanel,
                itemPanel,
                targetInfoPanel,
                skillPanel,
                battleControlPanel,
                miniMapPanel);
            embeddedPanels.Create();
            RegisterPanelEvents();
        }

        protected override void OnOpen(object args)
        {
            embeddedPanels.Open(args);
            RegisterMessages();
        }

        protected override void OnClose()
        {
            StopAllCoroutines();
            ClearMessages();
            // Non-serialized lifecycle state is cleared by an Editor domain reload
            // while UIView.IsOpen can still be restored. Destruction must therefore
            // tolerate a page that is closing before OnCreate rebuilt the group.
            embeddedPanels?.Close();
        }

        protected override void OnDestroyed()
        {
            ClearMessages();
            embeddedPanels?.Close();
            UnregisterPanelEvents();

            TowerBuildClicked = null;
            SkillClicked = null;
            TargetActionClicked = null;
            SpeedChanged = null;
            AutoNextWaveChanged = null;
            ItemClicked = null;
        }

        public void SetBaseLife(int current, int max)
        {
            topPanel?.SetBaseLife(current, max);
        }

        public void SetGold(int gold)
        {
            topPanel?.SetGold(gold);
        }

        public void SetWave(int currentWave, int totalWave)
        {
            topPanel?.SetWave(currentWave, totalWave);
        }

        public void SetEnemyCount(int alive, int total)
        {
            topPanel?.SetEnemyCount(alive, total);
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

        private void RegisterPanelEvents()
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
                targetInfoPanel.TargetActionClicked += OnTargetActionClicked;
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
        }

        private void UnregisterPanelEvents()
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
                targetInfoPanel.TargetActionClicked -= OnTargetActionClicked;
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
        }

        private void RegisterMessages()
        {
            ClearMessages();
            subscriber = new Subscriber();
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, BattleEndedMessage>(BattleMessageTopic.BattleEnded, OnBattleEnded));
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, GoldFlyMessage>(BattleMessageTopic.GoldFlyRequested, OnGoldFlyRequested));
            subscriber.Add(Messager.Instance.Subscribe<BattleMessageTopic, ItemFlyMessage>(BattleMessageTopic.ItemFlyRequested, OnItemFlyRequested));
        }

        private void ClearMessages()
        {
            if (subscriber != null)
            {
                subscriber.Clear();
                subscriber = null;
            }
        }

        private void OnTowerBuildClicked(int towerId) => TowerBuildClicked?.Invoke(towerId);
        private void OnSkillClicked(int skillId) => SkillClicked?.Invoke(skillId);
        private void OnTargetActionClicked(TdTargetActionRequest request) => TargetActionClicked?.Invoke(request);
        private void OnSpeedChanged(float speed) => SpeedChanged?.Invoke(speed);
        private void OnAutoNextWaveChanged(bool value) => AutoNextWaveChanged?.Invoke(value);
        private void OnItemClicked(int itemId) => ItemClicked?.Invoke(itemId);

        private void OnSettingClicked()
        {
            OpenSettingsAsync().Forget();
        }

        private async Task OpenSettingsAsync()
        {
            await UIManager.Instance.Panels.ShowAsync(WorldMenuPanel.PrefabPath);
        }

        private void OnBattleEnded(BattleEndedMessage message)
        {
            OpenBattleResultAsync(message).Forget();
        }

        private async Task OpenBattleResultAsync(BattleEndedMessage message)
        {
            await UIManager.Instance.Popups.OpenAsync(
                BattleResultPopup.PrefabPath,
                message,
                new PopupOptions { Modal = true, SingletonByPath = true, CacheOnClose = false, BlockerAlpha = 0.55f });
        }

        private void OnGoldFlyRequested(GoldFlyMessage message)
        {
            if (message == null || message.Count <= 0 || topPanel == null)
            {
                return;
            }

            PlayFly(message.WorldPosition, topPanel.GoldAnchor, message.Count, GetGoldIconSprite(), true);
        }

        private void OnItemFlyRequested(ItemFlyMessage message)
        {
            if (message == null || message.ItemId <= 0 || message.Count <= 0)
            {
                return;
            }

            if (message.ItemId == ItemIds.Gold)
            {
                OnGoldFlyRequested(new GoldFlyMessage { WorldPosition = message.WorldPosition, Count = message.Count });
                return;
            }

            RectTransform target = GetItemFlyTarget(message.ItemId);
            PlayFly(message.WorldPosition, target, message.Count, GetItemIconSprite(message.ItemId), false);
        }

        private void PlayFly(Vector3 worldPosition, RectTransform target, int count, Sprite icon, bool gold)
        {
            RectTransform root = transform as RectTransform;
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

            Vector2 startScreenPosition = worldCamera.WorldToScreenPoint(worldPosition);
            Vector2 endScreenPosition = RectTransformUtility.WorldToScreenPoint(uiCamera, target.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, startScreenPosition, uiCamera, out Vector2 startLocalPosition) ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, endScreenPosition, uiCamera, out Vector2 endLocalPosition))
            {
                return;
            }

            StartCoroutine(PlayFlyAsync(root, startLocalPosition, endLocalPosition, count, icon, target, gold));
        }

        private IEnumerator PlayFlyAsync(RectTransform root, Vector2 startPosition, Vector2 endPosition, int count, Sprite icon, RectTransform target, bool gold)
        {
            GameObject iconObject = CreateRectObject(gold ? "GoldFlyIcon" : "ItemFlyIcon", root);
            RectTransform iconRect = iconObject.transform as RectTransform;
            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(34f, 34f);
            iconRect.anchoredPosition = startPosition;

            Image iconImage = iconObject.AddComponent<Image>();
            iconImage.raycastTarget = false;
            iconImage.sprite = icon;
            iconImage.color = icon != null ? Color.white : new Color(0.72f, 0.92f, 1f, 1f);

            TMP_Text countText = null;
            if (count > 1)
            {
                countText = CreateText("Count", iconObject.transform, 18, FontStyles.Bold, TextAlignmentOptions.Center);
                countText.text = $"+{count}";
                countText.raycastTarget = false;
                countText.color = gold ? new Color(1f, 0.96f, 0.55f, 1f) : new Color(0.85f, 0.96f, 1f, 1f);
                SetRect(countText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -15f), new Vector2(80f, 24f));
            }

            Vector2 controlPosition = (startPosition + endPosition) * 0.5f + new Vector2(0f, 95f);
            const float duration = 0.62f;
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

        private static IEnumerator PunchRectAsync(RectTransform target)
        {
            if (target == null)
            {
                yield break;
            }

            Vector3 originalScale = target.localScale;
            const float duration = 0.16f;
            float elapsed = 0f;
            while (elapsed < duration && target != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                target.localScale = originalScale * (1f + Mathf.Sin(t * Mathf.PI) * 0.18f);
                yield return null;
            }

            if (target != null)
            {
                target.localScale = originalScale;
            }
        }

        private RectTransform GetItemFlyTarget(int itemId)
        {
            if (skillPanel != null && skillPanel.TryGetTargetForItem(itemId, out RectTransform skillTarget))
            {
                return skillTarget;
            }

            return itemPanel != null && itemPanel.TryGetSlotTransform(itemId, out RectTransform itemTarget) ? itemTarget : null;
        }

        private static Sprite GetItemIconSprite(int itemId)
        {
            if (DataManager.Instance.Item == null || !DataManager.Instance.Item.TryGet(itemId, out ItemConfig config) || config == null ||
                string.IsNullOrWhiteSpace(config.IconLocation) || !config.IconLocation.StartsWith("Assets/", StringComparison.Ordinal))
            {
                return null;
            }

            return ResourceManager.Instance.LoadAsset<Sprite>(config.IconLocation);
        }

        private static Sprite GetGoldIconSprite()
        {
            if (goldIconSprite == null)
            {
                goldIconSprite = ResourceManager.Instance.LoadAsset<Sprite>(GoldIconAssetPath);
            }

            return goldIconSprite;
        }

        private static Camera GetCanvasCamera(Canvas canvas)
        {
            return canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        private static Vector2 EvaluateQuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float t)
        {
            float u = 1f - t;
            return u * u * start + 2f * u * t * control + t * t * end;
        }

        private static GameObject CreateRectObject(string name, Transform parent)
        {
            GameObject instance = new GameObject(name, typeof(RectTransform));
            instance.transform.SetParent(parent, false);
            return instance;
        }

        private static TMP_Text CreateText(string name, Transform parent, float fontSize, FontStyles style, TextAlignmentOptions alignment)
        {
            GameObject instance = CreateRectObject(name, parent);
            TextMeshProUGUI text = instance.AddComponent<TextMeshProUGUI>();
            if (defaultFontAsset == null)
            {
                defaultFontAsset = ResourceManager.Instance.LoadAsset<TMP_FontAsset>(DefaultFontAssetPath);
            }

            if (defaultFontAsset != null)
            {
                text.font = defaultFontAsset;
            }

            text.fontSize = fontSize;
            text.fontStyle = style;
            text.alignment = alignment;
            return text;
        }

        private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private void ValidateReferences()
        {
            Validate(topPanel, nameof(topPanel));
            Validate(buildTowerPanel, nameof(buildTowerPanel));
            Validate(itemPanel, nameof(itemPanel));
            Validate(targetInfoPanel, nameof(targetInfoPanel));
            Validate(skillPanel, nameof(skillPanel));
            Validate(battleControlPanel, nameof(battleControlPanel));

            if (miniMapPanel == null)
            {
                Debug.LogWarning($"[{nameof(BattlePage)}] {nameof(miniMapPanel)} is not assigned; mini map remains disabled.", this);
            }
        }

        private void Validate(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                Debug.LogError($"[{nameof(BattlePage)}] {fieldName} is not assigned on the prefab.", this);
            }
        }
    }
}
