namespace Game
{
    public readonly struct WorldGatherStatus
    {
        public readonly int ObjectId;
        public readonly int GatherConfigId;
        public readonly int RemainingTimes;
        public readonly long AvailableAtUnixTime;
        public readonly bool CanGather;

        public WorldGatherStatus(int objectId, int gatherConfigId, int remainingTimes, long availableAtUnixTime, bool canGather)
        {
            ObjectId = objectId;
            GatherConfigId = gatherConfigId;
            RemainingTimes = remainingTimes;
            AvailableAtUnixTime = availableAtUnixTime;
            CanGather = canGather;
        }
    }
}
