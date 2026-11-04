using System.Linq;
using Game.Ability;
using Game.Ability.Configuration;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Scenarios")]
    public sealed class ProjectileActionContractTests
    {
        [TestCase("\"valueSource\":1,\"value\":{\"baseValue\":0.2}")]
        [TestCase("\"valueSpecialName\":\"missing_special\"")]
        public void ProjectileHitActions_RejectUnsupportedEventValuesAndMissingSpecials(string actionValue)
        {
            string json = "{\"abilities\":[{\"id\":\"bolt\",\"name\":\"bolt\",\"behavior\":8,\"onProjectileHit\":[{\"actionType\":1,\"target\":7," + actionValue + "}]}]}";
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();

            registry.LoadProvider(new JsonAbilityDefinitionProvider(json, "invalid_hit.json"));

            Assert.That(registry.IsValid, Is.False);
            Assert.That(registry.ApplyTo(new AbilitySystem()), Is.False);
        }

        [TestCase(8, 16)]
        [TestCase(7, 1024)]
        public void TriggerExcludedDamageFlags_RejectNonDamageEventsAndUnknownFlags(int eventType, int flags)
        {
            string json = "{\"modifiers\":[{\"id\":\"filter\",\"name\":\"filter\",\"triggerEventType\":" + eventType + ",\"triggerExcludedDamageFlags\":" + flags + "}]}";
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();

            registry.LoadProvider(new JsonAbilityDefinitionProvider(json, "invalid_damage_filter.json"));

            Assert.That(registry.IsValid, Is.False);
        }

        [Test]
        public void HitOnlyDefinition_SelectsConfiguredScriptAndSupportsEventTarget()
        {
            const string json = "{\"abilities\":[{\"id\":\"bolt\",\"name\":\"bolt\",\"behavior\":8,\"onProjectileHit\":[{\"actionType\":1,\"target\":7,\"damageType\":3,\"value\":{\"baseValue\":12}}]}]}";
            AbilityDefinitionRegistry registry = new AbilityDefinitionRegistry();
            registry.LoadProvider(new JsonAbilityDefinitionProvider(json, "hit_only.json"));
            AbilityTestRig rig = new AbilityTestRig();
            Assert.That(registry.ApplyTo(rig.Engine), Is.True);
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            Game.Ability.Ability ability = rig.Engine.AddAbility(caster, "bolt");
            rig.Engine.CreateTrackingProjectile(ability, caster, victim, new ProjectileDefinition { Speed = 2f, Radius = 0.05f });

            rig.Tick(0.5f);

            Assert.That(victim.DamageTaken, Is.EqualTo(12f));
            Assert.That(victim.DamageResults.Single().Ability, Is.SameAs(ability));
        }
    }
}
