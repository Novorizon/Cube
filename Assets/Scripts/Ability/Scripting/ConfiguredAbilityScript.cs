namespace Game.Ability
{
    /// <summary>
    /// Default script used when an ability is fully described by configured actions.
    /// </summary>
    public sealed class ConfiguredAbilityScript : AbilityScript
    {
        public override void OnSpellStart(CastContext context)
        {
            if (Ability == null || Ability.Definition == null)
            {
                return;
            }

            ActionRunner.Execute(Ability.Definition.Actions, context);
        }
    }
}
