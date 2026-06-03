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
        BattleStateChanged,
        BattleEnded,
        GoldFlyRequested,
        ItemFlyRequested,
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

    public class GoldFlyMessage : IMessage
    {
        public UnityEngine.Vector3 WorldPosition;
        public int Count;
    }

    public class ItemFlyMessage : IMessage
    {
        public UnityEngine.Vector3 WorldPosition;
        public int ItemId;
        public int Count;
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
        public int AliveEnemyCount;
        public int TotalEnemyCount;
        public int KilledEnemyCount;
        public int CurrentWaveSpawnedCount;
        public int CurrentWaveTotalCount;
    }

    public class BattleStateMessage : IMessage
    {
        public BattleState State;
        public int MapId;
        public string MapName;
    }

    public class BattleEndedMessage : IMessage
    {
        public BattleState State;
        public bool Victory;
        public int MapId;
        public string MapName;
        public string Reason;
        public BattleSettlementReward Reward;
    }

    public class TargetInfoMessage : IMessage
    {
        public TdTargetRuntimeInfo Info;
    }

    public class TargetInfoClearMessage : IMessage
    {
    }
}
