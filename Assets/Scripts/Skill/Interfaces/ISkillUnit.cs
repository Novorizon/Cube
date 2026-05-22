using UnityEngine;

namespace Game.Skill
{
    public interface ISkillUnit
    {
        int RuntimeId { get; }
        int TeamId { get; }
        bool IsAlive { get; }
        Vector3 Position { get; }

        void TakeDamage(SkillDamageInfo damageInfo);
        void Heal(SkillHealInfo healInfo);
    }

    public interface ISkillWorld
    {
        float Time { get; }
        void FindUnits(Vector3 center, float radius, SkillTargetFilter filter, SkillTargetResult result);
        bool HasLineOfSight(ISkillUnit caster, Vector3 position);
    }

    public interface ISkillResourceOwner
    {
        bool HasResource(int resourceId, int count);
        bool TryConsumeResource(int resourceId, int count);
    }

    public interface ISkillEffectService
    {
        void PlayEffect(string location, Vector3 position);
        void PlayEffect(string location, ISkillUnit target);
        void PlaySound(string location, Vector3 position);
    }
}
