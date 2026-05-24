namespace Game.Ability
{
    public abstract class ModifierScript
    {
        public Modifier Modifier { get; private set; }
        public AbilitySystem Engine => Modifier != null ? Modifier.Engine : null;
        public IUnit Parent => Modifier != null ? Modifier.Parent : null;
        public IUnit Caster => Modifier != null ? Modifier.Caster : null;
        public Ability Ability => Modifier != null ? Modifier.Ability : null;

        internal void Bind(Modifier modifier)
        {
            Modifier = modifier;
        }

        public virtual void OnCreated(ModifierApplyOptions options) { }
        public virtual void OnRefresh(ModifierApplyOptions options) { }
        public virtual void OnDestroy() { }
        public virtual void OnIntervalThink() { }
        public virtual void OnEvent(ModifierEvent modifierEvent) { }

        public virtual float GetProperty(ModifierProperty property, ModifierPropertyContext context)
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

        public virtual bool CheckState(UnitState state)
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

    public sealed class DefaultModifierScript : ModifierScript
    {
    }
}
