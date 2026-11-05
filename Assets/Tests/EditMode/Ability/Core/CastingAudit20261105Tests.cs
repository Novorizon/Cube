using System;
using System.Collections.Generic;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class CastingAudit20261105Tests
    {
        [Test]
        public void LongFrameCastCompletion_AdvancesNewCooldownOnlyByPostCommitTime()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.cooldown", CastPoint = LevelValue.Constant(0.2f), Cooldown = LevelValue.Constant(1f) });

            Assert.That(rig.Engine.CastAbility(caster, ability.Definition.Name).Success, Is.True);
            rig.Tick(0.4f);

            Assert.That(ability.CooldownRemaining, Is.EqualTo(0.8f).Within(0.00001f));
        }

        [Test]
        public void LongFrameCastCompletion_AdvancesNewChargeRecoveryOnlyByPostCommitTime()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.charge", CastPoint = LevelValue.Constant(0.2f), Charges = new ChargeDefinition { MaxCharges = 2, RestoreTime = 1f, UsesCooldown = false } });

            Assert.That(rig.Engine.CastAbility(caster, ability.Definition.Name).Success, Is.True);
            rig.Tick(0.4f);

            Assert.That(ability.Charges, Is.EqualTo(1));
            Assert.That(ability.ChargeRestoreRemaining, Is.EqualTo(0.8f).Within(0.00001f));
        }

        [Test]
        public void CooldownFinishedCallback_NewCastDoesNotAgeItsNewChargeTimerByOldFrame()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.recharge_callback", Charges = new ChargeDefinition { MaxCharges = 1, RestoreTime = 1f, UsesCooldown = false } });
            ability.StartCooldown(0.1f);
            bool recast = false;
            rig.Engine.EventRaised += value =>
            {
                if (value.EventType != RuntimeEventType.CooldownFinished || recast) return;
                recast = true;
                Assert.That(rig.Engine.CastAbility(caster, ability.Definition.Name).Success, Is.True);
            };

            rig.Tick(0.5f);

            Assert.That(recast, Is.True);
            Assert.That(ability.Charges, Is.Zero);
            Assert.That(ability.ChargeRestoreRemaining, Is.EqualTo(1f).Within(0.00001f));
        }

        [Test]
        public void CastStartedObserverFailure_InterruptsCastBeforeAnyLaterSpellOrPayment()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CountingScript script = new CountingScript();
            FakeResourceOwner resources = new FakeResourceOwner(50f);
            AbilityDefinition definition = new AbilityDefinition { Name = "audit.started_throw", CastPoint = LevelValue.Constant(0.2f), ManaCost = LevelValue.Constant(10f) };
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition, resources);
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.CastStarted) throw new InvalidOperationException("observer failure"); };
            Exception failure = null;

            try { rig.Engine.CastAbility(caster, definition.Name); }
            catch (Exception error) { failure = error; }
            rig.Tick(1f);

            Assert.That(failure, Is.Not.Null);
            Assert.That(script.SpellCount, Is.Zero);
            Assert.That(resources.SpendCount, Is.Zero);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        [Test]
        public void Stop_InterruptsCurrentCastEvenWhenQueuedCancellationObserverThrows()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CountingScript script = new CountingScript();
            AbilityDefinition first = new AbilityDefinition { Name = "audit.stop_current", CastPoint = LevelValue.Constant(0.5f) };
            AbilityDefinition second = new AbilityDefinition { Name = "audit.stop_queued" };
            rig.Engine.RegisterAbilityScript(first.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, first);
            rig.RegisterAndAddAbility(caster, second);
            rig.Engine.CastAbility(caster, first.Name);
            CastResult queued = rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = second.Name, Queue = true });
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.CastInterrupted && value.CastId == queued.CastId) throw new InvalidOperationException("queued observer failure"); };
            Exception failure = null;

            try { rig.Engine.Stop(caster); }
            catch (Exception error) { failure = error; }
            rig.Tick(1f);

            Assert.That(failure, Is.Not.Null);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(script.SpellCount, Is.Zero);
            Assert.That(rig.Engine.GetQueuedOrders(caster).Count, Is.Zero);
        }

        [Test]
        public void RemovedTargetCastFailure_PreservesTheOriginalCastId()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AbilityDefinition definition = new AbilityDefinition { Name = "audit.removed_target", Behavior = AbilityBehavior.UnitTarget, TargetTeam = TargetTeam.Enemy, CastRange = LevelValue.Constant(10f), CastPoint = LevelValue.Constant(1f) };
            rig.RegisterAndAddAbility(caster, definition);
            List<RuntimeEvent> events = new List<RuntimeEvent>();
            rig.Engine.EventRaised += events.Add;
            CastResult cast = rig.Engine.CastAbilityOnTarget(caster, definition.Name, target);

            rig.Engine.RemoveUnit(target);

            RuntimeEvent failure = events.Find(value => value.EventType == RuntimeEventType.CastFailed && value.Ability?.Definition.Name == definition.Name);
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.CastId, Is.EqualTo(cast.CastId));
            Assert.That(failure.FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
        }

        [Test]
        public void ChargeCatchUp_StopsAtExecutionBudgetInsteadOfContinuingSilentRestores()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.charge_budget", Charges = new ChargeDefinition { MaxCharges = 1000, StartFull = false, RestoreTime = 0.000001f, UsesCooldown = false } });
            rig.Engine.MaxExecutionOperations = 16;
            int aborts = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ExecutionAborted) aborts++; };

            rig.Tick(1f);

            Assert.That(aborts, Is.EqualTo(1));
            Assert.That(ability.Charges < 1000, Is.True);
            Assert.That(ability.Charges <= rig.Engine.MaxExecutionOperations, Is.True);
        }

        [Test]
        public void ExistingChargeTimer_IsNotAgedTwiceWhenAnotherCastCompletesWithinFrame()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.existing_charge", CastPoint = LevelValue.Constant(0.2f), Charges = new ChargeDefinition { MaxCharges = 3, RestoreTime = 1f, UsesCooldown = false } });
            rig.Engine.CastAbility(caster, ability.Definition.Name);
            rig.Tick(0.2f);
            Assert.That(ability.Charges, Is.EqualTo(2));
            Assert.That(ability.ChargeRestoreRemaining, Is.EqualTo(1f));

            rig.Engine.CastAbility(caster, ability.Definition.Name);
            rig.Tick(0.4f);

            Assert.That(ability.Charges, Is.EqualTo(1));
            Assert.That(ability.ChargeRestoreRemaining, Is.EqualTo(0.6f).Within(0.00001f));
        }

        [Test]
        public void ChargeObserverFreshReset_IsNotOverwrittenByCatchUpRemainder()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.charge_reset", Charges = new ChargeDefinition { MaxCharges = 3, StartFull = false, RestoreTime = 1f, UsesCooldown = false } });
            bool reset = false;
            rig.Engine.EventRaised += value =>
            {
                if (value.EventType != RuntimeEventType.ChargeChanged || reset) return;
                reset = true;
                ability.RestoreFullCharges();
                ability.SetCharges(0);
            };

            rig.Tick(1.5f);

            Assert.That(ability.Charges, Is.Zero);
            Assert.That(ability.ChargeRestoreRemaining, Is.EqualTo(1f));
        }

        [Test]
        public void ChargeCatchUp_PreservesOrdinaryMultipleRecoveryAndExactFullBoundary()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.charge_catchup", Charges = new ChargeDefinition { MaxCharges = 3, StartFull = false, RestoreTime = 0.2f, UsesCooldown = false } });
            int changes = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ChargeChanged) changes++; };

            rig.Tick(0.6f);

            Assert.That(ability.Charges, Is.EqualTo(3));
            Assert.That(ability.ChargeRestoreRemaining, Is.Zero);
            Assert.That(changes, Is.EqualTo(3));
        }

        [Test]
        public void Stop_DeliversEveryQueuedCancellationAfterOneObserverFailure()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.cancel_all", CastPoint = LevelValue.Constant(1f) });
            CastResult first = rig.Engine.CastAbility(caster, ability.Definition.Name);
            CastResult second = rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = ability.Definition.Name, Queue = true });
            CastResult third = rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = ability.Definition.Name, Queue = true });
            List<RuntimeEvent> events = new List<RuntimeEvent>();
            rig.Engine.EventRaised += events.Add;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.CastInterrupted && value.CastId == second.CastId) throw new InvalidOperationException("queued failure"); };

            try { rig.Engine.Stop(caster); }
            catch (AggregateException) { }

            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(events.FindAll(value => value.EventType == RuntimeEventType.CastInterrupted && value.CastId == first.CastId).Count, Is.EqualTo(1));
            Assert.That(events.FindAll(value => value.EventType == RuntimeEventType.CastInterrupted && value.CastId == second.CastId).Count, Is.EqualTo(1));
            Assert.That(events.FindAll(value => value.EventType == RuntimeEventType.CastInterrupted && value.CastId == third.CastId).Count, Is.EqualTo(1));
        }

        [Test]
        public void CastStartedFailureAfterObserverRecast_DoesNotCancelTheReplacementCast()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CountingScript script = new CountingScript();
            AbilityDefinition definition = new AbilityDefinition { Name = "audit.started_recast", CastPoint = LevelValue.Constant(0.2f) };
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);
            bool replaced = false;
            rig.Engine.EventRaised += value =>
            {
                if (value.EventType != RuntimeEventType.CastStarted || replaced) return;
                replaced = true;
                rig.Engine.Stop(caster);
                Assert.That(rig.Engine.CastAbility(caster, definition.Name).Success, Is.True);
                throw new InvalidOperationException("old cast observer failed after replacement");
            };
            try { rig.Engine.CastAbility(caster, definition.Name); }
            catch (AggregateException) { }

            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Casting));
            rig.Tick(0.2f);

            Assert.That(script.SpellCount, Is.EqualTo(1));
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        [Test]
        public void CastFilterFailureAtCommit_InterruptsInsteadOfLeavingADeferredSpell()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            ValidationFailureScript script = new ValidationFailureScript();
            AbilityDefinition definition = new AbilityDefinition { Name = "audit.filter_throw", CastPoint = LevelValue.Constant(0.2f) };
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);
            rig.Engine.CastAbility(caster, definition.Name);
            Exception failure = null;

            try { rig.Tick(0.2f); }
            catch (Exception error) { failure = error; }
            rig.Tick(1f);

            Assert.That(failure, Is.Not.Null);
            Assert.That(script.SpellCount, Is.Zero);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        [Test]
        public void PhaseParameterGetterFailure_DoesNotLeaveCastingState()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            ValidationFailureScript script = new ValidationFailureScript { FailCastPoint = true };
            AbilityDefinition definition = new AbilityDefinition { Name = "audit.phase_getter_throw", CastPoint = LevelValue.Constant(0.2f) };
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);
            Exception failure = null;

            try { rig.Engine.CastAbility(caster, definition.Name); }
            catch (Exception error) { failure = error; }

            Assert.That(failure, Is.Not.Null);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        [Test]
        public void Untraining_CleansIntrinsicAndNotifiesUpgradeDespiteInterruptionHookFailure()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            UntrainingScript script = new UntrainingScript();
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "audit.intrinsic", Duration = -1f });
            AbilityDefinition definition = new AbilityDefinition { Name = "audit.untrain", CastPoint = LevelValue.Constant(1f), IntrinsicModifierName = "audit.intrinsic" };
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);
            rig.Engine.CastAbility(caster, definition.Name);
            Exception failure = null;

            try { ability.SetLevel(0); }
            catch (Exception error) { failure = error; }

            Assert.That(failure, Is.Not.Null);
            Assert.That(ability.Level, Is.Zero);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
            Assert.That(script.Upgrades, Is.EqualTo(1));
        }

        [Test]
        public void EarlierAbilityCallback_NewCastOnLaterExistingAbilityWaitsForNextFrame()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability first = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.first_tick" });
            CountingScript script = new CountingScript();
            AbilityDefinition definition = new AbilityDefinition { Name = "audit.later_tick", CastPoint = LevelValue.Constant(0.5f) };
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability later = rig.RegisterAndAddAbility(caster, definition);
            first.StartCooldown(0.1f);
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.CooldownFinished && ReferenceEquals(value.Ability, first)) Assert.That(rig.Engine.CastAbility(caster, definition.Name).Success, Is.True); };

            rig.Tick(1f);

            Assert.That(script.SpellCount, Is.Zero);
            Assert.That(later.Phase, Is.EqualTo(AbilityPhase.Casting));
            rig.Tick(0.5f);
            Assert.That(script.SpellCount, Is.EqualTo(1));
        }

        [Test]
        public void EarlierAbilityCallback_NewTimersOnLaterAbilityWaitThenAdvanceNormally()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability first = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.first_timer_tick" });
            Game.Ability.Ability later = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "audit.later_timers", Charges = new ChargeDefinition { MaxCharges = 1, RestoreTime = 1f, UsesCooldown = false } });
            first.StartCooldown(0.1f);
            rig.Engine.EventRaised += value =>
            {
                if (value.EventType != RuntimeEventType.CooldownFinished || !ReferenceEquals(value.Ability, first)) return;
                later.StartCooldown(1f);
                later.SetCharges(0);
            };

            rig.Tick(0.5f);

            Assert.That(later.CooldownRemaining, Is.EqualTo(1f));
            Assert.That(later.ChargeRestoreRemaining, Is.EqualTo(1f));
            rig.Tick(0.25f);
            Assert.That(later.CooldownRemaining, Is.EqualTo(0.75f));
            Assert.That(later.ChargeRestoreRemaining, Is.EqualTo(0.75f));
            Assert.That(later.Charges, Is.Zero);
        }


        private sealed class CountingScript : AbilityScript
        {
            public int SpellCount;
            public override void OnSpellStart(CastContext context) { SpellCount++; }
        }

        private sealed class ValidationFailureScript : AbilityScript
        {
            public bool FailCastPoint;
            public int SpellCount;
            private bool threw;
            public override CastResult CastFilter(CastContext context)
            {
                if (!FailCastPoint && Ability.Phase == AbilityPhase.Casting && !threw) { threw = true; throw new InvalidOperationException("commit filter failure"); }
                return CastResult.Ok();
            }
            public override float GetCastPoint()
            {
                if (FailCastPoint && Ability.Phase == AbilityPhase.Casting) throw new InvalidOperationException("phase getter failure");
                return base.GetCastPoint();
            }
            public override void OnSpellStart(CastContext context) { SpellCount++; }
        }

        private sealed class UntrainingScript : AbilityScript
        {
            public int Upgrades;
            public override void OnAbilityPhaseInterrupted() { throw new InvalidOperationException("untraining interruption failure"); }
            public override void OnUpgrade() { Upgrades++; }
        }
    }
}
