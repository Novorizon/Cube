using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    public sealed class MineManager
    {
        public static MineManager Instance { get; } = new MineManager();

        private MineManager()
        {
        }

        public void Initialize()
        {
        }

        public bool TryBuildMine(WorldResourceView resourceView, WorldResourceConfig config)
        {
            if (resourceView == null || resourceView.MapObject == null || config == null || !config.Enable)
            {
                return false;
            }

            if (!WorldBuildingManager.Instance.HasActiveBuildingType(WorldBuildingType.MainBase))
            {
                return false;
            }

            if (config.MineBuildingId <= 0 || !WorldBuildingManager.Instance.IsBuildingUnlocked(config.MineBuildingId))
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
