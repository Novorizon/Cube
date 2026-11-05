using System;
using System.Collections;
using System.Reflection;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class CombatAudit20261105Tests
    {
        [Test]
        public void RemovedDamageSourceCannotCommitDamage()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterUnit(attacker);
            rig.Engine.RemoveUnit(attacker);

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Pure });

            Assert.That(result.Blocked, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
        }

        [Test]
        public void ReusedEntityIdDoesNotAuthorizeTheOldDamageSource()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterUnit(attacker);
            rig.Engine.RemoveUnit(attacker);
            rig.Engine.RegisterUnit(new FakeUnit(attacker.EntityId, attacker.TeamId, attacker.Position));

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Pure });

            Assert.That(result.Blocked, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
        }

        [Test]
        public void RemovedHealSourceCannotCommitHealing()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(1, Vector3.right);
            rig.Engine.RegisterUnit(source);
            rig.Engine.RemoveUnit(source);

            HealResult result = rig.Engine.HealWithResult(new HealInfo { Source = source, Target = target, Amount = 20f });

            Assert.That(result.Applied, Is.False);
            Assert.That(target.HealingReceived, Is.Zero);
        }

        [Test]
        public void DeadRegisteredAndEnvironmentSourcesRemainValid()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterUnit(source);
            source.IsAlive = false;

            DamageResult damage = rig.Engine.ApplyDamage(new DamageInfo { Attacker = source, Victim = target, Amount = 20f, DamageType = DamageType.Pure });
            HealResult healing = rig.Engine.HealWithResult(new HealInfo { Source = source, Target = target, Amount = 10f });
            DamageResult environment = rig.Engine.ApplyDamage(new DamageInfo { Victim = target, Amount = 5f, DamageType = DamageType.Pure });

            Assert.That(damage.AppliedAmount, Is.EqualTo(20f));
            Assert.That(healing.AppliedAmount, Is.EqualTo(10f));
            Assert.That(environment.AppliedAmount, Is.EqualTo(5f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void CombatRejectsForeignOrPreviousBattleAbilitySources(bool previousBattle)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AbilityTestRig foreign = previousBattle ? rig : new AbilityTestRig();
            FakeUnit abilityOwner = previousBattle ? owner : foreign.AddUnit(1, Vector3.zero);
            Game.Ability.Ability ability = foreign.RegisterAndAddAbility(abilityOwner, new AbilityDefinition { Name = "historical" });
            if (previousBattle)
            {
                rig.Engine.ClearRuntime();
                rig.Engine.RegisterUnit(owner);
                rig.Engine.RegisterUnit(target);
            }

            DamageResult damage = rig.Engine.ApplyDamage(new DamageInfo { Attacker = owner, Victim = target, Ability = ability, Amount = 20f, DamageType = DamageType.Pure });
            HealResult healing = rig.Engine.HealWithResult(new HealInfo { Source = owner, Target = target, Ability = ability, Amount = 10f });
            AttackRecord attack = rig.Engine.BeginAttack(new DamageInfo { Attacker = owner, Victim = target, Ability = ability, Amount = 20f });

            Assert.That(damage.Blocked, Is.True);
            Assert.That(healing.Applied, Is.False);
            Assert.That(attack, Is.Null);
            Assert.That(target.DamageTaken, Is.Zero);
            Assert.That(target.HealingReceived, Is.Zero);
        }

        [Test]
        public void RemovedAbilityRemainsAValidSameBattleAttribution()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(owner, new AbilityDefinition { Name = "historical" });
            rig.Engine.RemoveAbility(owner, ability.Definition.Name);

            DamageResult damage = rig.Engine.ApplyDamage(new DamageInfo { Attacker = owner, Victim = target, Ability = ability, Amount = 20f, DamageType = DamageType.Pure });
            HealResult healing = rig.Engine.HealWithResult(new HealInfo { Source = owner, Target = target, Ability = ability, Amount = 10f });

            Assert.That(damage.AppliedAmount, Is.EqualTo(20f));
            Assert.That(healing.AppliedAmount, Is.EqualTo(10f));
        }

        [Test]
        public void SourceRemovalDuringCalculationCancelsTheUncommittedHit()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit victim = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterUnit(attacker);
            Listen(rig, attacker, "remove_source", value =>
            {
                if (value.EventType == ModifierEventType.DamageCalculated) rig.Engine.RemoveUnit(attacker);
            });

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Pure });

            Assert.That(result.Blocked, Is.True);
            Assert.That(victim.DamageTaken, Is.Zero);
        }

        [Test]
        public void AttackCommitSurvivesThrowingPostDamageObserver()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            PreciseVictim victim = AddVictim(rig);
            int landed = 0;
            Listen(rig, attacker, "throw_after_commit", value =>
            {
                if (value.EventType == ModifierEventType.DamageTaken) throw new InvalidOperationException("Post-commit observer failed.");
                if (value.EventType == ModifierEventType.AttackLanded) landed++;
            });
            AttackRecord attack = rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Pure });
            bool threw = false;

            try { rig.Engine.ResolveAttack(attack); }
            catch (AggregateException) { threw = true; }

            Assert.That(threw, Is.True);
            Assert.That(victim.CurrentHealth, Is.EqualTo(80f));
            Assert.That(attack.IsResolved, Is.True);
            Assert.That(attack.Result, Is.Not.Null);
            Assert.That(attack.Result.AppliedAmount, Is.EqualTo(20f));
            Assert.That(landed, Is.EqualTo(1));
            DamageResult repeated = rig.Engine.ResolveAttack(attack);
            Assert.That(repeated.AppliedAmount, Is.EqualTo(20f));
            Assert.That(victim.CurrentHealth, Is.EqualTo(80f));
        }

        [Test]
        public void PreCommitAttackFailureDoesNotLeaveAnUnresolvableToken()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            PreciseVictim victim = AddVictim(rig);
            Listen(rig, attacker, "throw_before_commit", value =>
            {
                if (value.EventType == ModifierEventType.DamageCalculated) throw new InvalidOperationException("Pre-commit observer failed.");
            });
            AttackRecord attack = rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Pure });
            bool threw = false;

            try { rig.Engine.ResolveAttack(attack); }
            catch (InvalidOperationException) { threw = true; }
            catch (AggregateException) { threw = true; }

            Assert.That(threw, Is.True);
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f));
            Assert.That(attack.IsCancelled || attack.IsResolved, Is.True);
            Assert.That(attack.IsPending, Is.False);
            Assert.That(rig.Engine.ResolveAttack(attack).Blocked, Is.True);
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void BattleClearAfterCommitRetainsAttackFactWithoutLateLanded()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            PreciseVictim victim = AddVictim(rig);
            int landed = 0;
            Listen(rig, attacker, "clear_after_commit", value =>
            {
                if (value.EventType == ModifierEventType.DamageTaken)
                {
                    rig.Engine.ClearRuntime();
                    throw new InvalidOperationException("Observer failed after settlement.");
                }
                if (value.EventType == ModifierEventType.AttackLanded) landed++;
            });
            AttackRecord attack = rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Pure });
            bool threw = false;

            try { rig.Engine.ResolveAttack(attack); }
            catch (AggregateException) { threw = true; }

            Assert.That(threw, Is.True);
            Assert.That(attack.IsResolved, Is.True);
            Assert.That(attack.Result.AppliedAmount, Is.EqualTo(20f));
            Assert.That(victim.CurrentHealth, Is.EqualTo(80f));
            Assert.That(landed, Is.Zero);
            Assert.That(rig.Engine.Modifiers.Count, Is.Zero);
        }

        [Test]
        public void SourceRemovalAfterLethalCommitDoesNotSkipVictimDeathCleanup()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            FakeUnit observer = rig.AddUnit(1, Vector3.left);
            rig.Engine.RegisterUnit(attacker);
            PreciseVictim victim = AddVictim(rig);
            int deaths = 0;
            Listen(rig, observer, "remove_source_after_commit", value =>
            {
                if (value.EventType == ModifierEventType.DamageTaken) rig.Engine.RemoveUnit(attacker);
                if (value.EventType == ModifierEventType.Death) deaths++;
            });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "victim_death_cleanup", Duration = -1f, RemoveOnDeath = true });
            Modifier effect = rig.Engine.AddModifier(victim, victim, "victim_death_cleanup", -1f);

            DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 200f, DamageType = DamageType.Pure });

            Assert.That(result.Killed, Is.True);
            Assert.That(result.AppliedAmount, Is.EqualTo(100f));
            Assert.That(effect.IsDestroyed, Is.True);
            Assert.That(deaths, Is.EqualTo(1));
        }

        [Test]
        public void ThrowingAttackStartDoesNotLeakPendingAttackRecords()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit attacker = rig.AddUnit(1, Vector3.zero);
            PreciseVictim victim = AddVictim(rig);
            Listen(rig, attacker, "throw_at_launch", value =>
            {
                if (value.EventType == ModifierEventType.AttackStart) throw new InvalidOperationException("Launch observer failed.");
            });
            bool threw = false;

            try { rig.Engine.BeginAttack(new DamageInfo { Attacker = attacker, Victim = victim, Amount = 20f, DamageType = DamageType.Pure }); }
            catch (InvalidOperationException) { threw = true; }
            catch (AggregateException) { threw = true; }

            Assert.That(threw, Is.True);
            FieldInfo field = typeof(AbilitySystem).GetField("pendingAttacks", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(((IDictionary)field.GetValue(rig.Engine)).Count, Is.Zero);
            Assert.That(victim.CurrentHealth, Is.EqualTo(100f));
        }

        [Test]
        public void ThrowingHealingObserverDoesNotHideTheAppliedFactEvent()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit source = rig.AddUnit(1, Vector3.zero);
            PreciseVictim target = AddVictim(rig);
            rig.Engine.ApplyDamage(new DamageInfo { Victim = target, Amount = 40f, DamageType = DamageType.Pure });
            Listen(rig, source, "throw_after_heal", value =>
            {
                if (value.EventType == ModifierEventType.Healed) throw new InvalidOperationException("Post-heal observer failed.");
            });
            int appliedEvents = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.HealApplied) appliedEvents++; };
            bool threw = false;

            try { rig.Engine.HealWithResult(new HealInfo { Source = source, Target = target, Amount = 20f }); }
            catch (AggregateException) { threw = true; }

            Assert.That(threw, Is.True);
            Assert.That(target.CurrentHealth, Is.EqualTo(80f));
            Assert.That(appliedEvents, Is.EqualTo(1));
        }

        private static PreciseVictim AddVictim(AbilityTestRig rig)
        {
            PreciseVictim victim = new PreciseVictim(100);
            rig.World.AddUnit(victim);
            rig.Engine.RegisterUnit(victim);
            return victim;
        }

        private static void Listen(AbilityTestRig rig, IUnit owner, string name, Action<ModifierEvent> callback)
        {
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = name, Duration = -1f, RemoveOnDeath = false });
            rig.Engine.RegisterModifierScript(name, () => new Observer(callback));
            rig.Engine.AddModifier(owner, owner, name, -1f);
        }

        private sealed class Observer : ModifierScript
        {
            private readonly Action<ModifierEvent> callback;
            public Observer(Action<ModifierEvent> callback) { this.callback = callback; }
            public override void OnEvent(ModifierEvent value) => callback(value);
        }

        private sealed class PreciseVictim : ICombatUnit
        {
            public PreciseVictim(int id) { EntityId = id; }
            public int EntityId { get; }
            public int TeamId => 2;
            public UnitType UnitType => UnitType.Basic;
            public bool IsAlive => CurrentHealth > 0f;
            public bool IsMagicImmune => false;
            public bool IsInvulnerable => false;
            public Vector3 Position => Vector3.right;
            public float CurrentHealth { get; private set; } = 100f;
            public float MaxHealth => 100f;
            public float BaseArmor => 0f;
            public float BaseMagicResistancePercent => 0f;
            public float BasePhysicalDamageBlock => 0f;
            public bool IsVisibleToTeam(int teamId) => true;
            public void ApplyDamage(DamageResult value) => ApplyFinalDamage(value);
            public void Heal(HealInfo value) => ApplyFinalHeal(value);
            public DamageApplicationResult ApplyFinalDamage(DamageResult value)
            {
                float before = CurrentHealth;
                CurrentHealth = Math.Max(0f, before - value.FinalAmount);
                return new DamageApplicationResult(before - CurrentHealth, CurrentHealth == 0f);
            }
            public HealApplicationResult ApplyFinalHeal(HealInfo value)
            {
                float before = CurrentHealth;
                CurrentHealth = Math.Min(MaxHealth, before + value.Amount);
                return new HealApplicationResult(CurrentHealth - before);
            }
        }
    }
}
