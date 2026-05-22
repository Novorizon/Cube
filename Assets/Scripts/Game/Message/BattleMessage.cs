using Game.Framework;

namespace Game
{
    public enum BattleMessageTopic
    {
        None = 0,

        StatusChanged = 1,
        GoldChanged,
        BaseLifeChanged,
        WaveChanged,
        NpcKilled,
        NpcSpawned,
        TargetInfoChanged,
        TargetInfoCleared,
    }

    public class BattleStatusMessage : IMessage
    {
        public int Gold;
        public int CurrentLife;
        public int MaxLife;
        public int CurrentWave;
        public int MaxWave;
    }

    public class GoldsMessage : IMessage
    {
        public int Gold;
    }

    public class BaseLifeMessage : IMessage
    {
        public int CurrentLife;
        public int MaxLife;
    }

    public class WaveMessage : IMessage
    {
        public int CurrentWave;
        public int MaxWave;
    }

    public class TargetInfoMessage : IMessage
    {
        public TdTargetRuntimeInfo Info;
    }

    public class TargetInfoClearMessage : IMessage
    {
    }
}
