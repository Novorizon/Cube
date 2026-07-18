using System.Linq;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Baseline")]
    public sealed class ActionRunnerTests
    {
        [Test]
        public void AreaDamage_AffectsEveryValidTargetInRadius()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit first = rig.AddUnit(2, new Vector3(2f, 0f, 0f));
            FakeUnit second = rig.AddUnit(2, new Vector3(3f, 0f, 0f));
            FakeUnit outside = rig.AddUnit(2, new Vector3(7f, 0f, 0f));
            AbilityDefinition definition = CreatePointAreaAbility("area_damage", 3f);
            definition.Actions.Add(new ActionDefinition
            {
                ActionType = ActionType.Damage,
                Target = ActionTarget.ContextTargets,
                Value = LevelValue.Constant(80f),
                DamageType = DamageType.Magical
            });
            rig.RegisterAndAddAbility(caster, definition);

            CastResult result = rig.Engine.CastAbilityOnPosition(caster, definition.Name, new Vector3(2f, 0f, 0f));

            Assert.That(result.Success, Is.True);
            Assert.That(first.DamageTaken, Is.EqualTo(80f).Within(0.001f));
            Assert.That(second.DamageTaken, Is.EqualTo(80f).Within(0.001f));
            Assert.That(outside.DamageTaken, Is.Zero);
        }

        [Test]
        public void AreaModifier_WithNestedCreatedEffect_AppliesToEveryTarget()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit first = rig.AddUnit(2, new Vector3(2f, 0f, 0f));
            FakeUnit second = rig.AddUnit(2, new Vector3(3f, 0f, 0f));
            ModifierDefinition slow = new ModifierDefinition
            {
                Name = "baseline_area_slow",
                IsDebuff = true,
                Duration = 4f
            };
            slow.OnCreatedActions.Add(new ActionDefinition
            {
                ActionType = ActionType.PlayEffect,
                Target = ActionTarget.PrimaryTarget,
                EffectName = "slow_created"
            });
            rig.Engine.RegisterModifierDefinition(slow);

            AbilityDefinition definition = CreatePointAreaAbility("area_modifier", 3f);
            definition.Actions.Add(new ActionDefinition
            {
                ActionType = ActionType.AddModifier,
                Target = ActionTarget.ContextTargets,
                ModifierName = slow.Name,
                Duration = LevelValue.Constant(4f)
            });
            rig.RegisterAndAddAbility(caster, definition);

            CastResult result = rig.Engine.CastAbilityOnPosition(caster, definition.Name, new Vector3(2f, 0f, 0f));

            Assert.That(result.Success, Is.True);
            Assert.That(rig.Engine.Modifiers.Count(x => x.Parent == first), Is.EqualTo(1));
            Assert.That(rig.Engine.Modifiers.Count(x => x.Parent == second), Is.EqualTo(1));
        }

        private static AbilityDefinition CreatePointAreaAbility(string name, float radius)
        {
            return new AbilityDefinition
            {
                Name = name,
                Behavior = AbilityBehavior.PointTarget | AbilityBehavior.Aoe,
                TargetTeam = TargetTeam.Enemy,
                TargetType = UnitType.All,
                CastRange = LevelValue.Constant(10f),
                AoeRadius = LevelValue.Constant(radius)
            };
        }
    }
}
