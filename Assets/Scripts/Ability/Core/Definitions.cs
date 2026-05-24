using System;
using System.Collections.Generic;

namespace Game.Ability
{
    [Serializable]
    public sealed class LevelValue
    {
        public float BaseValue;
        public readonly List<float> ValuesByLevel = new List<float>();

        public static LevelValue Constant(float value)
        {
            return new LevelValue { BaseValue = value };
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
    public sealed class ChargeDefinition
    {
        public int MaxCharges;
        public float RestoreTime;
        public bool StartFull = true;
        public bool UsesCooldown = true;
    }

    [Serializable]
    public sealed class AbilityDefinition
    {
        public string Name;
        public string DisplayName;
        public string Description;
        public string Icon;
        public int MaxLevel = 1;
        public AbilityBehavior Behavior = AbilityBehavior.NoTarget;
        public TargetTeam TargetTeam = TargetTeam.Enemy;
        public UnitType TargetType = UnitType.All;
        public TargetFlags TargetFlags = TargetFlags.None;
        public LevelValue CastRange = LevelValue.Constant(0f);
        public LevelValue AoeRadius = LevelValue.Constant(0f);
        public LevelValue CastPoint = LevelValue.Constant(0f);
        public LevelValue CastBackswing = LevelValue.Constant(0f);
        public LevelValue ChannelTime = LevelValue.Constant(0f);
        public LevelValue Cooldown = LevelValue.Constant(0f);
        public LevelValue ManaCost = LevelValue.Constant(0f);
        public ChargeDefinition Charges;
        public string IntrinsicModifierName;
        public readonly Dictionary<string, LevelValue> SpecialValues = new Dictionary<string, LevelValue>();
        public readonly List<ActionDefinition> Actions = new List<ActionDefinition>();

        public float GetSpecialValue(string name, int level)
        {
            if (string.IsNullOrEmpty(name) || !SpecialValues.TryGetValue(name, out LevelValue value))
            {
                return 0f;
            }

            return value.GetValue(level);
        }
    }

    [Serializable]
    public sealed class ActionDefinition
    {
        public ActionType ActionType;
        public ActionTarget Target = ActionTarget.ContextTargets;
        public LevelValue Value = LevelValue.Constant(0f);
        public string ValueSpecialName;
        public LevelValue Duration = LevelValue.Constant(float.NaN);
        public string DurationSpecialName;
        public DamageType DamageType = DamageType.Magical;
        public DamageFlags DamageFlags = DamageFlags.None;
        public string ModifierName;
        public bool PurgePositiveBuffs;
        public bool PurgeDebuffs = true;
        public bool PurgeOnlyPurgable = true;
        public ProjectileDefinition Projectile;
        public string EffectName;
        public string SoundName;

        public float ResolveValue(Ability ability)
        {
            if (ability == null)
            {
                return 0f;
            }

            return !string.IsNullOrEmpty(ValueSpecialName) ? ability.GetSpecialValue(ValueSpecialName) : Value.GetValue(ability.Level);
        }

        public float ResolveDuration(Ability ability)
        {
            if (ability == null)
            {
                return float.NaN;
            }

            return !string.IsNullOrEmpty(DurationSpecialName) ? ability.GetSpecialValue(DurationSpecialName) : Duration.GetValue(ability.Level);
        }
    }

    [Serializable]
    public sealed class ModifierDefinition
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
        public ModifierAttribute Attributes = ModifierAttribute.None;
        public readonly Dictionary<ModifierProperty, float> Properties = new Dictionary<ModifierProperty, float>();
        public UnitState States = UnitState.None;
        public readonly List<ActionDefinition> OnCreatedActions = new List<ActionDefinition>();
        public readonly List<ActionDefinition> OnRefreshActions = new List<ActionDefinition>();
        public readonly List<ActionDefinition> OnDestroyActions = new List<ActionDefinition>();
        public readonly List<ActionDefinition> IntervalActions = new List<ActionDefinition>();
        public ModifierEventType TriggerEventType = ModifierEventType.None;
        public readonly List<ActionDefinition> TriggerActions = new List<ActionDefinition>();
        public string AuraModifierName;
        public float AuraRadius;
        public float AuraDuration = 0.5f;
        public float AuraThinkInterval = 0.25f;
        public TargetTeam AuraTargetTeam = TargetTeam.Friendly;
        public UnitType AuraTargetType = UnitType.All;
        public TargetFlags AuraTargetFlags = TargetFlags.None;
    }

    [Serializable]
    public sealed class ProjectileDefinition
    {
        public string Name;
        public float Speed = 900f;
        public float Radius = 96f;
        public float Distance = 1200f;
        public bool DeleteOnHit = true;
        public bool ProvidesVision;
        public float VisionRadius;
        public TargetTeam TargetTeam = TargetTeam.Enemy;
        public UnitType TargetType = UnitType.All;
        public TargetFlags TargetFlags = TargetFlags.None;
        public string EffectName;
        public string SoundName;
    }
}
