using System;
using System.Collections.Generic;

namespace Game
{
    public sealed class ProductionStatsProvider
    {
        public static ProductionStatsProvider Instance { get; } = new ProductionStatsProvider();

        private readonly Dictionary<int, float> perMinuteByItem = new Dictionary<int, float>();
        private readonly WorldRewardResolver rewardResolver = new WorldRewardResolver(DataManager.Instance.WorldReward);

        private ProductionStatsProvider()
        {
        }

        public List<ProductionStat> GetStats(ProductionStatGroup group)
        {
            BuildPerMinuteIndex();

            switch (group)
            {
                case ProductionStatGroup.Crops:
                    return BuildStats(BuildCropOutputItemIds());
                case ProductionStatGroup.Ores:
                    return BuildStats(new[] { ItemIds.CopperOre, ItemIds.IronOre });
                case ProductionStatGroup.BasicResources:
                    return BuildStats(new[] { ItemIds.Wood, ItemIds.Stone });
                case ProductionStatGroup.Buildings:
                    return BuildStatsFromProducedItems();
                default:
                    return BuildOverview();
            }
        }

        private void BuildPerMinuteIndex()
        {
            perMinuteByItem.Clear();
            AddFarmRates();
            AddBuildingRates();
        }

        private void AddFarmRates()
        {
            int mapId = MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
            if (mapId <= 0)
            {
                return;
            }

            long currentUnixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (KeyValuePair<int, Farm> pair in FarmManager.Instance.GetAllFarms())
            {
                Farm farm = pair.Value;
                if (farm == null || farm.MapId != mapId || !farm.HasCrop || farm.CellCount <= 0)
                {
                    continue;
                }

                if (currentUnixTime < farm.MatureAtUnixTime)
                {
                    continue;
                }

                if (!FarmManager.Instance.Crops.TryGetValue(farm.CropId, out WorldCropDefinition crop) ||
                    crop == null ||
                    crop.OutputItemId <= 0 ||
                    crop.OutputCountPerSecond <= 0)
                {
                    continue;
                }

                AddPerMinute(crop.OutputItemId, crop.OutputCountPerSecond * farm.CellCount * 60f);
            }
        }

        private void AddBuildingRates()
        {
            int mapId = MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
            if (mapId <= 0)
            {
                return;
            }

            foreach (KeyValuePair<int, WorldBuilding> pair in WorldBuildingManager.Instance.GetAllBuildings())
            {
                WorldBuilding building = pair.Value;
                if (building == null || building.MapId != mapId || building.Status != WorldBuildingStatus.Active)
                {
                    continue;
                }

                if (!WorldIncomeManager.Instance.TryGetBuildingIncome(building.ConfigId, building.Level, out WorldBuildingIncomeConfig income) ||
                    income == null ||
                    income.CycleSeconds <= 0)
                {
                    continue;
                }

                IReadOnlyList<WorldItem> rewards = rewardResolver.GetRewardGroup(income.OutputRewardGroupId);
                for (int i = 0; i < rewards.Count; i++)
                {
                    WorldItem reward = rewards[i];
                    if (reward == null || reward.ItemId <= 0 || reward.Count <= 0 || BagManager.IsBagItem(reward.ItemId))
                    {
                        continue;
                    }

                    AddPerMinute(reward.ItemId, reward.Count * 60f / income.CycleSeconds);
                }
            }
        }

        private List<ProductionStat> BuildOverview()
        {
            List<ProductionStat> result = new List<ProductionStat>();
            result.AddRange(GetStats(ProductionStatGroup.Crops));
            result.AddRange(GetStats(ProductionStatGroup.Ores));
            result.AddRange(GetStats(ProductionStatGroup.BasicResources));
            return result;
        }

        private List<ProductionStat> BuildStatsFromProducedItems()
        {
            List<int> itemIds = new List<int>(perMinuteByItem.Keys);
            itemIds.Sort();
            return BuildStats(itemIds);
        }

        private static List<int> BuildCropOutputItemIds()
        {
            HashSet<int> itemIdSet = new HashSet<int>();
            foreach (KeyValuePair<int, WorldCropDefinition> pair in FarmManager.Instance.Crops)
            {
                WorldCropDefinition crop = pair.Value;
                if (crop == null || crop.OutputItemId <= 0)
                {
                    continue;
                }

                itemIdSet.Add(crop.OutputItemId);
            }

            List<int> result = new List<int>(itemIdSet);
            result.Sort();
            return result;
        }

        private List<ProductionStat> BuildStats(IReadOnlyList<int> itemIds)
        {
            List<ProductionStat> result = new List<ProductionStat>();
            if (itemIds == null)
            {
                return result;
            }

            for (int i = 0; i < itemIds.Count; i++)
            {
                int itemId = itemIds[i];
                if (itemId <= 0 || BagManager.IsBagItem(itemId))
                {
                    continue;
                }

                int count = WorldItemManager.Instance.GetCount(itemId);
                perMinuteByItem.TryGetValue(itemId, out float perMinute);
                if (count <= 0 && perMinute <= 0.001f)
                {
                    continue;
                }

                result.Add(new ProductionStat(itemId, GetItemName(itemId), count, perMinute));
            }

            return result;
        }

        private void AddPerMinute(int itemId, float value)
        {
            if (itemId <= 0 || value <= 0f)
            {
                return;
            }

            if (perMinuteByItem.TryGetValue(itemId, out float current))
            {
                perMinuteByItem[itemId] = current + value;
            }
            else
            {
                perMinuteByItem[itemId] = value;
            }
        }

        private static string GetItemName(int itemId)
        {
            return LocalizedConfigText.ItemName(itemId);
        }
    }
}
