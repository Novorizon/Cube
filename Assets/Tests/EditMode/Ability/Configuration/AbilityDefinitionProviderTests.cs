using System;
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
    public sealed class AbilityDefinitionProviderTests
    {
        [Test]
        public void JsonProvider_LoadsNestedDefinitionsAndPreservesOrigin()
        {
            const string json = @"{
  ""schemaVersion"": 1,
  ""namespace"": ""sample"",
  ""projectiles"": [
    { ""id"": ""p1"", ""name"": ""sample.private_bolt"", ""isPrivate"": true, ""speed"": 10, ""radius"": 1, ""distance"": 5, ""deleteOnHit"": true, ""targetTeam"": 2, ""targetType"": 63 }
  ],
  ""modifiers"": [
    { ""id"": ""m1"", ""name"": ""sample.private_mark"", ""isPrivate"": true, ""duration"": 2, ""maxStack"": 1, ""isPurgable"": true, ""removeOnDeath"": true }
  ],
  ""abilities"": [
    {
      ""id"": ""a1"", ""name"": ""sample.cast"", ""maxLevel"": 2,
      ""behavior"": 8, ""targetTeam"": 2, ""targetType"": 63,
      ""castRange"": { ""values"": [5, 7] },
      ""actions"": [
        { ""actionType"": 5, ""target"": 2, ""projectileName"": ""sample.private_bolt"" },
        { ""actionType"": 3, ""target"": 2, ""modifierName"": ""sample.private_mark"", ""duration"": { ""baseValue"": 2 } }
      ]
    }
  ]
}";
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProviders(new[]
            {
                new JsonAbilityDefinitionProvider(json, "Data/AbilityJsonSources/sample.json")
            });

            Assert.That(registry.IsValid, Is.True);
            Assert.That(registry.Abilities.Count, Is.EqualTo(1));
            Assert.That(registry.Modifiers.Count, Is.EqualTo(1));
            Assert.That(registry.Projectiles.Count, Is.EqualTo(1));
            AbilityDefinition definition = registry.Abilities["sample.cast"].Definition;
            Assert.That(definition.CastRange.GetValue(2), Is.EqualTo(7f));
            Assert.That(definition.Actions[0].Projectile, Is.Not.Null);
            Assert.That(registry.TryGetAbilityOrigin("sample.cast", out AbilityDefinitionOrigin origin), Is.True);
            Assert.That(origin.SourceType, Is.EqualTo(AbilityDefinitionSourceType.Json));
            Assert.That(origin.SourcePath, Is.EqualTo("Data/AbilityJsonSources/sample.json"));
        }

        [Test]
        public void Registry_DuplicateNameAcrossExcelAndJsonDoesNotUseLoadOrderOverride()
        {
            FixedProvider excel = AbilityProvider("Excel", AbilityDefinitionSourceType.Excel, "same_name", "5001", "Data/Excel/skill.xlsx");
            FixedProvider json = AbilityProvider("Json", AbilityDefinitionSourceType.Json, "same_name", "json.ability", "Data/AbilityJsonSources/a.json");
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();

            registry.LoadProviders(new IAbilityDefinitionProvider[] { excel, json });

            Assert.That(registry.IsValid, Is.False);
            AbilityValidationIssue collision = registry.Validation.Issues.Single(issue => issue.Code == "ABILITYPROVIDER009");
            Assert.That(collision.Message, Does.Contain("No load-order override"));
            Assert.That(registry.Abilities.Count, Is.EqualTo(1));
            Assert.That(registry.ApplyTo(new AbilitySystem()), Is.False);
        }

        [Test]
        public void Registry_DuplicateStableIdIsRejectedEvenWhenNamesDiffer()
        {
            FixedProvider first = AbilityProvider("Excel", AbilityDefinitionSourceType.Excel, "excel_name", "shared-id", "Data/Excel/skill.xlsx");
            FixedProvider second = AbilityProvider("Json", AbilityDefinitionSourceType.Json, "json_name", "shared-id", "Data/AbilityJsonSources/a.json");
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();

            registry.LoadProviders(new IAbilityDefinitionProvider[] { first, second });

            Assert.That(registry.Validation.Issues.Any(issue => issue.Code == "ABILITYPROVIDER009"), Is.True);
            Assert.That(registry.Abilities.ContainsKey("json_name"), Is.False);
        }

        [Test]
        public void JsonAbility_CanReferencePublicModifierFromExcelProvider()
        {
            AbilityDefinitionBundle excelBundle = new AbilityDefinitionBundle();
            excelBundle.Modifiers.Add(new ModifierDefinitionRegistration
            {
                Definition = new ModifierDefinition { Name = "modifier_global_slow" },
                Origin = Origin(AbilityDefinitionSourceType.Excel, "50500001", "Data/Excel/skill_modifier.xlsx")
            });
            FixedProvider excel = new FixedProvider("Excel", AbilityDefinitionSourceType.Excel, excelBundle);
            const string json = @"{
  ""schemaVersion"": 1,
  ""namespace"": ""sample"",
  ""abilities"": [
    {
      ""id"": ""a1"", ""name"": ""sample.cast"", ""maxLevel"": 1,
      ""behavior"": 4, ""targetTeam"": 2, ""targetType"": 63,
      ""actions"": [ { ""actionType"": 3, ""target"": 1, ""modifierName"": ""modifier_global_slow"" } ]
    }
  ]
}";
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();

            registry.LoadProviders(new IAbilityDefinitionProvider[]
            {
                excel,
                new JsonAbilityDefinitionProvider(json, "Data/AbilityJsonSources/global_ref.json")
            });

            Assert.That(registry.IsValid, Is.True);
        }

        [Test]
        public void PrivateJsonDefinitionWithoutNamespacePrefix_ProducesWarning()
        {
            const string json = @"{
  ""schemaVersion"": 1,
  ""namespace"": ""expected"",
  ""modifiers"": [ { ""id"": ""m1"", ""name"": ""private_mark"", ""isPrivate"": true, ""maxStack"": 1 } ]
}";
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();

            registry.LoadProviders(new[] { new JsonAbilityDefinitionProvider(json, "private.json") });

            Assert.That(registry.IsValid, Is.True);
            Assert.That(registry.Validation.Issues.Any(issue => issue.Code == "ABILITYPROVIDER007"), Is.True);
        }

        private static FixedProvider AbilityProvider(string providerName, AbilityDefinitionSourceType type, string name, string stableId, string path)
        {
            AbilityDefinitionBundle bundle = new AbilityDefinitionBundle();
            bundle.Abilities.Add(new AbilityDefinitionRegistration
            {
                Definition = new AbilityDefinition { Name = name },
                Origin = Origin(type, stableId, path)
            });
            return new FixedProvider(providerName, type, bundle);
        }

        private static AbilityDefinitionOrigin Origin(AbilityDefinitionSourceType type, string stableId, string path)
        {
            return new AbilityDefinitionOrigin
            {
                SourceType = type,
                ProviderName = type.ToString(),
                SourcePath = path,
                StableId = stableId
            };
        }

        private sealed class FixedProvider : IAbilityDefinitionProvider
        {
            private readonly AbilityDefinitionBundle bundle;

            public FixedProvider(string providerName, AbilityDefinitionSourceType sourceType, AbilityDefinitionBundle bundle)
            {
                ProviderName = providerName;
                SourceType = sourceType;
                this.bundle = bundle;
            }

            public string ProviderName { get; }
            public AbilityDefinitionSourceType SourceType { get; }
            public AbilityDefinitionBundle Load() => bundle;
        }
    }
}
