using System;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Configuration")]
    public sealed class ModifierLevelPropertyTests
    {
        [TestCase("default")]
        [TestCase("configured")]
        [TestCase("custom")]
        public void PropertiesFollowSourceLevelAndMultiplyStacksExactlyOnce(string scriptKind)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(owner, new AbilityDefinition { Name = "source", MaxLevel = 3 });
            ModifierDefinition definition = new ModifierDefinition { Name = "scaling", MaxStack = 3 };
            definition.Properties.Add(ModifierProperty.MoveSpeedBonusPercent, 7f);
            definition.LevelProperties.Add(ModifierProperty.ArmorBonus, Levels(3f, 6f, 9f));
            definition.LevelProperties.Add(ModifierProperty.HealthRegen, Levels(2f, 4f, 6f));
            rig.Engine.RegisterModifierDefinition(definition);
            ObserveProperty custom = null;
            if (scriptKind == "configured") rig.Engine.RegisterModifierScript(definition.Name, () => new ConfiguredModifierScript());
            if (scriptKind == "custom") rig.Engine.RegisterModifierScript(definition.Name, () => custom = new ObserveProperty());
            Modifier modifier = rig.Engine.AddModifier(owner, owner, ability, definition.Name);
            modifier.SetStackCount(2);

            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(6f));
            ability.SetLevel(3);
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(18f));
            ability.SetLevel(2);
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(12f));
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.MoveSpeedBonusPercent, null), Is.EqualTo(14f));
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.CastRangeBonus, null), Is.Zero);
            rig.Tick(0.5f);
            Assert.That(owner.HealingReceived, Is.EqualTo(4f));
            if (custom != null) Assert.That(custom.ContextCount > 0, Is.True);
        }

        [Test]
        public void StandaloneModifiersUseApplyLevelAndRefreshLevelWithNormalClamping()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            ModifierDefinition definition = new ModifierDefinition { Name = "standalone" };
            definition.LevelProperties.Add(ModifierProperty.ArmorBonus, Levels(3f, 6f));
            rig.Engine.RegisterModifierDefinition(definition);
            Modifier modifier = rig.Engine.AddModifier(owner, owner, null, definition.Name);
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(3f));
            modifier.Refresh(new ModifierApplyOptions { Level = 99 });
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(6f));
            modifier.Refresh(new ModifierApplyOptions { Level = 0 });
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(3f));
        }

        [Test]
        public void JsonLevelPropertiesPublishAlongsideLegacyConstantsAndTrackUpgrades()
        {
            const string json = "{\"abilities\":[{\"name\":\"aura\",\"maxLevel\":3,\"behavior\":2,\"intrinsicModifierName\":\"buff\"}],\"modifiers\":[{\"name\":\"buff\",\"properties\":[{\"property\":1,\"value\":12}],\"levelProperties\":[{\"property\":6,\"value\":{\"values\":[2,4,6]}},{\"property\":2,\"value\":{\"baseValue\":10}}]}]}";
            AbilityDefinitionRegistry registry = Registry(json);
            AbilityTestRig rig = new AbilityTestRig();
            Assert.That(registry.ApplyTo(rig.Engine), Is.True);
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.Engine.AddAbility(owner, "aura", 2);
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(4f));
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.AttackSpeedBonus, null), Is.EqualTo(10f));
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.MoveSpeedBonusPercent, null), Is.EqualTo(12f));
            ability.SetLevel(3);
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(6f));
        }

        [Test]
        public void PublicationDeepCopiesLevelPropertyNumbersAndRevalidatesAuthoringMutation()
        {
            AbilityDefinitionRegistry registry = Registry("{\"modifiers\":[{\"name\":\"buff\",\"levelProperties\":[{\"property\":6,\"value\":{\"values\":[2,4,6]}}]}]}");
            ModifierDefinition authoring = registry.Modifiers["buff"].Definition;
            AbilityTestRig rig = new AbilityTestRig();
            Assert.That(registry.ApplyTo(rig.Engine), Is.True);
            authoring.LevelProperties[ModifierProperty.ArmorBonus].ValuesByLevel[1] = 999f;
            authoring.LevelProperties[ModifierProperty.ArmorBonus].BaseValue = 999f;
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            rig.Engine.AddModifier(owner, owner, null, "buff", new ModifierApplyOptions { Level = 2 });
            Assert.That(rig.Engine.GetModifierProperty(owner, ModifierProperty.ArmorBonus, null), Is.EqualTo(4f));

            AbilitySystem untouched = new AbilitySystem();
            untouched.RegisterModifierDefinition(new ModifierDefinition { Name = "old" });
            authoring.LevelProperties[ModifierProperty.ArmorBonus].ValuesByLevel[0] = float.NaN;
            Assert.That(registry.ApplyTo(untouched), Is.False);
            Assert.That(untouched.TryGetModifierDefinition("old", out _), Is.True);
            Assert.That(untouched.TryGetModifierDefinition("buff", out _), Is.False);
        }

        [Test]
        public void AuraChildReadsSourceAbilityUpgradeBeforeItsNextMaintenancePulse()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            FakeUnit ally = rig.AddUnit(1, Vector3.right);
            ModifierDefinition child = new ModifierDefinition { Name = "aura.child" };
            child.LevelProperties.Add(ModifierProperty.ArmorBonus, Levels(2f, 4f, 6f));
            ModifierDefinition source = new ModifierDefinition { Name = "aura.source", Attributes = ModifierAttribute.Aura, AuraModifierName = child.Name, AuraRadius = 10f, AuraDuration = 0.5f, AuraThinkInterval = 0.25f };
            rig.Engine.RegisterModifierDefinition(child);
            rig.Engine.RegisterModifierDefinition(source);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(owner, new AbilityDefinition { Name = "aura", Behavior = AbilityBehavior.Passive, MaxLevel = 3, IntrinsicModifierName = source.Name });
            Assert.That(rig.Engine.GetModifierProperty(ally, ModifierProperty.ArmorBonus, null), Is.EqualTo(2f));
            ability.SetLevel(3);
            Assert.That(rig.Engine.GetModifierProperty(ally, ModifierProperty.ArmorBonus, null), Is.EqualTo(6f));
            rig.Tick(0.25f);
            Assert.That(rig.Engine.GetModifierProperty(ally, ModifierProperty.ArmorBonus, null), Is.EqualTo(6f));
        }

        [TestCase("\"levelProperties\":[null]")]
        [TestCase("\"levelProperties\":[{\"property\":6}]")]
        [TestCase("\"levelProperties\":[{\"property\":6,\"value\":null}]")]
        [TestCase("\"levelProperties\":[{\"property\":0,\"value\":{\"baseValue\":1}}]")]
        [TestCase("\"levelProperties\":[{\"property\":999,\"value\":{\"baseValue\":1}}]")]
        [TestCase("\"levelProperties\":[{\"property\":6,\"value\":{\"values\":[1,null]}}]")]
        [TestCase("\"levelProperties\":[{\"property\":6,\"value\":{\"values\":[1e100]}}]")]
        [TestCase("\"levelProperties\":[{\"property\":6,\"value\":{\"values\":[\"NaN\"]}}]")]
        [TestCase("\"levelProperties\":[{\"property\":6,\"value\":{\"values\":[1]}},{\"property\":6,\"value\":{\"baseValue\":2}}]")]
        [TestCase("\"properties\":[{\"property\":6,\"value\":1}],\"levelProperties\":[{\"property\":6,\"value\":{\"baseValue\":2}}]")]
        [TestCase("\"levelProperties\":[{\"property\":7,\"value\":{\"values\":[1,-1]}}]")]
        public void InvalidJsonLevelPropertiesAreRejectedWithoutPartiallyPublishing(string fields)
        {
            AbilityDefinitionRegistry registry = Registry("{\"modifiers\":[{\"name\":\"bad\"," + fields + "}]}");
            AbilitySystem engine = new AbilitySystem();
            engine.RegisterModifierDefinition(new ModifierDefinition { Name = "old" });
            Assert.That(registry.ApplyTo(engine), Is.False);
            Assert.That(engine.TryGetModifierDefinition("old", out _), Is.True);
            Assert.That(engine.TryGetModifierDefinition("bad", out _), Is.False);
        }

        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("null")]
        [TestCase("infinite")]
        public void CodeProvidersCannotBypassLevelPropertyValidation(string invalid)
        {
            ModifierDefinition definition = new ModifierDefinition { Name = "bad" };
            ModifierProperty property = invalid == "unknown" ? (ModifierProperty)999 : ModifierProperty.ArmorBonus;
            definition.LevelProperties.Add(property, invalid == "null" ? null : Levels(1f, invalid == "infinite" ? float.PositiveInfinity : 2f));
            if (invalid == "duplicate") definition.Properties.Add(property, 1f);
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            bundle.Modifiers.Add(new ModifierDefinitionRegistration { Definition = definition });
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new Provider(bundle));
            Assert.That(registry.IsValid, Is.False);
            Assert.That(registry.ApplyTo(new AbilitySystem()), Is.False);
        }

        private static LevelValue Levels(params float[] values)
        {
            LevelValue value = LevelValue.Constant(0f);
            value.ValuesByLevel.AddRange(values);
            return value;
        }

        private static AbilityDefinitionRegistry Registry(string json)
        {
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new JsonAbilityDefinitionProvider(json, "level-properties.json"));
            return registry;
        }

        private sealed class ObserveProperty : ModifierScript
        {
            public int ContextCount;
            public override float GetProperty(ModifierProperty property, ModifierPropertyContext context)
            {
                if (context != null) ContextCount++;
                return base.GetProperty(property, context);
            }
        }

        private sealed class Provider : IAbilityDefinitionProvider
        {
            private readonly AbilityDefinitionBundle bundle;
            public Provider(AbilityDefinitionBundle bundle) { this.bundle = bundle; }
            public string ProviderName => "code-level-properties";
            public AbilityDefinitionSourceType SourceType => AbilityDefinitionSourceType.Code;
            public AbilityDefinitionBundle Load() => bundle;
        }
    }
}
