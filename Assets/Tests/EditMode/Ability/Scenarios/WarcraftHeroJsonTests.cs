using System;
using System.IO;
using System.Linq;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;
using UnityEngine;
using RuntimeAbility = Game.Ability.Ability;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("HeroJson")]
    public sealed class WarcraftHeroJsonTests
    {
        private const string Mountain = "wc3.mountain_king.";
        private const string Tauren = "wc3.tauren_chieftain.";
        private const string Paladin = "wc3.paladin.";

        [TestCase("mountain_king.json", 2)]
        [TestCase("tauren_chieftain.json", 3)]
        [TestCase("paladin.json", 2)]
        public void HeroFile_PublishesIndependentlyWithoutExternalScripts(string file, int expectedAbilities)
        {
            Scenario scenario = new Scenario(file);
            Assert.That(scenario.Registry.Abilities.Count, Is.EqualTo(expectedAbilities));
            foreach (AbilityDefinitionRegistration registration in scenario.Registry.Abilities.Values)
            {
                Assert.That(registration.RequiresCustomScript, Is.False);
                Assert.That(registration.Definition.MaxLevel, Is.EqualTo(3));
            }
        }

        [Test]
        public void AllHeroFiles_CanPublishTogetherWithoutIdentifierCollisions()
        {
            Scenario scenario = new Scenario("mountain_king.json", "tauren_chieftain.json", "paladin.json");
            Assert.That(scenario.Registry.Abilities.Count, Is.EqualTo(7));
            Assert.That(scenario.Registry.Modifiers.Count, Is.EqualTo(8));
            Assert.That(scenario.Registry.Projectiles.Count, Is.EqualTo(2));
        }

        [TestCase(1, 100f)]
        [TestCase(2, 225f)]
        [TestCase(3, 350f)]
        public void StormBolt_TracksThenDealsRankDamageAndStunsOnlyOnImpact(int level, float expectedDamage)
        {
            Scenario scenario = new Scenario("mountain_king.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit target = scenario.Rig.AddUnit(2, new Vector3(12f, 0f, 0f), UnitType.Hero);
            FakeResourceOwner resources = new FakeResourceOwner(200f);
            RuntimeAbility ability = scenario.Add(caster, Mountain + "storm_bolt", level, resources);

            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, ability.Definition.Name, target).Success, Is.True);
            Assert.That(target.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.HasState(target, UnitState.Stunned), Is.False);
            Assert.That(resources.Mana, Is.EqualTo(125f));
            Assert.That(resources.SpendCount, Is.EqualTo(1));
            Assert.That(ability.CooldownDuration, Is.EqualTo(9f));
            Assert.That(scenario.Engine.Projectiles.Count, Is.EqualTo(1));

            scenario.Rig.Tick(0.21f);

            Assert.That(target.DamageTaken, Is.EqualTo(expectedDamage));
            Assert.That(target.DamageResults.Count, Is.EqualTo(1));
            Assert.That(target.DamageResults[0].Ability, Is.SameAs(ability));
            Assert.That(scenario.Engine.HasState(target, UnitState.Stunned), Is.True);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
            scenario.Rig.Tick(2.99f);
            Assert.That(scenario.Engine.HasState(target, UnitState.Stunned), Is.True);
            scenario.Rig.Tick(0.02f);
            Assert.That(scenario.Engine.HasState(target, UnitState.Stunned), Is.False);
        }

        [Test]
        public void StormBolt_RejectsImmuneTargetWithoutSpendingOrStartingCooldown()
        {
            Scenario scenario = new Scenario("mountain_king.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit target = scenario.Rig.AddUnit(2, Vector3.right);
            target.IsMagicImmune = true;
            FakeResourceOwner resources = new FakeResourceOwner(200f);
            RuntimeAbility ability = scenario.Add(caster, Mountain + "storm_bolt", 3, resources);

            CastResult result = scenario.Engine.CastAbilityOnTarget(caster, ability.Definition.Name, target);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
            Assert.That(resources.SpendCount, Is.Zero);
            Assert.That(ability.CooldownRemaining, Is.Zero);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void StormBolt_InvisibleTarget_IsRejectedWithoutSpendingOrCooldown()
        {
            Scenario scenario = new Scenario("mountain_king.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit target = scenario.Rig.AddUnit(2, Vector3.right);
            target.IsVisible = false;
            FakeResourceOwner resources = new FakeResourceOwner(200f);
            RuntimeAbility ability = scenario.Add(caster, Mountain + "storm_bolt", 3, resources);

            CastResult result = scenario.Engine.CastAbilityOnTarget(caster, ability.Definition.Name, target);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
            Assert.That(resources.Mana, Is.EqualTo(200f));
            Assert.That(resources.SpendCount, Is.Zero);
            Assert.That(ability.CooldownRemaining, Is.Zero);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void StormBolt_TargetBecomesInvisibleAfterLaunch_StillReceivesDamageAndStun()
        {
            Scenario scenario = new Scenario("mountain_king.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit target = scenario.Rig.AddUnit(2, new Vector3(12f, 0f, 0f));
            RuntimeAbility ability = scenario.Add(caster, Mountain + "storm_bolt", 3);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, ability.Definition.Name, target).Success, Is.True);
            target.IsVisible = false;

            scenario.Rig.Tick(0.21f);

            Assert.That(target.DamageTaken, Is.EqualTo(350f));
            Assert.That(scenario.Engine.HasState(target, UnitState.Stunned), Is.True);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void StormBolt_TargetDiesInFlight_DoesNotApplyDamageOrControl()
        {
            Scenario scenario = new Scenario("mountain_king.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit target = scenario.Rig.AddUnit(2, new Vector3(12f, 0f, 0f));
            RuntimeAbility ability = scenario.Add(caster, Mountain + "storm_bolt");
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, ability.Definition.Name, target).Success, Is.True);
            target.IsAlive = false;

            scenario.Rig.Tick(1f);

            Assert.That(target.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [TestCase(1, 60f, false)]
        [TestCase(2, 100f, true)]
        [TestCase(3, 140f, true)]
        public void ThunderClap_RankChangesDamageAndRadius_AndAppliesBothSlowProperties(int level, float expectedDamage, bool hitsOuter)
        {
            Scenario scenario = new Scenario("mountain_king.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit inner = scenario.Rig.AddUnit(2, new Vector3(20f, 0f, 0f));
            FakeUnit outer = scenario.Rig.AddUnit(2, new Vector3(28f, 0f, 0f));
            RuntimeAbility ability = scenario.Add(caster, Mountain + "thunder_clap", level);

            Assert.That(scenario.Engine.CastAbility(caster, ability.Definition.Name).Success, Is.True);

            Assert.That(inner.DamageTaken, Is.EqualTo(expectedDamage));
            Assert.That(outer.DamageTaken, Is.EqualTo(hitsOuter ? expectedDamage : 0f));
            Assert.That(scenario.Property(inner, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(-50f));
            Assert.That(scenario.Property(inner, ModifierProperty.AttackSpeedBonus), Is.EqualTo(-50f));
            scenario.Rig.Tick(5.01f);
            Assert.That(scenario.Property(inner, ModifierProperty.MoveSpeedBonusPercent), Is.Zero);
            Assert.That(scenario.Property(inner, ModifierProperty.AttackSpeedBonus), Is.Zero);
        }

        [Test]
        public void ThunderClap_ExcludesAlliesBuildingsAndMagicImmuneEnemies()
        {
            Scenario scenario = new Scenario("mountain_king.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit ally = scenario.Rig.AddUnit(1, Vector3.right);
            FakeUnit building = scenario.Rig.AddUnit(2, Vector3.right, UnitType.Building);
            FakeUnit immune = scenario.Rig.AddUnit(2, Vector3.right);
            immune.IsMagicImmune = true;
            RuntimeAbility ability = scenario.Add(caster, Mountain + "thunder_clap", 3);

            Assert.That(scenario.Engine.CastAbility(caster, ability.Definition.Name).Success, Is.True);

            Assert.That(caster.DamageTaken + ally.DamageTaken + building.DamageTaken + immune.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [TestCase(1, 75f)]
        [TestCase(2, 130f)]
        [TestCase(3, 200f)]
        public void Shockwave_PiercesEachEnemyOnce_ExcludesOffAxisAndFriendlyUnits(int level, float expectedDamage)
        {
            Scenario scenario = new Scenario("tauren_chieftain.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit first = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            FakeUnit second = scenario.Rig.AddUnit(2, new Vector3(20f, 0f, 0f));
            FakeUnit offAxis = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 5f));
            FakeUnit ally = scenario.Rig.AddUnit(1, new Vector3(15f, 0f, 0f));
            RuntimeAbility ability = scenario.Add(caster, Tauren + "shockwave", level);
            Assert.That(scenario.Engine.CastAbilityOnPosition(caster, ability.Definition.Name, new Vector3(70f, 0f, 0f)).Success, Is.True);
            Assert.That(first.DamageTaken + second.DamageTaken, Is.Zero);

            scenario.Rig.Tick(0.5f);
            scenario.Rig.Tick(0.6f);

            Assert.That(first.DamageTaken, Is.EqualTo(expectedDamage));
            Assert.That(second.DamageTaken, Is.EqualTo(expectedDamage));
            Assert.That(first.DamageResults.Count, Is.EqualTo(1));
            Assert.That(second.DamageResults.Count, Is.EqualTo(1));
            Assert.That(offAxis.DamageTaken + ally.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [TestCase(1, 25f, 3f)]
        [TestCase(2, 50f, 4f)]
        [TestCase(3, 75f, 5f)]
        public void WarStomp_RankDamageAndControlExpireAtConfiguredBoundary(int level, float expectedDamage, float duration)
        {
            Scenario scenario = new Scenario("tauren_chieftain.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit target = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            FakeUnit ally = scenario.Rig.AddUnit(1, Vector3.right);
            RuntimeAbility ability = scenario.Add(caster, Tauren + "war_stomp", level);

            Assert.That(scenario.Engine.CastAbility(caster, ability.Definition.Name).Success, Is.True);

            Assert.That(target.DamageTaken, Is.EqualTo(expectedDamage));
            Assert.That(scenario.Engine.HasState(target, UnitState.Stunned), Is.True);
            Assert.That(ally.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.HasState(ally, UnitState.Stunned), Is.False);
            scenario.Rig.Tick(duration - 0.01f);
            Assert.That(scenario.Engine.HasState(target, UnitState.Stunned), Is.True);
            scenario.Rig.Tick(0.02f);
            Assert.That(scenario.Engine.HasState(target, UnitState.Stunned), Is.False);
        }

        [TestCase(1, 10f, 5f)]
        [TestCase(2, 20f, 10f)]
        [TestCase(3, 30f, 15f)]
        public void EnduranceAura_GrantsRankMovementAndAttackSpeedToSelfAndNearbyAllies(int level, float movement, float attackSpeed)
        {
            Scenario scenario = new Scenario("tauren_chieftain.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit ally = scenario.Rig.AddUnit(1, new Vector3(10f, 0f, 0f));
            FakeUnit enemy = scenario.Rig.AddUnit(2, Vector3.right);
            scenario.Add(caster, Tauren + "endurance_aura", level);

            Assert.That(scenario.Property(caster, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(movement));
            Assert.That(scenario.Property(ally, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(movement));
            Assert.That(scenario.Property(ally, ModifierProperty.AttackSpeedBonus), Is.EqualTo(attackSpeed));
            Assert.That(scenario.Property(enemy, ModifierProperty.AttackSpeedBonus), Is.Zero);
            scenario.Rig.Tick(2f);
            Assert.That(scenario.Property(ally, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(movement));
            ally.Position = new Vector3(100f, 0f, 0f);
            scenario.Rig.Tick(0.51f);
            Assert.That(scenario.Property(ally, ModifierProperty.MoveSpeedBonusPercent), Is.Zero);
            Assert.That(scenario.Property(ally, ModifierProperty.AttackSpeedBonus), Is.Zero);
        }

        [TestCase(1, 1.5f)]
        [TestCase(2, 3f)]
        [TestCase(3, 4.5f)]
        public void DevotionAura_GrantsRankArmorAndLeavesOnlyLingerWhenSourceRemoved(int level, float armor)
        {
            Scenario scenario = new Scenario("paladin.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit ally = scenario.Rig.AddUnit(1, Vector3.right);
            RuntimeAbility ability = scenario.Add(caster, Paladin + "devotion_aura", level);
            Assert.That(scenario.Property(caster, ModifierProperty.ArmorBonus), Is.EqualTo(armor));
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.EqualTo(armor));

            Assert.That(scenario.Engine.RemoveAbility(caster, ability.Definition.Name), Is.True);
            scenario.Rig.Tick(0.51f);

            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void AuraUpgrade_UpdatesExistingChildrenWithoutReapplyingOrStacking()
        {
            Scenario scenario = new Scenario("paladin.json", "tauren_chieftain.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit ally = scenario.Rig.AddUnit(1, Vector3.right);
            RuntimeAbility devotion = scenario.Add(caster, Paladin + "devotion_aura");
            RuntimeAbility endurance = scenario.Add(caster, Tauren + "endurance_aura");
            Modifier child = scenario.Engine.Modifiers.Single(modifier => modifier.Parent == ally && modifier.Name == Paladin + "devotion_buff");
            int before = scenario.Engine.Modifiers.Count;

            devotion.SetLevel(3);
            endurance.SetLevel(3);

            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.EqualTo(4.5f));
            Assert.That(scenario.Property(ally, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(30f));
            Assert.That(scenario.Property(ally, ModifierProperty.AttackSpeedBonus), Is.EqualTo(15f));
            scenario.Rig.Tick(1f);
            Assert.That(scenario.Engine.Modifiers.Count, Is.EqualTo(before));
            Assert.That(scenario.Engine.Modifiers.Single(modifier => modifier.Parent == ally && modifier.Name == child.Name), Is.SameAs(child));
            Assert.That(child.StackCount, Is.EqualTo(1));
        }

        [TestCase(1, 15f, 35f)]
        [TestCase(2, 30f, 50f)]
        [TestCase(3, 45f, 65f)]
        public void DivineShield_BlocksDamageAndNormalPurge_UntilRankDurationExpires(int level, float duration, float cooldown)
        {
            Scenario scenario = new Scenario("paladin.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit enemy = scenario.Rig.AddUnit(2, Vector3.right);
            FakeResourceOwner resources = new FakeResourceOwner(100f);
            RuntimeAbility ability = scenario.Add(caster, Paladin + "divine_shield", level, resources);

            Assert.That(scenario.Engine.CastAbility(caster, ability.Definition.Name).Success, Is.True);
            Assert.That(resources.Mana, Is.EqualTo(75f));
            Assert.That(ability.CooldownDuration, Is.EqualTo(cooldown));
            Assert.That(scenario.Engine.HasState(caster, UnitState.Invulnerable), Is.True);
            Assert.That(scenario.Engine.ApplyDamage(new DamageInfo { Attacker = enemy, Victim = caster, DamageType = DamageType.Pure, Amount = 100f }).Blocked, Is.True);
            Assert.That(scenario.Engine.Purge(caster, true, false, true), Is.Zero);
            scenario.Rig.Tick(duration - 0.01f);
            Assert.That(scenario.Engine.HasState(caster, UnitState.Invulnerable), Is.True);
            scenario.Rig.Tick(0.02f);
            Assert.That(scenario.Engine.HasState(caster, UnitState.Invulnerable), Is.False);
            Assert.That(scenario.Engine.ApplyDamage(new DamageInfo { Attacker = enemy, Victim = caster, DamageType = DamageType.Pure, Amount = 100f }).AppliedAmount, Is.EqualTo(100f));
            Assert.That(caster.DamageTaken, Is.EqualTo(100f));
        }

        [Test]
        public void HeroCombination_ClearRuntimeStopsPendingProjectileAndAuraCoverage()
        {
            Scenario scenario = new Scenario("mountain_king.json", "tauren_chieftain.json", "paladin.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit ally = scenario.Rig.AddUnit(1, Vector3.right);
            FakeUnit enemy = scenario.Rig.AddUnit(2, new Vector3(40f, 0f, 0f));
            scenario.Add(caster, Paladin + "devotion_aura", 3);
            scenario.Add(caster, Tauren + "endurance_aura", 3);
            RuntimeAbility bolt = scenario.Add(caster, Mountain + "storm_bolt", 3);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, bolt.Definition.Name, enemy).Success, Is.True);
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.EqualTo(4.5f));

            scenario.Engine.ClearRuntime();
            scenario.Rig.Tick(10f);

            Assert.That(enemy.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
            Assert.That(scenario.Engine.GetAbilities(caster), Is.Empty);
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.Zero);
        }

        private sealed class Scenario
        {
            public Scenario(params string[] files)
            {
                foreach (string file in files)
                {
                    string path = FindFixture(file);
                    Registry.LoadProvider(new JsonAbilityDefinitionProvider(File.ReadAllText(path), path));
                }
                string issues = string.Join("; ", Registry.Validation.Issues.Select(issue => issue.Code + ": " + issue.Message));
                Assert.That(Registry.IsValid, Is.True, issues);
                Assert.That(Registry.ApplyTo(Engine), Is.True, issues);
            }

            public AbilityTestRig Rig { get; } = new AbilityTestRig();
            public AbilityDefinitionRegistry Registry { get; } = new AbilityDefinitionRegistry();
            public AbilitySystem Engine => Rig.Engine;
            public float Property(IUnit unit, ModifierProperty property) => Engine.GetModifierProperty(unit, property, null);
            public RuntimeAbility Add(IUnit unit, string name, int level = 1, IResourceOwner resources = null)
            {
                RuntimeAbility ability = Engine.AddAbility(unit, name, level, resources ?? new FakeResourceOwner(10000f));
                Assert.That(ability, Is.Not.Null, name);
                return ability;
            }
        }

        private static string FindFixture(string file)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (DirectoryInfo directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    string path = Path.Combine(directory.FullName, "Data", "AbilityJsonSources", "Heroes", "Warcraft3", file);
                    if (File.Exists(path)) return path;
                }
            }
            throw new FileNotFoundException("Hero JSON fixture not found: " + file);
        }
    }
}
