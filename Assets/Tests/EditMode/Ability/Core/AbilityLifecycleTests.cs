using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class AbilityLifecycleTests
    {
        [Test]
        public void RemoveUnit_CleansOwnedAndReferencedRuntimeObjects()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit leavingUnit = rig.AddUnit(1, Vector3.zero);
            FakeUnit remainingUnit = rig.AddUnit(2, Vector3.right);
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = "lifecycle_host",
                Behavior = AbilityBehavior.NoTarget
            };
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(leavingUnit, definition);

            ModifierDefinition modifierDefinition = new ModifierDefinition
            {
                Name = "lifecycle_modifier",
                Duration = -1f
            };
            rig.Engine.RegisterModifierDefinition(modifierDefinition);
            rig.Engine.AddModifier(leavingUnit, remainingUnit, ability, modifierDefinition.Name, new ModifierApplyOptions { Duration = -1f });

            rig.Engine.CreateLinearProjectile(
                ability,
                leavingUnit,
                leavingUnit.Position,
                Vector3.right,
                new ProjectileDefinition { Name = "lifecycle_projectile", Distance = 10f, Speed = 1f });

            rig.Engine.CreateThinker(new ThinkerRequest
            {
                Name = "lifecycle_thinker",
                Ability = ability,
                Caster = leavingUnit,
                Position = leavingUnit.Position,
                Duration = 10f,
                Interval = 1f,
                Radius = 1f
            });

            rig.Engine.RemoveUnit(leavingUnit);

            Assert.That(rig.Engine.GetAbilities(leavingUnit), Is.Empty);
            Assert.That(rig.Engine.Modifiers, Is.Empty);
            Assert.That(rig.Engine.Projectiles, Is.Empty);
            Assert.That(rig.Engine.Thinkers, Is.Empty);
            Assert.That(remainingUnit.IsAlive, Is.True);
        }

        [Test]
        public void RemoveUnit_DoesNotRemoveUnrelatedUnitAbility()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit leavingUnit = rig.AddUnit(1, Vector3.zero);
            FakeUnit remainingUnit = rig.AddUnit(1, Vector3.right);
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = "independent_host",
                Behavior = AbilityBehavior.NoTarget
            };
            rig.Engine.RegisterAbilityDefinition(definition);
            rig.Engine.AddAbility(leavingUnit, definition.Name);
            rig.Engine.AddAbility(remainingUnit, definition.Name);

            rig.Engine.RemoveUnit(leavingUnit);

            Assert.That(rig.Engine.GetAbilities(leavingUnit), Is.Empty);
            Assert.That(rig.Engine.GetAbilities(remainingUnit).Count, Is.EqualTo(1));
        }

        [Test]
        public void RemoveUnit_CancelsAnotherUnitsPendingCastOnThatTarget()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit leavingTarget = rig.AddUnit(2, Vector3.right);
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = "pending_target_host",
                Behavior = AbilityBehavior.UnitTarget,
                TargetTeam = TargetTeam.Enemy,
                TargetType = UnitType.All,
                CastRange = LevelValue.Constant(5f),
                CastPoint = LevelValue.Constant(1f)
            };
            definition.Actions.Add(new ActionDefinition
            {
                ActionType = ActionType.Damage,
                Target = ActionTarget.PrimaryTarget,
                Value = LevelValue.Constant(50f),
                DamageType = DamageType.Magical
            });
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition);

            CastResult castResult = rig.Engine.CastAbilityOnTarget(caster, definition.Name, leavingTarget);
            Assert.That(castResult.Success, Is.True);
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Casting));

            rig.Engine.RemoveUnit(leavingTarget);
            rig.Tick(1f);

            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(leavingTarget.DamageTaken, Is.Zero);
        }

        [Test]
        public void ClearRuntime_DoesNotExecuteConfiguredModifierDestroyActions()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            AbilityDefinition abilityDefinition = new AbilityDefinition
            {
                Name = "clear_runtime_host",
                Behavior = AbilityBehavior.NoTarget
            };
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(unit, abilityDefinition);
            ModifierDefinition modifierDefinition = new ModifierDefinition
            {
                Name = "clear_runtime_modifier",
                Duration = -1f
            };
            modifierDefinition.OnDestroyActions.Add(new ActionDefinition
            {
                ActionType = ActionType.Damage,
                Target = ActionTarget.PrimaryTarget,
                Value = LevelValue.Constant(25f),
                DamageType = DamageType.Pure
            });
            rig.Engine.RegisterModifierDefinition(modifierDefinition);
            rig.Engine.AddModifier(unit, unit, ability, modifierDefinition.Name, new ModifierApplyOptions { Duration = -1f });

            rig.Engine.ClearRuntime();

            Assert.That(unit.DamageTaken, Is.Zero);
            Assert.That(rig.Engine.Modifiers, Is.Empty);
            Assert.That(rig.Engine.GetAbilities(unit), Is.Empty);
            Assert.That(rig.Engine.IsClearingRuntime, Is.False);
        }

        [Test]
        public void RuntimeSnapshot_ExposesStateAndBecomesEmptyAfterUnitCleanup()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit unit = rig.AddUnit(1, Vector3.zero);
            AbilityDefinition abilityDefinition = new AbilityDefinition
            {
                Name = "snapshot_host",
                Behavior = AbilityBehavior.NoTarget
            };
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(unit, abilityDefinition);
            ModifierDefinition modifierDefinition = new ModifierDefinition
            {
                Name = "snapshot_modifier",
                Duration = 5f,
                States = UnitState.Stunned,
                SustainedEffectName = "effects/snapshot_loop"
            };
            modifierDefinition.Properties[ModifierProperty.ArmorBonus] = 3f;
            rig.Engine.RegisterModifierDefinition(modifierDefinition);
            rig.Engine.AddModifier(unit, unit, ability, modifierDefinition.Name, new ModifierApplyOptions { Duration = 5f });
            rig.Engine.CreateLinearProjectile(
                ability,
                unit,
                unit.Position,
                Vector3.right,
                new ProjectileDefinition { Name = "snapshot_projectile", Speed = 1f, Distance = 10f });
            rig.Engine.CreateThinker(new ThinkerRequest
            {
                Ability = ability,
                Caster = unit,
                Position = unit.Position,
                Duration = 5f,
                Interval = 1f,
                Radius = 2f
            });

            AbilityRuntimeSnapshot before = rig.Engine.CreateRuntimeSnapshot();
            Assert.That(before.Units.Count, Is.EqualTo(1));
            Assert.That(before.Units[0].Abilities[0].Name, Is.EqualTo(abilityDefinition.Name));
            Assert.That(before.Modifiers[0].States, Is.EqualTo(UnitState.Stunned));
            Assert.That(before.Modifiers[0].Properties[0].Property, Is.EqualTo(ModifierProperty.ArmorBonus));
            Assert.That(before.Projectiles.Count, Is.EqualTo(1));
            Assert.That(before.Thinkers.Count, Is.EqualTo(1));
            Assert.That(before.PresentationHandles.Count, Is.EqualTo(1));
            Assert.That(before.PresentationHandles[0].TargetEntityId, Is.EqualTo(unit.EntityId));

            rig.Engine.RemoveUnit(unit);
            AbilityRuntimeSnapshot after = rig.Engine.CreateRuntimeSnapshot();
            Assert.That(after.Units, Is.Empty);
            Assert.That(after.Modifiers, Is.Empty);
            Assert.That(after.Projectiles, Is.Empty);
            Assert.That(after.Thinkers, Is.Empty);
            Assert.That(after.PresentationHandles, Is.Empty);
        }
    }
}
