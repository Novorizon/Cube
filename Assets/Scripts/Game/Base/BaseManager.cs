using Game.Framework;
using UnityEngine;

namespace Game
{
    public sealed class BaseManager : Singleton<BaseManager>
    {
        private int maxLife;
        private int currentLife;
        private bool initialized;

        public void Initialize(int life)
        {
            maxLife = Mathf.Max(1, life);
            currentLife = maxLife;
            initialized = true;
            Debug.Log($"Base initialized. Life: {currentLife}/{maxLife}");
        }

        public void TakeDamage(int damage)
        {
            if (!initialized)
            {
                Initialize(20);
            }

            if (damage <= 0 || currentLife <= 0)
            {
                return;
            }

            currentLife -= damage;

            if (currentLife < 0)
            {
                currentLife = 0;
            }

            Debug.Log($"Base damaged. Damage: {damage}, Life: {currentLife}/{maxLife}");
        }
    }
}
