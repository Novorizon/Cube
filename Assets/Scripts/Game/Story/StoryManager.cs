using Game.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class StoryManager
    {
        public static StoryManager Instance { get; } = new StoryManager();

        private readonly Dictionary<int, StoryConfig> configs = new Dictionary<int, StoryConfig>();
        private readonly List<StoryConfig> configList = new List<StoryConfig>();
        private readonly HashSet<int> completedStoryIds = new HashSet<int>();
        private readonly StoryPresenter presenter = new StoryPresenter();

        private ISubscription questCompletedSubscription;
        private int currentStoryId;
        private int currentStepIndex;
        private bool loading;

        private StoryManager()
        {
        }

        public void Initialize()
        {
            configs.Clear();
            configList.Clear();
            completedStoryIds.Clear();
            currentStoryId = 0;
            currentStepIndex = 0;

            LoadConfigs();

            questCompletedSubscription?.Dispose();
            questCompletedSubscription = Messager.Instance.Subscribe<WorldMessageTopic, QuestCompletedMessage>(
                WorldMessageTopic.QuestCompleted,
                OnQuestCompleted);
        }

        public void Release()
        {
            questCompletedSubscription?.Dispose();
            questCompletedSubscription = null;
        }

        public void LoadSaveData(StorageManager.StoryData data)
        {
            loading = true;
            completedStoryIds.Clear();
            currentStoryId = data != null ? data.CurrentStoryId : 0;
            currentStepIndex = data != null ? Mathf.Max(0, data.CurrentStepIndex) : 0;

            if (data?.CompletedStoryIds != null)
            {
                for (int i = 0; i < data.CompletedStoryIds.Length; i++)
                {
                    int storyId = data.CompletedStoryIds[i];
                    if (storyId > 0)
                    {
                        completedStoryIds.Add(storyId);
                    }
                }
            }

            loading = false;
        }

        public StorageManager.StoryData CreateSaveData()
        {
            List<int> completed = new List<int>(completedStoryIds);
            completed.Sort();

            return new StorageManager.StoryData
            {
                CurrentStoryId = currentStoryId,
                CurrentStepIndex = currentStepIndex,
                CompletedStoryIds = completed.ToArray(),
            };
        }

        public bool IsCompleted(int storyId)
        {
            return storyId > 0 && completedStoryIds.Contains(storyId);
        }

        public bool TryStartAutoStories()
        {
            if (TryResumeCurrentStory())
            {
                return true;
            }

            for (int i = 0; i < configList.Count; i++)
            {
                StoryConfig config = configList[i];
                if (config != null &&
                    config.TriggerMode == StoryTriggerMode.AutoOnNewGame &&
                    CanStart(config))
                {
                    Start(config);
                    return true;
                }
            }

            return false;
        }

        public bool TryResumeCurrentStory()
        {
            if (currentStoryId <= 0)
            {
                return false;
            }

            if (!configs.TryGetValue(currentStoryId, out StoryConfig config) ||
                config?.Steps == null ||
                config.Steps.Length == 0)
            {
                currentStoryId = 0;
                currentStepIndex = 0;
                MarkDirtyIfReady();
                return false;
            }

            currentStepIndex = Mathf.Clamp(currentStepIndex, 0, config.Steps.Length - 1);
            PresentCurrent(config);
            return true;
        }

        public bool TryStart(int storyId)
        {
            if (!configs.TryGetValue(storyId, out StoryConfig config))
            {
                return false;
            }

            if (!CanStart(config))
            {
                return false;
            }

            Start(config);
            return true;
        }

        public void NotifyEvent(StoryTriggerMode triggerMode, int targetId)
        {
            if (triggerMode == StoryTriggerMode.Manual || targetId <= 0)
            {
                return;
            }

            for (int i = 0; i < configList.Count; i++)
            {
                StoryConfig config = configList[i];
                if (config != null &&
                    config.TriggerMode == triggerMode &&
                    config.TriggerTargetId == targetId &&
                    CanStart(config))
                {
                    Start(config);
                    return;
                }
            }
        }

        private void LoadConfigs()
        {
            IReadOnlyDictionary<int, StoryTableConfig> table = DataManager.Instance.Story?.GetAll();
            if (table == null)
            {
                Debug.LogError("Story config table is not loaded.");
                return;
            }

            Dictionary<int, List<StoryStepTableConfig>> stepsByStoryId = BuildStoryStepIndex();
            List<StoryTableConfig> rows = new List<StoryTableConfig>(table.Values);
            rows.Sort((a, b) => a.Id.CompareTo(b.Id));

            for (int i = 0; i < rows.Count; i++)
            {
                AddConfig(CreateStoryConfig(rows[i], stepsByStoryId));
            }
        }

        private static Dictionary<int, List<StoryStepTableConfig>> BuildStoryStepIndex()
        {
            Dictionary<int, List<StoryStepTableConfig>> index = new Dictionary<int, List<StoryStepTableConfig>>();
            IReadOnlyDictionary<int, StoryStepTableConfig> table = DataManager.Instance.StoryStep?.GetAll();
            if (table == null)
            {
                return index;
            }

            foreach (KeyValuePair<int, StoryStepTableConfig> pair in table)
            {
                StoryStepTableConfig row = pair.Value;
                if (row == null || !row.Enable || row.StoryId <= 0)
                {
                    continue;
                }

                if (!index.TryGetValue(row.StoryId, out List<StoryStepTableConfig> rows))
                {
                    rows = new List<StoryStepTableConfig>();
                    index.Add(row.StoryId, rows);
                }

                rows.Add(row);
            }

            foreach (List<StoryStepTableConfig> rows in index.Values)
            {
                rows.Sort((a, b) => a.StepIndex != b.StepIndex
                    ? a.StepIndex.CompareTo(b.StepIndex)
                    : a.Id.CompareTo(b.Id));
            }

            return index;
        }

        private static StoryConfig CreateStoryConfig(
            StoryTableConfig row,
            IReadOnlyDictionary<int, List<StoryStepTableConfig>> stepsByStoryId)
        {
            if (row == null)
            {
                return null;
            }

            stepsByStoryId.TryGetValue(row.Id, out List<StoryStepTableConfig> steps);
            return new StoryConfig
            {
                Id = row.Id,
                Title = row.Title,
                Steps = CreateSteps(steps),
                TriggerMode = (StoryTriggerMode)row.TriggerMode,
                TriggerTargetId = row.TriggerTargetId,
                CompleteQuestEventType = (QuestEventType)row.CompleteQuestEventType,
                CompleteQuestTargetId = row.CompleteQuestTargetId,
                NextStoryId = row.NextStoryId,
                Repeatable = row.Repeatable,
                Enable = row.Enable,
            };
        }

        private static StoryStep[] CreateSteps(IReadOnlyList<StoryStepTableConfig> rows)
        {
            if (rows == null || rows.Count == 0)
            {
                return System.Array.Empty<StoryStep>();
            }

            List<StoryStep> steps = new List<StoryStep>(rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                StoryStepTableConfig row = rows[i];
                if (row != null)
                {
                    steps.Add(new StoryStep
                    {
                        Id = row.Id,
                        StepIndex = row.StepIndex,
                        StepType = (StoryStepType)row.StepType,
                        Text = row.Text,
                        IllustrationPath = row.IllustrationPath,
                        MotionPreset = (StoryMotionPreset)row.MotionPreset,
                        MotionDuration = Mathf.Max(0f, row.MotionDuration),
                        AdvanceMode = (StoryAdvanceMode)row.AdvanceMode,
                        AutoAdvanceDelay = Mathf.Max(0f, row.AutoAdvanceDelay),
                        GuideTargetId = row.GuideTargetId,
                        GuideText = row.GuideText,
                        AllowTargetInteraction = row.AllowTargetInteraction,
                    });
                }
            }

            return steps.ToArray();
        }

        private void AddConfig(StoryConfig config)
        {
            if (config == null || config.Id <= 0 || !config.Enable || configs.ContainsKey(config.Id))
            {
                return;
            }

            configs.Add(config.Id, config);
            configList.Add(config);
        }

        private bool CanStart(StoryConfig config)
        {
            return config != null &&
                   config.Enable &&
                   config.Steps != null &&
                   config.Steps.Length > 0 &&
                   currentStoryId == 0 &&
                   (config.Repeatable || !completedStoryIds.Contains(config.Id));
        }

        private void Start(StoryConfig config)
        {
            currentStoryId = config.Id;
            currentStepIndex = 0;
            MarkDirtyIfReady();
            PresentCurrent(config);
        }

        private void PresentCurrent(StoryConfig config)
        {
            presenter.Present(
                config,
                currentStepIndex,
                stepIndex => UpdateCurrentStepIndex(config.Id, stepIndex),
                () => Complete(config.Id));
        }

        private void UpdateCurrentStepIndex(int storyId, int stepIndex)
        {
            if (currentStoryId != storyId)
            {
                return;
            }

            currentStepIndex = Mathf.Max(0, stepIndex);
            MarkDirtyIfReady();
        }

        private void Complete(int storyId)
        {
            if (!configs.TryGetValue(storyId, out StoryConfig config))
            {
                currentStoryId = 0;
                currentStepIndex = 0;
                MarkDirtyIfReady();
                return;
            }

            if (!config.Repeatable)
            {
                completedStoryIds.Add(storyId);
            }

            currentStoryId = 0;
            currentStepIndex = 0;

            if (config.CompleteQuestEventType != QuestEventType.None && config.CompleteQuestTargetId > 0)
            {
                QuestManager.Instance.NotifyEvent(config.CompleteQuestEventType, config.CompleteQuestTargetId);
            }

            MarkDirtyIfReady();

            if (config.NextStoryId > 0)
            {
                TryStart(config.NextStoryId);
            }
        }

        private void OnQuestCompleted(QuestCompletedMessage message)
        {
            if (message == null)
            {
                return;
            }

            NotifyEvent(StoryTriggerMode.QuestCompleted, message.QuestId);
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
