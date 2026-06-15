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

        public IReadOnlyList<WorldItem> GetCostGroup(int groupId)
        {
            if (groupId <= 0)
            {
                return Array.Empty<WorldItem>();
            }

            IReadOnlyDictionary<int, WorldCostConfig> configs = worldCostTable?.GetAll();
            if (configs == null)
            {
                return Array.Empty<WorldItem>();
            }

            List<WorldItem> costs = new List<WorldItem>();
            foreach (KeyValuePair<int, WorldCostConfig> pair in configs)
            {
                WorldCostConfig config = pair.Value;
                if (config == null || config.GroupId != groupId || config.ItemId <= 0 || config.Count <= 0)
                {
                    continue;
                }

                costs.Add(new WorldItem(config.ItemId, config.Count));
            }

            return costs;
        }
    }
}
