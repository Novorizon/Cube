using System;
using System.Collections.Generic;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class AbilityCastingCorrectnessTests
    {
        [TestCase(AbilityBehavior.NoTarget)]
        [TestCase(AbilityBehavior.PointTarget)]
        public void UnsupportedPrimaryTarget_IsExcludedFromContext(AbilityBehavior behavior)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit injected = rig.AddUnit(1, new Vector3(100f, 0f, 0f));
            CaptureScript script = new CaptureScript();
            AbilityDefinition definition = Definition("target_shape", behavior);
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            rig.RegisterAndAddAbility(caster, definition);

            CastResult result = rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = definition.Name, Target = injected, HasTargetPosition = true, TargetPosition = Vector3.right });

            Assert.That(result.Success, Is.True);
            Assert.That(script.Context.Target, Is.Null);
            Assert.That(script.Context.Targets.Count, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void OptionalUnitTargetAlone_SupportsValidatedUnitAndUntargetedCast(bool hasUnit)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            CaptureScript script = new CaptureScript();
            AbilityDefinition definition = Definition("optional_unit", AbilityBehavior.OptionalUnitTarget);
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            rig.RegisterAndAddAbility(caster, definition);

            CastResult result = rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = definition.Name, Target = hasUnit ? target : null });

            Assert.That(result.Success, Is.True);
            Assert.That(script.Context.Target, Is.SameAs(hasUnit ? target : null));
            Assert.That(script.Context.Targets.Count, Is.EqualTo(hasUnit ? 1 : 0));
            Assert.That(script.Context.TargetPosition.x, Is.EqualTo(hasUnit ? target.Position.x : caster.Position.x));
            target.TeamId = caster.TeamId;
            Assert.That(rig.Engine.CastAbilityOnTarget(caster, definition.Name, target).FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
            target.TeamId = 2;
            target.Position = new Vector3(100f, 0f, 0f);
            Assert.That(rig.Engine.CastAbilityOnTarget(caster, definition.Name, target).FailureReason, Is.EqualTo(CastFailureReason.OutOfRange));
        }

        [Test]
        public void AcceptedOrder_IsUnaffectedByCallerAndOrderListenerMutation()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            FakeUnit injected = rig.AddUnit(1, Vector3.zero);
            CaptureScript script = new CaptureScript();
            AbilityDefinition definition = Definition("snapshot", AbilityBehavior.UnitTarget);
            definition.CastPoint = LevelValue.Constant(0.2f);
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            rig.RegisterAndAddAbility(caster, definition);
            ModifierDefinition listener = new ModifierDefinition { Name = "order_listener", Duration = -1f };
            rig.Engine.RegisterModifierDefinition(listener);
            rig.Engine.RegisterModifierScript(listener.Name, () => new OrderMutationScript { Replacement = injected });
            rig.Engine.AddModifier(caster, caster, null, listener.Name);
            CastOrder order = new CastOrder { Caster = caster, AbilityName = definition.Name, Target = target };

            Assert.That(rig.Engine.IssueOrder(order).Success, Is.True);
            order.Target = injected;
            order.Caster = injected;
            order.AbilityName = "changed";
            rig.Tick(0.2f);

            Assert.That(script.Context.Target, Is.SameAs(target));
            Assert.That(script.Context.Caster, Is.SameAs(caster));
        }

        [Test]
        public void DirectAbilityOrder_RejectsWrongIdentityAndRemovedReference()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit other = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, Definition("identity"));
            Assert.That(ability.IssueOrder(new CastOrder { Caster = other, AbilityName = "identity" }).Success, Is.False);
            Assert.That(ability.IssueOrder(new CastOrder { Caster = caster, AbilityName = "other" }).Success, Is.False);
            rig.Engine.RemoveAbility(caster, "identity");
            Assert.That(ability.IssueOrder(new CastOrder { Caster = caster, AbilityName = "identity" }).Success, Is.False);
        }

        [Test]
        public void OrdinaryCast_IsSerializedWhileImmediateCastCanRun()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            AbilityDefinition first = Definition("first");
            first.CastPoint = LevelValue.Constant(1f);
            rig.RegisterAndAddAbility(caster, first);
            rig.RegisterAndAddAbility(caster, Definition("second"));
            CaptureScript immediateScript = new CaptureScript();
            AbilityDefinition immediate = Definition("immediate", AbilityBehavior.NoTarget | AbilityBehavior.Immediate);
            immediate.CastPoint = LevelValue.Constant(10f);
            rig.Engine.RegisterAbilityScript(immediate.Name, () => immediateScript);
            rig.RegisterAndAddAbility(caster, immediate);

            Assert.That(rig.Engine.CastAbility(caster, first.Name).Success, Is.True);
            Assert.That(rig.Engine.CastAbility(caster, "second").FailureReason, Is.EqualTo(CastFailureReason.Casting));
            Assert.That(rig.Engine.CastAbility(caster, immediate.Name).Success, Is.True);
            Assert.That(immediateScript.Context, Is.Not.Null);
            Assert.That(rig.Engine.FindAbility(caster, first.Name).Phase, Is.EqualTo(AbilityPhase.Casting));
        }

        [Test]
        public void Queue_UsesSnapshotAndExecutesAfterRecovery_StopCancelsQueuedOrders()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CaptureScript firstScript = new CaptureScript();
            AbilityDefinition first = Definition("recovery");
            first.CastBackswing = LevelValue.Constant(0.5f);
            rig.Engine.RegisterAbilityScript(first.Name, () => firstScript);
            rig.RegisterAndAddAbility(caster, first);
            CaptureScript secondScript = new CaptureScript();
            AbilityDefinition second = Definition("queued");
            rig.Engine.RegisterAbilityScript(second.Name, () => secondScript);
            rig.RegisterAndAddAbility(caster, second);
            rig.Engine.CastAbility(caster, first.Name);
            CastOrder queued = new CastOrder { Caster = caster, AbilityName = second.Name, Queue = true };
            CastResult result = rig.Engine.IssueOrder(queued);
            queued.AbilityName = "missing";
            Assert.That(result.Queued, Is.True);
            Assert.That(rig.Engine.GetQueuedOrders(caster).Count, Is.EqualTo(1));
            rig.Tick(0.5f);
            Assert.That(secondScript.Context, Is.Not.Null);
            Assert.That(secondScript.Context.CastId, Is.EqualTo(result.CastId));
            rig.Engine.CastAbility(caster, first.Name);
            rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = second.Name, Queue = true });
            rig.Engine.Stop(caster);
            Assert.That(rig.Engine.GetQueuedOrders(caster).Count, Is.Zero);
            Assert.That(rig.Engine.FindAbility(caster, first.Name).Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        [Test]
        public void PhaseInterrupt_DoesNotSpendAndCallsHookOnce()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CaptureScript script = new CaptureScript();
            AbilityDefinition definition = Definition("phase");
            definition.CastPoint = LevelValue.Constant(0.5f);
            definition.ManaCost = LevelValue.Constant(10f);
            FakeResourceOwner resources = new FakeResourceOwner(50f);
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            rig.RegisterAndAddAbility(caster, definition, resources);
            rig.Engine.CastAbility(caster, definition.Name);
            rig.Engine.Stop(caster);
            rig.Engine.Stop(caster);
            rig.Tick(1f);
            Assert.That(script.PhaseStarts, Is.EqualTo(1));
            Assert.That(script.PhaseInterrupts, Is.EqualTo(1));
            Assert.That(script.Context, Is.Null);
            Assert.That(resources.SpendCount, Is.Zero);
        }

        [Test]
        public void SpellCallbackInterrupt_DoesNotCreateOrphanChannel()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CaptureScript script = new CaptureScript { SpellCallback = ability => ability.Interrupt() };
            AbilityDefinition definition = Definition("self_interrupt", AbilityBehavior.NoTarget | AbilityBehavior.Channelled);
            definition.ChannelTime = LevelValue.Constant(1f);
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);
            List<RuntimeEvent> events = new List<RuntimeEvent>();
            rig.Engine.EventRaised += events.Add;
            CastResult cast = rig.Engine.CastAbility(caster, definition.Name);
            rig.Tick(2f);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(script.ChannelFinishes, Is.Zero);
            Assert.That(events.FindAll(e => e.EventType == RuntimeEventType.ChannelStarted).Count, Is.Zero);
            Assert.That(events.FindAll(e => e.CastId == cast.CastId && e.EventType == RuntimeEventType.CastInterrupted).Count, Is.EqualTo(1));
        }

        [Test]
        public void ChannelFinishCanStartNextCast_WithoutFinishingItAgain()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CaptureScript script = new CaptureScript();
            AbilityDefinition definition = Definition("reentrant_channel", AbilityBehavior.NoTarget | AbilityBehavior.Channelled);
            definition.ChannelTime = LevelValue.Constant(0.1f);
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);
            script.ChannelFinishCallback = () => { if (script.ChannelFinishes == 1) rig.Engine.CastAbility(caster, definition.Name); };
            List<RuntimeEvent> events = new List<RuntimeEvent>();
            rig.Engine.EventRaised += events.Add;
            CastResult first = rig.Engine.CastAbility(caster, definition.Name);
            rig.Tick(0.5f);
            Assert.That(script.ChannelFinishes, Is.EqualTo(1));
            Assert.That(script.ChannelElapsed, Is.EqualTo(0.1f).Within(0.0001f));
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Channeling));
            Assert.That(events.FindAll(e => e.CastId == first.CastId && e.EventType == RuntimeEventType.CastCompleted).Count, Is.EqualTo(1));
            rig.Engine.Stop(caster);
            rig.Engine.Stop(caster);
            Assert.That(script.ChannelFinishes, Is.EqualTo(2));
        }

        [Test]
        public void CastPointOvershoot_OnlyAdvancesRemainingChannelTime()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CaptureScript script = new CaptureScript();
            AbilityDefinition definition = Definition("overshoot", AbilityBehavior.NoTarget | AbilityBehavior.Channelled);
            definition.CastPoint = LevelValue.Constant(0.2f);
            definition.ChannelTime = LevelValue.Constant(0.5f);
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            rig.RegisterAndAddAbility(caster, definition);
            rig.Engine.CastAbility(caster, definition.Name);
            rig.Tick(0.4f);
            Assert.That(script.ChannelElapsed, Is.EqualTo(0.2f).Within(0.0001f));
            rig.Tick(1f);
            Assert.That(script.ChannelElapsed, Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(script.ChannelFinishes, Is.EqualTo(1));
        }

        [Test]
        public void LevelZeroHasNoIntrinsic_TrainingAndRespawnRestoreIt()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "intrinsic", Duration = -1f });
            AbilityDefinition definition = Definition("trained");
            definition.IntrinsicModifierName = "intrinsic";
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition, null, 0);
            Assert.That(ability.Level, Is.Zero);
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
            Assert.That(rig.Engine.CastAbility(caster, definition.Name).FailureReason, Is.EqualTo(CastFailureReason.NotTrained));
            ability.SetLevel(1);
            Assert.That(rig.Engine.Modifiers.Count, Is.EqualTo(1));
            rig.Engine.RemoveModifier(rig.Engine.Modifiers[0]);
            rig.Tick(0.01f);
            Assert.That(rig.Engine.Modifiers.Count, Is.EqualTo(1));
            caster.IsAlive = false;
            rig.Engine.NotifyDeath(caster);
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
            caster.IsAlive = true;
            rig.Engine.NotifyRespawn(caster);
            Assert.That(rig.Engine.Modifiers.Count, Is.EqualTo(1));
            ability.SetLevel(0);
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
        }

        [Test]
        public void ToggleOn_CommitsCostAndExplicitActions_OffBypassesCooldownAndMana()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            AbilityDefinition definition = Definition("toggle", AbilityBehavior.NoTarget | AbilityBehavior.Toggle);
            definition.ManaCost = LevelValue.Constant(10f);
            definition.Cooldown = LevelValue.Constant(3f);
            definition.OnToggleOnActions.Add(new ActionDefinition { ActionType = ActionType.Heal, Target = ActionTarget.Caster, Value = LevelValue.Constant(5f) });
            definition.OnToggleOffActions.Add(new ActionDefinition { ActionType = ActionType.Heal, Target = ActionTarget.Caster, Value = LevelValue.Constant(7f) });
            FakeResourceOwner resources = new FakeResourceOwner(10f);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition, resources);
            Assert.That(rig.Engine.CastAbility(caster, definition.Name).Success, Is.True);
            Assert.That(ability.ToggleEnabled, Is.True);
            Assert.That(resources.SpendCount, Is.EqualTo(1));
            Assert.That(ability.CooldownRemaining, Is.EqualTo(3f));
            Assert.That(caster.HealingReceived, Is.EqualTo(5f));
            Assert.That(rig.Engine.CastAbility(caster, definition.Name).Success, Is.True);
            Assert.That(ability.ToggleEnabled, Is.False);
            Assert.That(caster.HealingReceived, Is.EqualTo(12f));
            Assert.That(resources.SpendCount, Is.EqualTo(1));
            Assert.That(rig.Engine.CastAbility(caster, definition.Name).FailureReason, Is.EqualTo(CastFailureReason.Cooldown));
        }

        [Test]
        public void Queue_IsBoundedAndRemovedAbilityCannotReviveWhenReadded()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            AbilityDefinition first = Definition("long_cast");
            first.CastPoint = LevelValue.Constant(1f);
            rig.RegisterAndAddAbility(caster, first);
            CaptureScript script = new CaptureScript();
            AbilityDefinition second = Definition("removed_queue");
            rig.Engine.RegisterAbilityScript(second.Name, () => script);
            rig.RegisterAndAddAbility(caster, second);
            rig.Engine.CastAbility(caster, first.Name);
            for (int i = 0; i < AbilitySystem.MaximumQueuedOrdersPerUnit; i++)
                Assert.That(rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = second.Name, Queue = true }).Queued, Is.True);
            Assert.That(rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = second.Name, Queue = true }).FailureReason, Is.EqualTo(CastFailureReason.QueueFull));
            rig.Engine.RemoveAbility(caster, second.Name);
            rig.Engine.AddAbility(caster, second.Name);
            rig.Tick(1f);
            Assert.That(script.Context, Is.Null);
            rig.Engine.Stop(caster);
            Assert.That(rig.Engine.GetQueuedOrders(caster).Count, Is.Zero);
        }

        [Test]
        public void PaymentFailure_IsReturnedSynchronouslyWithoutSpellExecution()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            CaptureScript script = new CaptureScript();
            AbilityDefinition definition = Definition("failed_payment");
            definition.ManaCost = LevelValue.Constant(10f);
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition, new RefusingResources());
            CastResult result = rig.Engine.CastAbility(caster, definition.Name);
            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CastFailureReason.NotEnoughMana));
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(script.Context, Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Removal_CleansCastQueueAndIntrinsic_WhenLifecycleCallbacksThrow(bool channelled)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            ThrowingRemovalScript script = new ThrowingRemovalScript();
            AbilityDefinition definition = Definition("throwing_removal", AbilityBehavior.NoTarget | (channelled ? AbilityBehavior.Channelled : AbilityBehavior.None));
            definition.CastPoint = LevelValue.Constant(channelled ? 0f : 1f);
            definition.ChannelTime = LevelValue.Constant(channelled ? 1f : 0f);
            definition.IntrinsicModifierName = "throwing_intrinsic";
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = definition.IntrinsicModifierName, Duration = -1f });
            rig.Engine.RegisterModifierScript(definition.IntrinsicModifierName, () => new ThrowingIntrinsicScript());
            rig.Engine.RegisterAbilityScript(definition.Name, () => script);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);
            List<RuntimeEvent> events = new List<RuntimeEvent>();
            rig.Engine.EventRaised += events.Add;
            CastResult cast = rig.Engine.CastAbility(caster, definition.Name);
            rig.Engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = definition.Name, Queue = true });
            AggregateException failure = null;
            try { rig.Engine.RemoveAbility(caster, definition.Name); }
            catch (AggregateException error) { failure = error; }

            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Flatten().InnerExceptions.Count, Is.EqualTo(3));
            Assert.That(script.RemovalCalls, Is.EqualTo(1));
            Assert.That(script.InterruptionCalls, Is.EqualTo(1));
            Assert.That(ability.IsRemoved, Is.True);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(rig.Engine.FindAbility(caster, definition.Name), Is.Null);
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
            Assert.That(rig.Engine.GetQueuedOrders(caster).Count, Is.Zero);
            Assert.That(events.FindAll(e => e.CastId == cast.CastId && e.EventType == RuntimeEventType.CastInterrupted).Count, Is.EqualTo(1));
            rig.Engine.Stop(caster);
            Assert.That(script.InterruptionCalls, Is.EqualTo(1));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void FailedExecutionCallback_PreservesFailureAndInterruptionFailure(bool channelled)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            AbilityDefinition definition = Definition("throwing_callback", AbilityBehavior.NoTarget | (channelled ? AbilityBehavior.Channelled : AbilityBehavior.None));
            definition.ChannelTime = LevelValue.Constant(channelled ? 1f : 0f);
            rig.Engine.RegisterAbilityScript(definition.Name, () => new ThrowingExecutionScript { Channelled = channelled });
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);
            AggregateException failure = null;
            try
            {
                rig.Engine.CastAbility(caster, definition.Name);
                if (channelled) rig.Tick(0.1f);
            }
            catch (AggregateException error) { failure = error; }
            Assert.That(failure, Is.Not.Null);
            Assert.That(failure.Flatten().InnerExceptions.Count, Is.EqualTo(2));
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        private static AbilityDefinition Definition(string name, AbilityBehavior behavior = AbilityBehavior.NoTarget)
        {
            return new AbilityDefinition { Name = name, Behavior = behavior, CastRange = LevelValue.Constant(10f), TargetTeam = TargetTeam.Enemy };
        }

        private sealed class CaptureScript : AbilityScript
        {
            public CastContext Context;
            public int PhaseStarts;
            public int PhaseInterrupts;
            public int ChannelFinishes;
            public float ChannelElapsed;
            public Action<Game.Ability.Ability> SpellCallback;
            public Action ChannelFinishCallback;
            public override bool OnAbilityPhaseStart(CastContext context) { PhaseStarts++; return true; }
            public override void OnAbilityPhaseInterrupted() { PhaseInterrupts++; }
            public override void OnSpellStart(CastContext context) { Context = context; SpellCallback?.Invoke(Ability); }
            public override void OnChannelThink(float deltaTime) { ChannelElapsed += deltaTime; }
            public override void OnChannelFinish(bool interrupted) { ChannelFinishes++; ChannelFinishCallback?.Invoke(); }
        }

        private sealed class OrderMutationScript : ModifierScript
        {
            public IUnit Replacement;
            public override void OnEvent(ModifierEvent modifierEvent)
            {
                if (modifierEvent.EventType != ModifierEventType.OrderIssued) return;
                modifierEvent.Order.Target = Replacement;
                modifierEvent.Order.AbilityName = "mutated";
            }
        }

        private sealed class RefusingResources : IResourceOwner
        {
            public float Mana => 100f;
            public bool HasMana(float amount) { return true; }
            public bool SpendMana(float amount) { return false; }
        }

        private sealed class ThrowingRemovalScript : AbilityScript
        {
            public int RemovalCalls;
            public int InterruptionCalls;
            public override void OnAbilityPhaseInterrupted() { InterruptionCalls++; throw new InvalidOperationException("Phase interruption failed."); }
            public override void OnChannelFinish(bool interrupted) { InterruptionCalls++; throw new InvalidOperationException("Channel finish failed."); }
            public override void OnRemoved() { RemovalCalls++; throw new InvalidOperationException("Removal failed."); }
        }

        private sealed class ThrowingIntrinsicScript : ModifierScript
        {
            public override void OnDestroy() { throw new InvalidOperationException("Intrinsic destruction failed."); }
        }

        private sealed class ThrowingExecutionScript : AbilityScript
        {
            public bool Channelled;
            public override void OnSpellStart(CastContext context) { if (!Channelled) throw new InvalidOperationException("Spell failed."); }
            public override void OnChannelThink(float deltaTime) { throw new InvalidOperationException("Channel think failed."); }
            public override void OnAbilityPhaseInterrupted() { throw new InvalidOperationException("Phase interruption failed."); }
            public override void OnChannelFinish(bool interrupted) { throw new InvalidOperationException("Channel finish failed."); }
        }
    }
}
