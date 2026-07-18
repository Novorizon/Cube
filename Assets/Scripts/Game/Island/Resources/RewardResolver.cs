using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class RewardResolver
    {
        private readonly ConfigTableReader<RewardConfig> rewardTable;

        public RewardResolver(ConfigTableReader<RewardConfig> rewardTable)
        {
            this.rewardTable = rewardTable;
        }

        public IReadOnlyList<ItemStack> GetRewardGroup(int groupId, System.Random random = null)
        {
            if (groupId <= 0)
            {
                return Array.Empty<ItemStack>();
            }

            IReadOnlyDictionary<int, RewardConfig> configs = rewardTable?.GetAll();
            if (configs == null)
            {
                return Array.Empty<ItemStack>();
            }

            random ??= new System.Random();
            List<ItemStack> rewards = new List<ItemStack>();
            foreach (KeyValuePair<int, RewardConfig> pair in configs)
            {
                RewardConfig config = pair.Value;
                if (config == null || config.GroupId != groupId || config.ItemId <= 0 || config.MaxCount <= 0)
                {
                    continue;
                }

                int minCount = Math.Max(0, config.MinCount);
                int maxCount = Math.Max(minCount, config.MaxCount);
                int count = minCount == maxCount ? minCount : random.Next(minCount, maxCount + 1);
                rewards.Add(new ItemStack(config.ItemId, count));
            }

            return rewards;
        }
    }
}
