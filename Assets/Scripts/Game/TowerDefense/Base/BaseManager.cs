using Game.Framework;
using UnityEngine;

namespace Game
{
    public sealed class BaseManager : Singleton<BaseManager>
    {
        private GameObject baseObject;
        private BaseConfig config;
        private int maxLife;
        private int currentLife;
        private int defense;
        private bool initialized;

        public int MaxLife => maxLife;

        public int CurrentLife => currentLife;

        public int Defense => defense;

        public BaseConfig Config => config;

        public string PreviewPrefabLocation => config != null ? config.PrefabLocation : string.Empty;

        public bool IsDead => initialized && currentLife <= 0;

        public bool HasBaseObject => baseObject != null;

        public Vector3 BasePosition
        {
            get
            {
                if (baseObject == null)
                {
                    return Vector3.zero;
                }

                return baseObject.transform.position;
            }
        }

        public void Initialize()
        {
            initialized = true;
        }

        public bool LoadBase(int baseId)
        {
            ClearBaseObject();
            config = null;
            maxLife = 0;
            currentLife = 0;
            defense = 0;

            if (DataManager.Instance.Base == null || !DataManager.Instance.Base.TryGet(baseId, out BaseConfig baseConfig) || baseConfig == null || !baseConfig.Enable)
            {
                Debug.LogWarning($"Load base failed. Missing base config. baseId: {baseId}");
                return false;
            }

            if (string.IsNullOrWhiteSpace(baseConfig.PrefabLocation))
            {
                Debug.LogWarning($"Load base failed. Missing prefab location. baseId: {baseId}");
                return false;
            }

            if (!MapManager.Instance.TryGetGoalPoint(out Vector3Int goalPoint))
            {
                Debug.LogWarning("Load base failed. Current map has no goal point.");
                return false;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(baseConfig.PrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Load base failed. Missing prefab: {baseConfig.PrefabLocation}");
                return false;
            }

            Vector3 position = GetBaseWorldPosition(goalPoint);
            baseObject = GameObject.Instantiate(prefab, position, Quaternion.identity);
            baseObject.name = "PlayerBase";
            EnsureBaseView(baseObject);

            config = baseConfig;
            maxLife = Mathf.Max(1, config.Hp);
            currentLife = maxLife;
            defense = Mathf.Max(0, config.Defense);

            Debug.Log($"Base initialized. Id: {config.Id}, Name: {config.Name}, Life: {currentLife}/{maxLife}, Defense: {defense}, GoalPoint: {goalPoint}, Position: {position}");
            NotifyBaseLifeChanged();

            return true;
        }

        public void ClearBaseObject()
        {
            if (baseObject == null)
            {
                return;
            }

            GameObject.Destroy(baseObject);
            baseObject = null;
            config = null;
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0)
            {
                return;
            }

            if (currentLife <= 0)
            {
                return;
            }

            int finalDamage = Mathf.Max(1, damage - defense);
            currentLife -= finalDamage;

            if (currentLife < 0)
            {
                currentLife = 0;
            }

            NotifyBaseLifeChanged();

            Debug.Log($"Base damaged. Damage: {damage}, FinalDamage: {finalDamage}, Defense: {defense}, Life: {currentLife}/{maxLife}");

            if (currentLife <= 0)
            {
                Debug.Log("Base destroyed. Game over.");
                BattleFlowManager.Instance.CompleteDefeat("Base destroyed.");
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (currentLife <= 0)
            {
                return;
            }

            int before = currentLife;
            currentLife += amount;

            if (currentLife > maxLife)
            {
                currentLife = maxLife;
            }

            if (currentLife == before)
            {
                return;
            }

            NotifyBaseLifeChanged();

            Debug.Log($"Base healed. Heal: {currentLife - before}, Life: {currentLife}/{maxLife}");
        }

        private void EnsureBaseView(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (target.GetComponent<BaseView>() == null)
            {
                target.AddComponent<BaseView>();
            }
        }

        private void NotifyBaseLifeChanged()
        {
            BaseLifeMessage message = new BaseLifeMessage();
            message.CurrentLife = currentLife;
            message.MaxLife = maxLife;
            Messager.Instance.Notify(BattleMessageTopic.BaseLifeChanged, message);
        }

        private Vector3 GetBaseWorldPosition(Vector3Int coord)
        {
            if (MapManager.Instance.TryGetTileView(coord, out TileView tileView))
            {
                return tileView.transform.position + Vector3.up * 0.6f;
            }

            return MapManager.Instance.GetTileWorldPosition(coord) + Vector3.up * 0.6f;
        }
    }
}
