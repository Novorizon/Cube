namespace Skill2
{
    public abstract class SkillModifierScript
    {
        public SkillModifier Modifier { get; private set; }
        public Skill2Engine Engine => Modifier != null ? Modifier.Engine : null;
        public ISkill2Unit Parent => Modifier != null ? Modifier.Parent : null;
        public ISkill2Unit Caster => Modifier != null ? Modifier.Caster : null;
        public SkillAbility Ability => Modifier != null ? Modifier.Ability : null;

        internal void Bind(SkillModifier modifier)
        {
            Modifier = modifier;
        }

        public virtual void OnCreated(SkillModifierApplyOptions options)
        {
        }

        public virtual void OnRefresh(SkillModifierApplyOptions options)
        {
        }

        public virtual void OnDestroy()
        {
        }

        public virtual void OnIntervalThink()
        {
        }

        public virtual void OnEvent(SkillModifierEvent modifierEvent)
        {
        }

        public virtual float GetProperty(SkillModifierProperty property, SkillModifierPropertyContext context)
        {
            if (Modifier == null || Modifier.Definition == null)
            {
                return 0f;
            }

            if (!Modifier.Definition.Properties.TryGetValue(property, out float value))
            {
                return 0f;
            }

            return value * Modifier.StackCount;
        }

        public virtual bool CheckState(SkillUnitState state)
        {
            return Modifier != null && Modifier.Definition != null && (Modifier.Definition.States & state) != 0;
        }

        public virtual bool IsHidden()
        {
            return Modifier != null && Modifier.Definition != null && Modifier.Definition.IsHidden;
        }

        public virtual bool IsDebuff()
        {
            return Modifier != null && Modifier.Definition != null && Modifier.Definition.IsDebuff;
        }

        public virtual bool IsPurgable()
        {
            return Modifier != null && Modifier.Definition != null && Modifier.Definition.IsPurgable;
        }
    }

    public sealed class DefaultSkillModifierScript : SkillModifierScript
    {
    }
}
