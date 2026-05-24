using System.Collections.Generic;
using UnityEngine;

namespace Skill2
{
    public interface ISkill2Unit
    {
        int EntityId { get; }
        int TeamId { get; }
        SkillUnitType UnitType { get; }
        bool IsAlive { get; }
        bool IsMagicImmune { get; }
        bool IsInvulnerable { get; }
        Vector3 Position { get; }
        bool IsVisibleToTeam(int teamId);
        void ApplyDamage(SkillDamageResult result);
        void Heal(SkillHealInfo info);
    }

    public interface ISkill2ResourceOwner
    {
        float Mana { get; }
        bool HasMana(float amount);
        bool SpendMana(float amount);
    }

    public interface ISkill2World
    {
        float Time { get; }
        /// <summary>
        /// Return candidate units near center. Skill2Engine applies SkillTargetQuery filtering again,
        /// so business adapters may use query for acceleration but do not need to own rule correctness.
        /// </summary>
        void FindUnits(Vector3 center, float radius, SkillTargetQuery query, IList<ISkill2Unit> results);
        bool HasLineOfSight(ISkill2Unit viewer, Vector3 position);
    }

    public interface ISkill2Presentation
    {
        void PlayEffect(string effectName, Vector3 position);
        void PlayEffect(string effectName, ISkill2Unit target);
        void PlaySound(string soundName, Vector3 position);
    }
}
