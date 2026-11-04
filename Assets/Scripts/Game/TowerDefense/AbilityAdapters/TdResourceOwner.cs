using Game.Ability;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Maps ability mana cost to the current item/resource bag system.
    /// </summary>
    public sealed class TdResourceOwner : IResourceOwner
    {
        private readonly int resourceItemId;

        public TdResourceOwner(int resourceItemId)
        {
            this.resourceItemId = resourceItemId;
        }

        public float Mana
        {
            get
            {
                if (resourceItemId <= 0)
                {
                    return 0f;
                }

                return Mathf.Max(0, BattleItemManager.Instance.GetCount(resourceItemId));
            }
        }

        public bool HasMana(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || (double)amount > int.MaxValue) return false;
            if (amount <= 0f)
            {
                return true;
            }

            if (resourceItemId <= 0)
            {
                return false;
            }

            return BattleItemManager.Instance.HasItem(resourceItemId, Mathf.CeilToInt(amount));
        }

        public bool SpendMana(float amount)
        {
            if (float.IsNaN(amount) || float.IsInfinity(amount) || (double)amount > int.MaxValue) return false;
            if (amount <= 0f)
            {
                return true;
            }

            if (resourceItemId <= 0)
            {
                return false;
            }

            return BattleItemManager.Instance.TryConsume(resourceItemId, Mathf.CeilToInt(amount));
        }
    }
}

