namespace Game.Skill
{
    /// <summary>
    /// C# Modifier 逻辑执行上下文。
    /// 普通 Modifier 只需要 SkillModifierData；当配置无法表达特殊逻辑时，可以通过 ISkillModifierLogic 读取这里的实例和技能系统入口。
    /// </summary>
    public sealed class SkillModifierContext
    {
        public SkillModifierInstance Instance;
        public SkillManager SkillManager;
    }

    /// <summary>
    /// C# Modifier 扩展点。
    /// 不接 Lua 时，特殊被动、护盾、暴击、吸血、反伤、层数结算等逻辑可以通过实现这个接口完成。
    /// </summary>
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

    /// <summary>
    /// 默认 Modifier 逻辑。
    /// 它根据 SkillModifierData.PropertyType / PropertyValue / State 提供最基础的属性和状态能力。
    /// 没有注册自定义逻辑的 Modifier 都会走这里。
    /// </summary>
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