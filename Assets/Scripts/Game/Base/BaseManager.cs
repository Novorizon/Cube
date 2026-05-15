using Game.Framework;
using System;
using UnityEngine;

namespace Game
{
    public sealed class BaseManager : Singleton<BaseManager>
    {
        private const string BasePrefabLocation = "Assets/Arts/Base/Base.prefab";

        private GameObject baseObject;
        private int maxLife;
        private int currentLife;
        private bool initialized;

        public int MaxLife=> maxLife;

        public int CurrentLife => currentLife;

        public bool IsDead=>initialized && currentLife <= 0;

        public bool HasBaseObject=> baseObject != null;

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

        public bool LoadBase(int life)
        {
            ClearBaseObject();

            if (!MapManager.Instance.TryGetGoalPoint(out Vector3Int goalPoint))
            {
                Debug.LogWarning("Load base failed. Current map has no goal point.");
                return false;
            }

            GameObject prefab = ResourceManager.Instance.LoadGameObject(BasePrefabLocation);

            if (prefab == null)
            {
                Debug.LogWarning($"Load base failed. Missing prefab: {BasePrefabLocation}");
                return false;
            }

            Vector3 position = GetBaseWorldPosition(goalPoint);
            baseObject = GameObject.Instantiate(prefab, position, Quaternion.identity);
            baseObject.name = "PlayerBase";
            Debug.Log($"Load base success. GoalPoint: {goalPoint}, Position: {position}");

            maxLife = Mathf.Max(1, life);
            currentLife = maxLife;
            Debug.Log($"Base initialized. Life: {currentLife}/{maxLife}");

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

            currentLife -= damage;

            if (currentLife < 0)
            {
                currentLife = 0;
            }

            BaseLifeMessage message = new BaseLifeMessage();
            message.CurrentLife = currentLife;
            message.MaxLife = maxLife;
            Messager.Instance.Notify(BattleMessageTopic.BaseLifeChanged, message);

            Debug.Log($"Base damaged. Damage: {damage}, Life: {currentLife}/{maxLife}");

            if (currentLife <= 0)
            {
                Debug.Log("Base destroyed. Game over.");
            }
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
