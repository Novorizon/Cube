using System.Collections.Generic;
using System.Linq;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Configuration")]
    public sealed class AbilityDefinitionPublicationTests
    {
        [Test]
        public void SingleProvider_MissingReferenceCannotBypassFinalValidation()
        {
            AbilityDefinition ability = new AbilityDefinition { Name = "new", IntrinsicModifierName = "missing" };
            AbilityDefinitionRegistry registry = Load(ability);
            AbilitySystem engine = new AbilitySystem();
            engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "old" });

            Assert.That(registry.ApplyTo(engine), Is.False);
            Assert.That(engine.TryGetAbilityDefinition("new", out _), Is.False);
            Assert.That(engine.TryGetAbilityDefinition("old", out _), Is.True);
            Assert.That(registry.Validation.Issues.Any(issue => issue.Code == "ABILITYPROVIDER010"), Is.True);
        }

        [Test]
        public void LaterProviderCanResolveEarlierMissingReference()
        {
            AbilityDefinitionRegistry registry = Load(new AbilityDefinition { Name = "cast", IntrinsicModifierName = "mark" });
            Assert.That(registry.IsValid, Is.False);
            AbilityDefinitionBundle modifiers = new AbilityDefinitionBundle();
            modifiers.Modifiers.Add(new ModifierDefinitionRegistration { Definition = new ModifierDefinition { Name = "mark" } });
            registry.LoadProvider(new Provider(modifiers));
            Assert.That(registry.IsValid, Is.True);
            Assert.That(registry.ApplyTo(new AbilitySystem()), Is.True);
        }

        [Test]
        public void ApplyRevalidatesAuthoringEditsAndDoesNotPartiallyPublish()
        {
            AbilityDefinition source = new AbilityDefinition { Name = "cast" };
            AbilityDefinitionRegistry registry = Load(source);
            Assert.That(registry.IsValid, Is.True);
            source.ManaCost.BaseValue = float.PositiveInfinity;
            AbilitySystem engine = new AbilitySystem();
            engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "old" });
            Assert.That(registry.ApplyTo(engine), Is.False);
            Assert.That(engine.TryGetAbilityDefinition("old", out _), Is.True);
            Assert.That(engine.TryGetAbilityDefinition("cast", out _), Is.False);
        }

        [Test]
        public void PublicationCopiesNestedGraphAndReplacesOldCatalog()
        {
            ProjectileDefinition projectile = new ProjectileDefinition { Name = "bolt", Speed = 10f };
            AbilityDefinition source = new AbilityDefinition { Name = "cast" };
            source.SpecialValues.Add("damage", LevelValue.Constant(42f));
            source.Actions.Add(new ActionDefinition { ActionType = ActionType.CreateLinearProjectile, Target = ActionTarget.Point, Projectile = projectile });
            AbilityDefinitionRegistry registry = Load(source);
            AbilitySystem engine = new AbilitySystem();
            engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "old" });
            Assert.That(registry.ApplyTo(engine), Is.True);
            source.SpecialValues["damage"].BaseValue = 0f;
            projectile.Speed = 1000f;
            source.Actions.Clear();
            Assert.That(engine.TryGetAbilityDefinition("cast", out AbilityDefinition published), Is.True);
            Assert.That(published.SpecialValues["damage"].BaseValue, Is.EqualTo(42f));
            Assert.That(published.Actions[0].Projectile.Speed, Is.EqualTo(10f));
            Assert.That(engine.TryGetAbilityDefinition("old", out _), Is.False);
        }

        [Test]
        public void NamedProjectileCanResolveAcrossProviders()
        {
            AbilityDefinition source = new AbilityDefinition { Name = "cast" };
            source.Actions.Add(new ActionDefinition { ActionType = ActionType.CreateLinearProjectile, Target = ActionTarget.Point, ProjectileName = "shared.bolt" });
            AbilityDefinitionRegistry registry = Load(source);
            AbilityDefinitionBundle second = new AbilityDefinitionBundle();
            second.Projectiles.Add(new ProjectileDefinitionRegistration { Definition = new ProjectileDefinition { Name = "shared.bolt", Speed = 19f } });
            registry.LoadProvider(new Provider(second));
            AbilitySystem engine = new AbilitySystem();
            Assert.That(registry.ApplyTo(engine), Is.True);
            Assert.That(engine.TryGetAbilityDefinition("cast", out AbilityDefinition published), Is.True);
            Assert.That(published.Actions[0].Projectile.Speed, Is.EqualTo(19f));
            Assert.That(source.Actions[0].Projectile, Is.Null);
        }

        [Test]
        public void ReachableModifierSpecialValuesAreCheckedAgainstEverySourceAbility()
        {
            AbilityDefinition source = new AbilityDefinition { Name = "cast", IntrinsicModifierName = "mark" };
            AbilityDefinitionBundle bundle = Bundle(source);
            ModifierDefinition modifier = new ModifierDefinition { Name = "mark" };
            modifier.OnCreatedActions.Add(new ActionDefinition { ActionType = ActionType.Heal, Target = ActionTarget.Caster, ValueSpecialName = "healing" });
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = modifier });
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            Assert.That(registry.IsValid, Is.False);
            source.SpecialValues.Add("healing", LevelValue.Constant(5f));
            Assert.That(registry.IsValid, Is.True);
        }

        [Test]
        public void ImmediateModifierCyclesAreRejectedButPeriodicRefreshIsAllowed()
        {
            ModifierDefinition first = new ModifierDefinition { Name = "a", Interval = 1f };
            ModifierDefinition second = new ModifierDefinition { Name = "b" };
            first.OnCreatedActions.Add(new ActionDefinition { ActionType = ActionType.AddModifier, Target = ActionTarget.Caster, ModifierName = "b" });
            second.OnCreatedActions.Add(new ActionDefinition { ActionType = ActionType.AddModifier, Target = ActionTarget.Caster, ModifierName = "a" });
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = first });
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = second });
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            Assert.That(registry.Validation.Issues.Any(issue => issue.Code == "ABILITYGRAPH002"), Is.True);
            second.OnCreatedActions.Clear();
            first.IntervalActions.Add(new ActionDefinition { ActionType = ActionType.AddModifier, Target = ActionTarget.Caster, ModifierName = "a" });
            Assert.That(registry.IsValid, Is.True);
        }

        [Test]
        public void InvalidChargesAndPointUnitOperationsAreRejected()
        {
            AbilityDefinition source = new AbilityDefinition { Name = "cast", Charges = new ChargeDefinition { MaxCharges = 0, RestoreTime = 0f } };
            source.Actions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.Point });
            Assert.That(Load(source).IsValid, Is.False);
        }

        [Test]
        public void StableIdsAndNamesResolveWithoutNumericConversion()
        {
            AbilityDefinitionBundle bundle = Bundle(new AbilityDefinition { Name = "fire.bolt" });
            bundle.Abilities[0].Origin = new AbilityDefinitionOrigin { StableId = "hero/mage/bolt" };
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            Assert.That(registry.TryResolveAbilityName("hero/mage/bolt", out string name), Is.True);
            Assert.That(name, Is.EqualTo("fire.bolt"));
            Assert.That(registry.TryResolveAbilityName("fire.bolt", out name), Is.True);
        }

        [Test]
        public void DeclaredCustomScriptRequiresFactoryAndCanRetryAfterRegistration()
        {
            AbilityDefinitionBundle bundle = Bundle(new AbilityDefinition { Name = "custom", Behavior = AbilityBehavior.NoTarget });
            bundle.Abilities[0].RequiresCustomScript = true;
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            AbilitySystem engine = new AbilitySystem();
            Assert.That(registry.IsValid, Is.True);
            Assert.That(registry.ApplyTo(engine), Is.False);
            Assert.That(registry.LastApplyValidation.Issues.Any(issue => issue.Code == "ABILITYPROVIDER011"), Is.True);
            Assert.That(registry.IsValid, Is.True);
            engine.RegisterAbilityScript("custom", () => new DefaultAbilityScript());
            Assert.That(registry.ApplyTo(engine), Is.True);
        }

        [Test]
        public void UnsupportedRuntimeCapabilitiesAndUnknownFlagsAreRejected()
        {
            AbilityDefinition source = new AbilityDefinition { Name = "cast", Behavior = AbilityBehavior.NoTarget | AbilityBehavior.AutoCast };
            AbilityDefinitionRegistry registry = Load(source);
            Assert.That(registry.IsValid, Is.False);
            source.Behavior = (AbilityBehavior)(1 << 28);
            Assert.That(registry.IsValid, Is.False);
            source.Behavior = AbilityBehavior.NoTarget;
            source.Actions.Add(new ActionDefinition { ActionType = ActionType.CreateLinearProjectile, Projectile = new ProjectileDefinition { Name = "vision", ProvidesVision = true } });
            Assert.That(registry.IsValid, Is.False);
        }

        [Test]
        public void EventActionTargetsAndValuesRequireTriggerContext()
        {
            AbilityDefinition source = new AbilityDefinition { Name = "cast" };
            source.Actions.Add(new ActionDefinition { ActionType = ActionType.Heal, Target = ActionTarget.EventSource, ValueSource = ActionValueSource.EventDamage });
            Assert.That(Load(source).IsValid, Is.False);
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            ModifierDefinition modifier = new ModifierDefinition { Name = "lifesteal", TriggerEventType = ModifierEventType.DamageDealt };
            modifier.TriggerActions.Add(source.Actions[0]);
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = modifier });
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            Assert.That(registry.IsValid, Is.True);
            modifier.TriggerEventType = ModifierEventType.Death;
            Assert.That(registry.IsValid, Is.False);
            modifier.TriggerEventType = ModifierEventType.DamageDealt;
            modifier.OnCreatedActions.Add(source.Actions[0]);
            Assert.That(registry.IsValid, Is.False);
        }

        [Test]
        public void MutatingRegistrationToNullIsReportedWithoutThrowing()
        {
            AbilityDefinitionRegistry registry = Load(new AbilityDefinition { Name = "cast" });
            registry.Abilities["cast"].Definition = null;
            Assert.That(registry.IsValid, Is.False);
            Assert.That(registry.ApplyTo(new AbilitySystem()), Is.False);
        }

        [Test]
        public void DeclaredCustomScriptStillRequiresRuntimeSupportedCastMode()
        {
            AbilityDefinition source = new AbilityDefinition { Name = "custom", Behavior = AbilityBehavior.None };
            AbilityDefinitionBundle bundle = Bundle(source);
            bundle.Abilities[0].RequiresCustomScript = true;
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            Assert.That(registry.IsValid, Is.False);
            source.Behavior = AbilityBehavior.Toggle;
            Assert.That(registry.IsValid, Is.False);
            source.Behavior = AbilityBehavior.NoTarget;
            Assert.That(registry.IsValid, Is.True);
        }

        [Test]
        public void MutedRequiresExplicitCustomItemPolicyDeclaration()
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            ModifierDefinitionRegistration registration = new ModifierDefinitionRegistration
            {
                Definition = new ModifierDefinition { Name = "mute", States = UnitState.Muted }
            };
            bundle.Modifiers.Add(registration);
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            Assert.That(registry.IsValid, Is.False);
            registration.RequiresCustomScript = true;
            Assert.That(registry.IsValid, Is.True);
            AbilitySystem engine = new AbilitySystem();
            Assert.That(registry.ApplyTo(engine), Is.False);
            engine.RegisterModifierScript("mute", () => new DefaultModifierScript());
            Assert.That(registry.ApplyTo(engine), Is.True);
        }

        [Test]
        public void AuraAttributeRequiresConfiguredChildOrExplicitCustomScript()
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            ModifierDefinitionRegistration registration = new ModifierDefinitionRegistration
            {
                Definition = new ModifierDefinition { Name = "aura", Attributes = ModifierAttribute.Aura }
            };
            bundle.Modifiers.Add(registration);
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            Assert.That(registry.IsValid, Is.False);
            registration.RequiresCustomScript = true;
            Assert.That(registry.IsValid, Is.True);
            registration.RequiresCustomScript = false;
            registration.Definition.AuraModifierName = "aura_child";
            AbilityDefinitionBundle children = new AbilityDefinitionBundle();
            children.Modifiers.Add(new ModifierDefinitionRegistration { Definition = new ModifierDefinition { Name = "aura_child" } });
            registry.LoadProvider(new Provider(children));
            Assert.That(registry.IsValid, Is.True);
        }

        private static AbilityDefinitionRegistry Load(AbilityDefinition source)
        {
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(Bundle(source)));
            return registry;
        }

        private static AbilityDefinitionBundle Bundle(AbilityDefinition source)
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            bundle.Abilities.Add(new AbilityDefinitionRegistration { Definition = source });
            return bundle;
        }

        private sealed class Provider : IAbilityDefinitionProvider
        {
            private readonly AbilityDefinitionBundle bundle;
            public Provider(AbilityDefinitionBundle bundle) { this.bundle = bundle; }
            public string ProviderName => "test";
            public AbilityDefinitionSourceType SourceType => AbilityDefinitionSourceType.Code;
            public AbilityDefinitionBundle Load() => bundle;
        }
    }
}
