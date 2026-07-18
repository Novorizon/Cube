using System.Collections.Generic;
using System.Text;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class BattleResultPopup : UIPopup
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Battle/BattleResultPopup.prefab";

        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text mapText;
        [SerializeField] private TMP_Text reasonText;
        [SerializeField] private TMP_Text rewardText;
        [SerializeField] private Button nextButton;
        [SerializeField] private TMP_Text nextButtonText;
        [SerializeField] private Button restartButton;
        [SerializeField] private TMP_Text restartButtonText;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private TMP_Text mainMenuButtonText;

        private BattleEndedMessage message;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton;

        protected override void OnCreate()
        {
            Bind(nextButton, LoadNextBattle, nameof(nextButton));
            Bind(restartButton, RestartBattle, nameof(restartButton));
            Bind(mainMenuButton, ReturnToMainMenu, nameof(mainMenuButton));
            LocalizationManager.LanguageChanged += Refresh;
        }

        protected override void OnOpen(object args)
        {
            message = args as BattleEndedMessage ?? BattleFlowManager.Instance.LastEndMessage;
            Time.timeScale = 0f;
            Refresh();
        }

        protected override void OnDestroyed()
        {
            LocalizationManager.LanguageChanged -= Refresh;
            Unbind(nextButton, LoadNextBattle);
            Unbind(restartButton, RestartBattle);
            Unbind(mainMenuButton, ReturnToMainMenu);
        }

        private void Refresh()
        {
            bool victory = message != null && message.Victory;
            SetText(titleText, victory ? LocalizationManager.Get("ui.td.result.victory") : LocalizationManager.Get("ui.td.result.defeat"));
            SetText(mapText, message != null && message.MapId > 0 ? LocalizedConfigText.MapName(message.MapId) : LocalizationManager.Get("ui.td.result.current_map"));
            SetText(reasonText, message != null ? message.Reason : string.Empty);
            SetText(rewardText, BuildRewardText(message));
            SetText(nextButtonText, LocalizationManager.Get("ui.td.button.next"));
            SetText(restartButtonText, LocalizationManager.Get("ui.td.button.restart"));
            SetText(mainMenuButtonText, LocalizationManager.Get("ui.td.button.main_menu"));

            bool hasNextMap = victory && MapManager.Instance.HasNextBattleMap(message != null ? message.MapId : 0);
            if (nextButton != null)
            {
                nextButton.gameObject.SetActive(hasNextMap);
            }
        }

        private static string BuildRewardText(BattleEndedMessage battleEndedMessage)
        {
            if (battleEndedMessage == null || !battleEndedMessage.Victory ||
                battleEndedMessage.Reward == null || battleEndedMessage.Reward.OuterItemMap == null ||
                battleEndedMessage.Reward.OuterItemMap.Count == 0)
            {
                return LocalizationManager.Get("ui.td.result.reward_none");
            }

            StringBuilder builder = new StringBuilder(LocalizationManager.Get("ui.td.result.reward_prefix"));
            bool first = true;
            foreach (KeyValuePair<int, int> pair in battleEndedMessage.Reward.OuterItemMap)
            {
                if (!first)
                {
                    builder.Append("，");
                }

                builder.Append(LocalizedConfigText.ItemName(pair.Key));
                builder.Append(" x");
                builder.Append(pair.Value);
                first = false;
            }

            return builder.ToString();
        }

        private void LoadNextBattle()
        {
            int mapId = message != null ? message.MapId : 0;
            CloseSelf();
            MapManager.Instance.LoadNextBattleMap(mapId);
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

        private void Bind(Button button, UnityEngine.Events.UnityAction action, string fieldName)
        {
            if (button == null)
            {
                Debug.LogError($"[{nameof(BattleResultPopup)}] {fieldName} is not assigned on {PrefabPath}.", this);
                return;
            }

            button.onClick.RemoveListener(action);
            button.onClick.AddListener(action);
        }

        private static void Unbind(Button button, UnityEngine.Events.UnityAction action)
        {
            button?.onClick.RemoveListener(action);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }
    }
}
