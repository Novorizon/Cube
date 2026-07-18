using System.Collections.Generic;
using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace Game
{
    public sealed class QuestPanel : UIPanel
    {
        public const string PrefabPath = "Assets/Arts/UI/Panels/Quest/QuestPanel.prefab";

        private const string QuestSlotPrefabPath = "Assets/Arts/UI/Panels/Quest/QuestSlot.prefab";
        private readonly List<QuestSlotView> listSlots = new List<QuestSlotView>();

        private Transform contentRoot;
        private TMP_Text titleText;
        private QuestSlotView detailSlot;
        private GameObject detailRoot;
        private GameObject slotPrefab;
        private int selectedQuestId;
        private int lastListHash = int.MinValue;

        public override UICloseTriggers CloseTriggers => UICloseTriggers.CloseButton | UICloseTriggers.Back | UICloseTriggers.RightOutside;

        protected override void OnCreate()
        {
            BindStaticLayout();
        }

        protected override void OnOpen(object args)
        {
            BindStaticLayout();
            RegisterDisposable(Messager.Instance.Subscribe<WorldMessageTopic, QuestChangedMessage>(
                WorldMessageTopic.QuestChanged,
                _ => RefreshNow()));
            RefreshNow();
        }

        protected override void OnClose()
        {
            ClearListSlots();
            lastListHash = int.MinValue;
        }

        private void BindStaticLayout()
        {
            contentRoot =
                transform.Find("Frame/ScrollView/Viewport/Content") ??
                transform.Find("ScrollView/Viewport/Content");

            titleText =
                WorldPanelBindingUtility.FindText(transform.Find("Frame/Header"), "Title") ??
                WorldPanelBindingUtility.FindText(transform, "Title");
            RefreshTitle();

            WorldPanelBindingUtility.BindButton(
                transform.Find("Frame/Header/Close") ?? transform.Find("Header/Close") ?? transform.Find("Close"),
                CloseSelf,
                "Quest panel close");

            Transform detailTransform = transform.Find("QuestSlot");
            detailRoot = detailTransform != null ? detailTransform.gameObject : null;
            detailSlot = detailTransform != null ? new QuestSlotView(detailTransform) : null;

            slotPrefab = ResourceManager.Instance.LoadGameObject(QuestSlotPrefabPath);
            if (slotPrefab == null)
            {
                Debug.LogError($"[{nameof(QuestPanel)}] Missing quest slot prefab: {QuestSlotPrefabPath}");
            }
        }

        private void RefreshNow()
        {
            IReadOnlyList<QuestSnapshot> quests = QuestManager.Instance.GetVisibleQuests();
            EnsureSelection(quests);

            int nextHash = CalculateListHash(quests);
            if (nextHash != lastListHash)
            {
                lastListHash = nextHash;
                RebuildList(quests);
            }
            else
            {
                RefreshListSelection(quests);
            }

            RefreshDetail(quests);
        }

        private void RebuildList(IReadOnlyList<QuestSnapshot> quests)
        {
            ClearListSlots();

            if (contentRoot == null || slotPrefab == null || quests == null)
            {
                return;
            }

            for (int i = 0; i < quests.Count; i++)
            {
                QuestSnapshot snapshot = quests[i];
                GameObject slotObject = Instantiate(slotPrefab, contentRoot, false);
                slotObject.name = snapshot?.Config != null ? $"QuestSlot_{snapshot.Config.Id}" : "QuestSlot";

                QuestSlotView view = new QuestSlotView(slotObject.transform);
                view.ConfigureAsListItem();
                view.BindList(snapshot, snapshot?.Config != null && snapshot.Config.Id == selectedQuestId, OnQuestClicked, OnTrackClicked);
                listSlots.Add(view);
            }

            RefreshContentLayout();
        }

        private void RefreshTitle()
        {
            if (titleText != null)
            {
                titleText.text = LocalizationManager.Get("ui.quest.main_title");
            }
        }

        private void RefreshListSelection(IReadOnlyList<QuestSnapshot> quests)
        {
            if (quests == null)
            {
                return;
            }

            for (int i = 0; i < listSlots.Count && i < quests.Count; i++)
            {
                QuestSnapshot snapshot = quests[i];
                listSlots[i].BindList(snapshot, snapshot?.Config != null && snapshot.Config.Id == selectedQuestId, OnQuestClicked, OnTrackClicked);
            }
        }

        private void RefreshDetail(IReadOnlyList<QuestSnapshot> quests)
        {
            QuestSnapshot selected = FindQuest(quests, selectedQuestId);
            if (detailRoot != null)
            {
                detailRoot.SetActive(selected != null);
            }

            detailSlot?.BindDetail(selected, OnDetailActionClicked);
        }

        private void EnsureSelection(IReadOnlyList<QuestSnapshot> quests)
        {
            if (FindQuest(quests, selectedQuestId) != null)
            {
                return;
            }

            selectedQuestId = QuestManager.Instance.TrackedQuestId;
            if (FindQuest(quests, selectedQuestId) != null)
            {
                return;
            }

            selectedQuestId = quests != null && quests.Count > 0 && quests[0]?.Config != null ? quests[0].Config.Id : 0;
        }

        private void OnQuestClicked(int questId)
        {
            selectedQuestId = questId;
            RefreshNow();
        }

        private void OnTrackClicked(int questId)
        {
            selectedQuestId = questId;
            QuestManager.Instance.SetTrackedQuest(questId);
            RefreshNow();
        }

        private void OnDetailActionClicked(int questId)
        {
            QuestSnapshot selected = FindQuest(QuestManager.Instance.GetVisibleQuests(), questId);
            if (selected == null)
            {
                return;
            }

            if (selected.State == QuestState.Completed)
            {
                QuestManager.Instance.TryClaim(questId);
            }
            else if (selected.State == QuestState.Accepted)
            {
                QuestManager.Instance.TryCompleteActiveBlueprintObjective(questId);
            }

            RefreshNow();
        }

        private void CloseSelf()
        {
            if (CanCloseBy(UICloseReason.CloseButton))
            {
                UIManager.Instance.Panels.Hide(PrefabPath);
            }
        }

        private void ClearListSlots()
        {
            if (contentRoot != null)
            {
                for (int i = contentRoot.childCount - 1; i >= 0; i--)
                {
                    Destroy(contentRoot.GetChild(i).gameObject);
                }
            }

            listSlots.Clear();
        }

        private void RefreshContentLayout()
        {
            if (contentRoot == null)
            {
                return;
            }

            RectTransform rect = contentRoot as RectTransform;
            if (rect != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
                Canvas.ForceUpdateCanvases();
                LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            }
        }

        private static QuestSnapshot FindQuest(IReadOnlyList<QuestSnapshot> quests, int questId)
        {
            if (quests == null || questId <= 0)
            {
                return null;
            }

            for (int i = 0; i < quests.Count; i++)
            {
                QuestSnapshot snapshot = quests[i];
                if (snapshot?.Config != null && snapshot.Config.Id == questId)
                {
                    return snapshot;
                }
            }

            return null;
        }

        private static int CalculateListHash(IReadOnlyList<QuestSnapshot> quests)
        {
            unchecked
            {
                int hash = 17;
                if (quests == null)
                {
                    return hash;
                }

                for (int i = 0; i < quests.Count; i++)
                {
                    QuestSnapshot snapshot = quests[i];
                    hash = hash * 31 + (snapshot?.Config != null ? snapshot.Config.Id : 0);
                    hash = hash * 31 + (int)(snapshot != null ? snapshot.State : QuestState.Locked);
                    hash = hash * 31 + (snapshot != null ? snapshot.Progress : 0);
                    hash = hash * 31 + (snapshot != null ? snapshot.Target : 0);
                }

                return hash;
            }
        }

    }
}
