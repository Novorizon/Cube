namespace Game.Skill
{
    public sealed class SkillModifierContext
    {
        public SkillModifierInstance Instance;
        public SkillManager SkillManager;
    }

    public interface ISkillModifierLogic
    {
        void OnCreated(SkillModifierContext context);
        void OnRefresh(SkillModifierContext context);
        void OnRemoved(SkillModifierContext context);
        void OnInterval(SkillModifierContext context);
        void OnTriggerEvent(SkillModifierContext context, SkillTriggerEvent triggerEvent);
        float GetProperty(SkillModifierContext context, SkillModifierPropertyType propertyType);
        bool HasState(SkillModifierContext context, SkillUnitState state);
    }

    public class DefaultSkillModifierLogic : ISkillModifierLogic
    {
        public virtual void OnCreated(SkillModifierContext context)
        {
        }

        public virtual void OnRefresh(SkillModifierContext context)
        {
        }

        public virtual void OnRemoved(SkillModifierContext context)
        {
        }

        public virtual void OnInterval(SkillModifierContext context)
        {
        }

        public virtual void OnTriggerEvent(SkillModifierContext context, SkillTriggerEvent triggerEvent)
        {
        }

        public virtual float GetProperty(SkillModifierContext context, SkillModifierPropertyType propertyType)
        {
            if (context == null || context.Instance == null || context.Instance.Data == null)
            {
                return 0f;
            }

            SkillModifierData data = context.Instance.Data;

            if (data.PropertyType != propertyType)
            {
                return 0f;
            }

            return data.PropertyValue * context.Instance.StackCount;
        }

        public virtual bool HasState(SkillModifierContext context, SkillUnitState state)
        {
            if (context == null || context.Instance == null || context.Instance.Data == null)
            {
                return false;
            }

            return context.Instance.Data.State == state;
        }
    }
}
