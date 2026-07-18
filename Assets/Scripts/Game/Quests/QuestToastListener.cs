using Game.Framework;
using UI;

namespace Game
{
    public sealed class QuestToastListener
    {
        public static QuestToastListener Instance { get; } = new QuestToastListener();

        private ISubscription acceptedSubscription;
        private ISubscription completedSubscription;

        private QuestToastListener()
        {
        }

        public void Initialize()
        {
            acceptedSubscription?.Dispose();
            completedSubscription?.Dispose();

            acceptedSubscription = Messager.Instance.Subscribe<WorldMessageTopic, QuestAcceptedMessage>(
                WorldMessageTopic.QuestAccepted,
                OnQuestAccepted);
            completedSubscription = Messager.Instance.Subscribe<WorldMessageTopic, QuestCompletedMessage>(
                WorldMessageTopic.QuestCompleted,
                OnQuestCompleted);
        }

        public void Release()
        {
            acceptedSubscription?.Dispose();
            completedSubscription?.Dispose();
            acceptedSubscription = null;
            completedSubscription = null;
        }

        private static void OnQuestAccepted(QuestAcceptedMessage message)
        {
            if (message == null)
            {
                return;
            }

            string fallback = string.IsNullOrWhiteSpace(message.QuestName)
                ? "New quest"
                : $"New quest: {message.QuestName}";
            Toast.Info(LocalizationManager.FormatOrFallback("ui.quest.toast.accepted", fallback, message.QuestName));
        }

        private static void OnQuestCompleted(QuestCompletedMessage message)
        {
            if (message == null)
            {
                return;
            }

            string fallback = string.IsNullOrWhiteSpace(message.QuestName)
                ? "Quest completed"
                : $"Quest completed: {message.QuestName}";
            Toast.Info(LocalizationManager.FormatOrFallback("ui.quest.toast.completed", fallback, message.QuestName));
        }
    }
}
