using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class WorldSavePanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Menu/SavePanel.prefab";

        [SerializeField] private Button closeButton;
        [SerializeField] private Button saveButton;
        [SerializeField] private TMP_Text statusText;
        private string statusKey = "ui.save.status.ready";

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            Bind(closeButton, CloseSelf, nameof(closeButton));
            Bind(saveButton, Save, nameof(saveButton));
            LocalizationManager.LanguageChanged += RefreshStatus;
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= RefreshStatus;
        }

        protected override void OnOpen(object args)
        {
            SetStatusKey("ui.save.status.ready");
        }

        private void Bind(Button button, UnityEngine.Events.UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(WorldSavePanel)}] {fieldName} is not assigned on prefab: {PrefabPath}");
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private void Save()
        {
            bool saved = StorageManager.Instance.Save();
            SetStatusKey(saved ? "ui.save.status.saved" : "ui.save.status.failed");

            if (saved)
            {
                Toast.Info(LocalizationManager.Get("ui.save.status.saved"));
            }
            else
            {
                Toast.Warning(LocalizationManager.Get("ui.save.status.failed"));
            }
        }

        private void SetStatusKey(string key)
        {
            statusKey = key;
            RefreshStatus();
        }

        private void RefreshStatus()
        {
            if (statusText != null)
            {
                statusText.text = LocalizationManager.Get(statusKey);
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
