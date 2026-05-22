using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    public sealed class SkillConfigData
    {
        public static readonly SkillConfigData Empty = new SkillConfigData();

        public int Id;
        public string Name;
        public string Description;
        public string IconLocation;
        public SkillAbilityBehavior Behavior;
        public SkillTargetTeam TargetTeam;
        public float CastRange;
        public float AoeRadius;
        public float CastPoint;
        public float ChannelTime;
        public float Cooldown;
        public int CostResourceId;
        public int CostCount;
        public int AbilityActionGroupId;
        public int IntrinsicModifierId;
        public bool Enable = true;
    }

    public sealed class SkillActionData
    {
        public int Id;
        public int GroupId;
        public int Order;
        public SkillActionType ActionType;
        public SkillTargetType TargetType;
        public float Value;
        public float Radius;
        public float Duration;
        public int ModifierId;
        public SkillDamageType DamageType = SkillDamageType.Magical;
        public string EffectLocation;
        public string SoundLocation;
    }

    public sealed class SkillModifierData
    {
        public int Id;
        public string Name;
        public SkillModifierType ModifierType;
        public bool IsDebuff;
        public bool IsHidden;
        public bool IsPurgable = true;
        public bool RemoveOnDeath = true;
        public float Duration;
        public float Interval;
        public int MaxStack = 1;
        public SkillModifierPropertyType PropertyType;
        public float PropertyValue;
        public SkillUnitState State;
        public SkillTriggerEventType TriggerEventType;
        public int TriggerActionGroupId;
        public int PeriodicActionGroupId;
        public int OnCreatedActionGroupId;
        public int OnDestroyActionGroupId;
        public string EffectLocation;
    }

    public sealed class SkillDamageInfo
    {
        public ISkillUnit Source;
        public ISkillUnit Target;
        public int SkillId;
        public float Value;
        public SkillDamageType DamageType;
        public bool IsCritical;
    }

    public sealed class SkillHealInfo
    {
        public ISkillUnit Source;
        public ISkillUnit Target;
        public int SkillId;
        public float Value;
    }

    public sealed class SkillRuntime
    {
        public int SkillId;
        public int OwnerRuntimeId;
        public int Level = 1;
        public float CooldownLeft;
        public float CastPointLeft;
        public bool IsCasting;
        public bool IsChanneling;
        public float ChannelTimeLeft;

        public SkillCastRequest PendingRequest;

        public SkillRuntime(int ownerRuntimeId, int skillId)
        {
            OwnerRuntimeId = ownerRuntimeId;
            SkillId = skillId;
        }
    }

    public sealed class SkillCastRequest
    {
        public int SkillId;
        public ISkillUnit Caster;
        public ISkillUnit TargetUnit;
        public Vector3 TargetPosition;
        public ISkillResourceOwner ResourceOwner;

        public SkillCastRequest(int skillId, ISkillUnit caster)
        {
            SkillId = skillId;
            Caster = caster;
        }
    }

    public sealed class SkillContext
    {
        public SkillConfigData Config;
        public SkillRuntime Runtime;
        public ISkillUnit Caster;
        public ISkillUnit TargetUnit;
        public Vector3 TargetPosition;
        public readonly SkillTargetResult Targets = new SkillTargetResult();
        public ISkillWorld World;
        public ISkillEffectService EffectService;
    }

    public sealed class SkillTriggerEvent
    {
        public SkillTriggerEventType EventType;
        public int SkillId;
        public ISkillUnit Source;
        public ISkillUnit Target;
        public Vector3 Position;
        public float Value;
    }

    public sealed class SkillActionGroup
    {
        private readonly List<SkillActionData> actions = new List<SkillActionData>();

        public IReadOnlyList<SkillActionData> Actions
        {
            get
            {
                return actions;
            }
        }

        public void Add(SkillActionData actionData)
        {
            if (actionData == null)
            {
                return;
            }

            actions.Add(actionData);
            actions.Sort((a, b) => a.Order.CompareTo(b.Order));
        }
    }
}
