using System.Collections.Generic;
using System.Text;

namespace Game
{
    public enum WorldTaskConditionType
    {
        None = 0,
        BuildBuildingType = 1,
        ItemCount = 2,
        FarmCount = 3,
        MineCount = 4,
    }

    public sealed class WorldTaskConfig
    {
        public int Id;
        public string Name;
        public WorldTaskConditionType ConditionType;
        public int TargetId;
        public int TargetCount;
        public bool Enable = true;
    }

    public sealed class WorldTaskManager
    {
        public static WorldTaskManager Instance { get; } = new WorldTaskManager();

        private readonly List<WorldTaskConfig> tasks = new List<WorldTaskConfig>();
        private readonly StringBuilder builder = new StringBuilder(256);

        private WorldTaskManager()
        {
        }

        public void Initialize()
        {
            tasks.Clear();
            AddDefaultTasks();
        }

        public string GetGoalText()
        {
            builder.Clear();
            builder.AppendLine(LocalizationManager.Get("ui.task.current_goals"));

            for (int i = 0; i < tasks.Count; i++)
            {
                WorldTaskConfig task = tasks[i];
                if (task == null || !task.Enable)
                {
                    continue;
                }

                int progress = GetProgress(task);
                int target = task.TargetCount > 0 ? task.TargetCount : 1;
                builder.Append(GetTaskName(task));
                builder.Append("  ");
                builder.Append(progress > target ? target : progress);
                builder.Append('/');
                builder.AppendLine(target.ToString());
            }

            return builder.ToString();
        }

        private static string GetTaskName(WorldTaskConfig task)
        {
            if (task == null)
            {
                return string.Empty;
            }

            return LocalizationManager.GetOrFallback($"task.{task.Id}.name", task.Name);
        }

        private int GetProgress(WorldTaskConfig task)
        {
            switch (task.ConditionType)
            {
                case WorldTaskConditionType.BuildBuildingType:
                    return WorldBuildingManager.Instance.CountActiveBuildingType((WorldBuildingType)task.TargetId);
                case WorldTaskConditionType.ItemCount:
                    return WorldItemManager.Instance.GetCount(task.TargetId);
                case WorldTaskConditionType.FarmCount:
                    return FarmManager.Instance.CountFarmsOnCurrentMap();
                case WorldTaskConditionType.MineCount:
                    return WorldBuildingManager.Instance.CountActiveBuildingType(WorldBuildingType.Mine);
                default:
                    return 0;
            }
        }

        private void AddDefaultTasks()
        {
            tasks.Add(new WorldTaskConfig
            {
                Id = 60000001,
                Name = "Build House",
                ConditionType = WorldTaskConditionType.BuildBuildingType,
                TargetId = (int)WorldBuildingType.House,
                TargetCount = 1,
            });

            tasks.Add(new WorldTaskConfig
            {
                Id = 60000002,
                Name = "Gather Wood",
                ConditionType = WorldTaskConditionType.ItemCount,
                TargetId = ItemIds.Wood,
                TargetCount = 500,
            });

            tasks.Add(new WorldTaskConfig
            {
                Id = 60000003,
                Name = "Gather Stone",
                ConditionType = WorldTaskConditionType.ItemCount,
                TargetId = ItemIds.Stone,
                TargetCount = 300,
            });

            tasks.Add(new WorldTaskConfig
            {
                Id = 60000004,
                Name = "Open Farm",
                ConditionType = WorldTaskConditionType.FarmCount,
                TargetCount = 2,
            });

            tasks.Add(new WorldTaskConfig
            {
                Id = 60000005,
                Name = "Build Mine",
                ConditionType = WorldTaskConditionType.MineCount,
                TargetCount = 1,
            });
        }
    }
}
