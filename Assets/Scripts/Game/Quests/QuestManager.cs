using Game.Framework;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Game
{
    public enum QuestState
    {
        Locked = 0,
        Available = 1,
        Accepted = 2,
        Completed = 3,
        Claimed = 4,
    }

    public enum QuestAcceptMode
    {
        Auto = 0,
        Manual = 1,
        Event = 2,
    }

    public enum QuestObjectiveType
    {
        None = 0,
        ItemCount = 1,
        ItemGainCount = 2,
        ItemUseCount = 3,
        Blueprint = 4,
        BuildBuilding = 5,
        BuildBuildingType = 6,
        UpgradeBuilding = 7,
        FarmCount = 8,
        PlantCrop = 9,
        HarvestCrop = 10,
        TechResearched = 11,
        TalkNpc = 12,
        EnterArea = 13,
        CustomFlag = 14,
    }

    public enum QuestEventType
    {
        None = 0,
        StartQuest = 1,
        CustomFlag = 2,
        TalkNpc = 3,
        EnterArea = 4,
        UseItem = 5,
        GainItem = 6,
        BlueprintCompleted = 7,
        BuildBuilding = 8,
        UpgradeBuilding = 9,
        PlantCrop = 10,
        HarvestCrop = 11,
    }

    public sealed class QuestObjectiveConfig
    {
        public int Id;
        public QuestObjectiveType Type;
        public int TargetId;
        public int TargetCount = 1;
        public string Text;
        public bool Enable = true;
    }

    public sealed class QuestConfig
    {
        public int Id;
        public string Name;
        public string Description;
        public string QuestType;
        public int RewardGroupId;
        public int[] PreQuestIds;
        public QuestAcceptMode AcceptMode = QuestAcceptMode.Auto;
        public QuestEventType AcceptEventType = QuestEventType.None;
        public int AcceptTargetId;
        public QuestObjectiveConfig[] Objectives;
        public bool AutoAccept = true;
        public bool Enable = true;
    }

    public sealed class QuestObjectiveData
    {
        public int ObjectiveId;
        public int Progress;
    }

    public sealed class QuestData
    {
        public int QuestId;
        public QuestState State;
        public QuestObjectiveData[] Objectives;
    }

    public sealed class QuestObjectiveSnapshot
    {
        public QuestObjectiveConfig Config;
        public int Progress;
        public int Target;
        public bool Completed;
    }

    public sealed class QuestSnapshot
    {
        public QuestConfig Config;
        public QuestState State;
        public QuestObjectiveSnapshot[] Objectives;
        public int Progress;
        public int Target;
    }

    public sealed class QuestEvent
    {
        public QuestEventType Type;
        public int TargetId;
        public int Count;
    }

    public sealed class QuestManager
    {
        public static QuestManager Instance { get; } = new QuestManager();

        private readonly List<QuestConfig> configs = new List<QuestConfig>();
        private readonly Dictionary<int, QuestConfig> configById = new Dictionary<int, QuestConfig>();
        private readonly Dictionary<int, QuestData> dataByQuestId = new Dictionary<int, QuestData>();
        private readonly List<QuestSnapshot> snapshotBuffer = new List<QuestSnapshot>();
        private readonly List<QuestObjectiveSnapshot> objectiveSnapshotBuffer = new List<QuestObjectiveSnapshot>();
        private readonly StringBuilder builder = new StringBuilder(512);

        private RewardResolver rewardResolver;
        private int trackedQuestId;
        private bool loading;

        private QuestManager()
        {
        }

        public void Initialize()
        {
            configs.Clear();
            configById.Clear();
            dataByQuestId.Clear();
            trackedQuestId = 0;
            rewardResolver = new RewardResolver(DataManager.Instance.Reward);
            LoadConfigs();
            EnsureAutoAcceptedQuests();
        }

        public void LoadSaveData(SaveQuestData data)
        {
            loading = true;
            dataByQuestId.Clear();
            trackedQuestId = data != null ? data.TrackedQuestId : 0;

            if (data?.Quests != null)
            {
                for (int i = 0; i < data.Quests.Length; i++)
                {
                    AddQuestData(data.Quests[i]);
                }
            }
            else
            {
                AddLegacyQuestIds(data?.AcceptedQuestIds, QuestState.Accepted);
                AddLegacyQuestIds(data?.CompletedQuestIds, QuestState.Completed);
                AddLegacyQuestIds(data?.ClaimedQuestIds, QuestState.Claimed);
            }

            RemoveInvalidQuestData();
            EnsureAutoAcceptedQuests();
            RefreshQuestStates();
            EnsureTrackedQuest();
            loading = false;
        }

        public SaveQuestData CreateSaveData()
        {
            RefreshQuestStates();

            List<QuestData> questData = new List<QuestData>();
            List<int> accepted = new List<int>();
            List<int> completed = new List<int>();
            List<int> claimed = new List<int>();

            foreach (KeyValuePair<int, QuestData> pair in dataByQuestId)
            {
                QuestData data = pair.Value;
                if (data == null || data.QuestId <= 0)
                {
                    continue;
                }

                questData.Add(CloneQuestData(data));
                switch (data.State)
                {
                    case QuestState.Accepted:
                        accepted.Add(data.QuestId);
                        break;
                    case QuestState.Completed:
                        completed.Add(data.QuestId);
                        break;
                    case QuestState.Claimed:
                        claimed.Add(data.QuestId);
                        break;
                }
            }

            questData.Sort((a, b) => a.QuestId.CompareTo(b.QuestId));
            accepted.Sort();
            completed.Sort();
            claimed.Sort();

            return new SaveQuestData
            {
                TrackedQuestId = trackedQuestId,
                Quests = questData.ToArray(),
                AcceptedQuestIds = accepted.ToArray(),
                CompletedQuestIds = completed.ToArray(),
                ClaimedQuestIds = claimed.ToArray(),
            };
        }

        public int TrackedQuestId
        {
            get
            {
                EnsureAutoAcceptedQuests();
                RefreshQuestStates();
                EnsureTrackedQuest();
                return trackedQuestId;
            }
        }

        public IReadOnlyList<QuestSnapshot> GetVisibleQuests()
        {
            EnsureAutoAcceptedQuests();
            RefreshQuestStates();

            snapshotBuffer.Clear();
            for (int i = 0; i < configs.Count; i++)
            {
                QuestConfig config = configs[i];
                if (config == null || !config.Enable)
                {
                    continue;
                }

                QuestState state = GetState(config);
                if (state == QuestState.Locked || state == QuestState.Claimed)
                {
                    continue;
                }

                if (state == QuestState.Available && config.AcceptMode == QuestAcceptMode.Event)
                {
                    continue;
                }

                QuestObjectiveSnapshot[] objectives = CreateObjectiveSnapshots(config);
                int totalProgress = 0;
                int totalTarget = 0;
                for (int j = 0; j < objectives.Length; j++)
                {
                    totalProgress += objectives[j].Progress;
                    totalTarget += objectives[j].Target;
                }

                snapshotBuffer.Add(new QuestSnapshot
                {
                    Config = config,
                    State = state,
                    Objectives = objectives,
                    Progress = totalProgress,
                    Target = Mathf.Max(1, totalTarget),
                });
            }

            return snapshotBuffer;
        }

        public string GetGoalText()
        {
            IReadOnlyList<QuestSnapshot> quests = GetVisibleQuests();
            builder.Clear();
            builder.AppendLine(LocalizationManager.GetOrFallback("ui.quest.current_goals", "Current Quests"));

            if (quests.Count == 0)
            {
                builder.AppendLine(LocalizationManager.GetOrFallback("ui.quest.none", "No active quests."));
                return builder.ToString();
            }

            for (int i = 0; i < quests.Count; i++)
            {
                QuestSnapshot quest = quests[i];
                AppendQuestText(builder, quest, true);
            }

            return builder.ToString();
        }

        public string GetTrackedQuestText()
        {
            QuestSnapshot quest = GetTrackedQuest();
            if (quest == null)
            {
                return LocalizationManager.GetOrFallback("ui.quest.none", "No active quests.");
            }

            builder.Clear();
            AppendQuestText(builder, quest, false);
            return builder.ToString();
        }

        public QuestSnapshot GetTrackedQuest()
        {
            IReadOnlyList<QuestSnapshot> quests = GetVisibleQuests();
            EnsureTrackedQuest();

            for (int i = 0; i < quests.Count; i++)
            {
                QuestSnapshot quest = quests[i];
                if (quest?.Config != null && quest.Config.Id == trackedQuestId)
                {
                    return quest;
                }
            }

            return null;
        }

        public bool SetTrackedQuest(int questId)
        {
            if (questId <= 0 || !CanTrackQuest(questId))
            {
                return false;
            }

            if (trackedQuestId == questId)
            {
                return true;
            }

            trackedQuestId = questId;
            MarkDirtyIfReady();
            NotifyQuestChanged(questId, true);
            return true;
        }

        public string GetQuestNameForUi(QuestConfig config)
        {
            return GetQuestName(config);
        }

        public string GetQuestDescriptionForUi(QuestConfig config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            return LocalizationManager.GetOrFallback($"quest.{config.Id}.description", config.Description);
        }

        public string GetObjectiveTextForUi(QuestConfig quest, QuestObjectiveConfig objective)
        {
            return GetObjectiveText(quest, objective);
        }

        public int GetRewardCountForUi(QuestConfig config)
        {
            return GetRewardRows(config?.RewardGroupId ?? 0).Count;
        }

        public string GetRewardAmountTextForUi(QuestConfig config, int index)
        {
            List<RewardConfig> rewards = GetRewardRows(config?.RewardGroupId ?? 0);
            if (index < 0 || index >= rewards.Count)
            {
                return string.Empty;
            }

            RewardConfig reward = rewards[index];
            if (reward == null)
            {
                return string.Empty;
            }

            int minCount = Mathf.Max(0, reward.MinCount);
            int maxCount = Mathf.Max(minCount, reward.MaxCount);
            string countText = minCount == maxCount ? minCount.ToString() : $"{minCount}-{maxCount}";
            return $"x{countText}";
        }

        public bool StartQuest(int questId)
        {
            return TryAccept(questId);
        }

        public void NotifyEvent(QuestEventType type, int targetId, int count = 1)
        {
            if (type == QuestEventType.None)
            {
                return;
            }

            int safeCount = Mathf.Max(1, count);
            if (type == QuestEventType.StartQuest)
            {
                TryAccept(targetId);
                return;
            }

            bool changed = AcceptEventQuests(type, targetId);
            changed |= UpdateCounterObjectives(type, targetId, safeCount);

            if (changed)
            {
                MarkDirtyIfReady();
            }

            RefreshQuestStates();
        }

        public bool TryAccept(int questId)
        {
            if (!configById.TryGetValue(questId, out QuestConfig config) || config == null || !config.Enable)
            {
                return false;
            }

            if (!ArePrerequisitesCompleted(config))
            {
                return false;
            }

            if (dataByQuestId.TryGetValue(questId, out QuestData existing) && existing != null)
            {
                return existing.State == QuestState.Accepted ||
                       existing.State == QuestState.Completed ||
                       existing.State == QuestState.Claimed;
            }

            AddAcceptedQuest(config);

            MarkDirtyIfReady();
            RefreshQuestStates();
            return true;
        }

        public bool TryClaim(int questId)
        {
            if (!dataByQuestId.TryGetValue(questId, out QuestData data) ||
                data == null ||
                data.State != QuestState.Completed ||
                !configById.TryGetValue(questId, out QuestConfig config) ||
                config == null)
            {
                return false;
            }

            if (!TryGrantRewards(config))
            {
                return false;
            }

            data.State = QuestState.Claimed;
            MarkDirtyIfReady();
            NotifyQuestChanged(questId);
            EnsureAutoAcceptedQuests();
            return true;
        }

        public bool IsCompleted(int questId)
        {
            return dataByQuestId.TryGetValue(questId, out QuestData data) &&
                   data != null &&
                   (data.State == QuestState.Completed || data.State == QuestState.Claimed);
        }

        public int GetActiveBlueprintObjectiveForBuilding(int buildingId)
        {
            if (buildingId <= 0)
            {
                return 0;
            }

            foreach (QuestConfig config in configs)
            {
                if (config == null ||
                    !dataByQuestId.TryGetValue(config.Id, out QuestData data) ||
                    data == null ||
                    data.State != QuestState.Accepted ||
                    config.Objectives == null)
                {
                    continue;
                }

                for (int i = 0; i < config.Objectives.Length; i++)
                {
                    QuestObjectiveConfig objective = config.Objectives[i];
                    if (objective == null ||
                        !objective.Enable ||
                        objective.Type != QuestObjectiveType.Blueprint ||
                        GetProgress(config.Id, objective) >= GetTarget(objective))
                    {
                        continue;
                    }

                    BlueprintConfig blueprint = BlueprintManager.Instance.Get(objective.TargetId);
                    if (blueprint != null && blueprint.BuildingId == buildingId)
                    {
                        return blueprint.Id;
                    }
                }
            }

            return 0;
        }

        public bool HasActiveBlueprintObjective(int questId)
        {
            return TryGetActiveBlueprintObjective(questId, out _);
        }

        public bool CanCompleteActiveBlueprintObjective(int questId)
        {
            return TryGetActiveBlueprintObjective(questId, out QuestObjectiveConfig objective) &&
                   BlueprintManager.Instance.CanComplete(objective.TargetId);
        }

        public bool TryCompleteActiveBlueprintObjective(int questId)
        {
            return TryGetActiveBlueprintObjective(questId, out QuestObjectiveConfig objective) &&
                   BlueprintManager.Instance.TryComplete(objective.TargetId);
        }

        private void LoadConfigs()
        {
            IReadOnlyDictionary<int, QuestTableConfig> table = DataManager.Instance.Quest?.GetAll();
            if (table == null)
            {
                Debug.LogError("Quest config table is not loaded.");
                return;
            }

            Dictionary<int, List<QuestObjectiveTableConfig>> objectivesByQuestId = BuildObjectiveIndex();
            List<QuestTableConfig> rows = new List<QuestTableConfig>(table.Values);
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));

            for (int i = 0; i < rows.Count; i++)
            {
                AddConfig(CreateQuestConfig(rows[i], objectivesByQuestId));
            }
        }

        private void AddConfig(QuestConfig config)
        {
            if (config == null || config.Id <= 0 || !config.Enable || configById.ContainsKey(config.Id))
            {
                return;
            }

            configs.Add(config);
            configById.Add(config.Id, config);
        }

        private bool TryGetActiveBlueprintObjective(int questId, out QuestObjectiveConfig objective)
        {
            objective = null;
            if (questId <= 0 ||
                !dataByQuestId.TryGetValue(questId, out QuestData data) ||
                data == null ||
                data.State != QuestState.Accepted ||
                !configById.TryGetValue(questId, out QuestConfig config) ||
                config == null ||
                config.Objectives == null)
            {
                return false;
            }

            for (int i = 0; i < config.Objectives.Length; i++)
            {
                QuestObjectiveConfig candidate = config.Objectives[i];
                if (candidate == null ||
                    !candidate.Enable ||
                    candidate.Type != QuestObjectiveType.Blueprint ||
                    GetProgress(config.Id, candidate) >= GetTarget(candidate))
                {
                    continue;
                }

                objective = candidate;
                return true;
            }

            return false;
        }

        private static Dictionary<int, List<QuestObjectiveTableConfig>> BuildObjectiveIndex()
        {
            Dictionary<int, List<QuestObjectiveTableConfig>> index = new Dictionary<int, List<QuestObjectiveTableConfig>>();
            IReadOnlyDictionary<int, QuestObjectiveTableConfig> table = DataManager.Instance.QuestObjective?.GetAll();
            if (table == null)
            {
                return index;
            }

            foreach (KeyValuePair<int, QuestObjectiveTableConfig> pair in table)
            {
                QuestObjectiveTableConfig row = pair.Value;
                if (row == null || !row.Enable || row.QuestId <= 0)
                {
                    continue;
                }

                if (!index.TryGetValue(row.QuestId, out List<QuestObjectiveTableConfig> rows))
                {
                    rows = new List<QuestObjectiveTableConfig>();
                    index.Add(row.QuestId, rows);
                }

                rows.Add(row);
            }

            foreach (List<QuestObjectiveTableConfig> rows in index.Values)
            {
                rows.Sort((a, b) => a.SortOrder != b.SortOrder
                    ? a.SortOrder.CompareTo(b.SortOrder)
                    : a.Id.CompareTo(b.Id));
            }

            return index;
        }

        private static QuestConfig CreateQuestConfig(
            QuestTableConfig row,
            IReadOnlyDictionary<int, List<QuestObjectiveTableConfig>> objectivesByQuestId)
        {
            if (row == null)
            {
                return null;
            }

            objectivesByQuestId.TryGetValue(row.Id, out List<QuestObjectiveTableConfig> objectives);
            return new QuestConfig
            {
                Id = row.Id,
                Name = row.Name,
                Description = row.Description,
                QuestType = row.QuestType,
                RewardGroupId = row.RewardGroupId,
                PreQuestIds = CreatePreQuestIds(row),
                AcceptMode = (QuestAcceptMode)row.AcceptMode,
                AcceptEventType = (QuestEventType)row.AcceptEventType,
                AcceptTargetId = row.AcceptTargetId,
                Objectives = CreateObjectiveConfigs(objectives),
                AutoAccept = row.AutoAccept,
                Enable = row.Enable,
            };
        }

        private static int[] CreatePreQuestIds(QuestTableConfig row)
        {
            List<int> ids = new List<int>(3);
            AddPreQuestId(ids, row.PreQuestId1);
            AddPreQuestId(ids, row.PreQuestId2);
            AddPreQuestId(ids, row.PreQuestId3);
            return ids.ToArray();
        }

        private static void AddPreQuestId(ICollection<int> ids, int questId)
        {
            if (questId > 0)
            {
                ids.Add(questId);
            }
        }

        private static QuestObjectiveConfig[] CreateObjectiveConfigs(IReadOnlyList<QuestObjectiveTableConfig> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return Array.Empty<QuestObjectiveConfig>();
            }

            List<QuestObjectiveConfig> objectives = new List<QuestObjectiveConfig>();
            for (int i = 0; i < rows.Count; i++)
            {
                QuestObjectiveTableConfig row = rows[i];
                if (row == null || !row.Enable)
                {
                    continue;
                }

                objectives.Add(new QuestObjectiveConfig
                {
                    Id = row.ObjectiveId,
                    Type = (QuestObjectiveType)row.Type,
                    TargetId = row.TargetId,
                    TargetCount = row.TargetCount,
                    Text = row.Text,
                    Enable = row.Enable,
                });
            }

            return objectives.ToArray();
        }

        private void EnsureAutoAcceptedQuests()
        {
            bool changed = false;
            for (int i = 0; i < configs.Count; i++)
            {
                QuestConfig config = configs[i];
                if (config == null ||
                    !config.Enable ||
                    config.AcceptMode != QuestAcceptMode.Auto ||
                    dataByQuestId.ContainsKey(config.Id) ||
                    !ArePrerequisitesCompleted(config))
                {
                    continue;
                }

                AddAcceptedQuest(config);
                changed = true;
            }

            if (changed)
            {
                MarkDirtyIfReady();
            }
        }

        private void RefreshQuestStates()
        {
            bool changed = false;
            List<int> ids = new List<int>(dataByQuestId.Keys);
            ids.Sort();

            for (int i = 0; i < ids.Count; i++)
            {
                int questId = ids[i];
                if (!dataByQuestId.TryGetValue(questId, out QuestData data) ||
                    data == null ||
                    data.State != QuestState.Accepted ||
                    !configById.TryGetValue(questId, out QuestConfig config))
                {
                    continue;
                }

                if (AreObjectivesCompleted(config))
                {
                    data.State = QuestState.Completed;
                    changed = true;
                    NotifyQuestCompleted(config);
                }
            }

            if (changed)
            {
                MarkDirtyIfReady();
                EnsureAutoAcceptedQuests();
            }
        }

        private bool AcceptEventQuests(QuestEventType type, int targetId)
        {
            bool changed = false;
            for (int i = 0; i < configs.Count; i++)
            {
                QuestConfig config = configs[i];
                if (config == null ||
                    !config.Enable ||
                    config.AcceptMode != QuestAcceptMode.Event ||
                    config.AcceptEventType != type ||
                    config.AcceptTargetId != targetId ||
                    dataByQuestId.ContainsKey(config.Id) ||
                    !ArePrerequisitesCompleted(config))
                {
                    continue;
                }

                AddAcceptedQuest(config);
                changed = true;
            }

            return changed;
        }

        private void AddAcceptedQuest(QuestConfig config)
        {
            if (config == null)
            {
                return;
            }

            dataByQuestId[config.Id] = new QuestData
            {
                QuestId = config.Id,
                State = QuestState.Accepted,
                Objectives = Array.Empty<QuestObjectiveData>(),
            };

            if (trackedQuestId <= 0 || IsTrackedQuestCompletedOrInvalid())
            {
                trackedQuestId = config.Id;
            }

            NotifyQuestAccepted(config);
            NotifyQuestChanged(config.Id);
        }

        private void EnsureTrackedQuest()
        {
            if (CanTrackQuest(trackedQuestId))
            {
                return;
            }

            trackedQuestId = 0;

            for (int i = 0; i < configs.Count; i++)
            {
                QuestConfig config = configs[i];
                if (config != null &&
                    dataByQuestId.TryGetValue(config.Id, out QuestData data) &&
                    data != null &&
                    data.State == QuestState.Accepted &&
                    CanTrackQuest(config.Id))
                {
                    trackedQuestId = config.Id;
                    return;
                }
            }

            for (int i = 0; i < configs.Count; i++)
            {
                QuestConfig config = configs[i];
                if (config != null &&
                    dataByQuestId.TryGetValue(config.Id, out QuestData data) &&
                    data != null &&
                    data.State == QuestState.Completed &&
                    CanTrackQuest(config.Id))
                {
                    trackedQuestId = config.Id;
                    return;
                }
            }
        }

        private bool CanTrackQuest(int questId)
        {
            if (questId <= 0 ||
                !configById.TryGetValue(questId, out QuestConfig config) ||
                config == null ||
                !config.Enable ||
                !dataByQuestId.TryGetValue(questId, out QuestData data) ||
                data == null)
            {
                return false;
            }

            return data.State == QuestState.Accepted || data.State == QuestState.Completed;
        }

        private bool IsTrackedQuestCompletedOrInvalid()
        {
            return trackedQuestId <= 0 ||
                   !dataByQuestId.TryGetValue(trackedQuestId, out QuestData data) ||
                   data == null ||
                   data.State != QuestState.Accepted;
        }

        private bool UpdateCounterObjectives(QuestEventType type, int targetId, int count)
        {
            bool changed = false;
            List<int> ids = new List<int>(dataByQuestId.Keys);
            ids.Sort();

            for (int questIndex = 0; questIndex < ids.Count; questIndex++)
            {
                int questId = ids[questIndex];
                if (!dataByQuestId.TryGetValue(questId, out QuestData data))
                {
                    continue;
                }

                if (data == null ||
                    data.State != QuestState.Accepted ||
                    !configById.TryGetValue(data.QuestId, out QuestConfig config) ||
                    config.Objectives == null)
                {
                    continue;
                }

                for (int i = 0; i < config.Objectives.Length; i++)
                {
                    QuestObjectiveConfig objective = config.Objectives[i];
                    if (objective == null ||
                        !objective.Enable ||
                        !IsCounterObjective(objective.Type) ||
                        !IsEventMatch(objective, type, targetId))
                    {
                        continue;
                    }

                    int current = GetSavedObjectiveProgress(data, objective.Id);
                    int target = GetTarget(objective);
                    if (current >= target)
                    {
                        continue;
                    }

                    SetSavedObjectiveProgress(data, objective.Id, Mathf.Min(target, current + count));
                    changed = true;
                    NotifyQuestChanged(config.Id);
                }
            }

            return changed;
        }

        private QuestObjectiveSnapshot[] CreateObjectiveSnapshots(QuestConfig config)
        {
            objectiveSnapshotBuffer.Clear();
            if (config?.Objectives == null)
            {
                return Array.Empty<QuestObjectiveSnapshot>();
            }

            for (int i = 0; i < config.Objectives.Length; i++)
            {
                QuestObjectiveConfig objective = config.Objectives[i];
                if (objective == null || !objective.Enable)
                {
                    continue;
                }

                int target = GetTarget(objective);
                int progress = Mathf.Clamp(GetProgress(config.Id, objective), 0, target);
                objectiveSnapshotBuffer.Add(new QuestObjectiveSnapshot
                {
                    Config = objective,
                    Progress = progress,
                    Target = target,
                    Completed = progress >= target,
                });
            }

            return objectiveSnapshotBuffer.ToArray();
        }

        private bool TryGrantRewards(QuestConfig config)
        {
            if (config == null || config.RewardGroupId <= 0)
            {
                return true;
            }

            rewardResolver ??= new RewardResolver(DataManager.Instance.Reward);
            IReadOnlyList<ItemStack> rewards = rewardResolver.GetRewardGroup(config.RewardGroupId);
            if (rewards.Count == 0)
            {
                Debug.LogWarning($"Quest claim failed. Empty reward group: {config.RewardGroupId}, questId: {config.Id}");
                return false;
            }

            return BagManager.Instance.TryAddItems(rewards);
        }

        private static List<RewardConfig> GetRewardRows(int rewardGroupId)
        {
            List<RewardConfig> rewards = new List<RewardConfig>();
            if (rewardGroupId <= 0)
            {
                return rewards;
            }

            IReadOnlyDictionary<int, RewardConfig> table = DataManager.Instance.Reward?.GetAll();
            if (table == null)
            {
                return rewards;
            }

            foreach (KeyValuePair<int, RewardConfig> pair in table)
            {
                RewardConfig reward = pair.Value;
                if (reward == null || reward.GroupId != rewardGroupId || reward.ItemId <= 0 || reward.MaxCount <= 0)
                {
                    continue;
                }

                rewards.Add(reward);
            }

            rewards.Sort((a, b) => a.Id.CompareTo(b.Id));
            return rewards;
        }

        private bool AreObjectivesCompleted(QuestConfig config)
        {
            if (config?.Objectives == null || config.Objectives.Length == 0)
            {
                return false;
            }

            bool hasObjective = false;
            for (int i = 0; i < config.Objectives.Length; i++)
            {
                QuestObjectiveConfig objective = config.Objectives[i];
                if (objective == null || !objective.Enable)
                {
                    continue;
                }

                hasObjective = true;
                if (GetProgress(config.Id, objective) < GetTarget(objective))
                {
                    return false;
                }
            }

            return hasObjective;
        }

        private int GetProgress(int questId, QuestObjectiveConfig objective)
        {
            if (objective == null)
            {
                return 0;
            }

            switch (objective.Type)
            {
                case QuestObjectiveType.ItemCount:
                    return ItemManager.Instance.GetCount(objective.TargetId);
                case QuestObjectiveType.BuildBuilding:
                    return WorldBuildingManager.Instance.CountBuildingConfig(objective.TargetId);
                case QuestObjectiveType.BuildBuildingType:
                    return WorldBuildingManager.Instance.CountActiveBuildingType((WorldBuildingType)objective.TargetId);
                case QuestObjectiveType.FarmCount:
                    return FarmManager.Instance.CountFarmsOnCurrentMap();
                case QuestObjectiveType.TechResearched:
                    return TechManager.Instance.IsResearched(objective.TargetId) ? 1 : 0;
                default:
                    return GetSavedObjectiveProgress(questId, objective.Id);
            }
        }

        private QuestState GetState(QuestConfig config)
        {
            if (config == null || !ArePrerequisitesCompleted(config))
            {
                return QuestState.Locked;
            }

            if (dataByQuestId.TryGetValue(config.Id, out QuestData data) && data != null)
            {
                return data.State;
            }

            return config.AcceptMode == QuestAcceptMode.Manual ? QuestState.Available : QuestState.Locked;
        }

        private bool ArePrerequisitesCompleted(QuestConfig config)
        {
            if (config?.PreQuestIds == null || config.PreQuestIds.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < config.PreQuestIds.Length; i++)
            {
                int prerequisiteId = config.PreQuestIds[i];
                if (prerequisiteId > 0 && !IsCompleted(prerequisiteId))
                {
                    return false;
                }
            }

            return true;
        }

        private static int GetTarget(QuestObjectiveConfig objective)
        {
            return Mathf.Max(1, objective != null ? objective.TargetCount : 1);
        }

        private static bool IsCounterObjective(QuestObjectiveType type)
        {
            switch (type)
            {
                case QuestObjectiveType.ItemGainCount:
                case QuestObjectiveType.ItemUseCount:
                case QuestObjectiveType.Blueprint:
                case QuestObjectiveType.UpgradeBuilding:
                case QuestObjectiveType.PlantCrop:
                case QuestObjectiveType.HarvestCrop:
                case QuestObjectiveType.TalkNpc:
                case QuestObjectiveType.EnterArea:
                case QuestObjectiveType.CustomFlag:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsEventMatch(QuestObjectiveConfig objective, QuestEventType type, int targetId)
        {
            if (objective == null)
            {
                return false;
            }

            if (objective.TargetId > 0 && objective.TargetId != targetId)
            {
                return false;
            }

            switch (objective.Type)
            {
                case QuestObjectiveType.ItemGainCount:
                    return type == QuestEventType.GainItem;
                case QuestObjectiveType.ItemUseCount:
                    return type == QuestEventType.UseItem;
                case QuestObjectiveType.Blueprint:
                    return type == QuestEventType.BlueprintCompleted;
                case QuestObjectiveType.UpgradeBuilding:
                    return type == QuestEventType.UpgradeBuilding;
                case QuestObjectiveType.PlantCrop:
                    return type == QuestEventType.PlantCrop;
                case QuestObjectiveType.HarvestCrop:
                    return type == QuestEventType.HarvestCrop;
                case QuestObjectiveType.TalkNpc:
                    return type == QuestEventType.TalkNpc;
                case QuestObjectiveType.EnterArea:
                    return type == QuestEventType.EnterArea;
                case QuestObjectiveType.CustomFlag:
                    return type == QuestEventType.CustomFlag;
                default:
                    return false;
            }
        }

        private int GetSavedObjectiveProgress(int questId, int objectiveId)
        {
            return dataByQuestId.TryGetValue(questId, out QuestData data)
                ? GetSavedObjectiveProgress(data, objectiveId)
                : 0;
        }

        private static int GetSavedObjectiveProgress(QuestData data, int objectiveId)
        {
            if (data?.Objectives == null)
            {
                return 0;
            }

            for (int i = 0; i < data.Objectives.Length; i++)
            {
                QuestObjectiveData objective = data.Objectives[i];
                if (objective != null && objective.ObjectiveId == objectiveId)
                {
                    return objective.Progress;
                }
            }

            return 0;
        }

        private static void SetSavedObjectiveProgress(QuestData data, int objectiveId, int progress)
        {
            if (data == null || objectiveId <= 0)
            {
                return;
            }

            if (data.Objectives == null)
            {
                data.Objectives = Array.Empty<QuestObjectiveData>();
            }

            for (int i = 0; i < data.Objectives.Length; i++)
            {
                QuestObjectiveData objective = data.Objectives[i];
                if (objective != null && objective.ObjectiveId == objectiveId)
                {
                    objective.Progress = Mathf.Max(0, progress);
                    return;
                }
            }

            QuestObjectiveData[] next = new QuestObjectiveData[data.Objectives.Length + 1];
            Array.Copy(data.Objectives, next, data.Objectives.Length);
            next[next.Length - 1] = new QuestObjectiveData
            {
                ObjectiveId = objectiveId,
                Progress = Mathf.Max(0, progress),
            };
            data.Objectives = next;
        }

        private void AddQuestData(QuestData data)
        {
            if (data == null || data.QuestId <= 0 || !configById.ContainsKey(data.QuestId))
            {
                return;
            }

            if (data.State == QuestState.Locked || data.State == QuestState.Available)
            {
                data.State = QuestState.Accepted;
            }

            dataByQuestId[data.QuestId] = CloneQuestData(data);
        }

        private void AddLegacyQuestIds(int[] questIds, QuestState state)
        {
            if (questIds == null)
            {
                return;
            }

            for (int i = 0; i < questIds.Length; i++)
            {
                int questId = questIds[i];
                if (questId <= 0 || !configById.ContainsKey(questId))
                {
                    continue;
                }

                dataByQuestId[questId] = new QuestData
                {
                    QuestId = questId,
                    State = state,
                    Objectives = Array.Empty<QuestObjectiveData>(),
                };
            }
        }

        private void RemoveInvalidQuestData()
        {
            List<int> invalidIds = new List<int>();
            foreach (int questId in dataByQuestId.Keys)
            {
                if (!configById.ContainsKey(questId))
                {
                    invalidIds.Add(questId);
                }
            }

            for (int i = 0; i < invalidIds.Count; i++)
            {
                dataByQuestId.Remove(invalidIds[i]);
            }
        }

        private static QuestData CloneQuestData(QuestData data)
        {
            QuestObjectiveData[] objectives = Array.Empty<QuestObjectiveData>();
            if (data.Objectives != null && data.Objectives.Length > 0)
            {
                objectives = new QuestObjectiveData[data.Objectives.Length];
                for (int i = 0; i < data.Objectives.Length; i++)
                {
                    QuestObjectiveData source = data.Objectives[i];
                    objectives[i] = source != null
                        ? new QuestObjectiveData { ObjectiveId = source.ObjectiveId, Progress = source.Progress }
                        : null;
                }
            }

            return new QuestData
            {
                QuestId = data.QuestId,
                State = data.State,
                Objectives = objectives,
            };
        }

        private static string GetQuestName(QuestConfig config)
        {
            if (config == null)
            {
                return string.Empty;
            }

            return LocalizationManager.GetOrFallback($"quest.{config.Id}.name", config.Name);
        }

        private static void AppendQuestText(StringBuilder builder, QuestSnapshot quest, bool includeBlankLine)
        {
            if (builder == null || quest == null)
            {
                return;
            }

            string marker = quest.State == QuestState.Completed ? "[Done] " : string.Empty;
            builder.Append(marker);
            builder.AppendLine(GetQuestName(quest.Config));

            if (quest.Objectives == null || quest.Objectives.Length == 0)
            {
                builder.Append("  ");
                builder.Append(quest.Progress);
                builder.Append('/');
                builder.AppendLine(quest.Target.ToString());
            }
            else
            {
                for (int i = 0; i < quest.Objectives.Length; i++)
                {
                    QuestObjectiveSnapshot objective = quest.Objectives[i];
                    builder.Append("  ");
                    builder.Append(GetObjectiveText(quest.Config, objective.Config));
                    builder.Append(' ');
                    builder.Append(objective.Progress);
                    builder.Append('/');
                    builder.AppendLine(objective.Target.ToString());
                }
            }

            if (includeBlankLine)
            {
                builder.AppendLine();
            }
        }

        private static string GetObjectiveText(QuestConfig quest, QuestObjectiveConfig objective)
        {
            if (objective == null)
            {
                return string.Empty;
            }

            string fallback = !string.IsNullOrWhiteSpace(objective.Text)
                ? objective.Text
                : GetDefaultObjectiveText(objective);

            return quest != null
                ? LocalizationManager.GetOrFallback($"quest.{quest.Id}.objective.{objective.Id}.text", fallback)
                : fallback;
        }

        private static string GetDefaultObjectiveText(QuestObjectiveConfig objective)
        {
            switch (objective.Type)
            {
                case QuestObjectiveType.ItemCount:
                case QuestObjectiveType.ItemGainCount:
                case QuestObjectiveType.ItemUseCount:
                    return LocalizedConfigText.ItemName(objective.TargetId);
                case QuestObjectiveType.Blueprint:
                    BlueprintConfig blueprint = BlueprintManager.Instance.Get(objective.TargetId);
                    return blueprint != null ? LocalizedConfigText.BlueprintName(blueprint.Id, blueprint.Name) : objective.TargetId.ToString();
                case QuestObjectiveType.BuildBuilding:
                    return LocalizedConfigText.BuildingName(objective.TargetId);
                case QuestObjectiveType.TechResearched:
                    return LocalizedConfigText.TechName(objective.TargetId);
                default:
                    return objective.Type.ToString();
            }
        }

        private void NotifyQuestChanged(int questId, bool fullRefresh = false)
        {
            if (loading)
            {
                return;
            }

            Messager.Instance.Notify(WorldMessageTopic.QuestChanged, new QuestChangedMessage
            {
                QuestId = questId,
                FullRefresh = fullRefresh,
            });
        }

        private void NotifyQuestCompleted(QuestConfig config)
        {
            if (loading || config == null)
            {
                return;
            }

            string questName = GetQuestName(config);
            Messager.Instance.Notify(WorldMessageTopic.QuestCompleted, new QuestCompletedMessage
            {
                QuestId = config.Id,
                QuestName = questName,
            });

            Messager.Instance.Notify(WorldMessageTopic.QuestChanged, new QuestChangedMessage
            {
                QuestId = config.Id,
                FullRefresh = false,
            });
        }

        private void NotifyQuestAccepted(QuestConfig config)
        {
            if (loading || config == null)
            {
                return;
            }

            Messager.Instance.Notify(WorldMessageTopic.QuestAccepted, new QuestAcceptedMessage
            {
                QuestId = config.Id,
                QuestName = GetQuestName(config),
            });
        }

        private void MarkDirtyIfReady()
        {
            if (!loading)
            {
                StorageManager.Instance.MarkDirty();
            }
        }
    }
}
