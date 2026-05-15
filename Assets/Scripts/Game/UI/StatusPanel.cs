using Game.Framework;
using TMPro;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class StatusPanel : UIPanel
    {
        [SerializeField]
        private TMP_Text goldText;

        [SerializeField]
        private TMP_Text baseLifeText;

        [SerializeField]
        private TMP_Text waveText;

        private int currentWave;
        private int maxWave;


        private ISubscription statusChangedSubscription;
        private Subscriber subscriber;

        protected override void OnCreate()
        {
            subscriber = new Subscriber();
            //statusChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, BattleStatusMessage>(BattleMessageTopic.StatusChanged, OnStatusChanged);
            ISubscription goldChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, GoldsMessage>(BattleMessageTopic.GoldChanged, OnGoldsMessage);
            ISubscription baseLifeChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, BaseLifeMessage>(BattleMessageTopic.BaseLifeChanged, OnRefreshBaseLife);
            subscriber.Add(goldChangedSubscription);
            subscriber.Add(baseLifeChangedSubscription);
        }
        protected override void OnDestroyed()
        {
            subscriber.Clear();
        }

        private void OnEnable()
        {
            RefreshAll();
        }


        public void SetWave(int currentWave, int maxWave)
        {
            this.currentWave = Mathf.Max(0, currentWave);
            this.maxWave = Mathf.Max(0, maxWave);

            RefreshWave(currentWave, maxWave);
        }

        public void RefreshAll()
        {
            if (ItemManager.Instance != null)
            {
                int gold = ItemManager.Instance.GetCount(ItemIds.Gold);
                RefreshGold(gold);
            }

            if (BaseManager.Instance != null)
            {
                int currentLife = BaseManager.Instance.CurrentLife;
                int maxLife = BaseManager.Instance.MaxLife;
                RefreshBaseLife(currentLife, maxLife);
            }

            RefreshWave(currentWave, maxWave);
        }

        private void OnGoldsMessage(GoldsMessage message)
        {
            RefreshGold(message.Gold);
        }

        private void RefreshGold(int gold)
        {
            if (goldText != null)
            {
                goldText.text = $"金币: {gold}";
            }
        }

        private void OnRefreshBaseLife(BaseLifeMessage message)
        {
            RefreshBaseLife(message.CurrentLife, message.MaxLife);
        }

        private void RefreshBaseLife(int currentLife, int maxLife)
        {
            if (baseLifeText != null)
            {
                baseLifeText.text = $"生命: {currentLife}/{maxLife}";
            }
        }

        private void RefreshWave(int currentWave, int maxWave)
        {
            if (waveText == null)
            {
                return;
            }

            if (maxWave > 0)
            {
                waveText.text = $"波次: {currentWave}/{maxWave}";
            }
            else
            {
                waveText.text = $"波次: {currentWave}";
            }
        }


        private void OnStatusChanged(BattleStatusMessage message)
        {
            if (message == null)
            {
                return;
            }

            RefreshGold(message.Gold);
            RefreshBaseLife(message.CurrentLife, message.MaxLife);
            RefreshWave(message.CurrentWave, message.MaxWave);
        }
    }
}