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
        [SerializeField] private Button closeButton;
        [SerializeField] private Button cameraModeButton;
        [SerializeField] private TMP_Text cameraModeButtonText;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            BindStaticButtons();
            LocalizationManager.LanguageChanged += RefreshCameraModeButton;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= RefreshCameraModeButton;
        }

        protected override void OnOpen(object args)
        {
            RefreshCameraModeButton();
        }

        private void BindStaticButtons()
        {
            BindButton(soundButton, ShowSoundPanel, nameof(soundButton));
            BindButton(languageButton, ShowLanguagePanel, nameof(languageButton));
            BindButton(saveButton, ShowSavePanel, nameof(saveButton));
            BindButton(gmButton, ShowGmPanel, nameof(gmButton));

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
    }
}
