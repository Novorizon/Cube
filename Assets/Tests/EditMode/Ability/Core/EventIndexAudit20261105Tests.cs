using System;
using System.Collections.Generic;
using System.Linq;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class EventIndexAudit20261105Tests
    {
        [TestCase(ModifierEventType.OrderIssued, "source,custom,global,inherited")]
        [TestCase(ModifierEventType.AbilityExecuted, "source,custom,global,caster,inherited")]
        [TestCase(ModifierEventType.AbilityFullyCast, "source,custom,global,caster,inherited")]
        [TestCase(ModifierEventType.ChannelFinished, "source,custom,global,caster,inherited")]
        [TestCase(ModifierEventType.DamageCalculated, "source,custom,global,target,inherited")]
        [TestCase(ModifierEventType.DamageDealt, "source,custom,global,inherited")]
        [TestCase(ModifierEventType.DamageTaken, "custom,global,target,inherited")]
        [TestCase(ModifierEventType.Healed, "custom,global,target,inherited")]
        [TestCase(ModifierEventType.AttackStart, "source,custom,global,inherited")]
        [TestCase(ModifierEventType.AttackLanded, "source,custom,global,inherited")]
        [TestCase(ModifierEventType.Death, "custom,global,target,inherited")]
        [TestCase(ModifierEventType.Respawn, "custom,global,target,inherited")]
        [TestCase(ModifierEventType.ProjectileHit, "source,custom,global,target,inherited")]
        [TestCase(ModifierEventType.ModifierAdded, "custom,global,target,inherited")]
        [TestCase(ModifierEventType.ModifierRemoved, "custom,global,target,inherited")]
        public void PublicEventsPreserveRelatedGlobalAndInheritedCustomOrder(ModifierEventType type, string expected)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            FakeUnit other = rig.AddUnit(1, Vector3.forward);
            FakeUnit unrelated = rig.AddUnit(1, Vector3.forward * 2f);
            AddConfigured(rig, other, source, "source", type);
            AddCustom(rig, other, "custom", () => new RecordingEvent(type, "custom"));
            AddConfigured(rig, other, other, "global", type, true);
            AddConfigured(rig, other, target, "target", type);
            AddConfigured(rig, source, unrelated, "caster", type);
            AddCustom(rig, other, "inherited", () => new InheritedRecordingEvent(type, "inherited"));
            int before = rig.Presentation.Sounds.Count;

            Fire(rig, source, target, type);

            Assert.That(rig.Presentation.Sounds.Skip(before).Select(value => value.Name).ToArray(), Is.EqualTo(expected.Split(',')));
        }

        [Test]
        public void SelfDamageCalculatedFindsOneRelatedListenerEvenWhenSourceAndTargetIdsMatch()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            AddConfigured(rig, unit, unit, "self", ModifierEventType.DamageCalculated);

            rig.Engine.ApplyDamage(new DamageInfo { Attacker = unit, Victim = unit, Amount = 1, DamageType = DamageType.Pure });

            Assert.That(rig.Presentation.Sounds.Count(value => value.Name == "self"), Is.EqualTo(1));
            Assert.That(unit.DamageTaken, Is.EqualTo(1));
        }

        [Test]
        public void JsonRelatedRespawnTargetsOnlyTheRevivedModifierParent()
        {
            const string json = "{\"modifiers\":[{\"name\":\"respawn\",\"removeOnDeath\":false,\"triggerEventType\":15,\"triggerActions\":[{\"actionType\":8,\"soundName\":\"revived\"}]}]}";
            AbilityTestRig rig = new AbilityTestRig();
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new JsonAbilityDefinitionProvider(json, "respawn.json"));
            Assert.That(registry.ApplyTo(rig.Engine), Is.True);
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit parent = rig.AddUnit(1, Vector3.right);
            FakeUnit other = rig.AddUnit(1, Vector3.forward);
            rig.Engine.AddModifier(caster, parent, "respawn");
            rig.Engine.AddModifier(parent, other, "respawn");
            parent.IsAlive = false;
            rig.Engine.NotifyDeath(parent);
            parent.IsAlive = true;

            rig.Engine.NotifyRespawn(parent);
            rig.Engine.NotifyRespawn(parent);

            Assert.That(rig.Presentation.Sounds.Count(value => value.Name == "revived"), Is.EqualTo(1));
        }

        [Test]
        public void ReentrantRemovalAndRegistrationUseIndependentEventSnapshots()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            List<string> log = new List<string>();
            Modifier removed = null;
            bool nested = false;
            AddCustom(rig, target, "mutator", () => new CallbackEvent(value =>
            {
                if (value.EventType != ModifierEventType.AttackLanded) return;
                log.Add(nested ? "mutator.inner" : "mutator.outer");
                if (nested) return;
                rig.Engine.RemoveModifier(removed);
                AddCustom(rig, target, "added", () => new CallbackEvent(evt => { if (evt.EventType == ModifierEventType.AttackLanded) log.Add("added.inner"); }));
                nested = true;
                rig.Engine.NotifyAttackLanded(source, target);
            }));
            removed = AddCustom(rig, target, "removed", () => new CallbackEvent(value => { if (value.EventType == ModifierEventType.AttackLanded) log.Add("removed"); }));

            rig.Engine.NotifyAttackLanded(source, target);

            Assert.That(log, Is.EqualTo(new[] { "mutator.outer", "mutator.inner", "added.inner" }));
        }

        [Test]
        public void ClearAndEntityReuseDoNotRetainRelatedOrCustomListeners()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AddConfigured(rig, source, source, "related", ModifierEventType.AttackLanded);
            AddCustom(rig, target, "custom", () => new RecordingEvent(ModifierEventType.AttackLanded, "custom"));
            rig.Engine.NotifyAttackLanded(source, target);
            int before = rig.Presentation.Sounds.Count;
            rig.Engine.ClearRuntime();
            FakeUnit replacement = new FakeUnit(source.EntityId, source.TeamId, source.Position);
            rig.Engine.RegisterUnit(replacement);
            rig.Engine.RegisterUnit(target);
            rig.Engine.NotifyAttackLanded(replacement, target);

            Assert.That(rig.Presentation.Sounds.Count, Is.EqualTo(before));
            AddConfigured(rig, replacement, replacement, "new_related", ModifierEventType.AttackLanded);
            rig.Engine.NotifyAttackLanded(replacement, target);
            Assert.That(rig.Presentation.Sounds.Skip(before).Select(value => value.Name).ToArray(), Is.EqualTo(new[] { "new_related" }));
        }

        [Test]
        public void PropertyOnlyScriptsDoNotConsumeAnEmptyEventListenerBudget()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "property_only", Attributes = ModifierAttribute.Multiple });
            rig.Engine.RegisterModifierScript("property_only", () => new PropertyOnly());
            for (int i = 0; i < 1000; i++) rig.Engine.AddModifier(source, target, "property_only");
            int aborts = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ExecutionAborted) aborts++; };
            rig.Engine.MaxExecutionOperations = 16;

            rig.Engine.NotifyAttackLanded(source, target);

            Assert.That(aborts, Is.Zero);
        }

        [Test]
        public void GenericOnEventHelperDoesNotMakeModifierRegistrationAmbiguous()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "generic_helper" });
            rig.Engine.RegisterModifierScript("generic_helper", () => new GenericHelper());
            Exception error = null;
            Modifier modifier = null;
            try { modifier = rig.Engine.AddModifier(source, source, "generic_helper"); }
            catch (Exception exception) { error = exception; }

            Assert.That(error, Is.Null);
            Assert.That(modifier, Is.Not.Null);
        }

        private static Modifier AddConfigured(AbilityTestRig rig, IUnit caster, IUnit parent, string name, ModifierEventType type, bool global = false)
        {
            ModifierDefinition definition = new ModifierDefinition { Name = name, RemoveOnDeath = false, TriggerEventType = type, TriggerEventScope = global ? ModifierEventScope.Global : ModifierEventScope.Related };
            definition.TriggerActions.Add(new ActionDefinition { ActionType = ActionType.PlaySound, SoundName = name });
            rig.Engine.RegisterModifierDefinition(definition);
            return rig.Engine.AddModifier(caster, parent, name);
        }

        private static Modifier AddCustom(AbilityTestRig rig, IUnit parent, string name, Func<ModifierScript> factory)
        {
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = name, RemoveOnDeath = false });
            rig.Engine.RegisterModifierScript(name, factory);
            return rig.Engine.AddModifier(parent, parent, name);
        }

        private static void Fire(AbilityTestRig rig, FakeUnit source, FakeUnit target, ModifierEventType type)
        {
            switch (type)
            {
                case ModifierEventType.DamageCalculated:
                case ModifierEventType.DamageTaken:
                case ModifierEventType.DamageDealt:
                    rig.Engine.ApplyDamage(new DamageInfo { Attacker = source, Victim = target, Amount = 1, DamageType = DamageType.Pure }); break;
                case ModifierEventType.Healed:
                    rig.Engine.Heal(new HealInfo { Source = source, Target = target, Amount = 1 }); break;
                case ModifierEventType.AttackStart: rig.Engine.NotifyAttackStart(source, target); break;
                case ModifierEventType.AttackLanded: rig.Engine.NotifyAttackLanded(source, target); break;
                case ModifierEventType.Death: target.IsAlive = false; rig.Engine.NotifyDeath(target, source); break;
                case ModifierEventType.Respawn:
                    target.IsAlive = false; rig.Engine.NotifyDeath(target, source); target.IsAlive = true; rig.Engine.NotifyRespawn(target); break;
                case ModifierEventType.ProjectileHit:
                    rig.Engine.CreateTrackingProjectile(null, source, target, new ProjectileDefinition { Speed = 10, Radius = 0.01f, Distance = 10 }); rig.Tick(0.2f); break;
                case ModifierEventType.ModifierAdded:
                case ModifierEventType.ModifierRemoved:
                    rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "probe" });
                    Modifier probe = rig.Engine.AddModifier(source, target, "probe");
                    if (type == ModifierEventType.ModifierRemoved) rig.Engine.RemoveModifier(probe);
                    break;
                default:
                    AbilityDefinition ability = new AbilityDefinition { Name = "cast", Behavior = AbilityBehavior.UnitTarget, CastRange = LevelValue.Constant(10) };
                    if (type == ModifierEventType.ChannelFinished) { ability.Behavior |= AbilityBehavior.Channelled; ability.ChannelTime = LevelValue.Constant(0.1f); }
                    rig.RegisterAndAddAbility(source, ability);
                    Assert.That(rig.Engine.CastAbilityOnTarget(source, ability.Name, target).Success, Is.True);
                    if (type == ModifierEventType.ChannelFinished) rig.Tick(0.2f);
                    break;
            }
        }

        private class RecordingEvent : ModifierScript
        {
            private readonly ModifierEventType type;
            private readonly string name;
            public RecordingEvent(ModifierEventType type, string name) { this.type = type; this.name = name; }
            public override void OnEvent(ModifierEvent value) { if (value.EventType == type) Engine.Presentation.PlaySound(name, Vector3.zero); }
        }
        private sealed class InheritedRecordingEvent : RecordingEvent
        {
            public InheritedRecordingEvent(ModifierEventType type, string name) : base(type, name) { }
            public override float GetProperty(ModifierProperty property, ModifierPropertyContext context) => 0;
        }
        private sealed class CallbackEvent : ModifierScript
        {
            private readonly Action<ModifierEvent> callback;
            public CallbackEvent(Action<ModifierEvent> callback) { this.callback = callback; }
            public override void OnEvent(ModifierEvent value) => callback(value);
        }
        private class PropertyOnly : ModifierScript
        {
            public override float GetProperty(ModifierProperty property, ModifierPropertyContext context) => 1;
        }
        private sealed class GenericHelper : PropertyOnly
        {
            public void OnEvent<T>(ModifierEvent value) { }
        }
    }
}
