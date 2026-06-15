using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class WorldIncomeManager
    {
        public static WorldIncomeManager Instance { get; } = new WorldIncomeManager();

        private readonly Dictionary<int, WorldBuildingIncomeConfig> buildingIncomeByLevel = new Dictionary<int, WorldBuildingIncomeConfig>();
        private WorldRewardResolver rewardResolver;

        private WorldIncomeManager()
        {
        }

        public void Initialize()
        {
            BuildProductionIndex();
            rewardResolver = new WorldRewardResolver(DataManager.Instance.WorldReward);
        }

        public void Update()
        {
            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            bool changed = false;

            changed |= FarmManager.Instance.UpdateIncome(currentUnixTime);
            changed |= UpdateMineIncome(currentUnixTime);
            changed |= UpdateProductionBuildingIncome(currentUnixTime);

            if (changed)
            {
                StorageManager.Instance.MarkDirty();
            }
        }

        public bool TryGetBuildingIncome(int buildingId, int level, out WorldBuildingIncomeConfig buildingIncome)
        {
            int key = DataManager.MakeWorldBuildingLevelId(buildingId, level);
            return buildingIncomeByLevel.TryGetValue(key, out buildingIncome);
        }

        private bool UpdateMineIncome(long currentUnixTime)
        {
            return UpdateBuildingIncome(MineManager.Instance.GetActiveMines(), currentUnixTime);
        }

        private bool UpdateProductionBuildingIncome(long currentUnixTime)
        {
            List<WorldBuilding> sources = new List<WorldBuilding>();
            int mapId = MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
            if (mapId <= 0)
            {
                return false;
            }

            foreach (KeyValuePair<int, WorldBuilding> pair in WorldBuildingManager.Instance.GetAllBuildings())
            {
                WorldBuilding building = pair.Value;
                if (building == null || building.MapId != mapId || building.Status != WorldBuildingStatus.Active)
                {
                    continue;
                }

                if (WorldBuildingManager.Instance.IsBuildingType(building, WorldBuildingType.Mine))
                {
                    continue;
                }

                sources.Add(building);
            }

            return UpdateBuildingIncome(sources, currentUnixTime);
        }

        private bool UpdateBuildingIncome(IReadOnlyList<WorldBuilding> buildings, long currentUnixTime)
        {
            if (buildings == null || buildings.Count == 0)
            {
                return false;
            }

            bool changed = false;
            for (int i = 0; i < buildings.Count; i++)
            {
                WorldBuilding building = buildings[i];
                if (building == null || building.Status != WorldBuildingStatus.Active)
                {
                    continue;
                }

                if (!TryGetBuildingIncome(building.ConfigId, building.Level, out WorldBuildingIncomeConfig buildingIncome))
                {
                    continue;
                }

                if (building.NextIncomeAtUnixTime <= 0)
                {
                    building.SetNextIncomeAt(currentUnixTime + buildingIncome.CycleSeconds);
                    changed = true;
                    continue;
                }

                if (currentUnixTime < building.NextIncomeAtUnixTime)
                {
                    continue;
                }

                IReadOnlyList<WorldItem> rewards = GetRewards(buildingIncome.OutputRewardGroupId);
                if (rewards.Count == 0)
                {
                    building.SetNextIncomeAt(currentUnixTime + buildingIncome.CycleSeconds);
                    changed = true;
                    continue;
                }

                int cycleSeconds = Math.Max(1, buildingIncome.CycleSeconds);
                int passedCycles = Math.Max(1, (int)((currentUnixTime - building.NextIncomeAtUnixTime) / cycleSeconds) + 1);
                AddRewards(rewards, passedCycles);
                building.SetNextIncomeAt(building.NextIncomeAtUnixTime + (long)passedCycles * cycleSeconds);
                changed = true;
            }

            return changed;
        }

        private void BuildProductionIndex()
        {
            buildingIncomeByLevel.Clear();

            IReadOnlyDictionary<int, WorldBuildingIncomeConfig> configs = DataManager.Instance.WorldBuildingIncome?.GetAll();
            if (configs == null)
            {
                return;
            }

            foreach (KeyValuePair<int, WorldBuildingIncomeConfig> pair in configs)
            {
                WorldBuildingIncomeConfig config = pair.Value;
                if (config == null || !config.Enable || config.BuildingId <= 0 || config.Level <= 0 || config.CycleSeconds <= 0)
                {
                    continue;
                }

                int key = DataManager.MakeWorldBuildingLevelId(config.BuildingId, config.Level);
                buildingIncomeByLevel[key] = config;
            }
        }

        private IReadOnlyList<WorldItem> GetRewards(int rewardGroupId)
        {
            if (rewardGroupId <= 0)
            {
                return Array.Empty<WorldItem>();
            }

            rewardResolver ??= new WorldRewardResolver(DataManager.Instance.WorldReward);
            return rewardResolver.GetRewardGroup(rewardGroupId);
        }

        private static void AddRewards(IReadOnlyList<WorldItem> rewards, int multiplier)
        {
            if (rewards == null || multiplier <= 0)
            {
                return;
            }

            for (int i = 0; i < rewards.Count; i++)
            {
                WorldItem reward = rewards[i];
                if (reward == null || reward.ItemId <= 0 || reward.Count <= 0)
                {
                    continue;
                }

                WorldItemManager.Instance.AddItem(reward.ItemId, reward.Count * multiplier);
            }
        }
    }
}
