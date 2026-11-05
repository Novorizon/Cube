using System;
using System.Linq;
using System.Threading;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Configuration")]
    public sealed class ConfigurationAudit20261105Tests
    {
        [TestCase("created")]
        [TestCase("refresh")]
        [TestCase("destroy")]
        [TestCase("interval")]
        [TestCase("trigger")]
        [TestCase("aura")]
        public void LongModifierReferenceChainsValidateOnSmallNativeStack(string slot)
        {
            AbilityDefinitionBundle bundle = Chain(5000, slot, true);
            AbilityDefinitionRegistry registry = Registry(bundle);
            bool valid = false;
            Exception failure = null;
            Thread worker = new Thread(() =>
            {
                try { valid = registry.IsValid; }
                catch (Exception exception) { failure = exception; }
            }, 64 * 1024) { IsBackground = true };
            worker.Start();

            Assert.That(worker.Join(10000), Is.True);
            Assert.That(failure, Is.Null);
            Assert.That(valid, Is.True);
        }

        [Test]
        public void LargeAcyclicCatalogPublishesWithoutAnEditorAuthoringLimit()
        {
            AbilityDefinitionRegistry registry = Registry(Chain(5000, "created", false));
            AbilitySystem engine = new AbilitySystem();

            Assert.That(registry.ApplyTo(engine), Is.True);
            Assert.That(engine.TryGetModifierDefinition("m4999", out _), Is.True);
        }

        [Test]
        public void ReachableSpecialsThroughLargeGraphsAreCheckedForEverySourceAbility()
        {
            AbilityDefinitionBundle bundle = Chain(2000, "interval", true);
            ModifierDefinition leaf = bundle.Modifiers.Last().Definition;
            leaf.Interval = 1f;
            leaf.IntervalActions.Add(new ActionDefinition { ActionType = ActionType.Heal, Target = ActionTarget.Parent, ValueSpecialName = "amount" });
            bundle.Abilities[0].Definition.SpecialValues.Add("amount", LevelValue.Constant(10f));
            AbilityDefinition other = new AbilityDefinition { Name = "other", Behavior = AbilityBehavior.Passive, IntrinsicModifierName = "m0" };
            bundle.Abilities.Add(new AbilityDefinitionRegistration { Definition = other, Origin = Origin("other.json") });
            AbilityDefinitionRegistry registry = Registry(bundle);

            Assert.That(registry.IsValid, Is.False);
            Assert.That(registry.Validation.Issues.Any(issue => issue.Code == "ABILITYPROVIDER010" && issue.Source?.Path == "other.json"), Is.True);
            other.SpecialValues.Add("amount", LevelValue.Constant(20f));
            Assert.That(registry.IsValid, Is.True);
            other.SpecialValues["amount"].BaseValue = -1f;
            Assert.That(registry.IsValid, Is.False);
            Assert.That(registry.Validation.Issues.Any(issue => issue.Code == "ABILITYGRAPH001" && issue.Source?.Path == "other.json"), Is.True);
        }

        [Test]
        public void ImmediateCyclesKeepTheirReferenceChainAndSourceDiagnostic()
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            ModifierDefinition a = Modifier(bundle, "scope.a");
            ModifierDefinition b = Modifier(bundle, "scope.b");
            ModifierDefinition c = Modifier(bundle, "scope.c");
            a.OnCreatedActions.Add(Add("scope.b"));
            b.OnRefreshActions.Add(Add("scope.c"));
            c.Attributes = ModifierAttribute.Aura;
            c.AuraModifierName = "scope.a";
            AbilityDefinitionRegistry registry = Registry(bundle);

            Assert.That(registry.IsValid, Is.False);
            AbilityValidationIssue cycle = registry.Validation.Issues.Single(issue => issue.Code == "ABILITYGRAPH002");
            Assert.That(cycle.Message, Does.Contain("scope.a"));
            Assert.That(cycle.Message, Does.Contain("scope.b"));
            Assert.That(cycle.Message, Does.Contain("scope.c"));
            string sourceName = cycle.ReferenceChain.Substring("modifier ".Length);
            Assert.That(cycle.Source.Path, Is.EqualTo(sourceName + ".json"));
            c.Attributes = ModifierAttribute.None;
            c.AuraModifierName = null;
            Assert.That(registry.IsValid, Is.True);
        }

        [Test]
        public void PeriodicAndEventReferenceCyclesRemainSupported()
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            ModifierDefinition a = Modifier(bundle, "periodic");
            ModifierDefinition b = Modifier(bundle, "reactive");
            a.Interval = 1f;
            a.IntervalActions.Add(Add("reactive"));
            b.TriggerEventType = ModifierEventType.DamageTaken;
            b.TriggerActions.Add(Add("periodic"));
            bundle.Abilities.Add(new AbilityDefinitionRegistration { Definition = new AbilityDefinition { Name = "source", Behavior = AbilityBehavior.Passive, IntrinsicModifierName = "periodic" } });

            Assert.That(Registry(bundle).IsValid, Is.True);
        }

        [Test]
        public void MissingReferenceAtDeepLeafDoesNotPartiallyReplaceDefinitions()
        {
            AbilityDefinitionBundle bundle = Chain(2000, "refresh", false);
            bundle.Modifiers.Last().Definition.OnRefreshActions.Add(Add("missing"));
            AbilityDefinitionRegistry registry = Registry(bundle);
            AbilitySystem engine = new AbilitySystem();
            engine.RegisterModifierDefinition(new ModifierDefinition { Name = "old" });

            Assert.That(registry.ApplyTo(engine), Is.False);
            Assert.That(engine.TryGetModifierDefinition("old", out _), Is.True);
            Assert.That(engine.TryGetModifierDefinition("m0", out _), Is.False);
            Assert.That(registry.LastApplyValidation.Issues.Any(issue => issue.Code == "ABILITYPROVIDER010" && issue.Source?.Path == "m1999.json"), Is.True);
        }

        [Test]
        public void MultipleImmediateRootsCanReuseAnAlreadyValidatedChild()
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            Modifier(bundle, "first").OnCreatedActions.Add(Add("child"));
            Modifier(bundle, "second").OnRefreshActions.Add(Add("child"));
            Modifier(bundle, "child");

            Assert.That(Registry(bundle).IsValid, Is.True);
        }

        [Test]
        public void LegacyCatalogValidatesDeepChainsAndStillRejectsItsPeriodicCycles()
        {
            AbilityConfigCatalog catalog = new AbilityConfigCatalog();
            const int count = 5000;
            for (int i = 1; i <= count; i++)
            {
                catalog.Modifiers.Add(new AbilityModifierConfigRecord
                {
                    Id = i,
                    Definition = new ModifierDefinition { Name = "legacy." + i, Interval = i < count ? 1f : 0f },
                    PeriodicActionGroupId = i < count ? i : 0,
                    Source = new AbilityConfigSource { Path = "legacy.xlsx", Row = i }
                });
                if (i < count) catalog.Actions.Add(new AbilityActionConfigRecord { Id = i, GroupId = i, ModifierId = i + 1, Definition = new ActionDefinition { ActionType = ActionType.AddModifier, Target = ActionTarget.PrimaryTarget, ModifierName = "legacy." + (i + 1) } });
            }
            AbilityValidationReport report = null;
            Exception failure = null;
            Thread worker = new Thread(() =>
            {
                try { report = AbilityConfigurationValidator.Validate(catalog); }
                catch (Exception exception) { failure = exception; }
            }, 64 * 1024) { IsBackground = true };
            worker.Start();
            Assert.That(worker.Join(10000), Is.True);
            Assert.That(failure, Is.Null);
            Assert.That(report.IsValid, Is.True);
            catalog.Modifiers.Last().PeriodicActionGroupId = count;
            catalog.Modifiers.Last().Definition.Interval = 1f;
            catalog.Actions.Add(new AbilityActionConfigRecord { Id = count, GroupId = count, ModifierId = 1, Definition = new ActionDefinition { ActionType = ActionType.AddModifier, Target = ActionTarget.PrimaryTarget, ModifierName = "legacy.1" } });

            AbilityValidationIssue cycle = AbilityConfigurationValidator.Validate(catalog).Issues.Single(issue => issue.Code == "ABILITY039");
            Assert.That(cycle.Source.Row, Is.EqualTo(count));
            Assert.That(cycle.ReferenceChain, Does.Contain("modifier:1"));
            Assert.That(cycle.ReferenceChain, Does.Contain("modifier:" + count));
        }

        private static AbilityDefinitionBundle Chain(int count, string slot, bool ability)
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            for (int i = 0; i < count; i++)
            {
                ModifierDefinition definition = Modifier(bundle, "m" + i);
                if (i + 1 >= count) continue;
                ActionDefinition action = Add("m" + (i + 1));
                switch (slot)
                {
                    case "created": definition.OnCreatedActions.Add(action); break;
                    case "refresh": definition.OnRefreshActions.Add(action); break;
                    case "destroy": definition.OnDestroyActions.Add(action); break;
                    case "interval": definition.Interval = 1f; definition.IntervalActions.Add(action); break;
                    case "trigger": definition.TriggerEventType = ModifierEventType.DamageTaken; definition.TriggerActions.Add(action); break;
                    case "aura": definition.Attributes = ModifierAttribute.Aura; definition.AuraModifierName = action.ModifierName; break;
                    default: throw new ArgumentException(slot);
                }
            }
            if (ability) bundle.Abilities.Add(new AbilityDefinitionRegistration { Definition = new AbilityDefinition { Name = "root", Behavior = AbilityBehavior.Passive, IntrinsicModifierName = "m0" }, Origin = Origin("root.json") });
            return bundle;
        }

        private static ActionDefinition Add(string name) => new ActionDefinition { ActionType = ActionType.AddModifier, Target = ActionTarget.Parent, ModifierName = name };

        private static ModifierDefinition Modifier(AbilityDefinitionBundle bundle, string name)
        {
            ModifierDefinition definition = new ModifierDefinition { Name = name };
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = definition, Origin = Origin(name + ".json") });
            return definition;
        }

        private static AbilityDefinitionOrigin Origin(string file) => new AbilityDefinitionOrigin { SourcePath = file, SourceType = AbilityDefinitionSourceType.Code };

        private static AbilityDefinitionRegistry Registry(AbilityDefinitionBundle bundle)
        {
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            return registry;
        }

        private sealed class Provider : IAbilityDefinitionProvider
        {
            private readonly AbilityDefinitionBundle bundle;
            public Provider(AbilityDefinitionBundle bundle) { this.bundle = bundle; }
            public string ProviderName => "configuration-audit";
            public AbilityDefinitionSourceType SourceType => AbilityDefinitionSourceType.Code;
            public AbilityDefinitionBundle Load() => bundle;
        }
    }
}
