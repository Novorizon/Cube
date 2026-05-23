using UnityEngine;

namespace Game.Skill
{
    /// <summary>
    /// 技能系统能作用的最小单位接口。
    /// 业务层可以用适配器包装 Npc、Tower、Base 或其他战斗对象。
    /// 注意：属性修正和状态查询不放在这里，统一通过 SkillManager / SkillModifierManager 查询。
    /// </summary>
    public interface ISkillUnit
    {
        int RuntimeId { get; }
        int TeamId { get; }
        bool IsAlive { get; }
        Vector3 Position { get; }

        void TakeDamage(SkillDamageInfo damageInfo);
        void Heal(SkillHealInfo healInfo);
    }

    /// <summary>
    /// 技能系统访问战场环境的接口。
    /// 范围技能、目标筛选、视野检查都通过这里和业务层通信，技能底层不直接访问 NpcManager 或地图系统。
    /// </summary>
    public interface ISkillWorld
    {
        float Time { get; }
        void FindUnits(Vector3 center, float radius, SkillTargetFilter filter, SkillTargetResult result);
        bool HasLineOfSight(ISkillUnit caster, Vector3 position);
    }

    /// <summary>
    /// 技能资源消耗接口。
    /// 金币、蓝量、能量、道具数量都可以由业务层适配成 resourceId + count。
    /// </summary>
    public interface ISkillResourceOwner
    {
        bool HasResource(int resourceId, int count);
        bool TryConsumeResource(int resourceId, int count);
    }

    /// <summary>
    /// 技能表现接口。
    /// 技能底层只发出播放请求，不直接 Instantiate、不直接依赖资源系统或业务特效系统。
    /// </summary>
    public interface ISkillEffectService
    {
        void PlayEffect(string location, Vector3 position);
        void PlayEffect(string location, ISkillUnit target);
        void PlaySound(string location, Vector3 position);
    }
}