using System.Collections.Generic;
using UnityEngine;

namespace Skill2
{
    public sealed class SkillCastOrder
    {
        public ISkill2Unit Caster;
        public string AbilityName;
        public ISkill2Unit Target;
        public Vector3 TargetPosition;
        public Vector3 Direction;
        public bool HasTargetPosition;
        public bool Queue;

        public SkillCastOrder Clone()
        {
            return new SkillCastOrder
            {
                Caster = Caster,
                AbilityName = AbilityName,
                Target = Target,
                TargetPosition = TargetPosition,
                Direction = Direction,
                HasTargetPosition = HasTargetPosition,
                Queue = Queue
            };
        }
    }

    public sealed class SkillCastResult
    {
        public bool Success;
        public SkillCastFailureReason FailureReason;
        public string Message;

        public static SkillCastResult Ok()
        {
            return new SkillCastResult { Success = true };
        }

        public static SkillCastResult Fail(SkillCastFailureReason reason, string message = null)
        {
            return new SkillCastResult
            {
                Success = false,
                FailureReason = reason,
                Message = message
            };
        }
    }

    public sealed class SkillCastContext
    {
        private readonly List<ISkill2Unit> targets = new List<ISkill2Unit>();

        public Skill2Engine Engine;
        public SkillAbility Ability;
        public ISkill2Unit Caster;
        public ISkill2Unit Target;
        public Vector3 TargetPosition;
        public Vector3 Direction;
        public IReadOnlyList<ISkill2Unit> Targets => targets;

        internal void ClearTargets()
        {
            targets.Clear();
        }

        internal void AddTarget(ISkill2Unit target)
        {
            if (target != null && !targets.Contains(target))
            {
                targets.Add(target);
            }
        }
    }

    public sealed class SkillModifierApplyOptions
    {
        public float Duration = float.NaN;
        public int StackCount = 1;
        public bool IsAura;
        public SkillModifier SourceModifier;
        public readonly Dictionary<string, float> Numbers = new Dictionary<string, float>();
        public readonly Dictionary<string, object> Objects = new Dictionary<string, object>();
    }

    public sealed class SkillDamageInfo
    {
        public Skill2Engine Engine;
        public ISkill2Unit Attacker;
        public ISkill2Unit Victim;
        public SkillAbility Ability;
        public float Amount;
        public SkillDamageType DamageType = SkillDamageType.Magical;
        public SkillDamageFlags Flags = SkillDamageFlags.None;
    }

    public sealed class SkillDamageResult
    {
        public ISkill2Unit Attacker;
        public ISkill2Unit Victim;
        public SkillAbility Ability;
        public float OriginalAmount;
        public float FinalAmount;
        public SkillDamageType DamageType;
        public SkillDamageFlags Flags;
        public bool Blocked;
        public string BlockReason;
    }

    public sealed class SkillHealInfo
    {
        public ISkill2Unit Source;
        public ISkill2Unit Target;
        public SkillAbility Ability;
        public float Amount;
    }

    public sealed class SkillModifierEvent
    {
        public SkillModifierEventType EventType;
        public Skill2Engine Engine;
        public ISkill2Unit Source;
        public ISkill2Unit Target;
        public SkillAbility Ability;
        public SkillModifier Modifier;
        public SkillDamageInfo DamageInfo;
        public SkillDamageResult DamageResult;
        public SkillHealInfo HealInfo;
        public SkillCastOrder Order;
        public SkillProjectile Projectile;
        public Vector3 Position;
        public float Value;
    }

    public sealed class SkillModifierPropertyContext
    {
        public Skill2Engine Engine;
        public ISkill2Unit Unit;
        public SkillAbility Ability;
        public SkillDamageInfo DamageInfo;
        public SkillDamageResult DamageResult;
    }

    public sealed class SkillProjectileRequest
    {
        public SkillProjectileDefinition Definition;
        public SkillAbility Ability;
        public ISkill2Unit Caster;
        public ISkill2Unit Source;
        public ISkill2Unit Target;
        public Vector3 Origin;
        public Vector3 Direction;
        public bool Tracking;
    }

    public sealed class SkillThinkerRequest
    {
        public string Name;
        public SkillAbility Ability;
        public ISkill2Unit Caster;
        public Vector3 Position;
        public float Duration;
        public float Interval;
        public float Radius;
        public SkillThinkerScript Script;
    }

    public sealed class SkillEvent
    {
        public SkillEventType EventType;
        public SkillAbility Ability;
        public SkillModifier Modifier;
        public SkillProjectile Projectile;
        public SkillThinker Thinker;
        public ISkill2Unit Caster;
        public ISkill2Unit Target;
        public Vector3 Position;
        public float Value;
        public SkillCastFailureReason FailureReason;
        public string Message;
    }
}
