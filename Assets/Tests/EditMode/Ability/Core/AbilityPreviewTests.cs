using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    public sealed class AbilityPreviewTests
    {
        [Test]
        public void Preview_UsesCastValidationWithoutIssuingOrdersOrSpendingResources()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            FakeResourceOwner resources = new FakeResourceOwner(20f);
            ProbeScript script = new ProbeScript();
            rig.Engine.RegisterAbilityScript("preview", () => script);
            AbilityDefinition definition = new AbilityDefinition
            {
                Name = "preview", Behavior = AbilityBehavior.UnitTarget, TargetTeam = TargetTeam.Enemy,
                CastRange = LevelValue.Constant(5f), ManaCost = LevelValue.Constant(5f), Cooldown = LevelValue.Constant(3f),
                Charges = new ChargeDefinition { MaxCharges = 2, RestoreTime = 4f }
            };
            definition.Actions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.PrimaryTarget, Value = LevelValue.Constant(10f) });
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, definition, resources);
            int events = 0;
            rig.Engine.EventRaised += _ => events++;
            CastOrder order = new CastOrder { Caster = caster, AbilityName = definition.Name, Target = target, TargetPosition = new Vector3(77f, 0f, 0f), HasTargetPosition = true };

            CastResult first = rig.Engine.EvaluateOrder(order);
            CastResult second = rig.Engine.EvaluateOrder(order);

            Assert.That(first.Success && second.Success, Is.True);
            Assert.That(first.CastId, Is.Zero);
            Assert.That(events, Is.Zero);
            Assert.That(script.Filters, Is.EqualTo(2));
            Assert.That(script.PhaseStarts + script.SpellStarts, Is.Zero);
            Assert.That(resources.SpendCount, Is.Zero);
            Assert.That(resources.Mana, Is.EqualTo(20f));
            Assert.That(ability.CooldownRemaining, Is.Zero);
            Assert.That(ability.Charges, Is.EqualTo(2));
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Idle));
            Assert.That(target.DamageTaken, Is.Zero);
            Assert.That(order.TargetPosition.x, Is.EqualTo(77f));
            Assert.That(order.HasTargetPosition, Is.True);
            Assert.That(rig.Engine.GetQueuedOrders(caster).Count, Is.Zero);

            CastResult issued = rig.Engine.IssueOrder(order);
            Assert.That(issued.Success, Is.True);
            Assert.That(issued.CastId, Is.EqualTo(1L));
            Assert.That(resources.SpendCount, Is.EqualTo(1));
            Assert.That(target.DamageTaken, Is.EqualTo(10f));
            Assert.That(script.PhaseStarts, Is.EqualTo(1));
            Assert.That(script.SpellStarts, Is.EqualTo(1));
        }

        [Test]
        public void PreviewAndCast_RejectTheSameTargetResourceAndCustomFilterFailures()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, new Vector3(9f, 0f, 0f));
            ProbeScript script = new ProbeScript();
            rig.Engine.RegisterAbilityScript("filter", () => script);
            rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "filter", Behavior = AbilityBehavior.UnitTarget, TargetTeam = TargetTeam.Enemy, CastRange = LevelValue.Constant(3f) });
            CastOrder order = new CastOrder { Caster = caster, AbilityName = "filter", Target = target };
            Assert.That(rig.Engine.EvaluateOrder(order).FailureReason, Is.EqualTo(CastFailureReason.OutOfRange));
            Assert.That(rig.Engine.IssueOrder(order).FailureReason, Is.EqualTo(CastFailureReason.OutOfRange));
            target.Position = Vector3.right;
            script.Reject = true;
            Assert.That(rig.Engine.EvaluateOrder(order).FailureReason, Is.EqualTo(CastFailureReason.CustomRejected));
            Assert.That(rig.Engine.IssueOrder(order).FailureReason, Is.EqualTo(CastFailureReason.CustomRejected));
            Assert.That(script.PhaseStarts, Is.Zero);
            Game.Ability.Ability costly = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "costly", ManaCost = LevelValue.Constant(5f) }, new FakeResourceOwner(4f));
            order.AbilityName = costly.Definition.Name;
            Assert.That(rig.Engine.EvaluateOrder(order).FailureReason, Is.EqualTo(CastFailureReason.NotEnoughMana));
            Assert.That(rig.Engine.IssueOrder(order).FailureReason, Is.EqualTo(CastFailureReason.NotEnoughMana));
        }

        [Test]
        public void PreviewWhileBusy_PreservesTheActiveCastAndQueue()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "slow.preview", CastPoint = LevelValue.Constant(1f) });
            CastOrder order = new CastOrder { Caster = caster, AbilityName = ability.Definition.Name };
            rig.Engine.IssueOrder(order);
            order.Queue = true;
            rig.Engine.IssueOrder(order);
            int events = 0;
            rig.Engine.EventRaised += _ => events++;
            Assert.That(rig.Engine.EvaluateOrder(order).FailureReason, Is.EqualTo(CastFailureReason.Casting));
            Assert.That(rig.Engine.GetQueuedOrders(caster).Count, Is.EqualTo(1));
            Assert.That(ability.Phase, Is.EqualTo(AbilityPhase.Casting));
            Assert.That(events, Is.Zero);
        }

        [Test]
        public void CooldownDuration_CapturesStartValueAndChargeQueryTracksRecovery()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "timers", Cooldown = LevelValue.Constant(10f), Charges = new ChargeDefinition { MaxCharges = 2, RestoreTime = 4f, UsesCooldown = true } });
            rig.Engine.CastAbility(caster, "timers");
            rig.Tick(1f);
            ModifierDefinition reduction = new ModifierDefinition { Name = "cooldown.reduction", Duration = -1f };
            reduction.Properties[ModifierProperty.CooldownReductionPercent] = 50f;
            rig.Engine.RegisterModifierDefinition(reduction);
            rig.Engine.AddModifier(caster, caster, reduction.Name, -1f);
            Assert.That(ability.GetCooldown(), Is.EqualTo(5f));
            Assert.That(ability.CooldownDuration, Is.EqualTo(10f));
            Assert.That(ability.CooldownRemaining, Is.EqualTo(9f));
            Assert.That(ability.ChargeRestoreRemaining, Is.EqualTo(3f));
            rig.Tick(3f);
            Assert.That(ability.Charges, Is.EqualTo(2));
            Assert.That(ability.ChargeRestoreRemaining, Is.Zero);
            ability.StartCooldown(7f);
            Assert.That(ability.CooldownDuration, Is.EqualTo(7f));
        }

        private sealed class ProbeScript : AbilityScript
        {
            public int Filters;
            public int PhaseStarts;
            public int SpellStarts;
            public bool Reject;
            public override CastResult CastFilter(CastContext context)
            {
                Filters++;
                return Reject ? CastResult.Fail(CastFailureReason.CustomRejected, "Example filter rejection") : CastResult.Ok();
            }
            public override bool OnAbilityPhaseStart(CastContext context) { PhaseStarts++; return true; }
            public override void OnSpellStart(CastContext context) { SpellStarts++; ActionRunner.Execute(Ability.Definition.Actions, context); }
        }
    }
}
