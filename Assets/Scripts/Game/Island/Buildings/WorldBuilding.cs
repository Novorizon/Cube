using UnityEngine;

namespace Game
{
    public enum WorldBuildingType
    {
        None = 0,
        House = 1,
        Warehouse = 2,
        Workbench = 3,
        CarpentryBench = 4,
        Furnace = 5,
        Blacksmith = 6,
        Mill = 7,
        FarmPlot = 8,
        Mine = 9,
    }

    public enum WorldFarmPlotType
    {
        None = 0,
        Crop = 1,
        Flower = 2,
        Herb = 3,
    }

    public enum WorldMineType
    {
        None = 0,
        Stone = 1,
        Copper = 2,
        Iron = 3,
    }

    public enum WorldBuildingStatus
    {
        None = 0,
        Constructing = 1,
        Active = 2,
    }

    public enum WorldBuildingUnlockSource
    {
        None = 0,
        Default = 1,
        Tech = 2,
        Runtime = 3,
    }

    public sealed class WorldBuilding
    {
        public int InstanceId { get; }
        public int MapId { get; }
        public int ConfigId { get; }
        public int Level { get; private set; }
        public Vector3Int Coord { get; }
        public WorldBuildingStatus Status { get; private set; }
        public long FinishAtUnixTime { get; private set; }
        public long NextIncomeAtUnixTime { get; private set; }

        public WorldBuilding(int instanceId, int mapId, int configId, int level, Vector3Int coord, WorldBuildingStatus status, long finishAtUnixTime, long nextIncomeAtUnixTime)
        {
            InstanceId = instanceId;
            MapId = mapId;
            ConfigId = configId;
            Level = level;
            Coord = coord;
            Status = status;
            FinishAtUnixTime = finishAtUnixTime;
            NextIncomeAtUnixTime = nextIncomeAtUnixTime;
        }

        public void CompleteConstruction()
        {
            if (Status != WorldBuildingStatus.Constructing)
            {
                return;
            }

            Status = WorldBuildingStatus.Active;
            FinishAtUnixTime = 0;
        }

        public void SetNextIncomeAt(long unixTime)
        {
            NextIncomeAtUnixTime = unixTime > 0 ? unixTime : 0;
        }

        public void UpgradeTo(int level)
        {
            if (level <= Level)
            {
                return;
            }

            Level = level;
            NextIncomeAtUnixTime = 0;
        }
    }

    public static class WorldBuildingFootprint
    {
        public static int GetSizeX(WorldBuildingConfig config)
        {
            return config != null && config.SizeX > 0 ? config.SizeX : 1;
        }

        public static int GetSizeZ(WorldBuildingConfig config)
        {
            return config != null && config.SizeZ > 0 ? config.SizeZ : 1;
        }

        public static bool Contains(Vector3Int anchor, int sizeX, int sizeZ, Vector3Int coord)
        {
            sizeX = Mathf.Max(1, sizeX);
            sizeZ = Mathf.Max(1, sizeZ);
            return coord.y == anchor.y &&
                   coord.x >= anchor.x &&
                   coord.x < anchor.x + sizeX &&
                   coord.z >= anchor.z &&
                   coord.z < anchor.z + sizeZ;
        }

        public static Vector3 GetCenterWorldPosition(Vector3Int anchor, int sizeX, int sizeZ, float tileSize)
        {
            sizeX = Mathf.Max(1, sizeX);
            sizeZ = Mathf.Max(1, sizeZ);
            float safeTileSize = Mathf.Max(0.01f, tileSize);
            return new Vector3(
                (anchor.x + (sizeX - 1) * 0.5f) * safeTileSize,
                anchor.y * safeTileSize,
                (anchor.z + (sizeZ - 1) * 0.5f) * safeTileSize);
        }
    }
}
