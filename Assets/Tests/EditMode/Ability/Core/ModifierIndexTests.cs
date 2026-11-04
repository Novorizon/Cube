using System;
using System.Collections.Generic;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    public sealed class ModifierIndexTests
    {
        [Test]
        public void RegistrationAndDestruction_UpdateUnitQueriesBeforeCallbacks()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            var script = new LifecycleQuery();
            var definition = new ModifierDefinition { Name = "indexed", States = UnitState.Rooted };
            definition.Properties.Add(ModifierProperty.ArmorBonus, 3);
            rig.Engine.RegisterModifierDefinition(definition);
            rig.Engine.RegisterModifierScript(definition.Name, () => script);
            Modifier modifier = rig.Engine.AddModifier(unit, unit, definition.Name);
            Assert.That(script.CreatedHasState, Is.True);
            Assert.That(script.CreatedArmor, Is.EqualTo(3));
            rig.Engine.RemoveModifier(modifier);
            Assert.That(script.DestroyedHasState, Is.False);
            Assert.That(script.DestroyedArmor, Is.Zero);
            Assert.That(rig.Engine.RemoveModifier(modifier), Is.False);
        }

        [Test]
        public void UnitRemovalAndClear_DoNotLeaveModifiersForAReusedEntityId()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            var definition = new ModifierDefinition { Name = "root", States = UnitState.Rooted };
            rig.Engine.RegisterModifierDefinition(definition);
            rig.Engine.RegisterUnit(unit);
            rig.Engine.AddModifier(unit, unit, definition.Name);
            rig.Engine.RemoveUnit(unit);
            var replacement = new FakeUnit(unit.EntityId, 1, Vector3.zero);
            rig.Engine.RegisterUnit(replacement);
            Assert.That(rig.Engine.HasState(replacement, UnitState.Rooted), Is.False);
            rig.Engine.AddModifier(replacement, replacement, definition.Name);
            Assert.That(rig.Engine.HasState(replacement, UnitState.Rooted), Is.True);
            rig.Engine.ClearRuntime();
            Assert.That(rig.Engine.HasState(replacement, UnitState.Rooted), Is.False);
            rig.Engine.RegisterUnit(replacement);
            Assert.That(rig.Engine.AddModifier(replacement, replacement, definition.Name), Is.Not.Null);
        }

        [Test]
        public void NestedPropertyQueries_SnapshotMembershipAndKeepDynamicValues()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero); var other = rig.AddUnit(2, Vector3.right);
            foreach (string name in new[] { "mutator", "old", "new", "other" })
            {
                var definition = new ModifierDefinition { Name = name };
                definition.Properties.Add(ModifierProperty.ArmorBonus, name == "new" ? 8 : name == "other" ? 7 : 4);
                rig.Engine.RegisterModifierDefinition(definition);
            }
            var script = new MutateProperties { Other = other };
            rig.Engine.RegisterModifierScript("mutator", () => script);
            rig.Engine.AddModifier(unit, unit, "mutator");
            script.Remove = rig.Engine.AddModifier(unit, unit, "old");
            rig.Engine.AddModifier(other, other, "other");
            Assert.That(rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null), Is.EqualTo(2));
            Assert.That(script.NestedArmor, Is.EqualTo(7));
            Assert.That(rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null), Is.EqualTo(10));
            script.Value = 5;
            Assert.That(rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null), Is.EqualTo(13));
        }

        [Test]
        public void StateQuery_CanRemoveItselfAndAddAReplacementWithoutAgingItsSnapshot()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "mutate" });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "new_root", States = UnitState.Rooted });
            var script = new MutateState();
            rig.Engine.RegisterModifierScript("mutate", () => script);
            rig.Engine.AddModifier(unit, unit, "mutate");
            script.Enabled = true;
            Assert.That(rig.Engine.HasState(unit, UnitState.Rooted), Is.False);
            Assert.That(rig.Engine.HasState(unit, UnitState.Rooted), Is.True);
        }

        [Test]
        public void QueryException_ReleasesSnapshotsAndExecutionBudgetForLaterQueries()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "throw" });
            var script = new ThrowProperty();
            rig.Engine.RegisterModifierScript("throw", () => script);
            rig.Engine.AddModifier(unit, unit, "throw");
            bool thrown = false;
            try { rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null); }
            catch (InvalidOperationException) { thrown = true; }
            script.Throw = false;
            Assert.That(thrown, Is.True);
            for (int i = 0; i < 10; i++) Assert.That(rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null), Is.EqualTo(6));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void BuiltInPropertyFastPath_StillReadsLiveDefinitionAndStacks(bool configured)
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero);
            var definition = new ModifierDefinition { Name = "live", MaxStack = 3 };
            definition.Properties.Add(ModifierProperty.ArmorBonus, 1);
            definition.Properties.Add(ModifierProperty.HealthRegen, 1);
            rig.Engine.RegisterModifierDefinition(definition);
            if (configured) rig.Engine.RegisterModifierScript(definition.Name, () => new ConfiguredModifierScript());
            Modifier modifier = rig.Engine.AddModifier(unit, unit, definition.Name);
            Assert.That(rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null), Is.EqualTo(1));
            definition.Properties[ModifierProperty.ArmorBonus] = 4;
            definition.Properties[ModifierProperty.HealthRegen] = 2;
            modifier.SetStackCount(2);
            Assert.That(rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, null), Is.EqualTo(8));
            rig.Tick(0.5f);
            Assert.That(unit.HealingReceived, Is.EqualTo(2));
        }

        [Test]
        public void CustomPropertyScripts_ReceiveFreshIsolatedContextsAndDamageSnapshots()
        {
            var rig = new AbilityTestRig(); var unit = rig.AddUnit(1, Vector3.zero); var other = rig.AddUnit(2, Vector3.right);
            var contexts = new List<ModifierPropertyContext>();
            foreach (string name in new[] { "mutate_context", "observe_context" }) rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = name });
            rig.Engine.RegisterModifierScript("mutate_context", () => new ContextListener { Inspect = value =>
            {
                contexts.Add(value);
                value.Unit = other;
                value.DamageInfo.Amount = 999;
                value.DamageResult.FinalAmount = 999;
            } });
            rig.Engine.RegisterModifierScript("observe_context", () => new ContextListener { Inspect = value =>
            {
                contexts.Add(value);
                Assert.That(value.Unit, Is.SameAs(unit));
                Assert.That(value.DamageInfo.Amount, Is.EqualTo(7));
                Assert.That(value.DamageResult.FinalAmount, Is.EqualTo(5));
            } });
            rig.Engine.AddModifier(unit, unit, "mutate_context");
            rig.Engine.AddModifier(unit, unit, "observe_context");
            var source = new ModifierPropertyContext { Unit = unit, DamageInfo = new DamageInfo { Victim = unit, Amount = 7 }, DamageResult = new DamageResult { Victim = unit, FinalAmount = 5 } };
            Assert.That(rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, source), Is.EqualTo(2));
            Assert.That(rig.Engine.GetModifierProperty(unit, ModifierProperty.ArmorBonus, source), Is.EqualTo(2));
            Assert.That(contexts.Count, Is.EqualTo(4));
            Assert.That(contexts[0], Is.Not.SameAs(contexts[1]));
            Assert.That(contexts[0], Is.Not.SameAs(contexts[2]));
            Assert.That(contexts[0].DamageInfo, Is.Not.SameAs(source.DamageInfo));
            Assert.That(source.DamageInfo.Amount, Is.EqualTo(7));
            Assert.That(source.DamageResult.FinalAmount, Is.EqualTo(5));
            Assert.That(source.Unit, Is.SameAs(unit));
        }

        [Test]
        public void ThousandsOfStatAndUnrelatedConfiguredModifiers_DoNotExhaustEventOrDamageBudget()
        {
            var rig = new AbilityTestRig(); var units = new List<FakeUnit>();
            var stats = new ModifierDefinition { Name = "stats", Attributes = ModifierAttribute.Multiple };
            stats.Properties.Add(ModifierProperty.ArmorBonus, 1);
            var reactive = new ModifierDefinition { Name = "reactive", TriggerEventType = ModifierEventType.AttackLanded };
            reactive.TriggerActions.Add(new ActionDefinition { ActionType = ActionType.Heal, Target = ActionTarget.Parent, Value = LevelValue.Constant(1) });
            rig.Engine.RegisterModifierDefinition(stats);
            rig.Engine.RegisterModifierDefinition(reactive);
            for (int i = 0; i < 1000; i++)
            {
                FakeUnit unit = rig.AddUnit(i % 2 + 1, Vector3.zero); units.Add(unit);
                for (int j = 0; j < 3; j++) rig.Engine.AddModifier(unit, unit, stats.Name);
                rig.Engine.AddModifier(unit, unit, reactive.Name);
            }
            int aborts = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ExecutionAborted) aborts++; };
            rig.Engine.MaxExecutionOperations = 128;
            rig.Engine.NotifyAttackLanded(units[999], units[0]);
            Assert.That(units[999].HealingReceived, Is.EqualTo(1));
            Assert.That(units[0].HealingReceived, Is.Zero);
            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = units[999], Victim = units[0], Amount = 5, DamageType = DamageType.Pure });
            Assert.That(result.Blocked, Is.False);
            Assert.That(result.AppliedAmount, Is.EqualTo(5));
            Assert.That(aborts, Is.Zero);
        }

        [Test]
        public void CustomGlobalListeners_KeepRegistrationOrderAcrossConfiguredListenersAndRemoval()
        {
            var rig = new AbilityTestRig(); var source = rig.AddUnit(1, Vector3.zero); var distant = rig.AddUnit(2, Vector3.right);
            var order = new List<string>();
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "custom_a" });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "custom_b" });
            var configured = new ModifierDefinition { Name = "configured", TriggerEventType = ModifierEventType.AttackLanded };
            configured.TriggerActions.Add(new ActionDefinition { ActionType = ActionType.Heal, Target = ActionTarget.Parent, Value = LevelValue.Constant(1) });
            rig.Engine.RegisterModifierDefinition(configured);
            Modifier remove = null;
            rig.Engine.RegisterModifierScript("custom_a", () => new EventListener { OnAttack = () => { order.Add("first"); rig.Engine.RemoveModifier(remove); } });
            rig.Engine.RegisterModifierScript("custom_b", () => new EventListener { OnAttack = () => order.Add("removed") });
            rig.Engine.AddModifier(distant, distant, "custom_a");
            rig.Engine.AddModifier(source, source, configured.Name);
            remove = rig.Engine.AddModifier(distant, distant, "custom_b");
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.HealApplied) order.Add("configured"); };
            rig.Engine.NotifyAttackLanded(source, distant);
            Assert.That(order, Is.EqualTo(new[] { "first", "configured" }));
        }

        private sealed class LifecycleQuery : ModifierScript
        {
            public bool CreatedHasState; public bool DestroyedHasState; public float CreatedArmor; public float DestroyedArmor;
            public override void OnCreated(ModifierApplyOptions options) { CreatedHasState = Engine.HasState(Parent, UnitState.Rooted); CreatedArmor = Engine.GetModifierProperty(Parent, ModifierProperty.ArmorBonus, null); }
            public override void OnDestroy() { DestroyedHasState = Engine.HasState(Parent, UnitState.Rooted); DestroyedArmor = Engine.GetModifierProperty(Parent, ModifierProperty.ArmorBonus, null); }
        }

        private sealed class MutateProperties : ModifierScript
        {
            public Modifier Remove; public IUnit Other; public float NestedArmor; public float Value = 2; private bool changed;
            public override float GetProperty(ModifierProperty property, ModifierPropertyContext context)
            {
                if (property != ModifierProperty.ArmorBonus) return 0;
                if (!changed)
                {
                    changed = true;
                    Engine.RemoveModifier(Remove);
                    Engine.AddModifier(Parent, Parent, "new");
                    NestedArmor = Engine.GetModifierProperty(Other, property, null);
                }
                return Value;
            }
        }

        private sealed class MutateState : ModifierScript
        {
            public bool Enabled;
            public override bool CheckState(UnitState state)
            {
                if (Enabled) { Enabled = false; Engine.RemoveModifier(Modifier); Engine.AddModifier(Parent, Parent, "new_root"); }
                return false;
            }
        }

        private sealed class ThrowProperty : ModifierScript
        {
            public bool Throw = true;
            public override float GetProperty(ModifierProperty property, ModifierPropertyContext context) { if (Throw) throw new InvalidOperationException("query"); return 6; }
        }

        private sealed class EventListener : ModifierScript
        {
            public Action OnAttack;
            public override void OnEvent(ModifierEvent value) { if (value.EventType == ModifierEventType.AttackLanded) OnAttack(); }
        }

        private sealed class ContextListener : ModifierScript
        {
            public Action<ModifierPropertyContext> Inspect;
            public override float GetProperty(ModifierProperty property, ModifierPropertyContext context)
            {
                if (property != ModifierProperty.ArmorBonus) return 0;
                Inspect(context);
                return 1;
            }
        }
    }
}
