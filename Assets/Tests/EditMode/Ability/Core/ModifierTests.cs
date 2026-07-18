using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Baseline")]
    public sealed class ModifierTests
    {
        [Test]
        public void StunnedState_PreventsAbilityCast()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            ModifierDefinition stun = new ModifierDefinition
            {
                Name = "baseline_stun",
                Duration = 1f,
                States = UnitState.Stunned
            };
            rig.Engine.RegisterModifierDefinition(stun);
            rig.Engine.AddModifier(target, caster, stun.Name, 1f);

            AbilityDefinition spell = new AbilityDefinition
            {
                Name = "blocked_by_stun",
                Behavior = AbilityBehavior.UnitTarget,
                TargetTeam = TargetTeam.Enemy,
                TargetType = UnitType.All,
                CastRange = LevelValue.Constant(5f)
            };
            rig.RegisterAndAddAbility(caster, spell);

            CastResult result = rig.Engine.CastAbilityOnTarget(caster, spell.Name, target);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CastFailureReason.Stunned));
        }

        [Test]
        public void DamageProperties_CombineOutgoingAndIncomingMultipliers()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            ModifierDefinition outgoing = new ModifierDefinition { Name = "outgoing_bonus", Duration = -1f };
            outgoing.Properties[ModifierProperty.DamageOutgoingPercent] = 50f;
            ModifierDefinition incoming = new ModifierDefinition { Name = "incoming_reduction", Duration = -1f };
            incoming.Properties[ModifierProperty.DamageIncomingPercent] = -20f;
            rig.Engine.RegisterModifierDefinition(outgoing);
            rig.Engine.RegisterModifierDefinition(incoming);
            rig.Engine.AddModifier(attacker, attacker, outgoing.Name, -1f);
            rig.Engine.AddModifier(victim, victim, incoming.Name, -1f);

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo
            {
                Engine = rig.Engine,
                Attacker = attacker,
                Victim = victim,
                Amount = 100f,
                DamageType = DamageType.Physical
            });

            Assert.That(result.Blocked, Is.False);
            Assert.That(result.FinalAmount, Is.EqualTo(120f).Within(0.001f));
            Assert.That(victim.DamageTaken, Is.EqualTo(120f).Within(0.001f));
        }

        [Test]
        public void TimedModifier_ExpiresAtConfiguredDuration()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            ModifierDefinition modifier = new ModifierDefinition
            {
                Name = "temporary_modifier",
                Duration = 1f
            };
            rig.Engine.RegisterModifierDefinition(modifier);
            rig.Engine.AddModifier(unit, unit, modifier.Name, 1f);

            rig.Tick(0.99f);
            Assert.That(rig.Engine.Modifiers.Count, Is.EqualTo(1));

            rig.Tick(0.01f);
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
        }

        [Test]
        public void IntervalModifier_ExecutesConfiguredDamageOnEachCompletedInterval()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            ModifierDefinition damageOverTime = new ModifierDefinition
            {
                Name = "baseline_damage_over_time",
                IsDebuff = true,
                Duration = 2.5f,
                Interval = 1f
            };
            damageOverTime.IntervalActions.Add(new ActionDefinition
            {
                ActionType = ActionType.Damage,
                Target = ActionTarget.PrimaryTarget,
                Value = LevelValue.Constant(10f),
                DamageType = DamageType.Magical
            });
            rig.Engine.RegisterModifierDefinition(damageOverTime);

            AbilityDefinition spell = new AbilityDefinition
            {
                Name = "apply_damage_over_time",
                Behavior = AbilityBehavior.UnitTarget,
                TargetTeam = TargetTeam.Enemy,
                TargetType = UnitType.All,
                CastRange = LevelValue.Constant(5f)
            };
            spell.Actions.Add(new ActionDefinition
            {
                ActionType = ActionType.AddModifier,
                Target = ActionTarget.PrimaryTarget,
                ModifierName = damageOverTime.Name,
                Duration = LevelValue.Constant(2.5f)
            });
            rig.RegisterAndAddAbility(caster, spell);
            rig.Engine.CastAbilityOnTarget(caster, spell.Name, target);

            rig.Tick(0.99f);
            Assert.That(target.DamageTaken, Is.Zero);

            rig.Tick(0.01f);
            Assert.That(target.DamageTaken, Is.EqualTo(10f).Within(0.001f));

            rig.Tick(1f);
            Assert.That(target.DamageTaken, Is.EqualTo(20f).Within(0.001f));

            rig.Tick(0.5f);
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
            Assert.That(target.DamageTaken, Is.EqualTo(20f).Within(0.001f));
        }

        [Test]
        public void IntervalModifier_ExpiryBoundaryTickIsFramePartitionIndependent()
        {
            float singleFrameDamage = RunPoisonDot(6f);
            float partitionedDamage = RunPoisonDot(0.25f, 0.75f, 2.4f, 0.6f, 2f);

            Assert.That(singleFrameDamage, Is.EqualTo(90f).Within(0.001f));
            Assert.That(partitionedDamage, Is.EqualTo(singleFrameDamage).Within(0.001f));
        }

        private static float RunPoisonDot(params float[] deltaTimes)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            ModifierDefinition poisonDot = new ModifierDefinition
            {
                Name = "poison_dot_timing",
                IsDebuff = true,
                Duration = 6f,
                Interval = 1f
            };
            poisonDot.IntervalActions.Add(new ActionDefinition
            {
                ActionType = ActionType.Damage,
                Target = ActionTarget.PrimaryTarget,
                Value = LevelValue.Constant(15f),
                DamageType = DamageType.Magical
            });
            rig.Engine.RegisterModifierDefinition(poisonDot);
            rig.Engine.AddModifier(caster, target, poisonDot.Name, 6f);

            for (int i = 0; i < deltaTimes.Length; i++)
            {
                rig.Tick(deltaTimes[i]);
            }

            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
            return target.DamageTaken;
        }
    }
}
