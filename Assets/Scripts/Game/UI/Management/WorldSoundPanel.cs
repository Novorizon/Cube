using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldSoundPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/SoundPanel.prefab";

        private const string VolumeKey = "World.Sound.Volume";
        private const float VolumeStep = 0.1f;

        [SerializeField] private Button closeButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private TMP_Text volumeText;
        [SerializeField] private TMP_Text muteButtonText;

        protected override void OnCreate()
        {
            Bind(closeButton, CloseSelf, nameof(closeButton));
            Bind(decreaseButton, () => ChangeVolume(-VolumeStep), nameof(decreaseButton));
            Bind(increaseButton, () => ChangeVolume(VolumeStep), nameof(increaseButton));
            Bind(muteButton, ToggleMute, nameof(muteButton));
            LocalizationManager.LanguageChanged += Refresh;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        protected override void OnOpen(object args)
        {
            AudioListener.volume = Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, AudioListener.volume));
            Refresh();
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(WorldSoundPanel)}] {fieldName} is not assigned on prefab: {PrefabPath}");
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void ChangeVolume(float delta)
        {
            SetVolume(AudioListener.volume + delta);
        }

        private void ToggleMute()
        {
            SetVolume(AudioListener.volume > 0.01f ? 0f : 1f);
        }

        private void SetVolume(float volume)
        {
            AudioListener.volume = Mathf.Clamp01(volume);
            PlayerPrefs.SetFloat(VolumeKey, AudioListener.volume);
            PlayerPrefs.Save();
            Refresh();
        }

        private void Refresh()
        {
            if (volumeText != null)
            {
                volumeText.text = LocalizationManager.Format(
                    "ui.sound.volume_current",
                    Mathf.RoundToInt(AudioListener.volume * 100f));
            }

            if (muteButtonText != null)
            {
                muteButtonText.text = AudioListener.volume > 0.01f
                    ? LocalizationManager.Get("ui.sound.mute")
                    : LocalizationManager.Get("ui.sound.unmute");
            }
        }

        private void CloseSelf()
        {
            UIManager.Instance.Panels.Hide(PrefabPath);
        }
    }
}
