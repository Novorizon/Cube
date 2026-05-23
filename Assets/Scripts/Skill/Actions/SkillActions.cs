using System.Collections.Generic;

namespace Game.Skill
{
    /// <summary>
    /// 单个技能动作处理器接口。
    /// Action 是技能系统最小的可组合效果单元，例如伤害、治疗、添加 Modifier。
    /// 普通技能优先通过 Excel 配置 Action；特殊技能可以新增 C# Action 实现这个接口。
    /// </summary>
    public interface ISkillAction
    {
        SkillActionType ActionType { get; }
        void Execute(SkillActionData actionData, SkillContext context, SkillManager skillManager);
    }

    /// <summary>
    /// 根据 SkillActionType 分发到对应的 ISkillAction。
    /// 它只负责查找和调用处理器，不保存技能配置，也不决定执行顺序。
    /// </summary>
    public sealed class SkillActionExecutor
    {
        private readonly Dictionary<SkillActionType, ISkillAction> actionMap = new Dictionary<SkillActionType, ISkillAction>();

        public void Register(ISkillAction action)
        {
            if (action == null)
            {
                return;
            }

            actionMap[action.ActionType] = action;
        }

        public bool Execute(SkillActionData actionData, SkillContext context, SkillManager skillManager)
        {
            if (actionData == null || context == null || skillManager == null)
            {
                return false;
            }

            if (!actionMap.TryGetValue(actionData.ActionType, out ISkillAction action))
            {
                return false;
            }

            action.Execute(actionData, context, skillManager);
            return true;
        }
    }

    /// <summary>
    /// 对上下文目标造成伤害。
    /// 真正的扣血逻辑由业务层 ISkillUnit.TakeDamage 实现，技能底层只构造 SkillDamageInfo。
    /// </summary>
    public sealed class DamageAction : ISkillAction
    {
        public SkillActionType ActionType
        {
            get
            {
                return SkillActionType.Damage;
            }
        }

        public void Execute(SkillActionData actionData, SkillContext context, SkillManager skillManager)
        {
            IReadOnlyList<ISkillUnit> targets = ResolveTargets(actionData, context);

            for (int i = 0; i < targets.Count; i++)
            {
                ISkillUnit target = targets[i];

                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                SkillDamageInfo damageInfo = new SkillDamageInfo();
                damageInfo.Source = context.Caster;
                damageInfo.Target = target;
                damageInfo.SkillId = context.SkillId;
                damageInfo.Value = actionData.Value;
                damageInfo.DamageType = actionData.DamageType;

                target.TakeDamage(damageInfo);
            }
        }

        private static IReadOnlyList<ISkillUnit> ResolveTargets(SkillActionData actionData, SkillContext context)
        {
            if (actionData.TargetType == SkillTargetType.Caster)
            {
                SkillTargetResult result = new SkillTargetResult();
                result.Add(context.Caster);
                return result.Units;
            }

            return context.Targets.Units;
        }
    }

    /// <summary>
    /// 对上下文目标进行治疗。
    /// 真正的回血逻辑由业务层 ISkillUnit.Heal 实现。
    /// </summary>
    public sealed class HealAction : ISkillAction
    {
        public SkillActionType ActionType
        {
            get
            {
                return SkillActionType.Heal;
            }
        }

        public void Execute(SkillActionData actionData, SkillContext context, SkillManager skillManager)
        {
            if (actionData.TargetType == SkillTargetType.Caster)
            {
                SkillHealInfo selfHealInfo = new SkillHealInfo();
                selfHealInfo.Source = context.Caster;
                selfHealInfo.Target = context.Caster;
                selfHealInfo.SkillId = context.SkillId;
                selfHealInfo.Value = actionData.Value;
                context.Caster.Heal(selfHealInfo);
                return;
            }

            IReadOnlyList<ISkillUnit> targets = context.Targets.Units;

            for (int i = 0; i < targets.Count; i++)
            {
                ISkillUnit target = targets[i];

                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                SkillHealInfo healInfo = new SkillHealInfo();
                healInfo.Source = context.Caster;
                healInfo.Target = target;
                healInfo.SkillId = context.SkillId;
                healInfo.Value = actionData.Value;

                target.Heal(healInfo);
            }
        }
    }

    /// <summary>
    /// 给目标添加 Modifier。减速、眩晕、中毒、被动属性都通过 Modifier 表达。
    /// </summary>
    public sealed class ApplyModifierAction : ISkillAction
    {
        public SkillActionType ActionType
        {
            get
            {
                return SkillActionType.ApplyModifier;
            }
        }

        public void Execute(SkillActionData actionData, SkillContext context, SkillManager skillManager)
        {
            if (actionData.TargetType == SkillTargetType.Caster)
            {
                skillManager.AddModifier(context.Caster, context.Caster, actionData.ModifierId, actionData.Duration, context.SkillId);
                return;
            }

            IReadOnlyList<ISkillUnit> targets = context.Targets.Units;

            for (int i = 0; i < targets.Count; i++)
            {
                ISkillUnit target = targets[i];

                if (target == null || !target.IsAlive)
                {
                    continue;
                }

                skillManager.AddModifier(context.Caster, target, actionData.ModifierId, actionData.Duration, context.SkillId);
            }
        }
    }

    /// <summary>
    /// 发送一个技能事件。当前实现较轻量，主要用于配置驱动的事件通知。
    /// </summary>
    public sealed class FireEventAction : ISkillAction
    {
        public SkillActionType ActionType
        {
            get
            {
                return SkillActionType.FireEvent;
            }
        }

        public void Execute(SkillActionData actionData, SkillContext context, SkillManager skillManager)
        {
            skillManager.FireEvent(SkillMessageTopic.ActionExecuted);
        }
    }
}