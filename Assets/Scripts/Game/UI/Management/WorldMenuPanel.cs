using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldMenuPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Menu/MenuPanel.prefab";
        public const string SettingsStackGroupId = "SystemSettings";

        [SerializeField] private Button soundButton;
        [SerializeField] private Button languageButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private Button gmButton;
        [SerializeField] private Button retreatButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Button cameraModeButton;
        [SerializeField] private TMP_Text cameraModeButtonText;

        private GameObject retreatConfirmRoot;
        private TMP_Text retreatConfirmMessage;
        private Button confirmRetreatButton;
        private Button cancelRetreatButton;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            CreateRetreatConfirmation();
            BindStaticButtons();
            LocalizationManager.LanguageChanged += RefreshView;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= RefreshView;
            retreatButton?.onClick.RemoveListener(ShowRetreatConfirmation);
            confirmRetreatButton?.onClick.RemoveListener(ConfirmRetreat);
            cancelRetreatButton?.onClick.RemoveListener(HideRetreatConfirmation);
        }

        protected override void OnOpen(object args)
        {
            bool isBattleRunning = BattleFlowManager.Instance.IsRunning;
            saveButton?.gameObject.SetActive(!isBattleRunning);
            retreatButton?.gameObject.SetActive(isBattleRunning);
            HideRetreatConfirmation();
            RefreshView();
        }

        protected override void OnClose()
        {
            HideRetreatConfirmation();
        }

        private void BindStaticButtons()
        {
            BindButton(soundButton, ShowSoundPanel, nameof(soundButton));
            BindButton(languageButton, ShowLanguagePanel, nameof(languageButton));
            BindButton(saveButton, ShowSavePanel, nameof(saveButton));
            BindButton(gmButton, ShowGmPanel, nameof(gmButton));
            BindButton(retreatButton, ShowRetreatConfirmation, nameof(retreatButton));

            if (confirmRetreatButton != null)
            {
                confirmRetreatButton.onClick.RemoveListener(ConfirmRetreat);
                confirmRetreatButton.onClick.AddListener(ConfirmRetreat);
            }

            if (cancelRetreatButton != null)
            {
                cancelRetreatButton.onClick.RemoveListener(HideRetreatConfirmation);
                cancelRetreatButton.onClick.AddListener(HideRetreatConfirmation);
            }

            if (closeButton == null)
            {
                Debug.LogError($"[{nameof(WorldMenuPanel)}] Close button is not assigned on prefab: {PrefabPath}");
            }
            else
            {
                closeButton.onClick.RemoveListener(CloseSelf);
                closeButton.onClick.AddListener(CloseSelf);
            }

            if (cameraModeButton == null)
            {
                Debug.LogError($"[{nameof(WorldMenuPanel)}] Camera mode button is not assigned on prefab: {PrefabPath}");
            }
            else
            {
                cameraModeButton.onClick.RemoveListener(ToggleCameraMode);
                cameraModeButton.onClick.AddListener(ToggleCameraMode);
            }
        }

        private void BindButton(Button button, UnityEngine.Events.UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(WorldMenuPanel)}] {fieldName} is not assigned on prefab: {PrefabPath}");
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void ShowSoundPanel()
        {
            ShowPanelAsync(WorldSoundPanel.PrefabPath).Forget();
        }

        private void ShowLanguagePanel()
        {
            ShowPanelAsync(WorldLanguagePanel.PrefabPath).Forget();
        }

        private void ShowSavePanel()
        {
            ShowPanelAsync(WorldSavePanel.PrefabPath).Forget();
        }

        private void ShowGmPanel()
        {
            ShowPanelAsync(WorldGmPanel.PrefabPath).Forget();
        }

        private async System.Threading.Tasks.Task ShowPanelAsync(string prefabPath)
        {
            await UIManager.Instance.Panels.PushStackAsync(SettingsStackGroupId, PrefabPath);
            await UIManager.Instance.Panels.PushStackAsync(SettingsStackGroupId, prefabPath);
        }

        private void CloseSelf()
        {
            if (!UIManager.Instance.Panels.PopStack(SettingsStackGroupId))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private void ToggleCameraMode()
        {
            GameplayController.Instance?.ToggleCameraFollowMode();
            RefreshCameraModeButton();
        }

        private void RefreshView()
        {
            RefreshCameraModeButton();
            string retreatText = Localize("ui.td.menu.retreat", "撤退", "Retreat");
            SetButtonText(retreatButton, retreatText);
            SetText(retreatConfirmMessage, Localize(
                "ui.td.menu.retreat_confirm_message",
                "确认撤退吗？\n撤退将结束当前战斗，且无法获得本场战斗奖励。",
                "Retreat from this battle?\nRetreating ends the current battle and grants no rewards."));
            SetButtonText(confirmRetreatButton, retreatText);
            SetButtonText(cancelRetreatButton, Localize("ui.common.cancel", "取消", "Cancel"));
        }

        private void RefreshCameraModeButton()
        {
            if (cameraModeButtonText == null)
            {
                return;
            }

            CameraFollowMode mode = GameplayController.Instance != null
                ? GameplayController.Instance.CurrentCameraFollowMode
                : CameraFollowMode.FollowPlayer;
            cameraModeButtonText.text = mode == CameraFollowMode.FollowPlayer
                ? LocalizationManager.Get("ui.menu.camera_free")
                : LocalizationManager.Get("ui.menu.follow_role");
        }

        private void ShowRetreatConfirmation()
        {
            if (!BattleFlowManager.Instance.IsRunning || retreatConfirmRoot == null)
            {
                return;
            }

            retreatConfirmRoot.SetActive(true);
            retreatConfirmRoot.transform.SetAsLastSibling();
        }

        private void HideRetreatConfirmation()
        {
            retreatConfirmRoot?.SetActive(false);
        }

        private void ConfirmRetreat()
        {
            if (!BattleFlowManager.Instance.IsRunning)
            {
                HideRetreatConfirmation();
                return;
            }

            HideRetreatConfirmation();
            UIManager.Instance.Panels.Hide(PrefabPath);
            MapManager.Instance.RetreatFromBattle();
        }

        private void CreateRetreatConfirmation()
        {
            if (retreatButton == null || retreatConfirmRoot != null)
            {
                return;
            }

            GameObject overlay = new GameObject(
                "RetreatConfirmation",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            overlay.transform.SetParent(transform, false);
            overlay.layer = gameObject.layer;
            Stretch(overlay.GetComponent<RectTransform>());
            Image overlayImage = overlay.GetComponent<Image>();
            overlayImage.color = new Color(0f, 0f, 0f, 0.58f);
            overlayImage.raycastTarget = true;

            GameObject dialog = new GameObject(
                "Dialog",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            dialog.transform.SetParent(overlay.transform, false);
            dialog.layer = gameObject.layer;
            SetRect(dialog.GetComponent<RectTransform>(), Vector2.zero, new Vector2(420f, 250f));
            Image dialogImage = dialog.GetComponent<Image>();
            Image menuBackground = transform.Find("Background")?.GetComponent<Image>();
            if (menuBackground != null)
            {
                dialogImage.sprite = menuBackground.sprite;
                dialogImage.type = menuBackground.type;
            }

            dialogImage.color = new Color(1f, 0.96f, 0.86f, 1f);
            dialogImage.raycastTarget = true;

            retreatConfirmMessage = CreateDialogText(
                "Message",
                dialog.transform,
                new Vector2(0f, 30f),
                new Vector2(360f, 120f),
                22f);
            confirmRetreatButton = CloneDialogButton(
                "Confirm",
                dialog.transform,
                new Vector2(-100f, -78f));
            cancelRetreatButton = CloneDialogButton(
                "Cancel",
                dialog.transform,
                new Vector2(100f, -78f));

            retreatConfirmRoot = overlay;
            retreatConfirmRoot.SetActive(false);
        }

        private Button CloneDialogButton(string name, Transform parent, Vector2 position)
        {
            GameObject instance = Instantiate(retreatButton.gameObject, parent, false);
            instance.name = name;
            instance.SetActive(true);
            Button button = instance.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            WorldLocalizedText localized = instance.GetComponentInChildren<WorldLocalizedText>(true);
            if (localized != null)
            {
                localized.enabled = false;
            }

            SetRect(instance.transform as RectTransform, position, new Vector2(170f, 54f));
            return button;
        }

        private TMP_Text CreateDialogText(string name, Transform parent, Vector2 position, Vector2 size, float fontSize)
        {
            GameObject instance = new GameObject(
                name,
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(TextMeshProUGUI));
            instance.transform.SetParent(parent, false);
            instance.layer = gameObject.layer;
            SetRect(instance.GetComponent<RectTransform>(), position, size);
            TextMeshProUGUI text = instance.GetComponent<TextMeshProUGUI>();
            if (cameraModeButtonText != null && cameraModeButtonText.font != null)
            {
                text.font = cameraModeButtonText.font;
            }

            text.fontSize = fontSize;
            text.color = new Color(0.2f, 0.15f, 0.1f, 1f);
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            return text;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
        }

        private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = Vector2.one * 0.5f;
            rect.anchorMax = Vector2.one * 0.5f;
            rect.pivot = Vector2.one * 0.5f;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            rect.localScale = Vector3.one;
        }

        private static void SetButtonText(Button button, string value)
        {
            if (button == null)
            {
                return;
            }

            TMP_Text tmpText = button.GetComponentInChildren<TMP_Text>(true);
            if (tmpText != null)
            {
                tmpText.text = value;
                return;
            }

            Text legacyText = button.GetComponentInChildren<Text>(true);
            if (legacyText != null)
            {
                legacyText.text = value;
            }
        }

        private static void SetText(TMP_Text target, string value)
        {
            if (target != null)
            {
                target.text = value;
            }
        }

        private static string Localize(string key, string chinese, string english)
        {
            string fallback = LocalizationManager.CurrentLanguage == LocalizationManager.Chinese
                ? chinese
                : english;
            return LocalizationManager.GetOrFallback(key, fallback);
        }

    }
}
