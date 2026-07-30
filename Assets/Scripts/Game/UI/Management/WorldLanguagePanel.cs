using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldLanguagePanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Menu/LanguagePanel.prefab";

        [SerializeField] private Button returnButton;
        [SerializeField] private Button englishButton;
        [SerializeField] private Button chineseButton;
        [SerializeField] private TMP_Text languageText;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            Bind(returnButton, CloseSelf, nameof(returnButton));
            Bind(englishButton, () => SetLanguage(LocalizationManager.English), nameof(englishButton));
            Bind(chineseButton, () => SetLanguage(LocalizationManager.Chinese), nameof(chineseButton));
            LocalizationManager.LanguageChanged += Refresh;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= Refresh;
        }

        protected override void OnOpen(object args)
        {
            Refresh();
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(WorldLanguagePanel)}] {fieldName} is not assigned on prefab: {PrefabPath}");
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void SetLanguage(string language)
        {
            LocalizationManager.SetLanguage(language);
            Refresh();
            Toast.Info(LocalizationManager.Format("ui.language.toast", LocalizationManager.GetLanguageDisplayName(language)));
        }

        private void Refresh()
        {
            if (languageText != null)
            {
                languageText.text = LocalizationManager.Format(
                    "ui.language.current",
                    LocalizationManager.GetLanguageDisplayName(LocalizationManager.CurrentLanguage));
            }
        }

        private void CloseSelf()
        {
            if (!UIManager.Instance.Panels.PopStack(WorldMenuPanel.SettingsStackGroupId))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }
    }
}
