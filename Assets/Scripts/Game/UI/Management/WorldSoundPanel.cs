using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldSoundPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Menu/SoundPanel.prefab";

        private const float VolumeStep = 0.1f;

        [SerializeField] private Button returnButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button onButton;
        [SerializeField] private Button offButton;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Text volumeText;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            Bind(returnButton, CloseSelf, nameof(returnButton));
            Bind(decreaseButton, DecreaseVolume, nameof(decreaseButton));
            Bind(increaseButton, IncreaseVolume, nameof(increaseButton));
            Bind(onButton, Mute, nameof(onButton));
            Bind(offButton, Unmute, nameof(offButton));
            BindSlider();
            LocalizationManager.LanguageChanged += Refresh;
            GameAudioSettings.VolumeChanged += OnVolumeChanged;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= Refresh;
            GameAudioSettings.VolumeChanged -= OnVolumeChanged;
            Unbind(returnButton, CloseSelf);
            Unbind(decreaseButton, DecreaseVolume);
            Unbind(increaseButton, IncreaseVolume);
            Unbind(onButton, Mute);
            Unbind(offButton, Unmute);
            volumeSlider?.onValueChanged.RemoveListener(OnSliderValueChanged);
        }

        protected override void OnOpen(object args)
        {
            GameAudioSettings.Load();
            Refresh();
        }

        private void BindSlider()
        {
            if (volumeSlider == null)
            {
                Debug.LogError($"[{nameof(WorldSoundPanel)}] volumeSlider is not assigned on prefab: {PrefabPath}");
                return;
            }

            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;
            volumeSlider.onValueChanged.RemoveListener(OnSliderValueChanged);
            volumeSlider.onValueChanged.AddListener(OnSliderValueChanged);
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

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            button?.onClick.RemoveListener(action);
        }

        private void OnSliderValueChanged(float value)
        {
            SetVolume(value);
        }

        private void ChangeVolume(float delta)
        {
            SetVolume(AudioListener.volume + delta);
        }

        private void DecreaseVolume()
        {
            ChangeVolume(-VolumeStep);
        }

        private void IncreaseVolume()
        {
            ChangeVolume(VolumeStep);
        }

        private void Mute()
        {
            GameAudioSettings.Mute();
        }

        private void Unmute()
        {
            GameAudioSettings.Unmute();
        }

        private void SetVolume(float volume)
        {
            GameAudioSettings.SetVolume(volume);
        }

        private void OnVolumeChanged(float volume)
        {
            Refresh();
        }

        private void Refresh()
        {
            if (volumeSlider != null)
            {
                volumeSlider.SetValueWithoutNotify(AudioListener.volume);
            }

            if (volumeText != null)
            {
                volumeText.text = LocalizationManager.Format(
                    "ui.sound.volume_current",
                    Mathf.RoundToInt(AudioListener.volume * 100f));
            }

            bool muted = GameAudioSettings.IsMuted;
            SetActive(onButton != null ? onButton.gameObject : null, !muted);
            SetActive(offButton != null ? offButton.gameObject : null, muted);
        }

        private void CloseSelf()
        {
            if (!UIManager.Instance.Panels.PopStack(WorldMenuPanel.SettingsStackGroupId))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private static void SetActive(GameObject target, bool active)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }

    }
}
