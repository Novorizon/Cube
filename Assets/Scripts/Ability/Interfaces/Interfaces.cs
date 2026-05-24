using System.Collections.Generic;
using UnityEngine;

namespace Ability
{
    public interface IUnit
    {
        int EntityId { get; }
        int TeamId { get; }
        UnitType UnitType { get; }
        bool IsAlive { get; }
        bool IsMagicImmune { get; }
        bool IsInvulnerable { get; }
        Vector3 Position { get; }
        bool IsVisibleToTeam(int teamId);
        void ApplyDamage(DamageResult result);
        void Heal(HealInfo info);
    }

    public interface IResourceOwner
    {
        float Mana { get; }
        bool HasMana(float amount);
        bool SpendMana(float amount);
    }

    public interface IWorld
    {
        float Time { get; }
        void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results);
        bool HasLineOfSight(IUnit viewer, Vector3 position);
    }

    public interface IPresentation
    {
        void PlayEffect(string effectName, Vector3 position);
        void PlayEffect(string effectName, IUnit target);
        void PlaySound(string soundName, Vector3 position);
    }
}
