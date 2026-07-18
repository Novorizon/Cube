namespace Game
{
    public sealed class WorldGatherNodeState
    {
        public int ObjectId { get; }
        public int GatherConfigId { get; }
        public int RemainingTimes { get; private set; }
        public long AvailableAtUnixTime { get; private set; }

        public bool IsDepleted
        {
            get
            {
                return RemainingTimes == 0;
            }
        }

        public WorldGatherNodeState(int objectId, GatherConfig config)
        {
            ObjectId = objectId;
            GatherConfigId = config != null ? config.Id : 0;
            RemainingTimes = GetInitialRemainingTimes(config);
        }

        public WorldGatherNodeState(int objectId, int gatherConfigId, int remainingTimes, long availableAtUnixTime)
        {
            ObjectId = objectId;
            GatherConfigId = gatherConfigId;
            RemainingTimes = remainingTimes;
            AvailableAtUnixTime = availableAtUnixTime;
        }

        public bool CanGather(long currentUnixTime, GatherConfig config)
        {
            if (config == null || !config.Enable)
            {
                return false;
            }

            if (RemainingTimes > 0 || config.DepleteAfterTimes <= 0)
            {
                return currentUnixTime >= AvailableAtUnixTime;
            }

            if (config.RespawnSeconds <= 0 || currentUnixTime < AvailableAtUnixTime)
            {
                return false;
            }

            RemainingTimes = GetInitialRemainingTimes(config);
            AvailableAtUnixTime = 0;
            return RemainingTimes > 0;
        }

        public void Consume(GatherConfig config, long currentUnixTime)
        {
            if (config == null || config.DepleteAfterTimes <= 0)
            {
                return;
            }

            RemainingTimes--;
            if (RemainingTimes > 0)
            {
                return;
            }

            RemainingTimes = 0;
            if (config.RespawnSeconds > 0)
            {
                AvailableAtUnixTime = currentUnixTime + config.RespawnSeconds;
            }
        }

        private static int GetInitialRemainingTimes(GatherConfig config)
        {
            if (config == null || config.DepleteAfterTimes <= 0)
            {
                return int.MaxValue;
            }

            return config.DepleteAfterTimes;
        }
    }
}
