using System;
using System.Collections.Generic;

namespace Skill2
{
    [Serializable]
    public sealed class SkillLevelValue
    {
        public float BaseValue;
        public readonly List<float> ValuesByLevel = new List<float>();

        public static SkillLevelValue Constant(float value)
        {
            SkillLevelValue result = new SkillLevelValue();
            result.BaseValue = value;
            return result;
        }

        public float GetValue(int level)
        {
            if (ValuesByLevel.Count == 0)
            {
                return BaseValue;
            }

            int index = Math.Max(0, level - 1);
            if (index >= ValuesByLevel.Count)
            {
                index = ValuesByLevel.Count - 1;
            }

            return ValuesByLevel[index];
        }
    }

    [Serializable]
    public sealed class SkillChargeDefinition
    {
        public int MaxCharges;
        public float RestoreTime;
        public bool StartFull = true;
        public bool UsesCooldown = true;
    }

    [Serializable]
    public sealed class SkillAbilityDefinition
    {
        public string Name;
        public string DisplayName;
        public string Description;
        public string Icon;
        public int MaxLevel = 1;
        public SkillAbilityBehavior Behavior = SkillAbilityBehavior.NoTarget;
        public SkillTargetTeam TargetTeam = SkillTargetTeam.Enemy;
        public SkillUnitType TargetType = SkillUnitType.All;
        public SkillTargetFlags TargetFlags = SkillTargetFlags.None;
        public SkillLevelValue CastRange = SkillLevelValue.Constant(0f);
        public SkillLevelValue AoeRadius = SkillLevelValue.Constant(0f);
        public SkillLevelValue CastPoint = SkillLevelValue.Constant(0f);
        public SkillLevelValue CastBackswing = SkillLevelValue.Constant(0f);
        public SkillLevelValue ChannelTime = SkillLevelValue.Constant(0f);
        public SkillLevelValue Cooldown = SkillLevelValue.Constant(0f);
        public SkillLevelValue ManaCost = SkillLevelValue.Constant(0f);
        public SkillChargeDefinition Charges;
        public string IntrinsicModifierName;
        public readonly Dictionary<string, SkillLevelValue> SpecialValues = new Dictionary<string, SkillLevelValue>();
        public readonly List<SkillActionDefinition> Actions = new List<SkillActionDefinition>();

        public float GetSpecialValue(string name, int level)
        {
            if (string.IsNullOrEmpty(name))
            {
                return 0f;
            }

            if (!SpecialValues.TryGetValue(name, out SkillLevelValue value))
            {
                return 0f;
            }

            return value.GetValue(level);
        }
    }

    [Serializable]
    public sealed class SkillActionDefinition
    {
        public SkillActionType ActionType;
        public SkillActionTarget Target = SkillActionTarget.ContextTargets;
        public SkillLevelValue Value = SkillLevelValue.Constant(0f);
        public string ValueSpecialName;
        public SkillLevelValue Duration = SkillLevelValue.Constant(float.NaN);
        public string DurationSpecialName;
        public SkillDamageType DamageType = SkillDamageType.Magical;
        public SkillDamageFlags DamageFlags = SkillDamageFlags.None;
        public string ModifierName;
        public bool PurgePositiveBuffs;
        public bool PurgeDebuffs = true;
        public bool PurgeOnlyPurgable = true;
        public SkillProjectileDefinition Projectile;
        public string EffectName;
        public string SoundName;

        public float ResolveValue(SkillAbility ability)
        {
            if (ability == null)
            {
                return 0f;
            }

            if (!string.IsNullOrEmpty(ValueSpecialName))
            {
                return ability.GetSpecialValue(ValueSpecialName);
            }

            return Value.GetValue(ability.Level);
        }

        public float ResolveDuration(SkillAbility ability)
        {
            if (ability == null)
            {
                return float.NaN;
            }

            if (!string.IsNullOrEmpty(DurationSpecialName))
            {
                return ability.GetSpecialValue(DurationSpecialName);
            }

            return Duration.GetValue(ability.Level);
        }
    }

    [Serializable]
    public sealed class SkillModifierDefinition
    {
        public string Name;
        public string DisplayName;
        public bool IsHidden;
        public bool IsDebuff;
        public bool IsPurgable = true;
        public bool RemoveOnDeath = true;
        public float Duration;
        public float Interval;
        public int MaxStack = 1;
        public SkillModifierAttribute Attributes = SkillModifierAttribute.None;
        public readonly Dictionary<SkillModifierProperty, float> Properties = new Dictionary<SkillModifierProperty, float>();
        public SkillUnitState States = SkillUnitState.None;

        public string AuraModifierName;
        public float AuraRadius;
        public float AuraDuration = 0.5f;
        public float AuraThinkInterval = 0.25f;
        public SkillTargetTeam AuraTargetTeam = SkillTargetTeam.Friendly;
        public SkillUnitType AuraTargetType = SkillUnitType.All;
        public SkillTargetFlags AuraTargetFlags = SkillTargetFlags.None;
    }

    [Serializable]
    public sealed class SkillProjectileDefinition
    {
        public string Name;
        public float Speed = 900f;
        public float Radius = 96f;
        public float Distance = 1200f;
        public bool DeleteOnHit = true;
        public bool ProvidesVision;
        public float VisionRadius;
        public SkillTargetTeam TargetTeam = SkillTargetTeam.Enemy;
        public SkillUnitType TargetType = SkillUnitType.All;
        public SkillTargetFlags TargetFlags = SkillTargetFlags.None;
        public string EffectName;
        public string SoundName;
    }
}
