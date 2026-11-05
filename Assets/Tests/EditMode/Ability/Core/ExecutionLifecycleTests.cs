using System;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    public sealed class ExecutionLifecycleTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ClearedExecutionChain_CannotStartFreshDamageBeforeItsCallbackReturns(bool channel)
        {
            var rig = new AbilityTestRig(); var owner = rig.AddUnit(1, Vector3.zero); var victim = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterAbilityScript("clear", () => new ClearThenDamage { Victim = victim, DuringChannel = channel });
            rig.RegisterAndAddAbility(owner, new AbilityDefinition { Name = "clear", Behavior = channel ? AbilityBehavior.NoTarget | AbilityBehavior.Channelled : AbilityBehavior.NoTarget, ChannelTime = LevelValue.Constant(1f) });
            rig.Engine.CastAbility(owner, "clear");
            if (channel) rig.Tick(0.1f);
            Assert.That(victim.DamageTaken, Is.Zero);
            rig.Engine.ApplyDamage(new DamageInfo { Victim = victim, Amount = 5f });
            Assert.That(victim.DamageTaken, Is.EqualTo(5f));
        }

        [Test]
        public void FeedbackBudget_AbortsOneChainAndDoesNotPoisonLaterOperations()
        {
            var rig = new AbilityTestRig();
            var unit = rig.AddUnit(1, Vector3.zero);
            rig.Engine.MaxExecutionDepth = 16;
            rig.Engine.MaxExecutionOperations = 128;
            int diagnostics = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ExecutionAborted) diagnostics++; };
            var definition = new ModifierDefinition { Name = "reactive", Duration = -1f, TriggerEventType = ModifierEventType.DamageTaken };
            definition.TriggerActions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.Parent, Value = LevelValue.Constant(1f) });
            rig.Engine.RegisterModifierDefinition(definition);
            Modifier modifier = rig.Engine.AddModifier(unit, unit, definition.Name);
            rig.Engine.ApplyDamage(new DamageInfo { Attacker = unit, Victim = unit, Amount = 1f });
            Assert.That(diagnostics, Is.EqualTo(1));
            Assert.That(unit.IsAlive, Is.True);
            rig.Engine.RemoveModifier(modifier);
            float before = unit.DamageTaken;
            rig.Engine.ApplyDamage(new DamageInfo { Victim = unit, Amount = 5f });
            Assert.That(unit.DamageTaken - before, Is.EqualTo(5f));
        }

        [Test]
        public void RuntimeEventRecursion_IsBounded()
        {
            var rig = new AbilityTestRig();
            var unit = rig.AddUnit(1, Vector3.zero);
            var ability = rig.RegisterAndAddAbility(unit, new AbilityDefinition { Name = "host" });
            int events = 0;
            rig.Engine.MaxExecutionDepth = 12;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.CooldownFinished) { events++; ability.EndCooldown(); } };
            ability.EndCooldown();
            Assert.That(events > 0 && events <= 12, Is.True);
        }

        [Test]
        public void PurgeAndClear_AllowCrossRemovalWithoutInvalidatingTraversal()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            var script = new RemovalScript();
            foreach (string name in new[] { "a", "b", "c" }) rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = name });
            rig.Engine.RegisterModifierScript("a", () => script);
            rig.Engine.AddModifier(unit, unit, "a");
            script.First = rig.Engine.AddModifier(unit, unit, "b"); script.Second = rig.Engine.AddModifier(unit, unit, "c");
            rig.Engine.Purge(unit, true, true, true);
            Assert.That(rig.Engine.Modifiers, Is.Empty);
            rig.Engine.AddModifier(unit, unit, "a");
            script.First = rig.Engine.AddModifier(unit, unit, "b"); script.Second = rig.Engine.AddModifier(unit, unit, "c");
            rig.Engine.ClearRuntime();
            Assert.That(rig.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void UnitRemoval_CannotBeUndoneInsideCallbackButAllowsExplicitLaterBinding()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            var script = new RebindOnRemoval();
            rig.Engine.RegisterAbilityScript("host", () => script);
            var definition = new AbilityDefinition { Name = "host" };
            rig.RegisterAndAddAbility(unit, definition);
            rig.Engine.RemoveUnit(unit);
            Assert.That(script.Rebound, Is.False);
            Assert.That(script.Recreated, Is.Null);
            Assert.That(rig.Engine.GetAbilities(unit), Is.Empty);
            Assert.That(rig.Engine.RegisterUnit(unit), Is.True);
            Assert.That(rig.Engine.AddAbility(unit, definition.Name), Is.Not.Null);
        }

        [Test]
        public void Cleanup_BlocksNewObjectsAndTerminatesProjectileHandle()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero); var target = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "late", SustainedEffectName = "fx" });
            var script = new SpawnOnRemoval();
            rig.Engine.RegisterAbilityScript("host", () => script);
            rig.RegisterAndAddAbility(unit, new AbilityDefinition { Name = "host" });
            var projectile = rig.Engine.CreateTrackingProjectile(null, unit, target, new ProjectileDefinition());
            int destroyed = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ProjectileDestroyed) destroyed++; };
            rig.Engine.ClearRuntime();
            Assert.That(script.Created, Is.Null);
            Assert.That(projectile.Destroyed, Is.True);
            Assert.That(destroyed, Is.EqualTo(1));
            Assert.That(rig.Engine.CreateRuntimeSnapshot().PresentationHandles, Is.Empty);
            Assert.That(rig.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void Cleanup_ReleasesEverythingBeforeReportingCallbackFailure()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "throws", SustainedEffectName = "fx" });
            rig.Engine.RegisterModifierScript("throws", () => new ThrowOnDestroy());
            rig.Engine.AddModifier(unit, unit, "throws");
            rig.Engine.CreateThinker(new ThinkerRequest { Caster = unit });
            bool failed = false;
            try { rig.Engine.ClearRuntime(); } catch (AggregateException) { failed = true; }
            Assert.That(failed, Is.True);
            Assert.That(rig.Engine.Modifiers, Is.Empty);
            Assert.That(rig.Engine.Thinkers, Is.Empty);
            Assert.That(rig.Engine.IsClearingRuntime, Is.False);
            Assert.That(rig.Engine.CreateRuntimeSnapshot().PresentationHandles, Is.Empty);
        }

        [Test]
        public void UnitIdentityAndSnapshots_IncludeDynamicStateAndProperty()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "dynamic" });
            rig.Engine.RegisterModifierScript("dynamic", () => new DynamicState());
            rig.Engine.AddModifier(unit, unit, "dynamic");
            var wrapper = new FakeUnit(unit.EntityId, unit.TeamId, unit.Position);
            Assert.That(rig.Engine.HasState(wrapper, UnitState.Stunned), Is.True);
            Assert.That(rig.Engine.GetModifierProperty(wrapper, ModifierProperty.ArmorBonus, null), Is.EqualTo(7f));
            var snapshot = rig.Engine.CreateRuntimeSnapshot();
            Assert.That(snapshot.Modifiers[0].States, Is.EqualTo(UnitState.Stunned));
            Assert.That(snapshot.Modifiers[0].Properties[0].Value, Is.EqualTo(7f));
            rig.Engine.Purge(wrapper, true, true, true);
            Assert.That(rig.Engine.Modifiers, Is.Empty);
        }

        private sealed class RemovalScript : ModifierScript
        {
            public Modifier First, Second;
            public override void OnDestroy() { Engine.RemoveModifier(First); Engine.RemoveModifier(Second); }
        }
        private sealed class RebindOnRemoval : AbilityScript
        {
            public bool Rebound; public Game.Ability.Ability Recreated;
            public override void OnRemoved() { Rebound = Engine.RegisterUnit(Caster); Recreated = Engine.AddAbility(Caster, "host"); }
        }
        private sealed class SpawnOnRemoval : AbilityScript
        {
            public Modifier Created;
            public override void OnRemoved() { Created = Engine.AddModifier(Caster, Caster, "late"); }
        }
        private sealed class ThrowOnDestroy : ModifierScript { public override void OnDestroy() { throw new InvalidOperationException("Test callback failure"); } }
        private sealed class DynamicState : ModifierScript
        {
            public override bool CheckState(UnitState state) => state == UnitState.Stunned;
            public override float GetProperty(ModifierProperty property, ModifierPropertyContext context) => property == ModifierProperty.ArmorBonus ? 7f : 0f;
        }
        private sealed class ClearThenDamage : AbilityScript
        {
            public IUnit Victim; public bool DuringChannel;
            public override void OnSpellStart(CastContext context) { if (!DuringChannel) ClearAndApply(); }
            public override void OnChannelThink(float deltaTime) { if (DuringChannel) ClearAndApply(); }
            private void ClearAndApply() { Engine.ClearRuntime(); Engine.ApplyDamage(new DamageInfo { Victim = Victim, Amount = 20f }); }
        }
    }
}
