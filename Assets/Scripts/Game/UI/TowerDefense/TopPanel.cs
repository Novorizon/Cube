using Game.Framework;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game
{
    public sealed class TopPanel : UIPanel
    {
        [SerializeField]
        [FormerlySerializedAs("baseLifeBar")]
        [FormerlySerializedAs("baseHpFill")]
        private UIProgressBar baseHpBar;

        [SerializeField]
        [FormerlySerializedAs("goldText")]
        private TMP_Text coinText;

        [SerializeField]
        [FormerlySerializedAs("baseLifeText")]
        [FormerlySerializedAs("baseHpText")]
        private TMP_Text baseHpValueText;

        [SerializeField]
        private TMP_Text waveText;

        [SerializeField]
        private TMP_Text enemyText;

        private int currentWave;
        private int maxWave;
        private int aliveEnemy;
        private int killedEnemy;
        private int totalEnemy;
        private Subscriber subscriber;

        public RectTransform GoldAnchor
        {
            get
            {
                return coinText != null ? coinText.rectTransform : transform as RectTransform;
            }
        }


        public override UICloseTriggers CloseTriggers => UICloseTriggers.None;

        protected override void OnOpen(object args)
        {
            ClearSubscriptions();
            subscriber = new Subscriber();

            ISubscription goldChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, GoldsMessage>(BattleMessageTopic.GoldChanged, OnGoldsMessage);
            ISubscription baseLifeChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, BaseLifeMessage>(BattleMessageTopic.BaseLifeChanged, OnBaseLifeMessage);
            ISubscription waveChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, WaveMessage>(BattleMessageTopic.WaveChanged, OnWaveMessage);

            subscriber.Add(goldChangedSubscription);
            subscriber.Add(baseLifeChangedSubscription);
            subscriber.Add(waveChangedSubscription);
            RefreshAll();
        }

        protected override void OnClose()
        {
            ClearSubscriptions();
        }

        protected override void OnDestroyed()
        {
            ClearSubscriptions();
        }

        private void ClearSubscriptions()
        {
            if (subscriber != null)
            {
                subscriber.Clear();
                subscriber = null;
            }
        }

        public void SetBaseLife(int currentLife, int maxLife)
        {
            RefreshBaseLife(currentLife, maxLife);
        }

        public void SetGold(int gold)
        {
            RefreshGold(gold);
        }

        public void SetWave(int currentWave, int maxWave)
        {
            this.currentWave = Mathf.Max(0, currentWave);
            this.maxWave = Mathf.Max(0, maxWave);

            RefreshWave(this.currentWave, this.maxWave);
        }

        public void SetEnemyCount(int alive, int total)
        {
            aliveEnemy = Mathf.Max(0, alive);
            totalEnemy = Mathf.Max(0, total);

            RefreshEnemyCount(aliveEnemy, totalEnemy);
        }

        public void SetWaveState(WaveMessage message)
        {
            if (message == null)
            {
                return;
            }

            currentWave = Mathf.Max(0, message.CurrentWave);
            maxWave = Mathf.Max(0, message.MaxWave);
            aliveEnemy = Mathf.Max(0, message.AliveEnemyCount);
            killedEnemy = Mathf.Max(0, message.KilledEnemyCount);
            totalEnemy = Mathf.Max(0, message.TotalEnemyCount);

            RefreshWave(currentWave, maxWave);
            RefreshEnemyCount(GetRemainingEnemyCount(), totalEnemy);
        }

        public void RefreshAll()
        {
            if (BattleItemManager.Instance != null)
            {
                int gold = BattleItemManager.Instance.GetCount(ItemIds.Gold);
                RefreshGold(gold);
            }

            if (BaseManager.Instance != null)
            {
                int currentLife = BaseManager.Instance.CurrentLife;
                int maxLife = BaseManager.Instance.MaxLife;
                RefreshBaseLife(currentLife, maxLife);
            }

            if (WaveManager.IsCreated)
            {
                currentWave = WaveManager.Instance.CurrentWave;
                maxWave = WaveManager.Instance.MaxWave;
                aliveEnemy = WaveManager.Instance.AliveEnemyCount;
                killedEnemy = WaveManager.Instance.KilledEnemyCount;
                totalEnemy = WaveManager.Instance.TotalEnemyCount;
            }

            RefreshWave(currentWave, maxWave);
            RefreshEnemyCount(GetRemainingEnemyCount(), totalEnemy);
        }

        private void OnGoldsMessage(GoldsMessage message)
        {
            RefreshGold(message.Gold);
        }

        private void OnBaseLifeMessage(BaseLifeMessage message)
        {
            RefreshBaseLife(message.CurrentLife, message.MaxLife);
        }

        private void OnWaveMessage(WaveMessage message)
        {
            SetWaveState(message);
        }

        private void RefreshGold(int gold)
        {
            if (coinText != null)
            {
                coinText.text = $"{gold}";
            }
        }

        private void RefreshBaseLife(int currentLife, int maxLife)
        {
            if (baseHpValueText != null)
            {
                baseHpValueText.text = $" {currentLife}/{maxLife}";
            }

            if (baseHpBar != null)
            {
                baseHpBar.SetValue(currentLife, maxLife);
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
                waveText.text = $"{currentWave}/{maxWave}";
            }
            else
            {
                waveText.text = $"{currentWave}";
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
                enemyText.text = $"{alive}/{total}";
            }
            else
            {
                enemyText.text = $" {alive}";
            }
        }

        private int GetRemainingEnemyCount()
        {
            if (totalEnemy <= 0)
            {
                return aliveEnemy;
            }

            return Mathf.Max(0, totalEnemy - killedEnemy);
        }
    }
}
