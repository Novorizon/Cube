using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("KnownDefect")]
    public sealed class KnownDefectSpecificationTests
    {
        [Test]
        public void CastPointCompletion_DoesNotExecuteAfterCasterBecomesStunned()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AbilityDefinition spell = CreateUnitDamageAbility("interruptible_spell", 50f, 0.5f);
            rig.RegisterAndAddAbility(caster, spell);

            ModifierDefinition stun = new ModifierDefinition
            {
                Name = "cast_interrupt_stun",
                Duration = 1f,
                States = UnitState.Stunned
            };
            rig.Engine.RegisterModifierDefinition(stun);

            rig.Engine.CastAbilityOnTarget(caster, spell.Name, target);
            rig.Tick(0.2f);
            rig.Engine.AddModifier(target, caster, stun.Name, 1f);
            rig.Tick(0.3f);

            Assert.That(target.DamageTaken, Is.Zero);
        }

        [Test]
        public void ModifierGrantedMagicImmunity_BlocksMagicalDamage()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            ModifierDefinition immunity = new ModifierDefinition
            {
                Name = "modifier_magic_immunity",
                Duration = 2f,
                States = UnitState.MagicImmune
            };
            rig.Engine.RegisterModifierDefinition(immunity);
            rig.Engine.AddModifier(victim, victim, immunity.Name, 2f);

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo
            {
                Engine = rig.Engine,
                Attacker = attacker,
                Victim = victim,
                Amount = 100f,
                DamageType = DamageType.Magical
            });

            Assert.That(result.Blocked, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
        }

        [Test]
        public void DamageTakenTrigger_DoesNotReactToDamageOnUnrelatedUnit()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            FakeUnit unrelatedVictim = rig.AddUnit(1, Vector3.right);
            FakeUnit attacker = rig.AddUnit(2, Vector3.left);
            AbilityDefinition hostDefinition = new AbilityDefinition
            {
                Name = "trigger_host",
                Behavior = AbilityBehavior.NoTarget
            };
            Game.Ability.Ability hostAbility = rig.RegisterAndAddAbility(owner, hostDefinition);

            ModifierDefinition reactive = new ModifierDefinition
            {
                Name = "react_only_to_parent_damage",
                Duration = -1f,
                TriggerEventType = ModifierEventType.DamageTaken
            };
            reactive.TriggerActions.Add(new ActionDefinition
            {
                ActionType = ActionType.Heal,
                Target = ActionTarget.PrimaryTarget,
                Value = LevelValue.Constant(10f)
            });
            rig.Engine.RegisterModifierDefinition(reactive);
            rig.Engine.AddModifier(owner, owner, hostAbility, reactive.Name, new ModifierApplyOptions { Duration = -1f });

            rig.Engine.ApplyDamage(new DamageInfo
            {
                Engine = rig.Engine,
                Attacker = attacker,
                Victim = unrelatedVictim,
                Amount = 25f,
                DamageType = DamageType.Physical
            });

            Assert.That(unrelatedVictim.HealingReceived, Is.Zero);
        }

        [Test]
        public void LinearProjectile_WithoutDeleteOnHit_HitsEachUnitAtMostOnce()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            rig.AddUnit(2, new Vector3(0.5f, 0f, 0f));
            AbilityDefinition spell = new AbilityDefinition
            {
                Name = "projectile_host",
                Behavior = AbilityBehavior.NoTarget
            };
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, spell);
            int hitCount = 0;
            rig.Engine.EventRaised += runtimeEvent =>
            {
                if (runtimeEvent.EventType == RuntimeEventType.ProjectileHit)
                {
                    hitCount++;
                }
            };

            rig.Engine.CreateLinearProjectile(
                ability,
                caster,
                Vector3.zero,
                Vector3.right,
                new ProjectileDefinition
                {
                    Name = "piercing_projectile",
                    Speed = 0f,
                    Radius = 1f,
                    Distance = 10f,
                    DeleteOnHit = false,
                    TargetTeam = TargetTeam.Enemy,
                    TargetType = UnitType.All
                });

            rig.Tick(0.1f);
            rig.Tick(0.1f);
            rig.Tick(0.1f);

            Assert.That(hitCount, Is.EqualTo(1));
        }

        [Test]
        public void ChargeRestore_WaitsForFullRestoreDuration()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            AbilityDefinition spell = new AbilityDefinition
            {
                Name = "charged_spell",
                Behavior = AbilityBehavior.NoTarget,
                Charges = new ChargeDefinition
                {
                    MaxCharges = 2,
                    RestoreTime = 5f,
                    StartFull = true,
                    UsesCooldown = false
                }
            };
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, spell);

            rig.Engine.CastAbility(caster, spell.Name);
            Assert.That(ability.Charges, Is.EqualTo(1));

            rig.Tick(4.99f);
            Assert.That(ability.Charges, Is.EqualTo(1));

            rig.Tick(0.01f);
            Assert.That(ability.Charges, Is.EqualTo(2));
        }

        [Test]
        public void CastPointCompletion_ReevaluatesCustomCastFilter()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AbilityDefinition spell = CreateUnitDamageAbility("filtered_cast_point_spell", 50f, 0.5f);
            GateCastScript script = new GateCastScript();
            rig.Engine.RegisterAbilityScript(spell.Name, () => script);
            rig.RegisterAndAddAbility(caster, spell);

            CastResult issued = rig.Engine.CastAbilityOnTarget(caster, spell.Name, target);
            Assert.That(issued.Success, Is.True);

            script.AllowCast = false;
            rig.Tick(0.5f);

            Assert.That(script.SpellStartCount, Is.Zero);
            Assert.That(rig.Engine.FindAbility(caster, spell.Name).Phase, Is.EqualTo(AbilityPhase.Idle));
        }

        [Test]
        public void ModifierGrantedUntargetableState_IsUsedByTargetValidation()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AbilityDefinition spell = CreateUnitDamageAbility("untargetable_check_spell", 50f, 0f);
            rig.RegisterAndAddAbility(caster, spell);
            ModifierDefinition untargetable = new ModifierDefinition
            {
                Name = "modifier_untargetable",
                Duration = 2f,
                States = UnitState.Untargetable
            };
            rig.Engine.RegisterModifierDefinition(untargetable);
            rig.Engine.AddModifier(target, target, untargetable.Name, 2f);

            CastResult result = rig.Engine.CastAbilityOnTarget(caster, spell.Name, target);

            Assert.That(result.Success, Is.False);
            Assert.That(result.FailureReason, Is.EqualTo(CastFailureReason.InvalidTarget));
            Assert.That(target.DamageTaken, Is.Zero);
        }

        [Test]
        public void GlobalDamageTakenTrigger_ExplicitlyReactsToUnrelatedUnit()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            FakeUnit unrelatedVictim = rig.AddUnit(1, Vector3.right);
            FakeUnit attacker = rig.AddUnit(2, Vector3.left);
            AbilityDefinition hostDefinition = new AbilityDefinition
            {
                Name = "global_trigger_host",
                Behavior = AbilityBehavior.NoTarget
            };
            Game.Ability.Ability hostAbility = rig.RegisterAndAddAbility(owner, hostDefinition);
            ModifierDefinition reactive = new ModifierDefinition
            {
                Name = "global_damage_reaction",
                Duration = -1f,
                TriggerEventType = ModifierEventType.DamageTaken,
                TriggerEventScope = ModifierEventScope.Global
            };
            reactive.TriggerActions.Add(new ActionDefinition
            {
                ActionType = ActionType.Heal,
                Target = ActionTarget.PrimaryTarget,
                Value = LevelValue.Constant(10f)
            });
            rig.Engine.RegisterModifierDefinition(reactive);
            rig.Engine.AddModifier(owner, owner, hostAbility, reactive.Name, new ModifierApplyOptions { Duration = -1f });

            rig.Engine.ApplyDamage(new DamageInfo
            {
                Engine = rig.Engine,
                Attacker = attacker,
                Victim = unrelatedVictim,
                Amount = 25f,
                DamageType = DamageType.Physical
            });

            Assert.That(unrelatedVictim.HealingReceived, Is.EqualTo(10f));
        }

        [Test]
        public void ChargeRestore_LargeTickRestoresAllCompletedPeriodsAndKeepsOvershoot()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            AbilityDefinition spell = new AbilityDefinition
            {
                Name = "multi_charge_spell",
                Behavior = AbilityBehavior.NoTarget,
                Charges = new ChargeDefinition
                {
                    MaxCharges = 3,
                    RestoreTime = 5f,
                    StartFull = true,
                    UsesCooldown = false
                }
            };
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, spell);
            rig.Engine.CastAbility(caster, spell.Name);
            rig.Engine.CastAbility(caster, spell.Name);
            rig.Engine.CastAbility(caster, spell.Name);
            Assert.That(ability.Charges, Is.Zero);

            rig.Tick(12f);
            Assert.That(ability.Charges, Is.EqualTo(2));

            rig.Tick(2.99f);
            Assert.That(ability.Charges, Is.EqualTo(2));
            rig.Tick(0.01f);
            Assert.That(ability.Charges, Is.EqualTo(3));
        }

        [Test]
        public void RootedState_OnlyRejectsAbilitiesMarkedRootDisables()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            ModifierDefinition root = new ModifierDefinition
            {
                Name = "modifier_rooted",
                Duration = 2f,
                States = UnitState.Rooted
            };
            rig.Engine.RegisterModifierDefinition(root);
            rig.Engine.AddModifier(caster, caster, root.Name, 2f);

            AbilityDefinition blocked = new AbilityDefinition
            {
                Name = "root_blocked_spell",
                Behavior = AbilityBehavior.NoTarget | AbilityBehavior.RootDisables
            };
            AbilityDefinition allowed = new AbilityDefinition
            {
                Name = "root_allowed_spell",
                Behavior = AbilityBehavior.NoTarget
            };
            rig.RegisterAndAddAbility(caster, blocked);
            rig.RegisterAndAddAbility(caster, allowed);

            CastResult blockedResult = rig.Engine.CastAbility(caster, blocked.Name);
            CastResult allowedResult = rig.Engine.CastAbility(caster, allowed.Name);

            Assert.That(blockedResult.Success, Is.False);
            Assert.That(blockedResult.FailureReason, Is.EqualTo(CastFailureReason.Rooted));
            Assert.That(allowedResult.Success, Is.True);
        }

        private static AbilityDefinition CreateUnitDamageAbility(string name, float damage, float castPoint)
        {
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = name,
                Behavior = AbilityBehavior.UnitTarget,
                TargetTeam = TargetTeam.Enemy,
                TargetType = UnitType.All,
                CastRange = LevelValue.Constant(5f),
                CastPoint = LevelValue.Constant(castPoint)
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

        private sealed class GateCastScript : AbilityScript
        {
            public bool AllowCast = true;
            public int SpellStartCount;

            public override CastResult CastFilter(CastContext context)
            {
                return AllowCast
                    ? CastResult.Ok()
                    : CastResult.Fail(CastFailureReason.CustomRejected, "Gate closed.");
            }

            public override void OnSpellStart(CastContext context)
            {
                SpellStartCount++;
            }
        }
    }
}
