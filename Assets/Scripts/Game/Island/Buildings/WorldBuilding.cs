using UnityEngine;

namespace Game
{
    public enum WorldBuildingType
    {
        None = 0,
        MainBase = 1,
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
    }
}
