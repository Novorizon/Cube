using System.Collections.Generic;

namespace Game.Skill
{
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

        public void RegisterActionHandler(ISkillAction action)
        {
            actionExecutor.Register(action);
        }

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
