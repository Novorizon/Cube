using System;
using System.Collections.Generic;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class CombatPipelineTests
    {
        [TestCase(DamageType.Physical, DamageCategory.Spell, 150f)]
        [TestCase(DamageType.Magical, DamageCategory.Spell, 150f)]
        [TestCase(DamageType.Pure, DamageCategory.Spell, 150f)]
        [TestCase(DamageType.Magical, DamageCategory.Attack, 100f)]
        [TestCase(DamageType.Pure, DamageCategory.HpLoss, 100f)]
        public void SpellAmplification_FollowsOriginInsteadOfDamageType(DamageType type, DamageCategory category, float expected)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            ModifierDefinition amplification = new ModifierDefinition { Name = "spell_amplification", Duration = -1f };
            amplification.Properties[ModifierProperty.SpellAmplifyPercent] = 50f;
            rig.Engine.RegisterModifierDefinition(amplification);
            rig.Engine.AddModifier(attacker, attacker, amplification.Name, -1f);

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 100f, DamageType = type, Category = category });

            Assert.That(result.AppliedAmount, Is.EqualTo(expected).Within(0.001f));
        }

        [Test]
        public void DamageCalculated_CannotRedirectDamageOrRewriteItsTypeAndAttribution()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            FakeUnit immuneBystander = rig.AddUnit(2, Vector3.left);
            immuneBystander.IsInvulnerable = true;
            Listen(rig, attacker, "redirect_attempt", value =>
            {
                if (value.EventType != ModifierEventType.DamageCalculated) return;
                value.DamageInfo.Victim = immuneBystander;
                value.DamageInfo.Attacker = immuneBystander;
                value.DamageResult.Victim = immuneBystander;
                value.DamageResult.DamageType = DamageType.Pure;
                value.DamageResult.Flags = DamageFlags.IgnoreInvulnerable;
                value.DamageResult.FinalAmount = 30f;
            });

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Physical });

            Assert.That(victim.DamageTaken, Is.EqualTo(30f));
            Assert.That(immuneBystander.DamageTaken, Is.Zero);
            Assert.That(result.Attacker, Is.SameAs(attacker));
            Assert.That(result.Victim, Is.SameAs(victim));
            Assert.That(result.DamageType, Is.EqualTo(DamageType.Physical));
            Assert.That(result.Flags, Is.EqualTo(DamageFlags.None));
        }

        [Test]
        public void DamageCalculated_RevalidatesImmunityBeforeApplyingAndHonorsBlocking()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            int notifications = 0;
            Listen(rig, attacker, "immunity_during_calculation", value =>
            {
                if (value.EventType == ModifierEventType.DamageCalculated) victim.IsInvulnerable = true;
                if (value.EventType == ModifierEventType.DamageTaken || value.EventType == ModifierEventType.DamageDealt) notifications++;
            });

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f });

            Assert.That(result.Blocked, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void DamageNotifications_CannotChangeCommittedFactsOrLaterObservers()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            float laterObservedAmount = 0f;
            float runtimeObservedAmount = 0f;
            Listen(rig, attacker, "mutate_damage_notification", value =>
            {
                if (value.EventType != ModifierEventType.DamageTaken) return;
                value.DamageResult.FinalAmount = 999f;
                value.DamageResult.AppliedAmount = 999f;
                value.DamageResult.Victim = attacker;
                value.DamageInfo.Amount = 999f;
            });
            Listen(rig, attacker, "observe_damage_notification", value =>
            {
                if (value.EventType == ModifierEventType.DamageTaken) laterObservedAmount = value.DamageResult.AppliedAmount;
            });
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.DamageApplied) runtimeObservedAmount = value.Value; };

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f });

            Assert.That(result.FinalAmount, Is.EqualTo(20f));
            Assert.That(result.AppliedAmount, Is.EqualTo(20f));
            Assert.That(result.Victim, Is.SameAs(victim));
            Assert.That(laterObservedAmount, Is.EqualTo(20f));
            Assert.That(runtimeObservedAmount, Is.EqualTo(20f));
        }

        [Test]
        public void CalculatedBlock_DoesNotApplyDamageOrPublishSuccessfulDamage()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            int damageNotifications = 0;
            Listen(rig, attacker, "calculated_block", value =>
            {
                if (value.EventType == ModifierEventType.DamageCalculated) { value.DamageResult.Blocked = true; value.DamageResult.BlockReason = "Shield"; }
                if (value.EventType == ModifierEventType.DamageTaken || value.EventType == ModifierEventType.DamageDealt) damageNotifications++;
            });

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f });

            Assert.That(result.BlockReason, Is.EqualTo("Shield"));
            Assert.That(result.AppliedAmount, Is.Zero);
            Assert.That(victim.DamageTaken, Is.Zero);
            Assert.That(damageNotifications, Is.Zero);
        }

        [TestCase(DamageCategory.HpLoss, DamageFlags.None)]
        [TestCase(DamageCategory.Spell, DamageFlags.HpLoss)]
        public void HealthRemoval_BypassesDamageProcsButStillReportsDeath(DamageCategory category, DamageFlags flags)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            PreciseUnit victim = AddPreciseUnit(rig, 100, 10f);
            int damageProcs = 0;
            int deaths = 0;
            int appliedEvents = 0;
            Listen(rig, attacker, "health_removal_observer", value =>
            {
                if (value.EventType == ModifierEventType.DamageCalculated || value.EventType == ModifierEventType.DamageTaken || value.EventType == ModifierEventType.DamageDealt)
                {
                    damageProcs++;
                    if (value.DamageResult != null) value.DamageResult.Blocked = true;
                }
                if (value.EventType == ModifierEventType.Death) deaths++;
            });
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.DamageApplied) appliedEvents++; };

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, Category = category, Flags = flags });

            Assert.That(result.AppliedAmount, Is.EqualTo(10f));
            Assert.That(result.Category, Is.EqualTo(DamageCategory.HpLoss));
            Assert.That(damageProcs, Is.Zero);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(appliedEvents, Is.EqualTo(1));
        }

        [Test]
        public void NonLethalDamage_LeavesOneHealthThroughThePreciseBoundary()
        {
            AbilityTestRig rig = new AbilityTestRig();
            PreciseUnit victim = AddPreciseUnit(rig, 100, 10f);

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Victim = victim, Amount = 100f, DamageType = DamageType.Pure, Flags = DamageFlags.NonLethal });

            Assert.That(victim.CurrentHealth, Is.EqualTo(1f));
            Assert.That(result.AppliedAmount, Is.EqualTo(9f));
            Assert.That(result.Killed, Is.False);
        }

        [Test]
        public void NonLethalDamage_RejectsAdaptersThatCannotEnforceTheHealthFloor()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit victim = rig.AddUnit(2, Vector3.zero);

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Victim = victim, Amount = 2000f, Flags = DamageFlags.NonLethal });

            Assert.That(result.Blocked, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
        }

        [TestCase(DamageType.Physical, DamageFlags.None, DamageCategory.Spell, 50f)]
        [TestCase(DamageType.Physical, DamageFlags.IgnoreBlock, DamageCategory.Spell, 62.5f)]
        [TestCase(DamageType.Magical, DamageFlags.None, DamageCategory.Spell, 75f)]
        [TestCase(DamageType.Pure, DamageFlags.None, DamageCategory.Spell, 100f)]
        [TestCase(DamageType.Physical, DamageFlags.None, DamageCategory.HpLoss, 100f)]
        public void Defenses_ApplyExactlyOnceAndRespectTypeAndFlags(DamageType type, DamageFlags flags, DamageCategory category, float expected)
        {
            AbilityTestRig rig = new AbilityTestRig();
            PreciseUnit victim = AddPreciseUnit(rig, 100, 1000f);
            victim.BaseArmor = 10f;
            victim.BasePhysicalDamageBlock = 20f;
            victim.BaseMagicResistancePercent = 25f;

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Victim = victim, Amount = 100f, DamageType = type, Flags = flags, Category = category });

            Assert.That(result.AppliedAmount, Is.EqualTo(expected).Within(0.001f));
            Assert.That(victim.CurrentHealth, Is.EqualTo(1000f - expected).Within(0.001f));
        }

        [Test]
        public void OverkillAndOverheal_ReportActualHealthChanges()
        {
            AbilityTestRig rig = new AbilityTestRig();
            PreciseUnit victim = AddPreciseUnit(rig, 100, 100f);
            victim.SetHealth(10f);
            DamageResult damage = rig.Engine.ApplyDamage(new DamageInfo { Victim = victim, Amount = 30f, DamageType = DamageType.Pure });
            Assert.That(damage.AppliedAmount, Is.EqualTo(10f));
            Assert.That(damage.FinalAmount, Is.EqualTo(10f));
            Assert.That(damage.Killed, Is.True);

            victim.SetHealth(95f);
            rig.Engine.NotifyRespawn(victim);
            float publishedHeal = 0f;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.HealApplied) publishedHeal = value.Value; };
            HealResult healed = rig.Engine.HealWithResult(new HealInfo { Target = victim, Amount = 30f });
            Assert.That(healed.RequestedAmount, Is.EqualTo(30f));
            Assert.That(healed.AppliedAmount, Is.EqualTo(5f));
            Assert.That(publishedHeal, Is.EqualTo(5f));
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f));
        }

        [TestCase(-10f)]
        [TestCase(0f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidHealing_DoesNotApplyOrPublishSuccessfulHealing(float amount)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit target = rig.AddUnit(1, Vector3.zero);
            int notifications = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.HealApplied) notifications++; };

            HealResult result = rig.Engine.HealWithResult(new HealInfo { Target = target, Amount = amount });

            Assert.That(result.Applied, Is.False);
            Assert.That(target.HealingReceived, Is.Zero);
            Assert.That(notifications, Is.Zero);
        }

        [Test]
        public void AttackRecord_ResolvesOnceAndPreservesItsIdentityAcrossCallbacks()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            List<ModifierEventType> events = new List<ModifierEventType>();
            List<long> attackIds = new List<long>();
            Listen(rig, attacker, "attack_observer", value =>
            {
                if (value.EventType != ModifierEventType.AttackStart && value.EventType != ModifierEventType.DamageTaken && value.EventType != ModifierEventType.AttackLanded) return;
                events.Add(value.EventType);
                attackIds.Add(value.AttackId);
            });

            AttackRecord attack = rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Physical });
            DamageResult first = rig.Engine.ResolveAttack(attack);
            first.AppliedAmount = 999f;
            DamageResult repeated = rig.Engine.ResolveAttack(attack);

            Assert.That(victim.DamageTaken, Is.EqualTo(20f));
            Assert.That(repeated.AppliedAmount, Is.EqualTo(20f));
            Assert.That(events, Is.EqualTo(new[] { ModifierEventType.AttackStart, ModifierEventType.DamageTaken, ModifierEventType.AttackLanded }));
            foreach (long attackId in attackIds) Assert.That(attackId, Is.EqualTo(attack.AttackId));
            Assert.That(attack.AttackId > 0, Is.True);
        }

        [Test]
        public void LaunchedAttack_SurvivesAttackerDeathButCannotResolveAfterBattleReset()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            AttackRecord launched = rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f });
            attacker.IsAlive = false;
            rig.Engine.ResolveAttack(launched);
            Assert.That(victim.DamageTaken, Is.EqualTo(20f));

            attacker.IsAlive = true;
            AttackRecord stale = rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f });
            rig.Engine.ClearRuntime();
            DamageResult result = rig.Engine.ResolveAttack(stale);
            Assert.That(result.Blocked, Is.True);
            Assert.That(stale.IsCancelled, Is.True);
            Assert.That(victim.DamageTaken, Is.EqualTo(20f));
        }

        [Test]
        public void AttackResolution_ReentrantCallbackCannotApplyTheSameRecordAgain()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            AttackRecord attack = rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f });
            DamageResult recursive = null;
            Listen(rig, attacker, "reentrant_attack", value =>
            {
                if (value.EventType == ModifierEventType.DamageCalculated) recursive = rig.Engine.ResolveAttack(attack);
            });

            rig.Engine.ResolveAttack(attack);

            Assert.That(recursive.Blocked, Is.True);
            Assert.That(victim.DamageTaken, Is.EqualTo(20f));
            Assert.That(attack.IsResolved, Is.True);
        }

        [Test]
        public void RemovedUnit_CancelsItsPendingAttackBeforeItsIdIsReused()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            AttackRecord attack = rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f });

            rig.Engine.RemoveUnit(victim);
            rig.Engine.RegisterUnit(new FakeUnit(victim.EntityId, victim.TeamId, victim.Position));
            DamageResult result = rig.Engine.ResolveAttack(attack);

            Assert.That(result.Blocked, Is.True);
            Assert.That(attack.IsCancelled, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
        }

        [Test]
        public void Death_IsPublishedOnceAndOnlyRemovesEffectsOnTheDeadParent()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            PreciseUnit victim = AddPreciseUnit(rig, 100, 10f);
            FakeUnit other = rig.AddUnit(2, Vector3.right);
            int deaths = 0;
            int respawns = 0;
            Listen(rig, attacker, "death_observer", value =>
            {
                if (value.EventType == ModifierEventType.Death) deaths++;
                if (value.EventType == ModifierEventType.Respawn) respawns++;
            });
            ModifierDefinition removable = new ModifierDefinition { Name = "death_removable", Duration = -1f, RemoveOnDeath = true };
            rig.Engine.RegisterModifierDefinition(removable);
            Modifier onVictim = rig.Engine.AddModifier(victim, victim, removable.Name, -1f);
            Modifier sourcedByVictim = rig.Engine.AddModifier(victim, other, removable.Name, -1f);

            rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 100f });
            rig.Engine.NotifyDeath(victim, attacker);
            Assert.That(deaths, Is.EqualTo(1));
            Assert.That(onVictim.IsDestroyed, Is.True);
            Assert.That(sourcedByVictim.IsDestroyed, Is.False);

            victim.SetHealth(10f);
            rig.Engine.NotifyRespawn(victim);
            rig.Engine.NotifyRespawn(victim);
            Assert.That(respawns, Is.EqualTo(1));
            rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 100f });
            Assert.That(deaths, Is.EqualTo(2));
        }

        [Test]
        public void ThrowingDamageNotification_DoesNotSkipLethalDeathCleanup()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            PreciseUnit victim = AddPreciseUnit(rig, 100, 10f);
            int deaths = 0;
            Listen(rig, attacker, "throwing_damage_listener", value =>
            {
                if (value.EventType == ModifierEventType.DamageTaken) throw new InvalidOperationException("Damage listener failed.");
                if (value.EventType == ModifierEventType.Death) deaths++;
            });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "death_cleanup_after_error", Duration = -1f });
            Modifier modifier = rig.Engine.AddModifier(victim, victim, "death_cleanup_after_error", -1f);
            bool failed = false;

            try { rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f }); }
            catch (AggregateException) { failed = true; }
            rig.Engine.NotifyDeath(victim, attacker);

            Assert.That(failed, Is.True);
            Assert.That(victim.IsAlive, Is.False);
            Assert.That(modifier.IsDestroyed, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingDeathCleanup_DoesNotLeaveOtherDeathModifiersOrRepeatDestruction()
        {
            AbilityTestRig rig = new AbilityTestRig();
            PreciseUnit victim = AddPreciseUnit(rig, 100, 10f);
            int destroyCalls = 0;
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "throwing_death_cleanup", Duration = -1f });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "remaining_death_cleanup", Duration = -1f });
            rig.Engine.RegisterModifierScript("throwing_death_cleanup", () => new ObserverScript(value => { }, () => { destroyCalls++; throw new InvalidOperationException("Destroy failed."); }));
            Modifier first = rig.Engine.AddModifier(victim, victim, "throwing_death_cleanup", -1f);
            Modifier second = rig.Engine.AddModifier(victim, victim, "remaining_death_cleanup", -1f);
            victim.SetHealth(0f);
            bool failed = false;

            try { rig.Engine.NotifyDeath(victim); }
            catch (AggregateException) { failed = true; }
            rig.Engine.NotifyDeath(victim);

            Assert.That(failed, Is.True);
            Assert.That(first.IsDestroyed, Is.True);
            Assert.That(second.IsDestroyed, Is.True);
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
            Assert.That(destroyCalls, Is.EqualTo(1));
        }

        [Test]
        public void RespawnInsideDeathCallback_RestoresIntrinsicAfterDeathCleanup()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit observer = rig.AddUnit(1, Vector3.zero);
            PreciseUnit victim = AddPreciseUnit(rig, 100, 10f);
            ModifierDefinition intrinsic = new ModifierDefinition { Name = "respawn_intrinsic", Duration = -1f, RemoveOnDeath = true };
            rig.Engine.RegisterModifierDefinition(intrinsic);
            rig.Engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "respawn_passive", Behavior = AbilityBehavior.Passive, IntrinsicModifierName = intrinsic.Name });
            rig.Engine.AddAbility(victim, "respawn_passive");
            Modifier original = rig.Engine.Modifiers[0];
            Listen(rig, observer, "resurrection", value =>
            {
                if (value.EventType != ModifierEventType.Death || value.Target != victim) return;
                victim.SetHealth(10f);
                rig.Engine.NotifyRespawn(victim);
            });

            rig.Engine.ApplyDamage(new DamageInfo { Attacker = observer, Victim = victim, Amount = 100f });

            Assert.That(original.IsDestroyed, Is.True);
            int livingIntrinsics = 0;
            foreach (Modifier modifier in rig.Engine.Modifiers) if (modifier.Name == intrinsic.Name && !modifier.IsDestroyed) livingIntrinsics++;
            Assert.That(livingIntrinsics, Is.EqualTo(1));
        }

        [Test]
        public void RuntimeClearDuringCalculation_CancelsApplicationAndFurtherNotifications()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            int committedNotifications = 0;
            Listen(rig, attacker, "end_battle_in_calculation", value =>
            {
                if (value.EventType == ModifierEventType.DamageCalculated) rig.Engine.ClearRuntime();
            });
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.DamageApplied) committedNotifications++; };

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f });

            Assert.That(result.Blocked, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
            Assert.That(committedNotifications, Is.Zero);
        }

        [Test]
        public void SynchronousBattleSettlement_ReturnsAppliedFactWithoutRunningOldBattleCallbacks()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit observer = rig.AddUnit(1, Vector3.zero);
            PreciseUnit victim = AddPreciseUnit(rig, 100, 10f);
            int notifications = 0;
            Listen(rig, observer, "old_battle_listener", value =>
            {
                if (value.EventType == ModifierEventType.DamageTaken || value.EventType == ModifierEventType.Death) notifications++;
            });
            victim.AfterDamageApplied = rig.Engine.ClearRuntime;

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = observer, Victim = victim, Amount = 20f });

            Assert.That(result.AppliedAmount, Is.EqualTo(10f));
            Assert.That(result.Killed, Is.True);
            Assert.That(notifications, Is.Zero);
        }

        private static void Listen(AbilityTestRig rig, IUnit owner, string name, Action<ModifierEvent> callback)
        {
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = name, Duration = -1f, RemoveOnDeath = false });
            rig.Engine.RegisterModifierScript(name, () => new ObserverScript(callback));
            rig.Engine.AddModifier(owner, owner, name, -1f);
        }

        private static PreciseUnit AddPreciseUnit(AbilityTestRig rig, int id, float maxHealth)
        {
            PreciseUnit unit = new PreciseUnit(id, maxHealth);
            rig.World.AddUnit(unit);
            rig.Engine.RegisterUnit(unit);
            return unit;
        }

        private sealed class ObserverScript : ModifierScript
        {
            private readonly Action<ModifierEvent> callback;
            private readonly Action onDestroy;
            internal ObserverScript(Action<ModifierEvent> callback, Action onDestroy = null) { this.callback = callback; this.onDestroy = onDestroy; }
            public override void OnEvent(ModifierEvent value) { callback(value); }
            public override void OnDestroy() { onDestroy?.Invoke(); }
        }

        private sealed class PreciseUnit : ICombatUnit
        {
            internal PreciseUnit(int id, float maxHealth) { EntityId = id; MaxHealth = maxHealth; CurrentHealth = maxHealth; }
            public int EntityId { get; }
            public int TeamId => 2;
            public UnitType UnitType => UnitType.Basic;
            public bool IsAlive => CurrentHealth > 0f;
            public bool IsMagicImmune => false;
            public bool IsInvulnerable => false;
            public Vector3 Position => Vector3.right;
            public float CurrentHealth { get; private set; }
            public float MaxHealth { get; }
            public float BaseArmor { get; set; }
            public float BaseMagicResistancePercent { get; set; }
            public float BasePhysicalDamageBlock { get; set; }
            internal Action AfterDamageApplied;
            public bool IsVisibleToTeam(int teamId) { return true; }
            internal void SetHealth(float amount) { CurrentHealth = amount; }
            public void ApplyDamage(DamageResult result) { ApplyFinalDamage(result); }
            public void Heal(HealInfo info) { ApplyFinalHeal(info); }
            public DamageApplicationResult ApplyFinalDamage(DamageResult result)
            {
                float before = CurrentHealth;
                CurrentHealth = Math.Max(0f, before - result.FinalAmount);
                AfterDamageApplied?.Invoke();
                return new DamageApplicationResult(before - CurrentHealth, before > 0f && CurrentHealth <= 0f);
            }
            public HealApplicationResult ApplyFinalHeal(HealInfo info)
            {
                float before = CurrentHealth;
                CurrentHealth = Math.Min(MaxHealth, before + info.Amount);
                return new HealApplicationResult(CurrentHealth - before);
            }
        }
    }
}
