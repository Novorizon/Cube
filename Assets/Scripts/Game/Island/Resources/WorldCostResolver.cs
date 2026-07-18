using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class WorldCostResolver
    {
        private readonly ConfigTableReader<WorldCostConfig> worldCostTable;

        public WorldCostResolver(ConfigTableReader<WorldCostConfig> worldCostTable)
        {
            this.worldCostTable = worldCostTable;
        }

        public IReadOnlyList<ItemStack> GetCostGroup(int groupId)
        {
            if (groupId <= 0)
            {
                return Array.Empty<ItemStack>();
            }

            IReadOnlyDictionary<int, WorldCostConfig> configs = worldCostTable?.GetAll();
            if (configs == null)
            {
                return Array.Empty<ItemStack>();
            }

            List<ItemStack> costs = new List<ItemStack>();
            foreach (KeyValuePair<int, WorldCostConfig> pair in configs)
            {
                WorldCostConfig config = pair.Value;
                if (config == null || config.GroupId != groupId || config.ItemId <= 0 || config.Count <= 0)
                {
                    continue;
                }

                costs.Add(new ItemStack(config.ItemId, config.Count));
            }

            return costs;
        }
    }
}
