using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game;
using Game.Ability;
using Luban;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability.Configuration
{
    [TestFixture]
    [Category("Ability")]
    [Category("Configuration")]
    [Category("Baseline")]
    public sealed class LegacySkillDataBaselineTests
    {
        private Tables tables;

        [OneTimeSetUp]
        public void LoadGeneratedTables()
        {
            string binDirectory = Path.Combine(Application.dataPath, "Data", "Bin");
            tables = new Tables(file =>
            {
                string path = Path.Combine(binDirectory, file + ".bytes");
                Assert.That(File.Exists(path), Is.True, "Missing generated table: " + path);
                return new ByteBuf(File.ReadAllBytes(path));
            });
        }

        [Test]
        public void LegacySkillTables_HaveExpectedBaselineCounts()
        {
            Assert.That(tables.TbSkill.DataList.Count, Is.EqualTo(7));
            Assert.That(tables.TbSkillAction.DataList.Count, Is.EqualTo(9));
            Assert.That(tables.TbSkillModifier.DataList.Count, Is.EqualTo(5));
        }

        [Test]
        public void CurrentNamedSkills_ArePresentAndEnabled()
        {
            int[] expectedIds =
            {
                50000001,
                50000002,
                50000003,
                50000004,
                50000005,
                50000006,
                50001001
            };

            for (int i = 0; i < expectedIds.Length; i++)
            {
                SkillConfig skill = tables.TbSkill.GetOrDefault(expectedIds[i]);
                Assert.That(skill, Is.Not.Null, "Missing skill: " + expectedIds[i]);
                Assert.That(skill.Enable, Is.True, "Disabled skill: " + expectedIds[i]);
            }
        }

        [Test]
        public void EnabledSkills_ReferenceExistingActionGroupsAndIntrinsicModifiers()
        {
            HashSet<int> actionGroups = new HashSet<int>(tables.TbSkillAction.DataList.Select(x => x.GroupId));
            IReadOnlyDictionary<int, SkillModifierConfig> modifiers = tables.TbSkillModifier.DataMap;

            foreach (SkillConfig skill in tables.TbSkill.DataList.Where(x => x.Enable))
            {
                if (skill.AbilityActionGroupId > 0)
                {
                    Assert.That(
                        actionGroups.Contains(skill.AbilityActionGroupId),
                        Is.True,
                        $"Skill {skill.Id} references missing action group {skill.AbilityActionGroupId}.");
                }

                if (skill.IntrinsicModifierId > 0)
                {
                    Assert.That(
                        modifiers.ContainsKey(skill.IntrinsicModifierId),
                        Is.True,
                        $"Skill {skill.Id} references missing intrinsic modifier {skill.IntrinsicModifierId}.");
                }
            }
        }

        [Test]
        public void ActionsAndModifiers_ReferenceExistingDefinitions()
        {
            IReadOnlyDictionary<int, SkillModifierConfig> modifiers = tables.TbSkillModifier.DataMap;
            HashSet<int> actionGroups = new HashSet<int>(tables.TbSkillAction.DataList.Select(x => x.GroupId));

            foreach (SkillActionConfig action in tables.TbSkillAction.DataList)
            {
                if (action.ModifierId > 0)
                {
                    Assert.That(
                        modifiers.ContainsKey(action.ModifierId),
                        Is.True,
                        $"Action {action.Id} references missing modifier {action.ModifierId}.");
                }
            }

            foreach (SkillModifierConfig modifier in tables.TbSkillModifier.DataList)
            {
                AssertActionGroupExists(actionGroups, modifier.Id, "trigger", modifier.TriggerActionGroupId);
                AssertActionGroupExists(actionGroups, modifier.Id, "periodic", modifier.PeriodicActionGroupId);
                AssertActionGroupExists(actionGroups, modifier.Id, "on-created", modifier.OnCreatedActionGroupId);
                AssertActionGroupExists(actionGroups, modifier.Id, "on-destroy", modifier.OnDestroyActionGroupId);
            }
        }

        [Test]
        public void NewAbilityTables_AreStillEmptyDuringLegacyCompatibilityPhase()
        {
            Assert.That(tables.TbAbilityConfig.DataList.Count, Is.Zero);
            Assert.That(tables.TbAbilityActionConfig.DataList.Count, Is.Zero);
            Assert.That(tables.TbAbilityModifierConfig.DataList.Count, Is.Zero);
            Assert.That(tables.TbAbilityModifierPropertyConfig.DataList.Count, Is.Zero);
            Assert.That(tables.TbAbilityProjectileConfig.DataList.Count, Is.Zero);
            Assert.That(tables.TbAbilitySpecialValueConfig.DataList.Count, Is.Zero);
        }

        [Test]
        public void PoisonDot_PeriodicGroupIsParentMagicalDamage()
        {
            SkillModifierConfig poison = tables.TbSkillModifier.Get(50500004);
            List<SkillActionConfig> periodicActions = tables.TbSkillAction.DataList
                .Where(x => x.GroupId == poison.PeriodicActionGroupId)
                .ToList();

            Assert.That(periodicActions, Has.Count.EqualTo(1));
            SkillActionConfig action = periodicActions[0];
            Assert.That(action.ActionType, Is.EqualTo(1), "Poison Dot must deal damage.");
            Assert.That(action.TargetType, Is.EqualTo(2), "Periodic modifier actions must target the modifier parent.");
            Assert.That(action.Value, Is.EqualTo(15f).Within(0.001f));
            Assert.That(action.ModifierId, Is.Zero, "Poison Dot must not add the attack-speed modifier.");
            Assert.That(action.DamageType, Is.EqualTo(2), "Poison Dot damage is magical.");
        }

        [Test]
        public void IceTowerShot_AddsFreezeWhileTowerLevelOwnsBaseAttack()
        {
            SkillConfig skill = tables.TbSkill.Get(50000003);
            List<SkillActionConfig> actions = tables.TbSkillAction.DataList
                .Where(x => x.GroupId == skill.AbilityActionGroupId)
                .ToList();
            List<TowerLevelConfig> towerLevels = tables.TbTowerLevel.DataList
                .Where(x => x.Enable && x.SkillId == skill.Id)
                .ToList();

            Assert.That(skill.Cooldown, Is.Zero, "Tower attack cadence must come from TowerLevelConfig.");
            Assert.That(actions, Has.Count.EqualTo(1));
            Assert.That(actions[0].ActionType, Is.EqualTo(3), "The ability should only add its freeze modifier.");
            Assert.That(actions[0].ModifierId, Is.EqualTo(50500002));
            Assert.That(actions.Any(x => x.ActionType == 1), Is.False, "Base damage must not be duplicated in the ability action group.");

            Assert.That(towerLevels, Is.Not.Empty);
            Assert.That(towerLevels.All(x => x.Damage > 0), Is.True);
            Assert.That(towerLevels.All(x => x.Range > 0f), Is.True);
            Assert.That(towerLevels.All(x => x.AttackInterval > 0f), Is.True);
        }

        [Test]
        public void ActionGroups_DoNotRepeatTheSameAutomaticEffect()
        {
            foreach (IGrouping<int, SkillActionConfig> group in tables.TbSkillAction.DataList.GroupBy(x => x.GroupId))
            {
                List<string> effects = group
                    .Select(x => x.EffectLocation)
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                Assert.That(
                    effects.Distinct().Count(),
                    Is.EqualTo(effects.Count),
                    $"Action group {group.Key} repeats an automatically played effect.");
            }
        }

        [Test]
        public void ModifierEffectLocation_IsNotConvertedToOneShotCreatedEffect()
        {
            SkillModifierConfig frostSlow = tables.TbSkillModifier.Get(50500001);
            ModifierDefinition definition = AbilityConfigConverter.ToModifierDefinition(
                frostSlow,
                new Dictionary<int, List<SkillActionConfig>>());

            Assert.That(frostSlow.EffectLocation, Is.Not.Empty);
            Assert.That(definition.OnCreatedActions.Any(x => x.ActionType == ActionType.PlayEffect), Is.False);
            Assert.That(definition.SustainedEffectName, Is.EqualTo(frostSlow.EffectLocation));
        }

        private static void AssertActionGroupExists(
            HashSet<int> actionGroups,
            int modifierId,
            string lifecycle,
            int actionGroupId)
        {
            if (actionGroupId <= 0)
            {
                return;
            }

            Assert.That(
                actionGroups.Contains(actionGroupId),
                Is.True,
                $"Modifier {modifierId} references missing {lifecycle} action group {actionGroupId}.");
        }
    }
}
