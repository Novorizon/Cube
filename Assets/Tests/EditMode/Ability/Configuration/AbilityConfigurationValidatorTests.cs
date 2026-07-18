using System.Linq;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Configuration")]
    public sealed class AbilityConfigurationValidatorTests
    {
        [Test]
        public void MissingReferencesAndUnsupportedValues_AreErrorsWithSourceLocations()
        {
            AbilityConfigCatalog catalog = new AbilityConfigCatalog();
            catalog.Abilities.Add(new AbilityConfigRecord
            {
                Id = 100,
                Definition = new AbilityDefinition
                {
                    Name = "ability_100",
                    Behavior = AbilityBehavior.UnitTarget,
                    TargetTeam = TargetTeam.Enemy,
                    TargetType = UnitType.All,
                    CastRange = LevelValue.Constant(5f)
                },
                ActionGroupId = 999,
                IntrinsicModifierId = 888,
                CostResourceId = 777,
                RawBehavior = 2,
                RawTargetTeam = 2,
                Source = Source("skill.xlsx", 12)
            });

            AbilityValidationReport report = AbilityConfigurationValidator.Validate(catalog);

            Assert.That(report.IsValid, Is.False);
            Assert.That(report.Issues.Any(issue => issue.Code == "ABILITY017"), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == "ABILITY018"), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == "ABILITY022"), Is.True);
            Assert.That(report.Issues.First(issue => issue.Code == "ABILITY017").Source.Row, Is.EqualTo(12));
        }

        [Test]
        public void PeriodicAction_MustTargetModifierParent()
        {
            AbilityConfigCatalog catalog = new AbilityConfigCatalog();
            catalog.Actions.Add(new AbilityActionConfigRecord
            {
                Id = 201,
                GroupId = 200,
                Order = 1,
                Definition = new ActionDefinition
                {
                    ActionType = ActionType.Damage,
                    Target = ActionTarget.Caster
                },
                RawActionType = 1,
                RawTargetType = 1,
                Source = Source("skill_action.xlsx", 5)
            });
            catalog.Modifiers.Add(new AbilityModifierConfigRecord
            {
                Id = 300,
                Definition = new ModifierDefinition
                {
                    Name = "modifier_300",
                    Interval = 1f
                },
                PeriodicActionGroupId = 200,
                RawPropertyType = 0,
                RawState = 0,
                RawTriggerEventType = 0,
                Source = Source("skill_modifier.xlsx", 7)
            });

            AbilityValidationReport report = AbilityConfigurationValidator.Validate(catalog);

            AbilityValidationIssue issue = report.Issues.Single(item => item.Code == "ABILITY037");
            Assert.That(issue.Severity, Is.EqualTo(AbilityValidationSeverity.Error));
            Assert.That(issue.ReferenceChain, Does.Contain("modifier:300"));
            Assert.That(issue.ReferenceChain, Does.Contain("action:201"));
        }

        [Test]
        public void ModifierReferenceCycle_IsRejected()
        {
            AbilityConfigCatalog catalog = new AbilityConfigCatalog();
            AddModifierWithCreatedGroup(catalog, 301, 401, 501, 302);
            AddModifierWithCreatedGroup(catalog, 302, 402, 502, 301);

            AbilityValidationReport report = AbilityConfigurationValidator.Validate(catalog);

            Assert.That(report.Issues.Any(issue => issue.Code == "ABILITY039"), Is.True);
        }

        [Test]
        public void TowerCooldownConflictAndRepeatedEffects_AreWarningsNotGenerationErrors()
        {
            AbilityConfigCatalog catalog = new AbilityConfigCatalog();
            catalog.Abilities.Add(new AbilityConfigRecord
            {
                Id = 101,
                Definition = new AbilityDefinition
                {
                    Name = "ability_101",
                    Behavior = AbilityBehavior.UnitTarget,
                    TargetTeam = TargetTeam.Enemy,
                    TargetType = UnitType.All,
                    CastRange = LevelValue.Constant(8f),
                    Cooldown = LevelValue.Constant(3f)
                },
                ActionGroupId = 601,
                RawBehavior = 2,
                RawTargetTeam = 2,
                Source = Source("skill.xlsx", 5)
            });
            catalog.Actions.Add(Action(701, 601, 1, "Assets/Effects/hit.prefab"));
            catalog.Actions.Add(Action(702, 601, 2, "Assets/Effects/hit.prefab"));
            catalog.TowerBindings.Add(new TowerAbilityBindingRecord
            {
                TowerId = 9,
                Level = 1,
                AbilityId = 101,
                AttackInterval = 1f,
                Source = Source("tower_level.xlsx", 8)
            });
            catalog.UsedAbilityIds.Add(101);

            AbilityValidationReport report = AbilityConfigurationValidator.Validate(
                catalog,
                new AbilityValidationOptions { AssetExists = _ => true });

            Assert.That(report.ErrorCount, Is.Zero);
            Assert.That(report.Issues.Any(issue => issue.Code == "ABILITY044"), Is.True);
            Assert.That(report.Issues.Any(issue => issue.Code == "ABILITY045"), Is.True);
        }

        [Test]
        public void PassiveAbilityWithoutIntrinsicModifier_IsAnError()
        {
            AbilityConfigCatalog catalog = new AbilityConfigCatalog();
            catalog.Abilities.Add(new AbilityConfigRecord
            {
                Id = 102,
                Definition = new AbilityDefinition
                {
                    Name = "ability_102",
                    Behavior = AbilityBehavior.Passive,
                    TargetTeam = TargetTeam.Friendly
                },
                RawBehavior = 8,
                RawTargetTeam = 1,
                Source = Source("skill.xlsx", 6)
            });

            AbilityValidationReport report = AbilityConfigurationValidator.Validate(catalog);

            Assert.That(report.Issues.Any(issue => issue.Code == "ABILITY019"), Is.True);
        }

        private static void AddModifierWithCreatedGroup(AbilityConfigCatalog catalog, int modifierId, int groupId, int actionId, int targetModifierId)
        {
            catalog.Modifiers.Add(new AbilityModifierConfigRecord
            {
                Id = modifierId,
                Definition = new ModifierDefinition { Name = "modifier_" + modifierId },
                OnCreatedActionGroupId = groupId,
                Source = Source("skill_modifier.xlsx", modifierId)
            });
            catalog.Actions.Add(new AbilityActionConfigRecord
            {
                Id = actionId,
                GroupId = groupId,
                Order = 1,
                ModifierId = targetModifierId,
                Definition = new ActionDefinition
                {
                    ActionType = ActionType.AddModifier,
                    Target = ActionTarget.PrimaryTarget,
                    ModifierName = "modifier_" + targetModifierId
                },
                Source = Source("skill_action.xlsx", actionId)
            });
        }

        private static AbilityActionConfigRecord Action(int id, int groupId, int order, string effect)
        {
            return new AbilityActionConfigRecord
            {
                Id = id,
                GroupId = groupId,
                Order = order,
                Definition = new ActionDefinition
                {
                    ActionType = ActionType.Damage,
                    Target = ActionTarget.PrimaryTarget,
                    EffectName = effect
                },
                Source = Source("skill_action.xlsx", id)
            };
        }

        private static AbilityConfigSource Source(string file, int row)
        {
            return new AbilityConfigSource
            {
                SourceType = "Excel",
                Path = "Data/Excel/" + file,
                Sheet = "Sheet1",
                Row = row
            };
        }
    }
}
