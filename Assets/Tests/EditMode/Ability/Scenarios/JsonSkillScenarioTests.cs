using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    /// <summary>Executable skill examples: the tests load the same JSON that designers can author.</summary>
    [TestFixture]
    [Category("Ability")]
    [Category("Scenarios")]
    public sealed class JsonSkillScenarioTests
    {
        private const string Prefix = "validation.";

        [Test]
        public void SharedJsonExamples_PassStrictPublicationAndResolveStableIds()
        {
            Scenario scenario = new Scenario();

            Assert.That(scenario.Registry.Abilities.Count, Is.EqualTo(9));
            Assert.That(scenario.Registry.Modifiers.Count, Is.EqualTo(6));
            Assert.That(scenario.Registry.Projectiles.Count, Is.EqualTo(2));
            Assert.That(scenario.Registry.TryResolveAbilityName(Prefix + "arc_bolt", out string name), Is.True);
            Assert.That(name, Is.EqualTo(Prefix + "arc_bolt"));
            Assert.That(scenario.Registry.TryGetAbilityOrigin(name, out AbilityDefinitionOrigin origin), Is.True);
            Assert.That(origin.SourceType, Is.EqualTo(AbilityDefinitionSourceType.Json));
            Assert.That(origin.SourcePath, Does.Contain("composed_skills.json"));
        }

        [Test]
        public void TwoProjectileSkills_OnOneCaster_DealOnlyTheirOwnDamageOnTheirOwnArrival()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit arcTarget = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            FakeUnit iceTarget = scenario.Rig.AddUnit(2, new Vector3(8f, 0f, 0f));
            scenario.Add(caster, "arc_bolt", 2);
            scenario.Add(caster, "ice_bolt");

            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Prefix + "arc_bolt", arcTarget).Success, Is.True);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Prefix + "ice_bolt", iceTarget).Success, Is.True);
            Assert.That(arcTarget.DamageTaken + iceTarget.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Projectiles.Count, Is.EqualTo(2));
            scenario.Rig.Tick(0.4f);
            Assert.That(arcTarget.DamageTaken + iceTarget.DamageTaken, Is.Zero);

            scenario.Rig.Tick(0.4f);
            Assert.That(iceTarget.DamageTaken, Is.EqualTo(40f));
            Assert.That(arcTarget.DamageTaken, Is.Zero);
            Assert.That(scenario.Property(iceTarget, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(-30f));
            Assert.That(scenario.Property(arcTarget, ModifierProperty.MoveSpeedBonusPercent), Is.Zero);

            scenario.Rig.Tick(0.2f);
            Assert.That(arcTarget.DamageTaken, Is.EqualTo(120f));
            Assert.That(iceTarget.DamageTaken, Is.EqualTo(40f));
            Assert.That(arcTarget.DamageResults.Count, Is.EqualTo(1));
            Assert.That(arcTarget.DamageResults[0].Ability.Definition.Name, Is.EqualTo(Prefix + "arc_bolt"));
            Assert.That(iceTarget.DamageResults.Count, Is.EqualTo(1));
            Assert.That(iceTarget.DamageResults[0].Ability.Definition.Name, Is.EqualTo(Prefix + "ice_bolt"));
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
            scenario.Rig.Tick(3f);
            Assert.That(arcTarget.DamageTaken + iceTarget.DamageTaken, Is.EqualTo(160f));
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void TrackingBolt_RevalidatesTargetImmunityAtArrival()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            scenario.Add(caster, "arc_bolt");
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "arc_bolt", victim);
            scenario.Rig.Tick(0.5f);
            victim.IsInvulnerable = true;

            scenario.Rig.Tick(0.5f);

            Assert.That(victim.DamageTaken, Is.Zero);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void LaunchedBolt_SurvivesAbilityRemovalWithItsPublishedHitActions()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            scenario.Add(caster, "arc_bolt");
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "arc_bolt", victim);
            scenario.Registry.Abilities[Prefix + "arc_bolt"].Definition.OnProjectileHitActions.Clear();

            Assert.That(scenario.Engine.RemoveAbility(caster, Prefix + "arc_bolt"), Is.True);
            scenario.Rig.Tick(1f);

            Assert.That(victim.DamageTaken, Is.EqualTo(90f));
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RemovingAProjectileOrClearingDuringItsFirstHitAction_StopsRemainingHitActions(bool clearRuntime)
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            scenario.Add(caster, "ice_bolt");
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "ice_bolt", victim);
            Projectile projectile = scenario.Engine.Projectiles.Single();
            scenario.Engine.EventRaised += e =>
            {
                if (e.EventType != RuntimeEventType.DamageApplied) return;
                if (clearRuntime) scenario.Engine.ClearRuntime();
                else scenario.Engine.RemoveProjectile(projectile);
            };

            scenario.Rig.Tick(1f);

            Assert.That(victim.DamageTaken, Is.EqualTo(40f));
            Assert.That(scenario.Property(victim, ModifierProperty.MoveSpeedBonusPercent), Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void PiercingWave_SweepsMultipleEnemiesOnceAndIgnoresAlliesAndOffAxisUnits()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit near = scenario.Rig.AddUnit(2, new Vector3(3f, 0f, 0f));
            FakeUnit far = scenario.Rig.AddUnit(2, new Vector3(7f, 0f, 0f));
            FakeUnit ally = scenario.Rig.AddUnit(1, new Vector3(5f, 0f, 0f));
            FakeUnit offAxis = scenario.Rig.AddUnit(2, new Vector3(7f, 0f, 2f));
            scenario.Add(caster, "piercing_wave");
            scenario.Engine.CastAbilityOnPosition(caster, Prefix + "piercing_wave", new Vector3(20f, 0f, 0f));
            Assert.That(near.DamageTaken + far.DamageTaken, Is.Zero);

            scenario.Rig.Tick(1f);

            Assert.That(near.DamageTaken, Is.EqualTo(35f));
            Assert.That(far.DamageTaken, Is.EqualTo(35f));
            Assert.That(ally.DamageTaken + offAxis.DamageTaken, Is.Zero);
            scenario.Rig.Tick(1f);
            Assert.That(near.DamageResults.Count, Is.EqualTo(1));
            Assert.That(far.DamageResults.Count, Is.EqualTo(1));
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void FrostNova_WaitsForCastPointAndAppliesDamageAndExpiringSlowToValidAreaTargets()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit center = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            FakeUnit edge = scenario.Rig.AddUnit(2, new Vector3(12.9f, 0f, 0f));
            FakeUnit outside = scenario.Rig.AddUnit(2, new Vector3(13.1f, 0f, 0f));
            FakeUnit ally = scenario.Rig.AddUnit(1, center.Position);
            FakeUnit immune = scenario.Rig.AddUnit(2, center.Position);
            immune.IsMagicImmune = true;
            scenario.Add(caster, "frost_nova");

            Assert.That(scenario.Engine.CastAbilityOnPosition(caster, Prefix + "frost_nova", center.Position).Success, Is.True);
            scenario.Rig.Tick(0.24f);
            Assert.That(center.DamageTaken + edge.DamageTaken, Is.Zero);
            scenario.Rig.Tick(0.01f);
            Assert.That(center.DamageTaken, Is.EqualTo(60f));
            Assert.That(edge.DamageTaken, Is.EqualTo(60f));
            Assert.That(outside.DamageTaken + ally.DamageTaken + immune.DamageTaken, Is.Zero);
            Assert.That(scenario.Property(center, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(-30f));
            Assert.That(scenario.Property(edge, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(-30f));
            Assert.That(scenario.Property(immune, ModifierProperty.MoveSpeedBonusPercent), Is.Zero);

            scenario.Rig.Tick(2f);

            Assert.That(scenario.Property(center, ModifierProperty.MoveSpeedBonusPercent), Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [TestCase(0.25f)]
        [TestCase(3f)]
        public void Venom_DealsThreeTicksIncludingExpiryAndNeverDamagesAfterExpiry(float frameTime)
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = scenario.Rig.AddUnit(2, Vector3.right);
            scenario.Add(caster, "venom", 2);
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "venom", victim);
            Assert.That(victim.DamageTaken, Is.Zero);

            for (float elapsed = 0f; elapsed < 3f; elapsed += frameTime) scenario.Rig.Tick(frameTime);

            Assert.That(victim.DamageTaken, Is.EqualTo(45f));
            Assert.That(victim.DamageResults.Count, Is.EqualTo(3));
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
            scenario.Rig.Tick(10f);
            Assert.That(victim.DamageTaken, Is.EqualTo(45f));
        }

        [Test]
        public void Venom_RefreshesOneInstanceWithoutResettingTickPhaseOrAddingStacks()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = scenario.Rig.AddUnit(2, Vector3.right);
            scenario.Add(caster, "venom");
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "venom", victim);
            Modifier poison = scenario.Find(victim, "poison");
            scenario.Rig.Tick(1.5f);
            Assert.That(victim.DamageTaken, Is.EqualTo(10f));

            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "venom", victim);

            Assert.That(scenario.Find(victim, "poison"), Is.SameAs(poison));
            Assert.That(scenario.Engine.Modifiers.Count, Is.EqualTo(1));
            Assert.That(poison.StackCount, Is.EqualTo(1));
            Assert.That(poison.RemainingTime, Is.EqualTo(3f));
            scenario.Rig.Tick(0.5f);
            Assert.That(victim.DamageTaken, Is.EqualTo(20f));
            scenario.Rig.Tick(2.5f);
            Assert.That(victim.DamageTaken, Is.EqualTo(40f));
            Assert.That(poison.IsDestroyed, Is.True);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void PurgingVenomAndSlow_StopsFutureTicksAndReleasesTheirProperties()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = scenario.Rig.AddUnit(2, Vector3.right);
            scenario.Add(caster, "venom");
            scenario.Add(caster, "frost_nova");
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "venom", victim);
            scenario.Engine.CastAbilityOnPosition(caster, Prefix + "frost_nova", victim.Position);
            scenario.Rig.Tick(0.25f);
            float damageBeforePurge = victim.DamageTaken;

            Assert.That(scenario.Engine.Purge(victim, false, true, true), Is.EqualTo(2));
            scenario.Rig.Tick(5f);

            Assert.That(victim.DamageTaken, Is.EqualTo(damageBeforePurge));
            Assert.That(scenario.Property(victim, ModifierProperty.MoveSpeedBonusPercent), Is.Zero);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void Mend_ClampsToMissingHealthAndPublishesOnlyEffectiveHealing()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            ScenarioUnit ally = scenario.Precise(100, 1, Vector3.right, 100f, 95f);
            ScenarioUnit enemy = scenario.Precise(101, 2, Vector3.right, 100f, 50f);
            scenario.Add(caster, "mend");
            float appliedHealing = 0f;
            scenario.Engine.EventRaised += e => { if (e.EventType == RuntimeEventType.HealApplied) appliedHealing += e.Value; };

            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Prefix + "mend", ally).Success, Is.True);
            Assert.That(ally.CurrentHealth, Is.EqualTo(100f));
            Assert.That(ally.HealingReceived, Is.EqualTo(5f));
            Assert.That(appliedHealing, Is.EqualTo(5f));
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Prefix + "mend", ally).Success, Is.True);
            Assert.That(ally.HealingReceived, Is.EqualTo(5f));
            Assert.That(appliedHealing, Is.EqualTo(5f));
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, Prefix + "mend", enemy).FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
            Assert.That(enemy.CurrentHealth, Is.EqualTo(50f));
        }

        [Test]
        public void SpellLeech_UsesActualDamageAndIgnoresOtherSourcesReflectionAndHealthRemoval()
        {
            Scenario scenario = new Scenario();
            ScenarioUnit owner = scenario.Precise(100, 1, Vector3.zero, 100f, 50f);
            ScenarioUnit ally = scenario.Precise(101, 1, Vector3.zero, 100f, 50f);
            ScenarioUnit overkillVictim = scenario.Precise(102, 2, Vector3.right, 100f, 10f);
            ScenarioUnit victim = scenario.Precise(103, 2, Vector3.right, 100f, 100f);
            scenario.Add(owner, "spell_leech");

            DamageResult hit = scenario.Engine.ApplyDamage(new DamageInfo { Attacker = owner, Victim = overkillVictim, Amount = 90f, DamageType = DamageType.Pure });

            Assert.That(hit.AppliedAmount, Is.EqualTo(10f));
            Assert.That(owner.HealingReceived, Is.EqualTo(2.5f));
            Assert.That(owner.CurrentHealth, Is.EqualTo(52.5f));
            scenario.Engine.ApplyDamage(new DamageInfo { Attacker = ally, Victim = victim, Amount = 20f, DamageType = DamageType.Pure });
            scenario.Engine.ApplyDamage(new DamageInfo { Attacker = owner, Victim = victim, Amount = 20f, DamageType = DamageType.Pure, Flags = DamageFlags.Reflected });
            scenario.Engine.ApplyDamage(new DamageInfo { Attacker = owner, Victim = victim, Amount = 20f, DamageType = DamageType.Pure, Category = DamageCategory.HpLoss });
            Assert.That(owner.HealingReceived, Is.EqualTo(2.5f));
            Assert.That(ally.HealingReceived, Is.Zero);
        }

        [Test]
        public void ReciprocalRetaliation_ReflectsActualDamageOnceWithoutUsingTheExecutionFuse()
        {
            Scenario scenario = new Scenario();
            ScenarioUnit attacker = scenario.Precise(100, 1, Vector3.zero, 1000f, 1000f);
            ScenarioUnit defender = scenario.Precise(101, 2, Vector3.right, 1000f, 1000f);
            scenario.Add(attacker, "retaliation");
            scenario.Add(defender, "retaliation");
            // Publication detaches the flag filter as well as action lists from authoring data.
            scenario.Registry.Modifiers[Prefix + "retaliation_passive"].Definition.TriggerExcludedDamageFlags = DamageFlags.None;
            int aborts = 0;
            int damageEvents = 0;
            scenario.Engine.EventRaised += e => { if (e.EventType == RuntimeEventType.ExecutionAborted) aborts++; if (e.EventType == RuntimeEventType.DamageApplied) damageEvents++; };

            scenario.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = defender, Amount = 100f, DamageType = DamageType.Pure });

            Assert.That(defender.DamageTaken, Is.EqualTo(100f));
            Assert.That(attacker.DamageTaken, Is.EqualTo(20f));
            Assert.That(damageEvents, Is.EqualTo(2));
            Assert.That(aborts, Is.Zero);
            scenario.Engine.ApplyDamage(new DamageInfo { Attacker = defender, Victim = attacker, Amount = 50f, DamageType = DamageType.Pure, Flags = DamageFlags.Reflected });
            Assert.That(defender.DamageTaken, Is.EqualTo(100f));
            Assert.That(attacker.DamageTaken, Is.EqualTo(70f));
            Assert.That(damageEvents, Is.EqualTo(3));
            Assert.That(aborts, Is.Zero);
        }

        [Test]
        public void GuardianAura_TracksEnteringAndLeavingAlliesWithoutScanDrivenStackGrowth()
        {
            Scenario scenario = new Scenario();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit ally = scenario.Rig.AddUnit(1, new Vector3(2f, 0f, 0f));
            FakeUnit incoming = scenario.Rig.AddUnit(1, new Vector3(8f, 0f, 0f));
            FakeUnit enemy = scenario.Rig.AddUnit(2, Vector3.right);
            scenario.Add(caster, "guardian_aura");

            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.EqualTo(4f));
            Assert.That(scenario.Property(incoming, ModifierProperty.ArmorBonus), Is.Zero);
            Assert.That(scenario.Property(enemy, ModifierProperty.ArmorBonus), Is.Zero);
            for (int i = 0; i < 10; i++) scenario.Rig.Tick(0.1f);
            Assert.That(scenario.Find(ally, "guardian_buff").StackCount, Is.EqualTo(1));
            Assert.That(scenario.Property(ally, ModifierProperty.MoveSpeedBonusPercent), Is.EqualTo(12f));

            incoming.Position = Vector3.right;
            ally.Position = new Vector3(8f, 0f, 0f);
            scenario.Rig.Tick(0.25f);
            Assert.That(scenario.Property(incoming, ModifierProperty.ArmorBonus), Is.EqualTo(4f));
            scenario.Rig.Tick(0.5f);
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.Zero);
            Assert.That(scenario.Find(incoming, "guardian_buff").StackCount, Is.EqualTo(1));
            Assert.That(scenario.Engine.RemoveAbility(caster, Prefix + "guardian_aura"), Is.True);
            scenario.Rig.Tick(0.5f);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
            List<PresentationHandleInfo> handles = new List<PresentationHandleInfo>();
            scenario.Rig.Presentation.GetActivePresentationHandles(handles);
            Assert.That(handles, Is.Empty);
        }

        [Test]
        public void JsonAndExcelAndCodeDefinitions_ReferenceEachOtherThroughOneValidatedGraph()
        {
            Scenario scenario = new Scenario(false);
            AbilityDefinitionBundle excel = new AbilityDefinitionBundle();
            excel.Modifiers.Add(new ModifierDefinitionRegistration
            {
                Definition = new ModifierDefinition { Name = "modifier_shared_root", States = UnitState.Rooted, IsDebuff = true },
                Origin = Origin(AbilityDefinitionSourceType.Excel, "shared.root", "Data/Excel/skill_modifier.xlsx")
            });
            AbilityDefinitionBundle code = new AbilityDefinitionBundle();
            ModifierDefinition guard = new ModifierDefinition { Name = "modifier_shared_guard" };
            guard.Properties[ModifierProperty.ArmorBonus] = 5f;
            code.Modifiers.Add(new ModifierDefinitionRegistration { Definition = guard, Origin = Origin(AbilityDefinitionSourceType.Code, "shared.guard", "BuiltInModifierLibrary") });
            AbilityDefinition disarm = new AbilityDefinition { Name = "shared_code_disarm", Behavior = AbilityBehavior.UnitTarget, CastRange = LevelValue.Constant(20f) };
            disarm.Actions.Add(new ActionDefinition { ActionType = ActionType.AddModifier, Target = ActionTarget.PrimaryTarget, ModifierName = "validation.mixed.public_disarm" });
            code.Abilities.Add(new AbilityDefinitionRegistration { Definition = disarm, Origin = Origin(AbilityDefinitionSourceType.Code, "shared.disarm", "BuiltInAbilityLibrary") });
            scenario.Registry.LoadProviders(new IAbilityDefinitionProvider[]
            {
                FixtureProvider("mixed_sources.json"),
                new FixedProvider(AbilityDefinitionSourceType.Excel, excel),
                new FixedProvider(AbilityDefinitionSourceType.Code, code)
            });
            scenario.Publish();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit enemy = scenario.Rig.AddUnit(2, Vector3.right);
            scenario.Engine.AddAbility(caster, "validation.mixed.excel_root");
            scenario.Engine.AddAbility(caster, "validation.mixed.code_guard");
            scenario.Engine.AddAbility(caster, "shared_code_disarm");

            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, "validation.mixed.excel_root", enemy).Success, Is.True);
            Assert.That(scenario.Engine.HasState(enemy, UnitState.Rooted), Is.True);
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, "validation.mixed.code_guard", caster).Success, Is.True);
            Assert.That(scenario.Property(caster, ModifierProperty.ArmorBonus), Is.EqualTo(5f));
            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, "shared_code_disarm", enemy).Success, Is.True);
            Assert.That(scenario.Engine.HasState(enemy, UnitState.Disarmed), Is.True);
            scenario.Rig.Tick(0.75f);
            Assert.That(scenario.Engine.HasState(enemy, UnitState.Disarmed), Is.False);
            Assert.That(scenario.Engine.HasState(enemy, UnitState.Rooted), Is.True);
            scenario.Rig.Tick(1.25f);
            Assert.That(scenario.Engine.HasState(enemy, UnitState.Rooted), Is.False);
            Assert.That(scenario.Property(caster, ModifierProperty.ArmorBonus), Is.Zero);
        }

        [Test]
        public void ExternalChainLightningScript_RegistersOutsideThePackageAndUsesJsonTuning()
        {
            Scenario scenario = new Scenario(false);
            scenario.Registry.LoadProvider(FixtureProvider("external_chain_lightning.json"));
            Assert.That(scenario.Registry.IsValid, Is.True);
            Assert.That(scenario.Registry.ApplyTo(scenario.Engine), Is.False, "Declared custom scripts must be present before publication.");
            const string name = "validation.external.chain_lightning";
            scenario.Engine.RegisterAbilityScript(name, () => new ExternalChainLightningScript());
            scenario.Publish();
            FakeUnit caster = scenario.Rig.AddUnit(1, Vector3.zero);
            FakeUnit first = scenario.Rig.AddUnit(2, new Vector3(3f, 0f, 0f));
            FakeUnit second = scenario.Rig.AddUnit(2, new Vector3(6f, 0f, 0f));
            FakeUnit third = scenario.Rig.AddUnit(2, new Vector3(9f, 0f, 0f));
            FakeUnit fourth = scenario.Rig.AddUnit(2, new Vector3(12f, 0f, 0f));
            FakeUnit ally = scenario.Rig.AddUnit(1, new Vector3(4f, 0f, 0f));
            FakeUnit immune = scenario.Rig.AddUnit(2, new Vector3(5f, 0f, 0f));
            immune.IsMagicImmune = true;
            scenario.Engine.AddAbility(caster, name);

            Assert.That(scenario.Engine.CastAbilityOnTarget(caster, name, first).Success, Is.True);

            Assert.That(first.DamageTaken, Is.EqualTo(70f));
            Assert.That(second.DamageTaken, Is.EqualTo(35f));
            Assert.That(third.DamageTaken, Is.EqualTo(17.5f));
            Assert.That(fourth.DamageTaken + ally.DamageTaken + immune.DamageTaken, Is.Zero);
            Assert.That(first.DamageResults.Count, Is.EqualTo(1));
            Assert.That(second.DamageResults.Count, Is.EqualTo(1));
            Assert.That(third.DamageResults.Count, Is.EqualTo(1));
        }

        [Test]
        public void ConcurrentSkillSet_ClearRuntimeStopsDamageProcsProjectilesAndPersistentEffects()
        {
            Scenario scenario = new Scenario();
            ScenarioUnit caster = scenario.Precise(100, 1, Vector3.zero, 1000f, 500f);
            FakeUnit victim = scenario.Rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            FakeUnit ally = scenario.Rig.AddUnit(1, Vector3.right);
            foreach (string skill in new[] { "spell_leech", "retaliation", "guardian_aura", "arc_bolt", "venom", "mend" }) scenario.Add(caster, skill);
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "arc_bolt", victim);
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "venom", victim);
            scenario.Engine.CastAbilityOnTarget(caster, Prefix + "mend", caster);
            scenario.Rig.Tick(0.5f);
            Assert.That(victim.DamageTaken, Is.Zero);
            Assert.That(caster.HealingReceived, Is.EqualTo(30f));
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.EqualTo(4f));
            Assert.That(scenario.Engine.Projectiles.Count, Is.EqualTo(1));

            scenario.Engine.ClearRuntime();
            scenario.Rig.Tick(10f);

            Assert.That(victim.DamageTaken, Is.Zero);
            Assert.That(caster.HealingReceived, Is.EqualTo(30f));
            Assert.That(scenario.Engine.Projectiles, Is.Empty);
            Assert.That(scenario.Engine.Modifiers, Is.Empty);
            Assert.That(scenario.Engine.GetAbilities(caster), Is.Empty);
            Assert.That(scenario.Property(ally, ModifierProperty.ArmorBonus), Is.Zero);
            List<PresentationHandleInfo> handles = new List<PresentationHandleInfo>();
            scenario.Rig.Presentation.GetActivePresentationHandles(handles);
            Assert.That(handles, Is.Empty);
        }

        private static IAbilityDefinitionProvider FixtureProvider(string fileName)
        {
            string path = FindFixture(fileName);
            return new JsonAbilityDefinitionProvider(File.ReadAllText(path), "Data/AbilityJsonSources/Validation/" + fileName);
        }

        private static string FindFixture(string fileName)
        {
            foreach (string start in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                for (DirectoryInfo directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
                {
                    string path = Path.Combine(directory.FullName, "Data", "AbilityJsonSources", "Validation", fileName);
                    if (File.Exists(path)) return path;
                }
            }
            throw new FileNotFoundException("Run these EditMode tests from a Cube checkout containing Data/AbilityJsonSources/Validation/" + fileName);
        }

        private static AbilityDefinitionOrigin Origin(AbilityDefinitionSourceType type, string stableId, string path)
        {
            return new AbilityDefinitionOrigin { SourceType = type, ProviderName = type.ToString(), SourcePath = path, StableId = stableId };
        }

        private sealed class FixedProvider : IAbilityDefinitionProvider
        {
            private readonly AbilityDefinitionBundle bundle;
            public FixedProvider(AbilityDefinitionSourceType sourceType, AbilityDefinitionBundle bundle) { SourceType = sourceType; this.bundle = bundle; }
            public string ProviderName => SourceType.ToString();
            public AbilityDefinitionSourceType SourceType { get; }
            public AbilityDefinitionBundle Load() => bundle;
        }

        // This assembly is a consumer of Ability.Core, as a game's own custom script assembly is.
        private sealed class ExternalChainLightningScript : AbilityScript
        {
            public override void OnSpellStart(CastContext context)
            {
                HashSet<int> visited = new HashSet<int>();
                List<IUnit> candidates = new List<IUnit>();
                IUnit target = context.Target;
                float damage = GetSpecialValue("damage");
                int maximumHits = Math.Max(1, Math.Min(32, (int)GetSpecialValue("maximum_hits")));
                TargetQuery query = new TargetQuery { Engine = Engine, Caster = Caster, Team = TargetTeam.Enemy };
                for (int hit = 0; hit < maximumHits && target != null && Engine.IsRuntimeAvailable && !Ability.IsRemoved; hit++)
                {
                    visited.Add(target.EntityId);
                    Vector3 impact = target.Position;
                    Engine.ApplyDamage(new DamageInfo { Attacker = Caster, Victim = target, Ability = Ability, Amount = damage, DamageType = DamageType.Magical });
                    if (!Engine.IsRuntimeAvailable || Ability.IsRemoved) return;
                    candidates.Clear();
                    Engine.FindUnits(impact, GetSpecialValue("bounce_radius"), query, candidates);
                    target = candidates.Where(unit => !visited.Contains(unit.EntityId)).OrderBy(unit => (unit.Position - impact).sqrMagnitude).ThenBy(unit => unit.EntityId).FirstOrDefault();
                    damage *= GetSpecialValue("bounce_multiplier");
                }
            }
        }

        private sealed class Scenario
        {
            public Scenario(bool loadExamples = true)
            {
                if (!loadExamples) return;
                Registry.LoadProvider(FixtureProvider("composed_skills.json"));
                Publish();
            }

            public AbilityTestRig Rig { get; } = new AbilityTestRig();
            public AbilityDefinitionRegistry Registry { get; } = new AbilityDefinitionRegistry();
            public AbilitySystem Engine => Rig.Engine;
            public void Publish()
            {
                string issues = string.Join("; ", Registry.Validation.Issues.Select(issue => issue.Code + ": " + issue.Message));
                Assert.That(Registry.IsValid, Is.True, issues);
                Assert.That(Registry.ApplyTo(Engine), Is.True, issues);
            }

            public Game.Ability.Ability Add(IUnit owner, string shortName, int level = 1)
            {
                Game.Ability.Ability ability = Engine.AddAbility(owner, Prefix + shortName, level);
                Assert.That(ability, Is.Not.Null, shortName);
                return ability;
            }

            public float Property(IUnit owner, ModifierProperty property) => Engine.GetModifierProperty(owner, property, null);
            public Modifier Find(IUnit parent, string shortName) => Engine.Modifiers.SingleOrDefault(modifier => modifier.Parent.EntityId == parent.EntityId && modifier.Name == Prefix + shortName);
            public ScenarioUnit Precise(int id, int team, Vector3 position, float maxHealth, float health)
            {
                ScenarioUnit unit = new ScenarioUnit(id, team, position, maxHealth, health);
                Rig.World.AddUnit(unit);
                return unit;
            }
        }

        private sealed class ScenarioUnit : ICombatUnit
        {
            public ScenarioUnit(int id, int team, Vector3 position, float maxHealth, float health)
            {
                EntityId = id; TeamId = team; Position = position; MaxHealth = maxHealth; CurrentHealth = health;
            }

            public int EntityId { get; }
            public int TeamId { get; }
            public UnitType UnitType => UnitType.Basic;
            public bool IsAlive => CurrentHealth > 0f;
            public bool IsMagicImmune => false;
            public bool IsInvulnerable => false;
            public Vector3 Position { get; }
            public float CurrentHealth { get; private set; }
            public float MaxHealth { get; }
            public float BaseArmor => 0f;
            public float BaseMagicResistancePercent => 0f;
            public float BasePhysicalDamageBlock => 0f;
            public float DamageTaken { get; private set; }
            public float HealingReceived { get; private set; }
            public bool IsVisibleToTeam(int teamId) => true;
            public void ApplyDamage(DamageResult result) { ApplyFinalDamage(result); }
            public void Heal(HealInfo info) { ApplyFinalHeal(info); }
            public DamageApplicationResult ApplyFinalDamage(DamageResult result)
            {
                float before = CurrentHealth;
                CurrentHealth = Math.Max(0f, before - result.FinalAmount);
                float applied = before - CurrentHealth;
                DamageTaken += applied;
                return new DamageApplicationResult(applied, before > 0f && CurrentHealth <= 0f);
            }

            public HealApplicationResult ApplyFinalHeal(HealInfo info)
            {
                float before = CurrentHealth;
                CurrentHealth = Math.Min(MaxHealth, before + info.Amount);
                float applied = CurrentHealth - before;
                HealingReceived += applied;
                return new HealApplicationResult(applied);
            }
        }
    }
}
