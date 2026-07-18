using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class MineManager
    {
        public static MineManager Instance { get; } = new MineManager();

        private readonly WorldCostResolver costResolver = new WorldCostResolver(DataManager.Instance.WorldCost);

        private MineManager()
        {
        }

        public void Initialize()
        {
        }

        public bool TryBuildMine(WorldResourceView resourceView, ResourceConfig config)
        {
            if (resourceView == null || resourceView.MapObject == null || config == null || !config.Enable)
            {
                return false;
            }

            if (!WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.House))
            {
                return false;
            }

            if (config.MineBuildingId <= 0 || !WorldBuildingManager.Instance.IsBuildingUnlocked(config.MineBuildingId))
            {
                return false;
            }

            if (!CanBuildMineBuilding(config.MineBuildingId))
            {
                return false;
            }

            Vector3Int coord = resourceView.Coord;
            int objectId = resourceView.MapObject.ObjectId;
            if (!MapManager.Instance.TryRemoveMapObject(objectId))
            {
                return false;
            }

            GameObject.Destroy(resourceView.gameObject);

            if (WorldBuildingManager.Instance.TryBuild(config.MineBuildingId, coord))
            {
                MapManager.Instance.MarkMapObjectRemoved(objectId);
                StorageManager.Instance.MarkDirty();
                return true;
            }

            Debug.LogWarning($"Build mine failed after removing mine target. buildingId: {config.MineBuildingId}, coord: {coord}");
            StorageManager.Instance.MarkDirty();
            return false;
        }

        private bool CanBuildMineBuilding(int buildingId)
        {
            if (DataManager.Instance.WorldBuilding == null ||
                !DataManager.Instance.WorldBuilding.TryGet(buildingId, out WorldBuildingConfig buildingConfig) ||
                buildingConfig == null ||
                !buildingConfig.Enable)
            {
                return false;
            }

            if (buildingConfig.MaxCount > 0 && WorldBuildingManager.Instance.CountBuildingConfig(buildingId) >= buildingConfig.MaxCount)
            {
                return false;
            }

            if (!DataManager.Instance.TryGetWorldBuildingLevel(buildingId, 1, out WorldBuildingLevelConfig levelConfig) || levelConfig == null)
            {
                return false;
            }

            if (levelConfig.BuildCostGroupId <= 0)
            {
                return true;
            }

            IReadOnlyList<ItemStack> costs = costResolver.GetCostGroup(levelConfig.BuildCostGroupId);
            return costs.Count > 0 && ItemManager.Instance.HasItems(costs);
        }

        public List<WorldBuilding> GetActiveMines()
        {
            List<WorldBuilding> mines = new List<WorldBuilding>();
            int mapId = MapManager.Instance.CurrentMap != null ? MapManager.Instance.CurrentMap.Id : 0;
            if (mapId <= 0)
            {
                return mines;
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
                    mines.Add(building);
                }
            }

            return mines;
        }
    }
}
