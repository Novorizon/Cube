using System.Collections.Generic;

namespace Game.Skill
{
    public interface ISkillAction
    {
        SkillActionType ActionType { get; }
        void Execute(SkillActionData actionData, SkillContext context, SkillManager skillManager);
    }

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
