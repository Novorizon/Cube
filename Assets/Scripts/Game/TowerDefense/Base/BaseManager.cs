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
        private bool removingBase;

        public event System.Action BaseLoaded;
        public event System.Action BaseRemoving;

        public int MaxLife => maxLife;

        public int CurrentLife => currentLife;

        public int Defense => defense;

        public BaseConfig Config => config;

        public string PreviewPrefabLocation => config != null ? config.PrefabLocation : string.Empty;

        public bool IsDead => initialized && currentLife <= 0;

        public bool HasBaseObject => baseObject != null && !removingBase;

        public Transform BaseTransform => baseObject != null ? baseObject.transform : null;

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
            if (removingBase) return false;
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
            BaseLoaded?.Invoke();

            return true;
        }

        public void ClearBaseObject()
        {
            if (baseObject == null || removingBase)
            {
                return;
            }

            removingBase = true;
            try { BaseRemoving?.Invoke(); }
            finally
            {
                GameObject.Destroy(baseObject);
                baseObject = null;
                config = null;
                removingBase = false;
            }
        }

        public void TakeDamage(int damage)
        {
            if (damage <= 0 || currentLife <= 0) return;
            ApplyFinalDamage(Mathf.Max(1, damage - defense), out _);
        }

        // Ability damage has already passed defense/block calculation. Never reduce it a second time.
        internal int ApplyFinalDamage(int finalDamage, out bool killed)
        {
            killed = false;
            if (finalDamage <= 0 || currentLife <= 0 || !HasBaseObject) return 0;
            int before = currentLife;
            currentLife = Mathf.Max(0, currentLife - finalDamage);
            int applied = before - currentLife;
            killed = currentLife <= 0;
            NotifyBaseLifeChanged();
            Debug.Log($"Base damaged. FinalDamage: {applied}, Life: {currentLife}/{maxLife}");
            if (killed)
            {
                Debug.Log("Base destroyed. Game over.");
                BattleFlowManager.Instance.CompleteDefeat("Base destroyed.");
            }
            return applied;
        }

        public void Heal(int amount) => ApplyFinalHeal(amount);

        internal int ApplyFinalHeal(int amount)
        {
            if (amount <= 0 || currentLife <= 0 || !HasBaseObject) return 0;
            int before = currentLife;
            currentLife = (int)System.Math.Min(maxLife, (long)currentLife + amount);
            int applied = currentLife - before;
            if (applied <= 0) return 0;
            NotifyBaseLifeChanged();
            Debug.Log($"Base healed. Heal: {applied}, Life: {currentLife}/{maxLife}");
            return applied;
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
            try { Messager.Instance.Notify(BattleMessageTopic.BaseLifeChanged, message); }
            catch (System.Exception exception) { Debug.LogException(exception); }
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

