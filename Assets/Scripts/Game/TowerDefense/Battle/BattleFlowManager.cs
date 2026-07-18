using Game.Framework;
using UnityEngine;

namespace Game
{
    public sealed class BattleFlowManager : Singleton<BattleFlowManager>
    {
        private readonly BattleSettlementBuilder settlementBuilder = new BattleSettlementBuilder();

        private BattleState state = BattleState.None;
        private int currentMapId;
        private string currentMapName;
        private BattleEndedMessage lastEndMessage;

        public event System.Action<BattleEndedMessage> BattleCompleted;

        public BattleState State => state;
        public bool IsRunning => state == BattleState.Running;
        public bool HasEnded => state == BattleState.Victory || state == BattleState.Defeat || state == BattleState.Settled;
        public BattleEndedMessage LastEndMessage => lastEndMessage;

        public void Initialize()
        {
            state = BattleState.None;
            currentMapId = 0;
            currentMapName = string.Empty;
            lastEndMessage = null;
        }

        public void BeginBattle(MapConfig mapConfig)
        {
            currentMapId = mapConfig != null ? mapConfig.Id : 0;
            currentMapName = currentMapId > 0 ? LocalizedConfigText.MapName(currentMapId) : string.Empty;
            lastEndMessage = null;

            ChangeState(BattleState.Running);
            Debug.Log($"Battle started. mapId: {currentMapId}, mapName: {currentMapName}");
        }

        public void CompleteVictory(string reason = null)
        {
            CompleteBattle(BattleState.Victory, true, string.IsNullOrEmpty(reason) ? LocalizationManager.Get("ui.td.result.reason_victory") : reason);
        }

        public void CompleteDefeat(string reason = null)
        {
            CompleteBattle(BattleState.Defeat, false, string.IsNullOrEmpty(reason) ? LocalizationManager.Get("ui.td.result.reason_defeat") : reason);
        }

        private void CompleteBattle(BattleState endState, bool victory, string reason)
        {
            if (HasEnded)
            {
                return;
            }

            ChangeState(endState);

            // Stop battle simulation first so the ending frame cannot spawn or kill extra units.
            WaveManager.Instance.Stop();
            TowerBuildManager.Instance.CancelSelect();
            BattleTargetClickManager.Instance.ClearSelection();
            Time.timeScale = 1f;

            if (GameInputManager.IsCreated)
            {
                GameInputManager.Instance.SetMode(InputMode.UI);
            }

            BattleSettlementReward reward = victory ? settlementBuilder.BuildReward(BattleItemManager.Instance.GetAllItems()) : new BattleSettlementReward();
            if (victory)
            {
                settlementBuilder.PrintReward(reward);
            }

            lastEndMessage = new BattleEndedMessage
            {
                State = endState,
                Victory = victory,
                MapId = currentMapId,
                MapName = currentMapName,
                Reason = reason,
                Reward = reward
            };

            Messager.Instance.Notify(BattleMessageTopic.BattleEnded, lastEndMessage);
            BattleCompleted?.Invoke(lastEndMessage);
            Debug.Log($"Battle ended. state: {endState}, victory: {victory}, reason: {reason}");
        }

        private void ChangeState(BattleState nextState)
        {
            if (state == nextState)
            {
                return;
            }

            state = nextState;
            Messager.Instance.Notify(BattleMessageTopic.BattleStateChanged, new BattleStateMessage
            {
                State = state,
                MapId = currentMapId,
                MapName = currentMapName
            });
        }
    }
}
