using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Baseline")]
    public sealed class AbilityCastingTests
    {
        [Test]
        public void UnitTargetCast_SpendsResourceAndDealsDamageAfterCastPoint()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, new Vector3(3f, 0f, 0f));
            FakeResourceOwner resources = new FakeResourceOwner(100f);
            AbilityDefinition definition = CreateDamageAbility("baseline_fireball", 120f, 20f, 0.2f, 3f, 6f);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition, resources);

            CastResult result = rig.Engine.CastAbilityOnTarget(caster, definition.Name, target);

            Assert.That(result.Success, Is.True);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Casting));
            Assert.That(target.DamageTaken, Is.Zero);
            Assert.That(resources.Mana, Is.EqualTo(100f));

            rig.Tick(0.19f);
            Assert.That(target.DamageTaken, Is.Zero);
            Assert.That(resources.Mana, Is.EqualTo(100f));

            rig.Tick(0.01f);
            Assert.That(target.DamageTaken, Is.EqualTo(120f).Within(0.001f));
            Assert.That(resources.Mana, Is.EqualTo(80f).Within(0.001f));
            Assert.That(resources.SpendCount, Is.EqualTo(1));
            Assert.That(ability.CooldownRemaining, Is.EqualTo(3f).Within(0.001f));
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        [Test]
        public void OutOfRangeCast_DoesNotSpendResourceOrStartCooldown()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, new Vector3(10f, 0f, 0f));
            FakeResourceOwner resources = new FakeResourceOwner(100f);
            AbilityDefinition definition = CreateDamageAbility("short_range_spell", 50f, 10f, 0f, 4f, 5f);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition, resources);

            CastResult result = rig.Engine.CastAbilityOnTarget(caster, definition.Name, target);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CastFailureReason.OutOfRange));
            Assert.That(resources.Mana, Is.EqualTo(100f));
            Assert.That(resources.SpendCount, Is.Zero);
            Assert.That(ability.CooldownRemaining, Is.Zero);
            Assert.That(target.DamageTaken, Is.Zero);
        }

        [Test]
        public void Cooldown_PreventsImmediateSecondCast()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AbilityDefinition definition = CreateDamageAbility("cooldown_spell", 25f, 0f, 0f, 2f, 5f);
            rig.RegisterAndAddAbility(caster, definition);

            CastResult first = rig.Engine.CastAbilityOnTarget(caster, definition.Name, target);
            CastResult second = rig.Engine.CastAbilityOnTarget(caster, definition.Name, target);

            Assert.That(first.Success, Is.True);
            Assert.That(second.Success, Is.False);
            Assert.That(second.FailureReason, Is.EqualTo(CastFailureReason.Cooldown));
            Assert.That(target.DamageTaken, Is.EqualTo(25f).Within(0.001f));
        }

        private static AbilityDefinition CreateDamageAbility(
            string name,
            float damage,
            float manaCost,
            float castPoint,
            float cooldown,
            float castRange)
        {
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = name,
                Behavior = AbilityBehavior.UnitTarget,
                TargetTeam = TargetTeam.Enemy,
                TargetType = UnitType.All,
                CastRange = LevelValue.Constant(castRange),
                CastPoint = LevelValue.Constant(castPoint),
                Cooldown = LevelValue.Constant(cooldown),
                ManaCost = LevelValue.Constant(manaCost)
            };
            definition.Actions.Add(new ActionDefinition
            {
                ActionType = ActionType.Damage,
                Target = ActionTarget.PrimaryTarget,
                Value = LevelValue.Constant(damage),
                DamageType = DamageType.Magical
            });
            return definition;
        }
    }
}
