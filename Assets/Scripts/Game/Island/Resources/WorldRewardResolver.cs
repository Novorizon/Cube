using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class WorldRewardResolver
    {
        private readonly ConfigTableReader<WorldRewardConfig> worldRewardTable;

        public WorldRewardResolver(ConfigTableReader<WorldRewardConfig> worldRewardTable)
        {
            this.worldRewardTable = worldRewardTable;
        }

        public IReadOnlyList<WorldItem> GetRewardGroup(int groupId, System.Random random = null)
        {
            if (groupId <= 0)
            {
                return Array.Empty<WorldItem>();
            }

            IReadOnlyDictionary<int, WorldRewardConfig> configs = worldRewardTable?.GetAll();
            if (configs == null)
            {
                return Array.Empty<WorldItem>();
            }

            random ??= new System.Random();
            List<WorldItem> rewards = new List<WorldItem>();
            foreach (KeyValuePair<int, WorldRewardConfig> pair in configs)
            {
                WorldRewardConfig config = pair.Value;
                if (config == null || config.GroupId != groupId || config.ItemId <= 0 || config.MaxCount <= 0)
                {
                    continue;
                }

                int minCount = Math.Max(0, config.MinCount);
                int maxCount = Math.Max(minCount, config.MaxCount);
                int count = minCount == maxCount ? minCount : random.Next(minCount, maxCount + 1);
                rewards.Add(new WorldItem(config.ItemId, count));
            }

            return rewards;
        }
    }
}
