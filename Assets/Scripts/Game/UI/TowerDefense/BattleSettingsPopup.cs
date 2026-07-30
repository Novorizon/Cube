using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BattleSettingsPopup : UIPopup
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Battle/BattleSettingsPopup.prefab";

        [SerializeField] private Button languageButton;
        [SerializeField] private Button soundButton;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private TMP_Text languageText;
        [SerializeField] private TMP_Text soundText;

        private float timeScaleBeforeOpen = 1f;

        public override bool CloseOnBlockerClick => true;

        protected override void OnCreate()
        {
            Bind(languageButton, ToggleLanguage, nameof(languageButton));
            Bind(soundButton, ToggleSound, nameof(soundButton));
            Bind(restartButton, RestartBattle, nameof(restartButton));
            Bind(mainMenuButton, ReturnToMainMenu, nameof(mainMenuButton));
            Bind(closeButton, CloseSelf, nameof(closeButton));
            LocalizationManager.LanguageChanged += Refresh;
            GameAudioSettings.VolumeChanged += OnVolumeChanged;
        }

        protected override void OnOpen(object args)
        {
            timeScaleBeforeOpen = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            GameAudioSettings.Load();
            Refresh();
        }

        protected override void OnClose()
        {
            if (BattleFlowManager.Instance.LastEndMessage == null)
            {
                Time.timeScale = timeScaleBeforeOpen;
            }
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= Refresh;
            GameAudioSettings.VolumeChanged -= OnVolumeChanged;
            Unbind(languageButton, ToggleLanguage);
            Unbind(soundButton, ToggleSound);
            Unbind(restartButton, RestartBattle);
            Unbind(mainMenuButton, ReturnToMainMenu);
            Unbind(closeButton, CloseSelf);
        }

        private void ToggleLanguage()
        {
            string nextLanguage = LocalizationManager.CurrentLanguage == LocalizationManager.Chinese
                ? LocalizationManager.English
                : LocalizationManager.Chinese;
            LocalizationManager.SetLanguage(nextLanguage);
            Toast.Info(LocalizationManager.Format("ui.language.toast", LocalizationManager.GetLanguageDisplayName(nextLanguage)));
        }

        private void ToggleSound()
        {
            GameAudioSettings.ToggleMute();
        }

        private void RestartBattle()
        {
            CloseSelf();
            MapManager.Instance.RestartCurrentBattleMap();
        }

        private void ReturnToMainMenu()
        {
            CloseSelf();
            MapManager.Instance.ReturnToMainMenu();
        }

        private void CloseSelf()
        {
            UIManager.Instance.Popups.CloseTop(UICloseReason.CloseButton);
        }

        private void Refresh()
        {
            if (languageText != null)
            {
                languageText.text = LocalizationManager.GetLanguageDisplayName(LocalizationManager.CurrentLanguage);
            }

            if (soundText != null)
            {
                soundText.text = GameAudioSettings.IsMuted
                    ? LocalizationManager.Get("ui.td.settings.sound_off")
                    : LocalizationManager.Get("ui.td.settings.sound_on");
            }

        }

        private void OnVolumeChanged(float volume)
        {
            Refresh();
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(BattleSettingsPopup)}] {fieldName} is not assigned on {PrefabPath}.", this);
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            button?.onClick.RemoveListener(action);
        }

    }
}
