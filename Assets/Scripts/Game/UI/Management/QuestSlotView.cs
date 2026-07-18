using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    internal sealed class QuestSlotView
    {
        private readonly Transform root;
        private readonly RectTransform rectTransform;
        private readonly Button rootButton;
        private readonly Image backgroundImage;
        private readonly Image cardImage;
        private readonly Image progressImage;
        private readonly TMP_Text listNameText;
        private readonly TMP_Text titleText;
        private readonly TMP_Text descriptionText;
        private readonly TMP_Text typeText;
        private readonly Button trackButton;
        private readonly TMP_Text trackText;
        private readonly Button rewardButton;
        private readonly TMP_Text rewardButtonText;
        private readonly Transform[] objectiveRoots = new Transform[3];
        private readonly TMP_Text[] objectiveTexts = new TMP_Text[3];
        private readonly Transform[] rewardRoots = new Transform[3];
        private readonly TMP_Text[] rewardTexts = new TMP_Text[3];
        private readonly Color backgroundDefaultColor;
        private readonly Color cardDefaultColor;
        private bool listItem;

        public QuestSlotView(Transform root)
        {
            this.root = root;
            rectTransform = root as RectTransform;
            rootButton = root != null ? root.GetComponent<Button>() : null;
            backgroundImage = root != null ? root.GetComponent<Image>() : null;
            cardImage = FindImage(root, "Image");
            progressImage = FindImage(root, "progress");
            listNameText = FindText(root, "Name") ?? FindText(root, "object0/Text (TMP)") ?? FindText(root, "object0/Text");
            titleText = FindText(root, "Title") ?? listNameText;
            descriptionText = FindText(root, "Description");
            typeText = FindText(root, "type");
            trackButton = FindButton(root, "Track") ?? FindButton(root, "object0/Track");
            trackText = trackButton != null ? trackButton.GetComponentInChildren<TMP_Text>(true) : null;
            rewardButton = FindButton(root, "Button");
            rewardButtonText = rewardButton != null ? rewardButton.GetComponentInChildren<TMP_Text>(true) : null;
            backgroundDefaultColor = backgroundImage != null ? backgroundImage.color : Color.white;
            cardDefaultColor = cardImage != null ? cardImage.color : Color.white;

            for (int i = 0; i < objectiveRoots.Length; i++)
            {
                objectiveRoots[i] = root != null ? root.Find($"object/object{i}") : null;
                objectiveTexts[i] = FindText(objectiveRoots[i], "Text (TMP)");
            }

            for (int i = 0; i < rewardRoots.Length; i++)
            {
                rewardRoots[i] = root != null ? root.Find($"reward/reward{i}") : null;
                rewardTexts[i] = FindText(rewardRoots[i], "Text (TMP)");
            }
        }

        public void ConfigureAsListItem()
        {
            listItem = true;
        }

        public void BindList(QuestSnapshot snapshot, bool selected, Action<int> clicked, Action<int> trackClicked)
        {
            int questId = snapshot?.Config != null ? snapshot.Config.Id : 0;
            SetRootActive(snapshot != null);
            if (snapshot == null)
            {
                return;
            }

            bool tracked = snapshot.Config.Id == QuestManager.Instance.TrackedQuestId;
            SetText(listNameText, QuestManager.Instance.GetQuestNameForUi(snapshot.Config));
            SetText(titleText, QuestManager.Instance.GetQuestNameForUi(snapshot.Config));
            SetText(descriptionText, QuestManager.Instance.GetQuestDescriptionForUi(snapshot.Config));
            SetText(typeText, string.Empty);
            SetProgress(snapshot);
            SetSelected(selected);
            SetSingleObjectiveSummary(snapshot);
            SetRewardsVisible(false);
            SetRewardButtonVisible(false);
            BindTrackButton(snapshot, questId, tracked, trackClicked);

            if (rootButton != null)
            {
                rootButton.onClick.RemoveAllListeners();
                rootButton.onClick.AddListener(() => clicked?.Invoke(questId));
            }
        }

        public void BindDetail(QuestSnapshot snapshot, Action<int> rewardClicked)
        {
            int questId = snapshot?.Config != null ? snapshot.Config.Id : 0;
            SetRootActive(snapshot != null);
            if (snapshot == null)
            {
                return;
            }

            SetText(listNameText, QuestManager.Instance.GetQuestNameForUi(snapshot.Config));
            SetText(titleText, QuestManager.Instance.GetQuestNameForUi(snapshot.Config));
            SetText(descriptionText, QuestManager.Instance.GetQuestDescriptionForUi(snapshot.Config));
            SetText(typeText, GetQuestType(snapshot.Config));
            SetProgress(snapshot);
            SetObjectives(snapshot);
            SetRewards(snapshot);
            SetSelected(false);
            SetTrackButtonVisible(false);
            BindRewardButton(snapshot, questId, rewardClicked);
        }

        private void BindTrackButton(QuestSnapshot snapshot, int questId, bool tracked, Action<int> trackClicked)
        {
            if (trackButton == null)
            {
                return;
            }

            bool canTrack = snapshot != null &&
                            (snapshot.State == QuestState.Accepted || snapshot.State == QuestState.Completed);
            trackButton.gameObject.SetActive(true);
            trackButton.interactable = canTrack && !tracked;
            SetText(trackText, tracked
                ? LocalizationManager.Get("ui.quest.tracking")
                : LocalizationManager.Get("ui.quest.track"));
            trackButton.onClick.RemoveAllListeners();
            trackButton.onClick.AddListener(() => trackClicked?.Invoke(questId));
        }

        private void BindRewardButton(QuestSnapshot snapshot, int questId, Action<int> rewardClicked)
        {
            if (rewardButton == null)
            {
                return;
            }

            bool completed = snapshot.State == QuestState.Completed;
            bool hasBlueprintAction = snapshot.State == QuestState.Accepted &&
                                      QuestManager.Instance.HasActiveBlueprintObjective(questId);
            rewardButton.gameObject.SetActive(true);
            rewardButton.interactable = completed ||
                                        (hasBlueprintAction && QuestManager.Instance.CanCompleteActiveBlueprintObjective(questId));
            SetText(rewardButtonText, GetRewardButtonText(snapshot.State, hasBlueprintAction));
            rewardButton.onClick.RemoveAllListeners();
            rewardButton.onClick.AddListener(() => rewardClicked?.Invoke(questId));
        }

        private static string GetRewardButtonText(QuestState state, bool hasBlueprintAction)
        {
            if (hasBlueprintAction)
            {
                return LocalizationManager.GetOrFallback("ui.quest.craft", "Craft");
            }

            switch (state)
            {
                case QuestState.Completed:
                    return LocalizationManager.Get("ui.quest.claim");
                case QuestState.Claimed:
                    return LocalizationManager.Get("ui.quest.claimed");
                case QuestState.Available:
                    return LocalizationManager.Get("ui.quest.not_started");
                case QuestState.Accepted:
                default:
                    return LocalizationManager.Get("ui.quest.incomplete");
            }
        }

        private void SetSingleObjectiveSummary(QuestSnapshot snapshot)
        {
            for (int i = 0; i < objectiveRoots.Length; i++)
            {
                bool visible = i == 0;
                SetActive(objectiveRoots[i], visible);
                if (visible)
                {
                    SetText(objectiveTexts[i], $"{snapshot.Progress}/{snapshot.Target}");
                }
            }
        }

        private void SetObjectives(QuestSnapshot snapshot)
        {
            for (int i = 0; i < objectiveRoots.Length; i++)
            {
                bool visible = snapshot.Objectives != null && i < snapshot.Objectives.Length;
                SetActive(objectiveRoots[i], visible);
                if (!visible)
                {
                    continue;
                }

                QuestObjectiveSnapshot objective = snapshot.Objectives[i];
                SetText(objectiveTexts[i], $"{objective.Progress}/{objective.Target}");
            }
        }

        private void SetRewards(QuestSnapshot snapshot)
        {
            int rewardCount = QuestManager.Instance.GetRewardCountForUi(snapshot?.Config);
            for (int i = 0; i < rewardRoots.Length; i++)
            {
                bool visible = i < rewardCount;
                SetActive(rewardRoots[i], visible);
                if (visible)
                {
                    SetText(rewardTexts[i], QuestManager.Instance.GetRewardAmountTextForUi(snapshot.Config, i));
                }
            }
        }

        private void SetRewardsVisible(bool visible)
        {
            for (int i = 0; i < rewardRoots.Length; i++)
            {
                SetActive(rewardRoots[i], visible);
            }
        }

        private void SetProgress(QuestSnapshot snapshot)
        {
            if (progressImage == null)
            {
                return;
            }

            progressImage.type = Image.Type.Filled;
            progressImage.fillMethod = Image.FillMethod.Horizontal;
            progressImage.fillAmount = snapshot.Target > 0 ? Mathf.Clamp01((float)snapshot.Progress / snapshot.Target) : 0f;
        }

        private void SetSelected(bool selected)
        {
            if (backgroundImage != null)
            {
                backgroundImage.color = listItem
                    ? (selected ? new Color(1f, 0.9f, 0.45f, 0.28f) : backgroundDefaultColor)
                    : backgroundDefaultColor;
            }

            if (listItem && cardImage != null)
            {
                cardImage.color = selected
                    ? new Color(1f, 0.94f, 0.72f, Mathf.Max(0.85f, cardDefaultColor.a))
                    : cardDefaultColor;
            }
        }

        private void SetRewardButtonVisible(bool visible)
        {
            if (rewardButton != null)
            {
                rewardButton.gameObject.SetActive(visible);
            }
        }

        private void SetTrackButtonVisible(bool visible)
        {
            if (trackButton != null)
            {
                trackButton.gameObject.SetActive(visible);
            }
        }

        private void SetRootActive(bool active)
        {
            if (root != null)
            {
                root.gameObject.SetActive(active);
            }
        }

        private static string GetQuestType(QuestConfig config)
        {
            return config != null && !string.IsNullOrWhiteSpace(config.QuestType)
                ? LocalizationManager.GetOrFallback($"quest.type.{config.QuestType}", config.QuestType)
                : string.Empty;
        }

        private static Image FindImage(Transform parent, string path)
        {
            Transform child = parent != null ? parent.Find(path) : null;
            return child != null ? child.GetComponent<Image>() : null;
        }

        private static Button FindButton(Transform parent, string path)
        {
            Transform child = parent != null ? parent.Find(path) : null;
            return child != null ? child.GetComponent<Button>() : null;
        }

        private static TMP_Text FindText(Transform parent, string path)
        {
            Transform child = parent != null ? parent.Find(path) : null;
            return child != null ? child.GetComponent<TMP_Text>() : null;
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
            {
                text.text = value ?? string.Empty;
            }
        }

        private static void SetActive(Transform transform, bool active)
        {
            if (transform != null)
            {
                transform.gameObject.SetActive(active);
            }
        }
    }
}
