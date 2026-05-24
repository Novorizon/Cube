namespace Game.Ability
{
    public sealed class ConfiguredModifierScript : ModifierScript
    {
        public override void OnCreated(ModifierApplyOptions options)
        {
            ActionRunner.Execute(Modifier.Definition.OnCreatedActions, CreateContext(Parent));
        }

        public override void OnRefresh(ModifierApplyOptions options)
        {
            ActionRunner.Execute(Modifier.Definition.OnRefreshActions, CreateContext(Parent));
        }

        public override void OnDestroy()
        {
            ActionRunner.Execute(Modifier.Definition.OnDestroyActions, CreateContext(Parent));
        }

        public override void OnIntervalThink()
        {
            ActionRunner.Execute(Modifier.Definition.IntervalActions, CreateContext(Parent));
        }

        public override void OnEvent(ModifierEvent modifierEvent)
        {
            if (modifierEvent == null || Modifier == null || Modifier.Definition == null)
            {
                return;
            }

            if (Modifier.Definition.TriggerEventType == ModifierEventType.None || modifierEvent.EventType != Modifier.Definition.TriggerEventType)
            {
                return;
            }

            IUnit target = modifierEvent.Target ?? Parent;
            ActionRunner.Execute(Modifier.Definition.TriggerActions, CreateContext(target));
        }

        private CastContext CreateContext(IUnit target)
        {
            return ActionRunner.CreateSingleTargetContext(Engine, Ability, Caster, target, Parent != null ? Parent.Position : UnityEngine.Vector3.zero);
        }
    }
}
