using Game.Ability;
using UnityEngine;

namespace Game.Tests.Ability
{
    public sealed class FakeResourceOwner : IResourceOwner
    {
        public FakeResourceOwner(float mana)
        {
            Mana = mana;
        }

        public float Mana { get; private set; }
        public int SpendCount { get; private set; }

        public bool HasMana(float amount)
        {
            return amount <= 0f || Mana + 0.0001f >= amount;
        }

        public bool SpendMana(float amount)
        {
            if (!HasMana(amount))
            {
                return false;
            }

            Mana = Mathf.Max(0f, Mana - Mathf.Max(0f, amount));
            SpendCount++;
            return true;
        }
    }
}
