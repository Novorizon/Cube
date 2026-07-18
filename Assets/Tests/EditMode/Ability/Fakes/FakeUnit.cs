using System.Collections.Generic;
using Game.Ability;
using UnityEngine;

namespace Game.Tests.Ability
{
    public sealed class FakeUnit : IUnit
    {
        private readonly List<DamageResult> damageResults = new List<DamageResult>();
        private readonly List<HealInfo> healResults = new List<HealInfo>();

        public FakeUnit(int entityId, int teamId, Vector3 position, UnitType unitType = UnitType.Basic)
        {
            EntityId = entityId;
            TeamId = teamId;
            Position = position;
            UnitType = unitType;
        }

        public int EntityId { get; }
        public int TeamId { get; set; }
        public UnitType UnitType { get; set; }
        public bool IsAlive { get; set; } = true;
        public bool IsMagicImmune { get; set; }
        public bool IsInvulnerable { get; set; }
        public bool IsVisible { get; set; } = true;
        public Vector3 Position { get; set; }
        public float Health { get; private set; } = 1000f;
        public float DamageTaken { get; private set; }
        public float HealingReceived { get; private set; }
        public IReadOnlyList<DamageResult> DamageResults => damageResults;
        public IReadOnlyList<HealInfo> HealResults => healResults;

        public bool IsVisibleToTeam(int teamId)
        {
            return IsVisible;
        }

        public void ApplyDamage(DamageResult result)
        {
            if (result == null || result.Blocked)
            {
                return;
            }

            damageResults.Add(result);
            DamageTaken += result.FinalAmount;
            Health = Mathf.Max(0f, Health - result.FinalAmount);
            if (Health <= 0f)
            {
                IsAlive = false;
            }
        }

        public void Heal(HealInfo info)
        {
            if (info == null || info.Amount <= 0f)
            {
                return;
            }

            healResults.Add(info);
            HealingReceived += info.Amount;
            Health += info.Amount;
        }
    }
}
