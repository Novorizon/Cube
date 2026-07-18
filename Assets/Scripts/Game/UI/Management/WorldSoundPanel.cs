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

        [SerializeField] private Button closeButton;
        [SerializeField] private Button decreaseButton;
        [SerializeField] private Button increaseButton;
        [SerializeField] private Button muteButton;
        [SerializeField] private Slider volumeSlider;
        [SerializeField] private TMP_Text volumeText;
        [SerializeField] private TMP_Text muteButtonText;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            BuildVolumeSliderIfMissing();
            Bind(closeButton, CloseSelf, nameof(closeButton));
            Bind(decreaseButton, () => ChangeVolume(-VolumeStep), nameof(decreaseButton));
            Bind(increaseButton, () => ChangeVolume(VolumeStep), nameof(increaseButton));
            Bind(muteButton, ToggleMute, nameof(muteButton));
            BindSlider();
            LocalizationManager.LanguageChanged += Refresh;
            GameAudioSettings.VolumeChanged += OnVolumeChanged;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= Refresh;
            GameAudioSettings.VolumeChanged -= OnVolumeChanged;
        }

        protected override void OnOpen(object args)
        {
            GameAudioSettings.Load();
            Refresh();
        }

        private void BuildVolumeSliderIfMissing()
        {
            if (volumeSlider != null)
            {
                return;
            }

            Transform existing = transform.Find("VolumeSlider");
            if (existing != null && existing.TryGetComponent(out volumeSlider))
            {
                return;
            }

            GameObject sliderObject = CreateChild("VolumeSlider", transform);
            RectTransform sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 0.5f);
            sliderRect.anchorMax = new Vector2(0.5f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(0f, -12f);
            sliderRect.sizeDelta = new Vector2(280f, 28f);

            volumeSlider = sliderObject.AddComponent<Slider>();
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.wholeNumbers = false;
            volumeSlider.direction = Slider.Direction.LeftToRight;

            GameObject background = CreateChild("Background", sliderObject.transform);
            RectTransform backgroundRect = background.GetComponent<RectTransform>();
            Stretch(backgroundRect, new Vector2(0f, 8f), new Vector2(0f, -8f));
            Image backgroundImage = background.AddComponent<Image>();
            backgroundImage.color = new Color(0.45f, 0.32f, 0.18f, 0.45f);

            GameObject fillArea = CreateChild("Fill Area", sliderObject.transform);
            RectTransform fillAreaRect = fillArea.GetComponent<RectTransform>();
            Stretch(fillAreaRect, new Vector2(0f, 8f), new Vector2(0f, -8f));

            GameObject fill = CreateChild("Fill", fillArea.transform);
            RectTransform fillRect = fill.GetComponent<RectTransform>();
            Stretch(fillRect, Vector2.zero, Vector2.zero);
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.26f, 0.58f, 0.25f, 0.95f);

            GameObject handleArea = CreateChild("Handle Slide Area", sliderObject.transform);
            RectTransform handleAreaRect = handleArea.GetComponent<RectTransform>();
            Stretch(handleAreaRect, new Vector2(12f, 0f), new Vector2(-12f, 0f));

            GameObject handle = CreateChild("Handle", handleArea.transform);
            RectTransform handleRect = handle.GetComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(30f, 30f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.18f, 0.42f, 0.86f, 0.98f);

            volumeSlider.fillRect = fillRect;
            volumeSlider.handleRect = handleRect;
            volumeSlider.targetGraphic = handleImage;
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

        private void OnSliderValueChanged(float value)
        {
            SetVolume(value);
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

            if (muteButtonText != null)
            {
                muteButtonText.text = AudioListener.volume > 0.01f
                    ? LocalizationManager.Get("ui.sound.mute")
                    : LocalizationManager.Get("ui.sound.unmute");
            }
        }

        private void CloseSelf()
        {
            if (!UIManager.Instance.Panels.PopStack(WorldMenuPanel.SettingsStackGroupId))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private static GameObject CreateChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<RectTransform>();
            return go;
        }

        private static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }
    }
}
