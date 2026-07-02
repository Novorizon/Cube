namespace Game
{
    public sealed class SaveData
    {
        public int Version = SaveVersion.Current;
        public long SavedAtUnixTime;
        public SaveWorldItemData[] WorldItems;
        public SaveGatherNodeData[] GatherNodes;
        public SaveWorldBuildingData[] WorldBuildings;
        public int[] RuntimeUnlockedBuildingIds;
        public SaveFarmData[] Farms;
        public SaveWorldFarmPlotData[] WorldFarmPlots;
        public SaveRemovedMapObjectData[] RemovedMapObjects;
        public SaveToolKitData ToolKit;
        public SaveCalendarData Calendar;
        public SaveBagData Bag;
        public SaveTechData Tech;
        public SavePlayerData Player;
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

    public sealed class SaveToolKitData
    {
        public int Level;
        public int[] SlotItemIds;
    }

    public sealed class SaveBagData
    {
        public SaveBagSlotData[] SlotItemIds;
    }

    public sealed class SaveBagSlotData
    {
        public int SlotIndex;
        public int ItemId;
    }

    public sealed class SaveCalendarData
    {
        public int Year;
        public int Month;
        public int Day;
        public int Hour;
        public int Minute;
        public float AccumulatedRealSeconds;
    }

    public sealed class SaveTechData
    {
        public int[] ResearchedTechIds;
    }

    public sealed class SavePlayerData
    {
        public int MapId;
        public float X;
        public float Y;
        public float Z;
        public float RotationY;
    }
}
