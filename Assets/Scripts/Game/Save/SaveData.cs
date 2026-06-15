namespace Game
{
    public sealed class SaveData
    {
        public int Version = SaveVersion.Current;
        public SaveWorldItemData[] WorldItems;
        public SaveGatherNodeData[] GatherNodes;
        public SaveWorldBuildingData[] WorldBuildings;
        public SaveFarmData[] Farms;
        public SaveWorldFarmPlotData[] WorldFarmPlots;
        public SaveRemovedMapObjectData[] RemovedMapObjects;
    }

    public sealed class SaveWorldItemData
    {
        public int ItemId;
        public int Count;
    }

    public sealed class SaveGatherNodeData
    {
        public int ObjectId;
        public int GatherConfigId;
        public int RemainingTimes;
        public long AvailableAtUnixTime;
    }

    public sealed class SaveWorldBuildingData
    {
        public int InstanceId;
        public int MapId;
        public int ConfigId;
        public int Level;
        public int X;
        public int Y;
        public int Z;
        public int Status;
        public long FinishAtUnixTime;
        public long NextIncomeAtUnixTime;
    }

    public sealed class SaveWorldFarmPlotData
    {
        public int MapId;
        public int X;
        public int Y;
        public int Z;
        public int CropId;
        public long PlantedAtUnixTime;
        public long MatureAtUnixTime;
        public long NextIncomeAtUnixTime;
    }

    public sealed class SaveFarmData
    {
        public int FarmId;
        public int MapId;
        public int CropId;
        public long PlantedAtUnixTime;
        public long MatureAtUnixTime;
        public long NextIncomeAtUnixTime;
        public SaveFarmCellData[] Cells;
    }

    public sealed class SaveFarmCellData
    {
        public int X;
        public int Y;
        public int Z;
    }

    public sealed class SaveRemovedMapObjectData
    {
        public int MapId;
        public int ObjectId;
    }
}
