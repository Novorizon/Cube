using System.Collections.Generic;

namespace Game.Skill
{
    /// <summary>
    /// 技能动作系统。
    /// 负责把多行 SkillActionData 按 groupId 组织为 ActionGroup，并按 order 顺序执行。
    /// 它不决定技能能否释放，也不管理 Modifier 生命周期；只负责“技能生效时具体做什么”。
    /// </summary>
    public sealed class SkillActionSystem
    {
        private readonly Dictionary<int, SkillActionGroup> actionGroupMap = new Dictionary<int, SkillActionGroup>();
        private readonly SkillActionExecutor actionExecutor = new SkillActionExecutor();
        private readonly SkillManager skillManager;

        public SkillActionSystem(SkillManager skillManager)
        {
            this.skillManager = skillManager;
        }

        public void Initialize()
        {
            actionGroupMap.Clear();
            RegisterBuiltinActions();
        }

        /// <summary>
        /// 注册一行动作配置。同一个 groupId 下的多行动作会被视为一个动作组。
        /// </summary>
        public void RegisterAction(SkillActionData actionData)
        {
            if (actionData == null)
            {
                return;
            }

            if (!actionGroupMap.TryGetValue(actionData.GroupId, out SkillActionGroup group))
            {
                group = new SkillActionGroup();
                actionGroupMap.Add(actionData.GroupId, group);
            }

            group.Add(actionData);
        }

        /// <summary>
        /// 注册 C# 自定义动作处理器。普通技能优先用配置组合，特殊技能再通过这里扩展。
        /// </summary>
        public void RegisterActionHandler(ISkillAction action)
        {
            actionExecutor.Register(action);
        }

        /// <summary>
        /// 执行动作组。调用方需要提前构造好 SkillContext，特别是 SkillId、Caster、Targets。
        /// </summary>
        public void ExecuteActionGroup(int actionGroupId, SkillContext context)
        {
            if (actionGroupId <= 0 || context == null)
            {
                return;
            }

            if (!actionGroupMap.TryGetValue(actionGroupId, out SkillActionGroup group))
            {
                return;
            }

            context.ActionGroupId = actionGroupId;
            IReadOnlyList<SkillActionData> actions = group.Actions;

            for (int i = 0; i < actions.Count; i++)
            {
                SkillActionData actionData = actions[i];
                actionExecutor.Execute(actionData, context, skillManager);
                skillManager.FireEvent(CreateActionEventData(context, actionData));
            }
        }

        private void RegisterBuiltinActions()
        {
            actionExecutor.Register(new DamageAction());
            actionExecutor.Register(new HealAction());
            actionExecutor.Register(new ApplyModifierAction());
            actionExecutor.Register(new FireEventAction());
        }

        private static SkillEventData CreateActionEventData(SkillContext context, SkillActionData actionData)
        {
            SkillEventData eventData = new SkillEventData();
            eventData.Topic = SkillMessageTopic.ActionExecuted;
            eventData.SkillId = context.SkillId;
            eventData.ActionId = actionData != null ? actionData.Id : 0;
            eventData.Caster = context.Caster;
            eventData.Target = context.TargetUnit;
            eventData.Position = context.TargetPosition;
            eventData.Value = actionData != null ? actionData.Value : 0f;
            return eventData;
        }
    }
}