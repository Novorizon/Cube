using Game.Framework;
using TMPro;
using UI;
using UnityEngine;

namespace Game
{
    public sealed class StatusPanel : MonoBehaviour
    {
        [SerializeField]
        private UIProgressBar baseHpFill;

        [SerializeField]
        private TMP_Text goldText;

        [SerializeField]
        private TMP_Text baseHpText;

        [SerializeField]
        private TMP_Text waveText;

        [SerializeField]
        private TMP_Text enemyText;

        private int currentWave;
        private int maxWave;
        private int aliveEnemy;
        private int totalEnemy;
        private Subscriber subscriber;


        private void OnEnable()
        {
            subscriber = new Subscriber();

            ISubscription goldChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, GoldsMessage>(BattleMessageTopic.GoldChanged, OnGoldsMessage);
            ISubscription baseLifeChangedSubscription = Messager.Instance.Subscribe<BattleMessageTopic, BaseLifeMessage>(BattleMessageTopic.BaseLifeChanged, OnBaseLifeMessage);

            subscriber.Add(goldChangedSubscription);
            subscriber.Add(baseLifeChangedSubscription);
            RefreshAll();
        }


        private void OnDestroy()
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
            RefreshEnemyCount(aliveEnemy, totalEnemy);
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
                goldText.text = $"{gold}";
            }
        }

        private void RefreshBaseLife(int currentLife, int maxLife)
        {
            if (baseHpText != null)
            {
                baseHpText.text = $" {currentLife}/{maxLife}";
            }

            if (baseHpFill != null)
            {
                baseHpFill.SetValue(currentLife, maxLife);
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
                waveText.text = $" {currentWave}/{maxWave}";
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
                enemyText.text = $" {alive}/{total}";
            }
            else
            {
                enemyText.text = $" {alive}";
            }
        }
    }
}
