using Game.Framework;
using TMPro;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class StatusPanel : UIPanel
    {
        [SerializeField]
        private UIProgressBar baseLifeBar;

        [SerializeField]
        private TMP_Text goldText;

        [SerializeField]
        private TMP_Text baseLifeText;

        [SerializeField]
        private TMP_Text waveText;

        [SerializeField]
        private TMP_Text enemyText;

        private int currentWave;
        private int maxWave;
        private Subscriber subscriber;

        protected override void OnCreate()
        {
            subscriber = new Subscriber();

            ISubscription goldChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, GoldsMessage>(BattleMessageTopic.GoldChanged, OnGoldsMessage);
            ISubscription baseLifeChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, BaseLifeMessage>(BattleMessageTopic.BaseLifeChanged, OnBaseLifeMessage);

            subscriber.Add(goldChangedSubscription);
            subscriber.Add(baseLifeChangedSubscription);

            RefreshAll();
        }

        protected override void OnDestroyed()
        {
            if (subscriber != null)
            {
                subscriber.Clear();
                subscriber = null;
            }
        }

        private void OnEnable()
        {
            RefreshAll();
        }

        public void SetWave(int currentWave, int maxWave)
        {
            this.currentWave = Mathf.Max(0, currentWave);
            this.maxWave = Mathf.Max(0, maxWave);

            RefreshWave(this.currentWave, this.maxWave);
        }

        public void SetEnemyCount(int alive, int total)
        {
            RefreshEnemyCount(alive, total);
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

            if (NpcManager.Instance != null)
            {
                RefreshEnemyCount(NpcManager.Instance.AliveEnemyCount, NpcManager.Instance.AliveEnemyCount);
            }
        }

        private void OnGoldsMessage(GoldsMessage message)
        {
            RefreshGold(message.Gold);
        }

        private void OnBaseLifeMessage(BaseLifeMessage message)
        {
            RefreshBaseLife(message.CurrentLife, message.MaxLife);
        }

        private void RefreshGold(int gold)
        {
            if (goldText != null)
            {
                goldText.text = $"金币: {gold}";
            }
        }

        private void RefreshBaseLife(int currentLife, int maxLife)
        {
            if (baseLifeText != null)
            {
                baseLifeText.text = $"生命: {currentLife}/{maxLife}";
            }

            if (baseLifeBar != null)
            {
                baseLifeBar.SetValue(currentLife, maxLife);
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

        private void RefreshEnemyCount(int alive, int total)
        {
            if (enemyText == null)
            {
                return;
            }

            if (total > 0)
            {
                enemyText.text = $"敌人: {alive}/{total}";
            }
            else
            {
                enemyText.text = $"敌人: {alive}";
            }
        }
    }
}