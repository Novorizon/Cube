using System;
using System.Collections.Generic;
using System.Linq;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class ModifierCorrectnessTests
    {
        [Test]
        public void StandaloneModifier_UsesExplicitLevelForConfiguredActions()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            ModifierDefinition definition = new ModifierDefinition { Name = "standalone_level", Duration = 1f, Interval = 1f };
            LevelValue amount = new LevelValue();
            amount.ValuesByLevel.Add(10f);
            amount.ValuesByLevel.Add(25f);
            definition.IntervalActions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.PrimaryTarget, Value = amount });
            rig.Engine.RegisterModifierDefinition(definition);

            rig.Engine.AddModifier(caster, target, null, definition.Name, new ModifierApplyOptions { Level = 2 });
            rig.Tick(2f);

            Assert.That(target.DamageTaken, Is.EqualTo(25f).Within(0.001f));
            Assert.That(rig.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void AbilityBackedModifier_ResolvesCurrentAbilityLevel()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "modifier_source", MaxLevel = 2 });
            ModifierDefinition definition = new ModifierDefinition { Name = "ability_level", Duration = 2f, Interval = 1f };
            LevelValue amount = new LevelValue();
            amount.ValuesByLevel.Add(10f);
            amount.ValuesByLevel.Add(25f);
            definition.IntervalActions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.Parent, Value = amount });
            rig.Engine.RegisterModifierDefinition(definition);
            rig.Engine.AddModifier(caster, target, ability, definition.Name);

            rig.Tick(1f);
            ability.SetLevel(2);
            rig.Tick(1f);

            Assert.That(target.DamageTaken, Is.EqualTo(35f).Within(0.001f));
        }

        [Test]
        public void InitialStacks_AreClampedBeforeCreatedCallback()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            int observedStacks = 0;
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "clamped", MaxStack = 2 });
            rig.Engine.RegisterModifierScript("clamped", () => new CallbackModifier { Created = script => observedStacks = script.Modifier.StackCount });

            Modifier modifier = rig.Engine.AddModifier(unit, unit, null, "clamped", new ModifierApplyOptions { StackCount = int.MaxValue });
            modifier.IncrementStack(int.MaxValue);

            Assert.That(observedStacks, Is.EqualTo(2));
            Assert.That(modifier.StackCount, Is.EqualTo(2));
        }

        [Test]
        public void CreatedCallback_CanRemoveRegisteredModifierWithoutPresentationLeak()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            bool wasRegistered = false;
            int destroyed = 0;
            Modifier createdModifier = null;
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "self_removing", SustainedEffectName = "persistent" });
            rig.Engine.RegisterModifierScript("self_removing", () => new CallbackModifier
            {
                Created = script =>
                {
                    createdModifier = script.Modifier;
                    wasRegistered = script.Engine.Modifiers.Contains(script.Modifier);
                    script.Engine.RemoveModifier(script.Modifier);
                },
                Destroyed = () => destroyed++
            });

            Modifier modifier = rig.Engine.AddModifier(unit, unit, "self_removing");
            List<PresentationHandleInfo> handles = new List<PresentationHandleInfo>();
            rig.Presentation.GetActivePresentationHandles(handles);

            Assert.That(wasRegistered, Is.True);
            Assert.That(modifier, Is.Null);
            Assert.That(createdModifier.IsDestroyed, Is.True);
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(rig.Engine.Modifiers, Is.Empty);
            Assert.That(handles, Is.Empty);
        }

        [Test]
        public void IntervalRefresh_ResetsLifetimeAtCallbackTime()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            int intervals = 0;
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "refresh_boundary", Duration = 1f, Interval = 1f });
            rig.Engine.RegisterModifierScript("refresh_boundary", () => new CallbackModifier
            {
                Interval = script =>
                {
                    intervals++;
                    script.Modifier.Refresh(new ModifierApplyOptions { Duration = 1f });
                }
            });
            Modifier modifier = rig.Engine.AddModifier(unit, unit, "refresh_boundary");

            rig.Tick(3.5f);

            Assert.That(intervals, Is.EqualTo(3));
            Assert.That(modifier.IsDestroyed, Is.False);
            Assert.That(modifier.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(modifier.ElapsedTime, Is.EqualTo(3.5f).Within(0.00001f));
        }

        [Test]
        public void IntervalCatchUp_StopsWhenParentDiesInsideCallback()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            int intervals = 0;
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "death_during_interval", Duration = 10f, Interval = 1f });
            rig.Engine.RegisterModifierScript("death_during_interval", () => new CallbackModifier { Interval = script => { intervals++; unit.IsAlive = false; } });
            Modifier modifier = rig.Engine.AddModifier(unit, unit, "death_during_interval");

            rig.Tick(5f);

            Assert.That(intervals, Is.EqualTo(1));
            Assert.That(modifier.IsDestroyed, Is.True);
        }

        [TestCase(false, 1)]
        [TestCase(true, 4)]
        public void AuraRefresh_OnlyIncrementsStacksWhenExplicitlyConfigured(bool incrementStacks, int expectedStacks)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            FakeUnit receiver = rig.AddUnit(1, Vector3.right);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "aura_child", Duration = 1f, MaxStack = 10 });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition
            {
                Name = "aura_source",
                AuraModifierName = "aura_child",
                AuraRadius = 5f,
                AuraDuration = 1f,
                AuraThinkInterval = 0.1f,
                AuraIncrementsStacks = incrementStacks
            });
            rig.Engine.AddModifier(source, source, "aura_source");

            rig.Tick(0.1f);
            rig.Tick(0.1f);
            rig.Tick(0.1f);

            Modifier child = rig.Engine.Modifiers.Single(modifier => modifier.Name == "aura_child" && modifier.Parent == receiver);
            Assert.That(child.StackCount, Is.EqualTo(expectedStacks));
        }

        [Test]
        public void HealthRegeneration_OnlyAppliesDuringActiveLifetime()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            ModifierDefinition definition = new ModifierDefinition { Name = "short_regeneration", Duration = 0.5f };
            definition.Properties[ModifierProperty.HealthRegen] = 10f;
            rig.Engine.RegisterModifierDefinition(definition);
            rig.Engine.AddModifier(unit, unit, definition.Name);

            rig.Tick(2f);

            Assert.That(unit.HealingReceived, Is.EqualTo(5f).Within(0.001f));
            Assert.That(rig.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void CatchUpLimit_SkipsExcessCallbacksWithoutLeavingTimerDebt()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            int intervals = 0;
            rig.Engine.MaxPeriodicCallbacksPerUpdate = 3;
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "bounded_interval", Interval = 0.01f });
            rig.Engine.RegisterModifierScript("bounded_interval", () => new CallbackModifier { Interval = script => intervals++ });
            Modifier modifier = rig.Engine.AddModifier(unit, unit, "bounded_interval");

            rig.Tick(10f);
            Assert.That(intervals, Is.EqualTo(3));
            Assert.That(modifier.ElapsedTime, Is.EqualTo(10f).Within(0.0001f));
            rig.Tick(0.0001f);
            Assert.That(intervals, Is.LessThanOrEqualTo(4));
        }

        [Test]
        public void Thinker_RemovalDuringOnThinkSuppressesIntervalsAndDestroysExactlyOnce()
        {
            AbilityTestRig rig = new AbilityTestRig();
            int intervals = 0;
            int destroyCallbacks = 0;
            int destroyEvents = 0;
            rig.Engine.EventRaised += runtimeEvent => { if (runtimeEvent.EventType == RuntimeEventType.ThinkerDestroyed) destroyEvents++; };
            Thinker thinker = rig.Engine.CreateThinker(new ThinkerRequest
            {
                Duration = 10f,
                Interval = 0.1f,
                Script = new CallbackThinker
                {
                    Think = (script, delta) => script.Engine.RemoveThinker(script.Thinker),
                    Interval = () => intervals++,
                    Destroyed = () => destroyCallbacks++
                }
            });

            rig.Tick(1f);
            rig.Engine.RemoveThinker(thinker);

            Assert.That(intervals, Is.Zero);
            Assert.That(destroyCallbacks, Is.EqualTo(1));
            Assert.That(destroyEvents, Is.EqualTo(1));
        }

        [Test]
        public void Thinker_LongFrameClipsOnThinkAndIntervalsToLifetime()
        {
            AbilityTestRig rig = new AbilityTestRig();
            float observedTime = 0f;
            int intervals = 0;
            Thinker thinker = rig.Engine.CreateThinker(new ThinkerRequest
            {
                Duration = 0.6f,
                Interval = 0.2f,
                Script = new CallbackThinker { Think = (script, delta) => observedTime += delta, Interval = () => intervals++ }
            });

            rig.Tick(2f);

            Assert.That(observedTime, Is.EqualTo(0.6f).Within(0.00001f));
            Assert.That(intervals, Is.EqualTo(3));
            Assert.That(thinker.IsDestroyed, Is.True);
        }

        [Test]
        public void Thinker_CreatedCallbackCanRemoveRegisteredInstance()
        {
            AbilityTestRig rig = new AbilityTestRig();
            int destroyed = 0;
            Thinker createdThinker = null;
            Thinker thinker = rig.Engine.CreateThinker(new ThinkerRequest
            {
                Script = new CallbackThinker
                {
                    Created = script => { createdThinker = script.Thinker; script.Engine.RemoveThinker(script.Thinker); },
                    Destroyed = () => destroyed++
                }
            });

            rig.Tick(1f);

            Assert.That(thinker, Is.Null);
            Assert.That(createdThinker.IsDestroyed, Is.True);
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(rig.Engine.Thinkers, Is.Empty);
        }

        [Test]
        public void ContextTargets_DoesNotSubstituteUnresolvedPrimaryTarget()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            CastContext context = new CastContext { Engine = rig.Engine, Caster = caster, Target = target };

            ActionRunner.Execute(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.ContextTargets, Value = LevelValue.Constant(50f) }, context);

            Assert.That(target.DamageTaken, Is.Zero);
        }

        [Test]
        public void LifecycleActions_StopAfterSourceRemovalButStillRunDestroyActions()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            ModifierDefinition definition = new ModifierDefinition { Name = "removed_mid_creation" };
            definition.OnCreatedActions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.PrimaryTarget, Value = LevelValue.Constant(2000f) });
            definition.OnCreatedActions.Add(new ActionDefinition { ActionType = ActionType.PlayEffect, EffectName = "stale_action" });
            definition.OnDestroyActions.Add(new ActionDefinition { ActionType = ActionType.PlayEffect, EffectName = "destroy_action" });
            rig.Engine.RegisterModifierDefinition(definition);

            Modifier modifier = rig.Engine.AddModifier(unit, unit, definition.Name);

            Assert.That(modifier, Is.Null);
            Assert.That(rig.Presentation.Effects.Count, Is.EqualTo(1));
            Assert.That(rig.Presentation.Effects[0].Name, Is.EqualTo("destroy_action"));
        }

        [Test]
        public void ActionList_StopsAfterRuntimeWasClearedByAnEarlierAction()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            rig.Engine.EventRaised += runtimeEvent => { if (runtimeEvent.EventType == RuntimeEventType.DamageApplied) rig.Engine.ClearRuntime(); };
            CastContext context = ActionRunner.CreateSingleTargetContext(rig.Engine, null, caster, target, target.Position);
            List<ActionDefinition> actions = new List<ActionDefinition>
            {
                new ActionDefinition { ActionType = ActionType.Damage, Value = LevelValue.Constant(1f) },
                new ActionDefinition { ActionType = ActionType.Damage, Value = LevelValue.Constant(50f) }
            };

            ActionRunner.Execute(actions, context);

            Assert.That(target.DamageTaken, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void DamageTakenAction_CanDamageEventAttackerUsingAppliedDamageFraction()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit defender = rig.AddUnit(2, Vector3.right);
            ModifierDefinition definition = new ModifierDefinition { Name = "counter_damage", TriggerEventType = ModifierEventType.DamageTaken };
            definition.TriggerActions.Add(new ActionDefinition
            {
                ActionType = ActionType.Damage,
                Target = ActionTarget.EventSource,
                ValueSource = ActionValueSource.EventDamage,
                Value = LevelValue.Constant(0.25f)
            });
            rig.Engine.RegisterModifierDefinition(definition);
            rig.Engine.AddModifier(defender, defender, definition.Name);

            rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = defender, Amount = 100f, DamageType = DamageType.Pure });

            Assert.That(defender.DamageTaken, Is.EqualTo(100f).Within(0.001f));
            Assert.That(attacker.DamageTaken, Is.EqualTo(25f).Within(0.001f));
        }

        [Test]
        public void DamageDealtAction_CanHealModifierParentUsingAppliedDamageFraction()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit defender = rig.AddUnit(2, Vector3.right);
            ModifierDefinition definition = new ModifierDefinition { Name = "configured_lifesteal", TriggerEventType = ModifierEventType.DamageDealt };
            definition.TriggerActions.Add(new ActionDefinition
            {
                ActionType = ActionType.Heal,
                Target = ActionTarget.Parent,
                ValueSource = ActionValueSource.EventDamage,
                Value = LevelValue.Constant(0.3f)
            });
            rig.Engine.RegisterModifierDefinition(definition);
            rig.Engine.AddModifier(attacker, attacker, definition.Name);

            rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = defender, Amount = 100f, DamageType = DamageType.Pure });

            Assert.That(attacker.HealingReceived, Is.EqualTo(30f).Within(0.001f));
            Assert.That(defender.HealingReceived, Is.Zero);
        }

        [Test]
        public void ReciprocalConfiguredTriggers_AreBoundedAndNextExecutionCanRecover()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit first = rig.AddUnit(1, Vector3.zero);
            FakeUnit second = rig.AddUnit(2, Vector3.right);
            int aborted = 0;
            rig.Engine.MaxExecutionDepth = 24;
            rig.Engine.EventRaised += runtimeEvent => { if (runtimeEvent.EventType == RuntimeEventType.ExecutionAborted) aborted++; };
            ModifierDefinition definition = new ModifierDefinition { Name = "reciprocal_counter", TriggerEventType = ModifierEventType.DamageTaken };
            definition.TriggerActions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.EventSource, Value = LevelValue.Constant(1f) });
            rig.Engine.RegisterModifierDefinition(definition);
            Modifier firstModifier = rig.Engine.AddModifier(first, first, definition.Name);
            rig.Engine.AddModifier(second, second, definition.Name);

            rig.Engine.ApplyDamage(new DamageInfo { Attacker = first, Victim = second, Amount = 1f });

            Assert.That(aborted, Is.EqualTo(1));
            Assert.That(first.DamageTaken + second.DamageTaken, Is.LessThanOrEqualTo(24f));
            rig.Engine.RemoveModifier(firstModifier);
            float previousDamage = second.DamageTaken;
            rig.Engine.ApplyDamage(new DamageInfo { Attacker = first, Victim = second, Amount = 2f });
            Assert.That(second.DamageTaken, Is.EqualTo(previousDamage + 2f).Within(0.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CastActions_StopWhenCallbackRemovesAbilityOrCaster(bool removeCaster)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = "removed_during_actions",
                Behavior = AbilityBehavior.UnitTarget,
                CastRange = LevelValue.Constant(5f)
            };
            definition.Actions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.PrimaryTarget, Value = LevelValue.Constant(10f) });
            definition.Actions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.PrimaryTarget, Value = LevelValue.Constant(10f) });
            rig.RegisterAndAddAbility(caster, definition);
            rig.Engine.EventRaised += runtimeEvent =>
            {
                if (runtimeEvent.EventType != RuntimeEventType.DamageApplied) return;
                if (removeCaster) rig.Engine.RemoveUnit(caster);
                else rig.Engine.RemoveAbility(caster, definition.Name);
            };

            rig.Engine.CastAbilityOnTarget(caster, definition.Name, target);

            Assert.That(target.DamageTaken, Is.EqualTo(10f).Within(0.001f));
        }

        [Test]
        public void ModifierActions_CanKeepHistoricalRemovedAbilityAttribution()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "historical_source" });
            ModifierDefinition definition = new ModifierDefinition { Name = "historical_modifier", Duration = 1f, Interval = 1f };
            definition.IntervalActions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.Parent, Value = LevelValue.Constant(10f) });
            rig.Engine.RegisterModifierDefinition(definition);
            rig.Engine.AddModifier(caster, target, ability, definition.Name);
            rig.Engine.RemoveAbility(caster, ability.Definition.Name);

            rig.Tick(1f);

            Assert.That(target.DamageTaken, Is.EqualTo(10f).Within(0.001f));
            Assert.That(target.DamageResults[0].Ability, Is.EqualTo(ability));
        }

        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void Refresh_RejectsInfiniteDurationBeforeChangingRuntime(float duration)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "finite_refresh", Duration = 1f, MaxStack = 3 });
            Modifier modifier = rig.Engine.AddModifier(unit, unit, "finite_refresh");
            bool rejected = false;

            try { modifier.Refresh(new ModifierApplyOptions { Duration = duration, StackCount = 2 }); }
            catch (ArgumentOutOfRangeException) { rejected = true; }

            Assert.That(rejected, Is.True);
            Assert.That(modifier.Duration, Is.EqualTo(1f));
            Assert.That(modifier.RemainingTime, Is.EqualTo(1f));
            Assert.That(modifier.StackCount, Is.EqualTo(1));
        }

        [Test]
        public void PresentationStopException_StillRunsDestroyCleanupAndRemovalEventOnce()
        {
            AbilityTestRig rig = new AbilityTestRig();
            rig.Engine.Initialize(rig.World, new ThrowingStopPresentation());
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            int destroyed = 0;
            int removedEvents = 0;
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "throwing_presentation", SustainedEffectName = "persistent" });
            rig.Engine.RegisterModifierScript("throwing_presentation", () => new CallbackModifier { Destroyed = () => destroyed++ });
            rig.Engine.EventRaised += runtimeEvent => { if (runtimeEvent.EventType == RuntimeEventType.ModifierRemoved) removedEvents++; };
            Modifier modifier = rig.Engine.AddModifier(unit, unit, "throwing_presentation");
            bool threw = false;

            try { rig.Engine.RemoveModifier(modifier); }
            catch (InvalidOperationException) { threw = true; }
            rig.Engine.RemoveModifier(modifier);

            Assert.That(threw, Is.True);
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(removedEvents, Is.EqualTo(1));
            Assert.That(rig.Engine.Modifiers, Is.Empty);
        }

        private sealed class ThrowingStopPresentation : IPresentation
        {
            public void PlayEffect(string effectName, Vector3 position) { }
            public void PlayEffect(string effectName, IUnit target) { }
            public void PlaySound(string soundName, Vector3 position) { }
            public IPresentationHandle PlayPersistentEffect(string effectName, IUnit target) => new ThrowingStopHandle();
        }

        private sealed class ThrowingStopHandle : IPresentationHandle
        {
            public string EffectName => "persistent";
            public int TargetEntityId => 0;
            public bool IsActive { get; private set; } = true;
            public void Stop() { IsActive = false; throw new InvalidOperationException("Presentation stop failed."); }
        }

        private sealed class CallbackModifier : ModifierScript
        {
            public Action<CallbackModifier> Created;
            public Action<CallbackModifier> Interval;
            public Action Destroyed;
            public override void OnCreated(ModifierApplyOptions options) => Created?.Invoke(this);
            public override void OnIntervalThink() => Interval?.Invoke(this);
            public override void OnDestroy() => Destroyed?.Invoke();
        }

        private sealed class CallbackThinker : ThinkerScript
        {
            public Action<CallbackThinker> Created;
            public Action<CallbackThinker, float> Think;
            public Action Interval;
            public Action Destroyed;
            public override void OnCreated() => Created?.Invoke(this);
            public override void OnThink(float deltaTime) => Think?.Invoke(this, deltaTime);
            public override void OnIntervalThink() => Interval?.Invoke();
            public override void OnDestroy() => Destroyed?.Invoke();
        }
    }
}
