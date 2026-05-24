using Ability;
using UnityEngine;

namespace Game
{
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

                return ItemManager.Instance.GetCount(resourceItemId);
            }
        }

        public bool HasMana(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (resourceItemId <= 0)
            {
                return false;
            }

            return ItemManager.Instance.HasItem(resourceItemId, Mathf.CeilToInt(amount));
        }

        public bool SpendMana(float amount)
        {
            if (amount <= 0f)
            {
                return true;
            }

            if (resourceItemId <= 0)
            {
                return false;
            }

            return ItemManager.Instance.TryConsume(resourceItemId, Mathf.CeilToInt(amount));
        }
    }
}
