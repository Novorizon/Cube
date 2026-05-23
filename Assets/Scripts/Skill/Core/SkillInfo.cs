using System.Collections.Generic;
using UnityEngine;

namespace Game.Skill
{
    /// <summary>
    /// 技能主配置数据，对应 skill.xlsx / SkillConfig。
    /// 它描述技能本体：目标方式、施法距离、前摇、引导、冷却、消耗、ActionGroup 和被动 intrinsic modifier。
    /// </summary>
    public sealed class SkillConfigData
    {
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

    /// <summary>
    /// 技能动作配置数据，对应 skill_action.xlsx / SkillActionConfig。
    /// 多行相同 GroupId 的动作组成一个动作组，按 Order 顺序执行。
    /// </summary>
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

    /// <summary>
    /// Modifier 配置数据，对应 skill_modifier.xlsx / SkillModifierConfig。
    /// 用于表达 Buff、Debuff、属性修正、状态控制、周期触发和事件触发。
    /// </summary>
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

    /// <summary>
    /// 传给业务单位的伤害信息。技能底层只生成信息，实际扣血由业务层实现。
    /// </summary>
    public sealed class SkillDamageInfo
    {
        public ISkillUnit Source;
        public ISkillUnit Target;
        public int SkillId;
        public float Value;
        public SkillDamageType DamageType;
        public bool IsCritical;
    }

    /// <summary>
    /// 传给业务单位的治疗信息。技能底层只生成信息，实际回血由业务层实现。
    /// </summary>
    public sealed class SkillHealInfo
    {
        public ISkillUnit Source;
        public ISkillUnit Target;
        public int SkillId;
        public float Value;
    }

    /// <summary>
    /// 单位身上某个技能的运行时状态。
    /// 同一个 skillId 在不同 ownerRuntimeId 上拥有独立冷却、前摇和引导状态。
    /// </summary>
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

    /// <summary>
    /// 一次释放技能的输入请求。业务层把 caster、目标单位、目标点和资源提供者填进来。
    /// </summary>
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

    /// <summary>
    /// Action 执行时共享的上下文。
    /// Config 可以为空，例如 Modifier 周期触发 ActionGroup 时没有主动技能配置；此时 SkillId 仍然表示来源技能。
    /// </summary>
    public sealed class SkillContext
    {
        public int SkillId;
        public int ActionGroupId;
        public SkillConfigData Config;
        public SkillRuntime Runtime;
        public ISkillUnit Caster;
        public ISkillUnit TargetUnit;
        public Vector3 TargetPosition;
        public readonly SkillTargetResult Targets = new SkillTargetResult();
        public ISkillWorld World;
        public ISkillEffectService EffectService;
    }

    /// <summary>
    /// 业务层传入技能系统的战斗事件，例如攻击命中、受到伤害、死亡。
    /// Modifier 可以根据 TriggerEventType 响应这些事件。
    /// </summary>
    public sealed class SkillTriggerEvent
    {
        public SkillTriggerEventType EventType;
        public int SkillId;
        public ISkillUnit Source;
        public ISkillUnit Target;
        public Vector3 Position;
        public float Value;
    }

    /// <summary>
    /// 同一个 groupId 下的动作组。Action 按 Order 从小到大执行。
    /// </summary>
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