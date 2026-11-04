using System;
using System.IO;
using System.Linq;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    /// <summary>Hero reference configurations are tested through the strict production JSON/publication path.</summary>
    [TestFixture]
    [Category("Ability")]
    [Category("HeroExamples")]
    public sealed class DotaHeroJsonTests
    {
        private const string Lina = "hero.dota2.lina.";
        private const string Maiden = "hero.dota2.crystal_maiden.";
        private const string Lion = "hero.dota2.lion.";
        private const string Sven = "hero.dota2.sven.";
        private static readonly string[] Documents = { "lina.json", "crystal_maiden.json", "lion.json", "sven.json" };

        [TestCase("lina.json", 2)]
        [TestCase("crystal_maiden.json", 2)]
        [TestCase("lion.json", 2)]
        [TestCase("sven.json", 1)]
        public void EachHeroDocument_PublishesWithoutOtherHeroDefinitions(string file, int expectedAbilities)
        {
            HeroScenario scenario = new HeroScenario(file);
            Assert.That(scenario.Registry.Abilities.Count, Is.EqualTo(expectedAbilities));
        }

        [Test]
        public void AllHeroDocuments_PublishTogetherWithUniquePublicIdsAndNoCustomScripts()
        {
            HeroScenario scenario = new HeroScenario(Documents);
            Assert.That(scenario.Registry.Abilities.Count, Is.EqualTo(7));
            Assert.That(scenario.Registry.Modifiers.Count, Is.EqualTo(5));
            Assert.That(scenario.Registry.Projectiles.Count, Is.EqualTo(2));
            foreach (string name in new[] { Lina + "dragon_slave", Lina + "laguna_blade", Maiden + "crystal_nova", Maiden + "frostbite", Lion + "earth_spike", Lion + "hex", Sven + "warcry" })
            {
                Assert.That(scenario.Registry.TryResolveAbilityName(name, out string resolved), Is.True, name);
                Assert.That(resolved, Is.EqualTo(name));
            }
        }

        [TestCase(1, 85f, 100f, 11f)]
        [TestCase(2, 160f, 115f, 10f)]
        [TestCase(3, 235f, 130f, 9f)]
        [TestCase(4, 310f, 145f, 8f)]
        public void DragonSlave_PiercesOncePerEnemyAndCommitsLevelScaledCosts(int level, float damage, float mana, float cooldown)
        {
            HeroScenario scenario = new HeroScenario("lina.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit first = scenario.Rig.AddUnit(2, new Vector3(200f, 0f, 0f));
            FakeUnit second = scenario.Rig.AddUnit(2, new Vector3(700f, 0f, 0f), UnitType.Hero);
            FakeUnit ally = scenario.Rig.AddUnit(1, new Vector3(300f, 0f, 0f));
            FakeUnit creep = scenario.Rig.AddUnit(2, new Vector3(600f, 0f, 0f), UnitType.Creep);
            FakeUnit outside = scenario.Rig.AddUnit(2, new Vector3(400f, 0f, 300f));
            FakeUnit building = scenario.Rig.AddUnit(2, new Vector3(500f, 0f, 0f), UnitType.Building);
            FakeResourceOwner resources = new FakeResourceOwner(1000f);
            Game.Ability.Ability ability = scenario.Add(caster, Lina + "dragon_slave", level, resources);

            Assert.That(scenario.Engine.CastAbilityOnPosition(caster, ability.Definition.Name, new Vector3(1000f, 0f, 0f)).Success, Is.True);
            Assert.That(resources.SpendCount, Is.Zero);
            Assert.That(first.DamageTaken, Is.Zero);
            scenario.Rig.Tick(0.45f);
            Assert.That(resources.SpendCount, Is.EqualTo(1));
            Assert.That(resources.Mana, Is.EqualTo(1000f - mana));
            Assert.That(ability.CooldownDuration, Is.EqualTo(cooldown));
            scenario.Rig.Tick(1f);
            scenario.Rig.Tick(1f);

            Assert.That(first.DamageTaken, Is.EqualTo(damage));
            Assert.That(second.DamageTaken, Is.EqualTo(damage));
            Assert.That(creep.DamageTaken, Is.EqualTo(damage));
            Assert.That(first.DamageResults.Count, Is.EqualTo(1));
            Assert.That(second.DamageResults.Count, Is.EqualTo(1));
            Assert.That(ally.DamageTaken + outside.DamageTaken + building.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void DragonSlave_RechecksMagicImmunityWhenProjectileArrives()
        {
            HeroScenario scenario = new HeroScenario("lina.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit victim = scenario.Rig.AddUnit(2, new Vector3(800f, 0f, 0f));
            scenario.Add(caster, Lina + "dragon_slave", 4);
            Assert.That(scenario.Engine.CastAbilityOnPosition(caster, Lina + "dragon_slave", victim.Position).Success, Is.True);
            scenario.Rig.Tick(0.45f);
            victim.IsMagicImmune = true;
            scenario.Rig.Tick(1f);
            Assert.That(victim.DamageTaken, Is.Zero);
        }

        [TestCase(1, 450f, 150f, 70f)]
        [TestCase(2, 650f, 300f, 60f)]
        [TestCase(3, 850f, 450f, 50f)]
        public void LagunaBlade_UsesSingleTargetDamageAndRankedManaAndCooldown(int level, float damage, float mana, float cooldown)
        {
            HeroScenario scenario = new HeroScenario("lina.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit victim = scenario.Rig.AddUnit(2, new Vector3(200f, 0f, 0f), UnitType.Hero);
            FakeUnit bystander = scenario.Rig.AddUnit(2, new Vector3(210f, 0f, 0f));
            FakeResourceOwner resources = new FakeResourceOwner(1000f);
            Game.Ability.Ability ability = scenario.Add(caster, Lina + "laguna_blade", level, resources);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, ability.Definition.Name, victim).Success, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
            scenario.Rig.Tick(0.45f);
            Assert.That(victim.DamageTaken, Is.EqualTo(damage));
            Assert.That(victim.DamageResults.Single().DamageType, Is.EqualTo(DamageType.Magical));
            Assert.That(bystander.DamageTaken, Is.Zero);
            Assert.That(resources.Mana, Is.EqualTo(1000f - mana));
            Assert.That(resources.SpendCount, Is.EqualTo(1));
            Assert.That(ability.CooldownDuration, Is.EqualTo(cooldown));
        }

        [Test]
        public void LagunaBlade_InvalidTeamAndRangeDoNotSpendManaOrStartCooldown()
        {
            HeroScenario scenario = new HeroScenario("lina.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit ally = scenario.Rig.AddUnit(1, Vector3.right, UnitType.Hero);
            FakeUnit farEnemy = scenario.Rig.AddUnit(2, new Vector3(601f, 0f, 0f));
            FakeResourceOwner resources = new FakeResourceOwner(1000f);
            Game.Ability.Ability ability = scenario.Add(caster, Lina + "laguna_blade", 1, resources);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, ability.Definition.Name, ally).FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, ability.Definition.Name, farEnemy).FailureReason, Is.EqualTo(CastFailureReason.OutOfRange));
            Assert.That(resources.SpendCount, Is.Zero);
            Assert.That(ability.CooldownRemaining, Is.Zero);
        }

        [TestCase(1, 110f, -20f, -30f, 3f)]
        [TestCase(2, 160f, -30f, -40f, 3.5f)]
        [TestCase(3, 210f, -40f, -50f, 4f)]
        [TestCase(4, 260f, -50f, -60f, 4.5f)]
        public void CrystalNova_AffectsOnlyEnemiesInsideAreaAndAppliesRankedSlow(int level, float damage, float movement, float attackSpeed, float duration)
        {
            HeroScenario scenario = new HeroScenario("crystal_maiden.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            Vector3 point = new Vector3(600f, 0f, 0f);
            FakeUnit center = scenario.Rig.AddUnit(2, point, UnitType.Hero);
            FakeUnit edge = scenario.Rig.AddUnit(2, new Vector3(1025f, 0f, 0f));
            FakeUnit outside = scenario.Rig.AddUnit(2, new Vector3(1026f, 0f, 0f));
            FakeUnit ally = scenario.Rig.AddUnit(1, point);
            FakeUnit immune = scenario.Rig.AddUnit(2, point);
            immune.IsMagicImmune = true;
            scenario.Add(caster, Maiden + "crystal_nova", level);
            Assert.That(scenario.Engine.CastAbilityOnPosition(caster, Maiden + "crystal_nova", point).Success, Is.True);
            Assert.That(center.DamageTaken, Is.Zero);
            scenario.Rig.Tick(0.3f);

            Assert.That(center.DamageTaken, Is.EqualTo(damage));
            Assert.That(edge.DamageTaken, Is.EqualTo(damage));
            Assert.That(outside.DamageTaken + ally.DamageTaken + immune.DamageTaken, Is.Zero);
            Assert.That(scenario.Property(center, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(movement));
            Assert.That(scenario.Property(center, ModifierProperty.AttackSpeedBonus), Is.EqualTo(attackSpeed));
            Assert.That(scenario.Find(center, Maiden + "nova_slow").RemainingTime, Is.EqualTo(duration));
            scenario.Rig.Tick(duration);
            Assert.That(scenario.Property(center, ModifierProperty.MoveSpeedBonusPercent), Is.Zero);
        }

        [TestCase("lina.json", "hero.dota2.lina.laguna_blade")]
        [TestCase("crystal_maiden.json", "hero.dota2.crystal_maiden.frostbite")]
        [TestCase("lion.json", "hero.dota2.lion.hex")]
        public void UnitTargetHeroSkills_RejectInvisibleEnemyWithoutSpendingManaOrCooldown(string file, string name)
        {
            HeroScenario scenario = new HeroScenario(file);
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit victim = scenario.Rig.AddUnit(2, Vector3.right, UnitType.Hero);
            victim.IsVisible = false;
            FakeResourceOwner resources = new FakeResourceOwner(1000f);
            Game.Ability.Ability ability = scenario.Add(caster, name, 1, resources);

            CastResult result = scenario.Engine.CastAbilityOnTarget(caster, name, victim);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
            Assert.That(resources.Mana, Is.EqualTo(1000f));
            Assert.That(resources.SpendCount, Is.Zero);
            Assert.That(ability.CooldownRemaining, Is.Zero);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            scenario.Rig.Tick(1f);
            Assert.That(victim.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void CrystalNova_AreaDamageStillAffectsInvisibleEnemiesAtTheSelectedPoint()
        {
            HeroScenario scenario = new HeroScenario("crystal_maiden.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit victim = scenario.Rig.AddUnit(2, new Vector3(200f, 0f, 0f), UnitType.Hero);
            victim.IsVisible = false;
            scenario.Add(caster, Maiden + "crystal_nova", 1);

            Assert.That(scenario.Engine.CastAbilityOnPosition(caster, Maiden + "crystal_nova", victim.Position).Success, Is.True);
            scenario.Rig.Tick(0.3f);

            Assert.That(victim.DamageTaken, Is.EqualTo(110f));
            Assert.That(scenario.Property(victim, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(-20f));
        }

        [Test]
        public void CrystalNova_InsufficientManaProducesNoAreaEffects()
        {
            HeroScenario scenario = new HeroScenario("crystal_maiden.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit victim = scenario.Rig.AddUnit(2, Vector3.right);
            FakeResourceOwner resources = new FakeResourceOwner(114f);
            scenario.Add(caster, Maiden + "crystal_nova", 1, resources);
            Assert.That(scenario.Engine.CastAbilityOnPosition(caster, Maiden + "crystal_nova", victim.Position).FailureReason, Is.EqualTo(CastFailureReason.NotEnoughMana));
            scenario.Rig.Tick(1f);
            Assert.That(victim.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
            Assert.That(resources.SpendCount, Is.Zero);
        }

        [TestCase(1, 25f, 1.5f, 100f)]
        [TestCase(2, 30f, 2f, 150f)]
        [TestCase(3, 35f, 2.5f, 210f)]
        [TestCase(4, 40f, 3f, 280f)]
        public void Frostbite_RootsAndDisarmsWhileDamageTicksThroughExactExpiry(int level, float firstTick, float duration, float totalDamage)
        {
            HeroScenario scenario = new HeroScenario("crystal_maiden.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit victim = scenario.Rig.AddUnit(2, Vector3.right, UnitType.Hero);
            scenario.Add(caster, Maiden + "frostbite", level);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Maiden + "frostbite", victim).Success, Is.True);
            scenario.Rig.Tick(0.3f);
            Assert.That(victim.DamageTaken, Is.EqualTo(firstTick));
            Assert.That(scenario.Engine.HasState(victim, UnitState.Rooted), Is.True);
            Assert.That(scenario.Engine.HasState(victim, UnitState.Disarmed), Is.True);
            Assert.That(scenario.Engine.BeginAttack(new DamageInfo { Attacker = victim, Victim = caster, Amount = 10f, DamageType = DamageType.Physical }), Is.Null);
            scenario.Rig.Tick(duration);
            Assert.That(victim.DamageTaken, Is.EqualTo(totalDamage));
            Assert.That(scenario.Engine.HasState(victim, UnitState.Rooted | UnitState.Disarmed), Is.False);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void Frostbite_HeroRuleRejectsCreepsAndPurgeStopsFutureTicks()
        {
            HeroScenario scenario = new HeroScenario("crystal_maiden.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit creep = scenario.Rig.AddUnit(2, Vector3.right, UnitType.Creep);
            FakeUnit victim = scenario.Rig.AddUnit(2, Vector3.right, UnitType.Hero);
            scenario.Add(caster, Maiden + "frostbite", 1);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Maiden + "frostbite", creep).FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Maiden + "frostbite", victim).Success, Is.True);
            scenario.Rig.Tick(0.3f);
            Assert.That(scenario.Engine.Purge(victim, false, true, true), Is.EqualTo(1));
            scenario.Rig.Tick(3f);
            Assert.That(victim.DamageTaken, Is.EqualTo(25f));
            Assert.That(scenario.Engine.HasState(victim, UnitState.Rooted | UnitState.Disarmed), Is.False);
        }

        [Test]
        public void EarthSpike_PiercesWithDamageAndStunButExcludesFriendsAndImmuneTargets()
        {
            HeroScenario scenario = new HeroScenario("lion.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit near = scenario.Rig.AddUnit(2, new Vector3(100f, 0f, 0f), UnitType.Hero);
            FakeUnit far = scenario.Rig.AddUnit(2, new Vector3(700f, 0f, 0f));
            FakeUnit ally = scenario.Rig.AddUnit(1, new Vector3(200f, 0f, 0f));
            FakeUnit immune = scenario.Rig.AddUnit(2, new Vector3(300f, 0f, 0f));
            immune.IsMagicImmune = true;
            scenario.Add(caster, Lion + "earth_spike", 3);
            Assert.That(scenario.Engine.CastAbilityOnPosition(caster, Lion + "earth_spike", new Vector3(800f, 0f, 0f)).Success, Is.True);
            scenario.Rig.Tick(0.3f);
            scenario.Rig.Tick(0.5f);
            Assert.That(near.DamageTaken, Is.EqualTo(200f));
            Assert.That(far.DamageTaken, Is.EqualTo(200f));
            Assert.That(ally.DamageTaken + immune.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.HasState(near, UnitState.Stunned), Is.True);
            Assert.That(scenario.Engine.HasState(far, UnitState.Stunned), Is.True);
            Assert.That(scenario.Find(near, Lion + "earth_spike_stun").Duration, Is.EqualTo(2f));
            scenario.Rig.Tick(2f);
            Assert.That(scenario.Engine.HasState(near, UnitState.Stunned), Is.False);
        }

        [Test]
        public void Hex_BlocksAttackAndCastingUntilExpiryAndResistsOrdinaryPurge()
        {
            HeroScenario scenario = new HeroScenario("lion.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit victim = scenario.Rig.AddUnit(2, Vector3.right, UnitType.Hero);
            scenario.Add(caster, Lion + "hex", 2);
            scenario.Add(victim, Lion + "hex", 1);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Lion + "hex", victim).Success, Is.True);
            Assert.That(scenario.Engine.HasState(victim, UnitState.Hexed), Is.True);
            Assert.That(scenario.Engine.HasState(victim, UnitState.NoUnitCollision), Is.False);
            Assert.That(scenario.Property(victim, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(-80f));
            Assert.That(scenario.Engine.CastAbilityOnTarget(victim, Lion + "hex", caster).Success, Is.False);
            Assert.That(scenario.Engine.BeginAttack(new DamageInfo { Attacker = victim, Victim = caster, Amount = 10f, DamageType = DamageType.Physical }), Is.Null);
            Assert.That(scenario.Engine.Purge(victim, false, true, true), Is.Zero);
            scenario.Rig.Tick(2.5f);
            Assert.That(scenario.Engine.HasState(victim, UnitState.Hexed), Is.False);
            Assert.That(scenario.Engine.CastAbilityOnTarget(victim, Lion + "hex", caster).Success, Is.True);
        }

        [TestCase(1, 5f, 8f)]
        [TestCase(2, 8f, 12f)]
        [TestCase(3, 11f, 16f)]
        [TestCase(4, 14f, 20f)]
        public void Warcry_GrantsRankedArmorAndMovementToSelfAndAlliesInCastArea(int level, float armor, float movement)
        {
            HeroScenario scenario = new HeroScenario("sven.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit ally = scenario.Rig.AddUnit(1, new Vector3(700f, 0f, 0f));
            FakeUnit outside = scenario.Rig.AddUnit(1, new Vector3(701f, 0f, 0f));
            FakeUnit enemy = scenario.Rig.AddUnit(2, Vector3.right);
            FakeResourceOwner resources = new FakeResourceOwner(100f);
            scenario.Add(caster, Sven + "warcry", level, resources);
            Assert.That(scenario.Engine.CastAbility(caster, Sven + "warcry").Success, Is.True);
            Assert.That(scenario.Property(caster, ModifierProperty.ArmorBonus), Is.EqualTo(armor));
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.EqualTo(armor));
            Assert.That(scenario.Property(ally, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(movement));
            Assert.That(scenario.Property(outside, ModifierProperty.ArmorBonus), Is.Zero);
            Assert.That(scenario.Property(enemy, ModifierProperty.ArmorBonus), Is.Zero);
            Assert.That(resources.Mana, Is.EqualTo(60f));
            ally.Position = new Vector3(900f, 0f, 0f);
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.EqualTo(armor));
            scenario.Rig.Tick(6f);
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void Warcry_ArmorReducesAppliedPhysicalDamageAndExpiryRestoresNormalDamage()
        {
            HeroScenario scenario = new HeroScenario("sven.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            FakeUnit enemy = scenario.Rig.AddUnit(2, Vector3.right);
            scenario.Add(caster, Sven + "warcry", 4);
            Assert.That(scenario.Engine.CastAbility(caster, Sven + "warcry").Success, Is.True);
            DamageResult protectedHit = scenario.Engine.ApplyDamage(new DamageInfo { Attacker = enemy, Victim = caster, Amount = 100f, DamageType = DamageType.Physical });
            Assert.That(protectedHit.AppliedAmount, Is.EqualTo(100f / 1.84f).Within(0.001f));
            Assert.That(caster.DamageTaken, Is.EqualTo(protectedHit.AppliedAmount));
            scenario.Rig.Tick(6f);
            DamageResult normalHit = scenario.Engine.ApplyDamage(new DamageInfo { Attacker = enemy, Victim = caster, Amount = 100f, DamageType = DamageType.Physical });
            Assert.That(normalHit.AppliedAmount, Is.EqualTo(100f));
        }

        [Test]
        public void Warcry_AbilityUpgradeUpdatesLiveConfiguredPropertiesWithoutAddingStacks()
        {
            HeroScenario scenario = new HeroScenario("sven.json");
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero, UnitType.Hero);
            Game.Ability.Ability ability = scenario.Add(caster, Sven + "warcry", 1);
            Assert.That(scenario.Engine.CastAbility(caster, Sven + "warcry").Success, Is.True);
            ability.SetLevel(4);
            Assert.That(scenario.Property(caster, ModifierProperty.ArmorBonus), Is.EqualTo(14f));
            Assert.That(scenario.Find(caster, Sven + "warcry_buff").StackCount, Is.EqualTo(1));
        }

        private sealed class HeroScenario
        {
            public HeroScenario(params string[] files)
            {
                foreach (string file in files) Registry.LoadProvider(Provider(file));
                string issues = string.Join("; ", Registry.Validation.Issues.Select(issue => issue.Code + ": " + issue.Message));
                Assert.That(Registry.IsValid, Is.True, issues);
                Assert.That(Registry.ApplyTo(Engine), Is.True, issues);
            }

            public AbilityTestRig Rig { get; } = new AbilityTestRig();
            public AbilitySystem Engine => Rig.Engine;
            public AbilityDefinitionRegistry Registry { get; } = new AbilityDefinitionRegistry();
            public Game.Ability.Ability Add(IUnit owner, string name, int level, IResourceOwner resources = null)
            {
                Game.Ability.Ability ability = Engine.AddAbility(owner, name, level, resources ?? new FakeResourceOwner(10000f));
                Assert.That(ability, Is.Not.Null, name);
                return ability;
            }
            public float Property(IUnit unit, ModifierProperty property) => Engine.GetModifierProperty(unit, property, null);
            public Modifier Find(IUnit unit, string name) => Engine.Modifiers.Single(modifier => modifier.Parent.EntityId == unit.EntityId && modifier.Name == name);

            private static IAbilityDefinitionProvider Provider(string file)
            {
                foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
                {
                    for (DirectoryInfo directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                    {
                        string path = Path.Combine(directory.FullName, "Data", "AbilityJsonSources", "Heroes", "Dota2", file);
                        if (File.Exists(path)) return new JsonAbilityDefinitionProvider(File.ReadAllText(path), "Data/AbilityJsonSources/Heroes/Dota2/" + file);
                    }
                }
                throw new FileNotFoundException("Missing hero JSON: Data/AbilityJsonSources/Heroes/Dota2/" + file);
            }
        }
    }
}
