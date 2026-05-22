using System;

namespace Game.Skill
{
    [Flags]
    public enum SkillAbilityBehavior
    {
        None = 0,
        NoTarget = 1,
        UnitTarget = 2,
        PointTarget = 4,
        Passive = 8,
        Toggle = 16,
        Channel = 32,
        Aoe = 64
    }

    public enum SkillTargetTeam
    {
        None = 0,
        Friendly = 1,
        Enemy = 2,
        Both = 3
    }

    public enum SkillTargetType
    {
        None = 0,
        Caster = 1,
        Unit = 2,
        Point = 3,
        Area = 4,
        CurrentTargets = 5
    }

    public enum SkillActionType
    {
        None = 0,
        Damage = 1,
        Heal = 2,
        ApplyModifier = 3,
        FireEvent = 4
    }

    public enum SkillModifierType
    {
        None = 0,
        Property = 1,
        Periodic = 2,
        State = 3,
        Aura = 4
    }

    public enum SkillModifierPropertyType
    {
        None = 0,
        MoveSpeedPercent = 1,
        AttackSpeedPercent = 2,
        DamageBonus = 3,
        ArmorBonus = 4,
        IncomingDamagePercent = 5,
        OutgoingDamagePercent = 6
    }

    public enum SkillUnitState
    {
        None = 0,
        Stunned = 1,
        Silenced = 2,
        Rooted = 3,
        Invulnerable = 4
    }

    public enum SkillDamageType
    {
        None = 0,
        Physical = 1,
        Magical = 2,
        Pure = 3
    }

    public enum SkillTriggerEventType
    {
        None = 0,
        AbilityCast = 1,
        DamageDealt = 2,
        DamageTaken = 3,
        Healed = 4,
        AttackLanded = 5,
        Death = 6
    }
}
