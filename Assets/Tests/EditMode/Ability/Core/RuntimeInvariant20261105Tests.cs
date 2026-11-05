using System;
using System.Collections.Generic;
using System.Linq;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    [Category("Stateful")]
    // Each family owns a small independent oracle: an explicit live set, absolute deadlines,
    // or an HP debit/credit ledger. Seeds and the last operations accompany every failure.
    public sealed class RuntimeInvariant20261105Tests
    {
        [TestCase(17)]
        [TestCase(20261105)]
        [TestCase(910201)]
        public void ModifierMembershipAndCallbacksFollowTheIndependentLiveSet(int seed)
        {
            Sequence sequence = new Sequence(seed);
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit[] units = Enumerable.Range(0, 5).Select(i => rig.AddUnit(i % 2 + 1, new Vector3(i, 0, 0))).ToArray();
            List<ModifierProbe> probes = new List<ModifierProbe>();
            Dictionary<Modifier, ModifierExpected> expected = new Dictionary<Modifier, ModifierExpected>();
            string[] names = { "sequence.root", "sequence.silence", "sequence.stat" };
            float[] armor = { 3f, -2f, 5f };
            UnitState[] states = { UnitState.Rooted, UnitState.Silenced, UnitState.None };
            for (int i = 0; i < names.Length; i++)
            {
                ModifierDefinition definition = new ModifierDefinition { Name = names[i], Duration = -1f, Interval = 0.25f, MaxStack = 3, Attributes = ModifierAttribute.Multiple, States = states[i] };
                definition.Properties.Add(ModifierProperty.ArmorBonus, armor[i]);
                rig.Engine.RegisterModifierDefinition(definition);
                rig.Engine.RegisterModifierScript(definition.Name, () => { ModifierProbe probe = new ModifierProbe(); probes.Add(probe); return probe; });
            }
            foreach (FakeUnit unit in units) rig.Engine.RegisterUnit(unit);
            int aborts = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ExecutionAborted) aborts++; };

            for (int step = 0; step < 1500; step++)
            {
                int operation = sequence.Random.Next(12);
                int unitIndex = sequence.Random.Next(units.Length);
                FakeUnit unit = units[unitIndex];
                sequence.Note(step, "modifier op=" + operation + " unit=" + unit.EntityId + " live=" + expected.Count);
                if (operation <= 3)
                {
                    int kind = sequence.Random.Next(names.Length);
                    Modifier modifier = rig.Engine.AddModifier(unit, unit, names[kind]);
                    sequence.Require(modifier != null, "A registered alive unit accepts the permanent modifier.");
                    expected.Add(modifier, new ModifierExpected { Parent = unit, Armor = armor[kind], State = states[kind], Stacks = 1 });
                }
                else if (operation <= 5 && expected.Count > 0)
                {
                    Modifier modifier = expected.Keys.ElementAt(sequence.Random.Next(expected.Count));
                    sequence.Require(rig.Engine.RemoveModifier(modifier), "Removing a live member succeeds once.");
                    expected.Remove(modifier);
                    sequence.Require(!rig.Engine.RemoveModifier(modifier), "Removing the same instance twice cannot succeed.");
                }
                else if (operation == 6 && expected.Count > 0)
                {
                    Modifier modifier = expected.Keys.ElementAt(sequence.Random.Next(expected.Count));
                    int count = sequence.Random.Next(1, 4);
                    modifier.SetStackCount(count);
                    expected[modifier].Stacks = count;
                }
                else if (operation == 7)
                {
                    unit.IsAlive = false;
                    rig.Engine.NotifyDeath(unit);
                    foreach (Modifier modifier in expected.Where(pair => ReferenceEquals(pair.Value.Parent, unit)).Select(pair => pair.Key).ToArray()) expected.Remove(modifier);
                    unit.IsAlive = true;
                    rig.Engine.NotifyRespawn(unit);
                }
                else if (operation == 8)
                {
                    rig.Engine.RemoveUnit(unit);
                    foreach (Modifier modifier in expected.Where(pair => ReferenceEquals(pair.Value.Parent, unit)).Select(pair => pair.Key).ToArray()) expected.Remove(modifier);
                    FakeUnit replacement = new FakeUnit(unit.EntityId, unit.TeamId, unit.Position);
                    units[unitIndex] = replacement;
                    rig.World.AddUnit(replacement);
                    rig.Engine.RegisterUnit(replacement);
                }
                else if (operation == 9)
                {
                    rig.Engine.ClearRuntime();
                    expected.Clear();
                    sequence.Require(rig.Engine.Modifiers.Count == 0 && rig.Engine.Projectiles.Count == 0 && rig.Engine.Thinkers.Count == 0, "Clear releases all runtime object containers.");
                    foreach (FakeUnit current in units) rig.Engine.RegisterUnit(current);
                }
                else rig.Tick((sequence.Random.Next(4) + 1) * 0.125f);

                sequence.Require(rig.Engine.Modifiers.Count == expected.Count && rig.Engine.Modifiers.All(value => expected.ContainsKey(value)), "The public live set agrees with command-owned membership.");
                foreach (FakeUnit current in units)
                {
                    ModifierExpected[] entries = expected.Values.Where(value => ReferenceEquals(value.Parent, current)).ToArray();
                    float expectedArmor = entries.Sum(value => value.Armor * value.Stacks);
                    sequence.Close(rig.Engine.GetModifierProperty(current, ModifierProperty.ArmorBonus, null), expectedArmor, "The parent index has no missing or stale stat entries.");
                    bool rooted = rig.Engine.HasState(current, UnitState.Rooted);
                    bool silenced = rig.Engine.HasState(current, UnitState.Silenced);
                    sequence.Require(rooted == entries.Any(value => value.State == UnitState.Rooted), "Root state follows live membership; id=" + current.EntityId + " actual=" + rooted);
                    sequence.Require(silenced == entries.Any(value => value.State == UnitState.Silenced), "Silence state follows live membership; id=" + current.EntityId + " actual=" + silenced);
                }
                foreach (ModifierProbe probe in probes) sequence.Require(probe.DestroyCalls <= 1 && probe.CallbacksAfterDestroy == 0, "Removed scripts receive no later periodic/property/event callbacks.");
                foreach (Modifier modifier in expected.Keys) sequence.Require(!modifier.IsDestroyed, "The command model never retains a destroyed instance.");
                sequence.Require(aborts == 0, "An ordinary bounded sequence must not use execution-budget truncation.");
            }
            rig.Engine.ClearRuntime();
            foreach (ModifierProbe probe in probes) sequence.Require(probe.DestroyCalls == 1, "Final cleanup destroys every created script exactly once.");
            rig.Tick(1f);
            foreach (ModifierProbe probe in probes) sequence.Require(probe.CallbacksAfterDestroy == 0, "A later tick cannot revive released callbacks.");
        }

        [TestCase(31)]
        [TestCase(20261105)]
        [TestCase(998177)]
        public void InstantCastsMatchAnAbsoluteDeadlineAndResourceLedger(int seed)
        {
            Sequence sequence = new Sequence(seed);
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            Resources resources = new Resources { Mana = 200f };
            List<AbilityProbe> probes = new List<AbilityProbe>();
            AbilityDefinition manaDefinition = new AbilityDefinition { Name = "sequence.mana", Behavior = AbilityBehavior.NoTarget, Cooldown = LevelValue.Constant(0.5f), ManaCost = LevelValue.Constant(7f) };
            AbilityDefinition chargeDefinition = new AbilityDefinition { Name = "sequence.charges", Behavior = AbilityBehavior.NoTarget, Charges = new ChargeDefinition { MaxCharges = 3, RestoreTime = 0.5f, UsesCooldown = false, StartFull = true } };
            rig.Engine.RegisterAbilityDefinition(manaDefinition);
            rig.Engine.RegisterAbilityDefinition(chargeDefinition);
            foreach (string name in new[] { manaDefinition.Name, chargeDefinition.Name })
                rig.Engine.RegisterAbilityScript(name, () => { AbilityProbe probe = new AbilityProbe(); probes.Add(probe); return probe; });
            Game.Ability.Ability mana = rig.Engine.AddAbility(owner, manaDefinition.Name, 1, resources);
            Game.Ability.Ability charged = rig.Engine.AddAbility(owner, chargeDefinition.Name);
            double time = 0, readyAt = 0;
            double? nextChargeAt = null;
            int charges = 3, spends = 0, spells = 0;
            float expectedMana = 200f;
            bool activated = true, trained = true, alive = true;

            for (int step = 0; step < 2500; step++)
            {
                int operation = sequence.Random.Next(13);
                sequence.Note(step, "cast op=" + operation + " t=" + time + " mana=" + expectedMana + " charges=" + charges);
                if (operation <= 2)
                {
                    bool allowed = alive && activated && trained && time >= readyAt && expectedMana >= 7f;
                    CastResult preview = rig.Engine.EvaluateOrder(new CastOrder { Caster = owner, AbilityName = manaDefinition.Name });
                    sequence.Require(preview.Success == allowed, "Preview agrees with the independent immediate-cast ledger.");
                    CastResult result = rig.Engine.CastAbility(owner, manaDefinition.Name);
                    sequence.Require(result.Success == allowed, "Immediate cast outcome follows availability, deadline and resources.");
                    if (allowed) { expectedMana -= 7f; spends++; spells++; readyAt = time + 0.5; }
                }
                else if (operation == 3)
                {
                    bool allowed = alive && charges > 0;
                    CastResult result = rig.Engine.CastAbility(owner, chargeDefinition.Name);
                    sequence.Require(result.Success == allowed, "Charge availability is independent of the other skill's activation and cost.");
                    if (allowed)
                    {
                        if (charges == 3) nextChargeAt = time + 0.5;
                        charges--;
                        spells++;
                    }
                }
                else if (operation <= 6)
                {
                    float delta = (sequence.Random.Next(4) + 1) * 0.125f;
                    time += delta;
                    while (nextChargeAt.HasValue && nextChargeAt.Value <= time && charges < 3)
                    {
                        charges++;
                        nextChargeAt = charges == 3 ? (double?)null : nextChargeAt.Value + 0.5;
                    }
                    rig.Tick(delta);
                }
                else if (operation == 7)
                {
                    activated = !activated;
                    mana.SetActivated(activated);
                }
                else if (operation == 8)
                {
                    trained = !trained;
                    mana.SetLevel(trained ? 1 : 0);
                }
                else if (operation == 9)
                {
                    alive = !alive;
                    owner.IsAlive = alive;
                    if (alive) rig.Engine.NotifyRespawn(owner); else rig.Engine.NotifyDeath(owner);
                }
                else if (operation == 10)
                {
                    expectedMana = sequence.Random.Next(0, 31);
                    resources.Mana = expectedMana;
                    charged.RestoreFullCharges();
                    charges = 3;
                    nextChargeAt = null;
                }
                else if (operation == 11)
                {
                    mana.EndCooldown();
                    readyAt = time;
                }
                else
                {
                    if (sequence.Random.Next(2) == 0) rig.Engine.ClearRuntime();
                    else { rig.Engine.RemoveAbility(owner, manaDefinition.Name); rig.Engine.RemoveAbility(owner, chargeDefinition.Name); }
                    rig.Engine.RegisterUnit(owner);
                    mana = rig.Engine.AddAbility(owner, manaDefinition.Name, 1, resources);
                    charged = rig.Engine.AddAbility(owner, chargeDefinition.Name);
                    activated = trained = true;
                    readyAt = time;
                    charges = 3;
                    nextChargeAt = null;
                }

                sequence.Close(resources.Mana, expectedMana, "Only committed paid casts change mana.");
                sequence.Require(resources.SpendCount == spends, "Each committed paid cast spends exactly once.");
                sequence.Require(probes.Sum(value => value.SpellCalls) == spells, "Each accepted instant cast executes exactly once.");
                sequence.Close(mana.CooldownRemaining, (float)Math.Max(0, readyAt - time), "Cooldown matches an absolute deadline.");
                sequence.Require(charged.Charges == charges && charged.Charges >= 0 && charged.Charges <= 3, "Charges match a serial replenishment schedule and remain bounded.");
                sequence.Close(charged.ChargeRestoreRemaining, nextChargeAt.HasValue ? (float)Math.Max(0, nextChargeAt.Value - time) : 0f, "The charge UI query matches the replenishment deadline.");
                sequence.Require(probes.All(value => value.RemoveCalls <= 1 && value.CallbacksAfterRemove == 0), "Removed abilities never execute again.");
            }
            rig.Engine.ClearRuntime();
            sequence.Require(rig.Engine.GetAbilities(owner).Count == 0 && rig.Engine.GetQueuedOrders(owner).Count == 0, "Final cleanup releases ability ownership and queued commands.");
            sequence.Require(probes.All(value => value.RemoveCalls == 1), "All ability scripts are released exactly once.");
        }

        [TestCase(41)]
        [TestCase(20261105)]
        [TestCase(38191)]
        public void PreciseDamageHealingAndStaleEntitiesMatchTheHpLedger(int seed)
        {
            Sequence sequence = new Sequence(seed);
            AbilityTestRig rig = new AbilityTestRig();
            LedgerUnit[] units = Enumerable.Range(0, 6).Select(i => new LedgerUnit(i + 100, 200f)).ToArray();
            float[] health = Enumerable.Repeat(200f, units.Length).ToArray();
            bool[] registered = Enumerable.Repeat(true, units.Length).ToArray();
            List<LedgerUnit> retired = new List<LedgerUnit>();
            int expectedDamageEvents = 0, expectedHealEvents = 0, damageEvents = 0, healEvents = 0;
            foreach (LedgerUnit unit in units) rig.Engine.RegisterUnit(unit);
            rig.Engine.EventRaised += value =>
            {
                if (value.EventType == RuntimeEventType.DamageApplied) damageEvents++;
                if (value.EventType == RuntimeEventType.HealApplied) healEvents++;
            };
            for (int step = 0; step < 2000; step++)
            {
                int index = sequence.Random.Next(units.Length);
                LedgerUnit unit = units[index];
                int operation = sequence.Random.Next(12);
                float amount = sequence.Random.Next(1, 101);
                sequence.Note(step, "combat op=" + operation + " id=" + unit.EntityId + " hp=" + health[index] + " amount=" + amount);
                if (operation <= 4)
                {
                    bool nonLethal = operation == 4;
                    bool bypassInvulnerability = operation == 3;
                    float applied = registered[index] && health[index] > 0 && (!unit.IsInvulnerable || bypassInvulnerability)
                        ? Math.Min(amount, Math.Max(0f, health[index] - (nonLethal ? 1f : 0f))) : 0f;
                    DamageFlags flags = (nonLethal ? DamageFlags.NonLethal : DamageFlags.None) | (bypassInvulnerability ? DamageFlags.IgnoreInvulnerable : DamageFlags.None);
                    DamageResult result = rig.Engine.ApplyDamage(new DamageInfo { Victim = unit, Amount = amount, DamageType = DamageType.Pure, Flags = flags });
                    health[index] -= applied;
                    sequence.Close(result.AppliedAmount, applied, "Reported damage equals the independent HP debit.");
                    if (applied > 0) expectedDamageEvents++;
                }
                else if (operation <= 6)
                {
                    float applied = registered[index] && health[index] > 0 ? Math.Min(amount, 200f - health[index]) : 0f;
                    HealResult result = rig.Engine.HealWithResult(new HealInfo { Target = unit, Amount = amount });
                    health[index] += applied;
                    sequence.Close(result.AppliedAmount, applied, "Reported healing equals the independent capped HP credit.");
                    if (applied > 0) expectedHealEvents++;
                }
                else if (operation == 7) unit.IsInvulnerable = !unit.IsInvulnerable;
                else if (operation == 8)
                {
                    health[index] = sequence.Random.Next(1, 201);
                    unit.Health = health[index];
                    rig.Engine.NotifyRespawn(unit);
                }
                else if (operation == 9)
                {
                    rig.Engine.RemoveUnit(unit);
                    registered[index] = false;
                }
                else if (operation == 10)
                {
                    retired.Add(unit);
                    units[index] = new LedgerUnit(unit.EntityId, 200f);
                    health[index] = 200f;
                    registered[index] = true;
                    rig.Engine.RegisterUnit(units[index]);
                }
                else
                {
                    rig.Engine.ClearRuntime();
                    for (int i = 0; i < units.Length; i++) { rig.Engine.RegisterUnit(units[i]); registered[i] = true; }
                }
                for (int i = 0; i < units.Length; i++) sequence.Close(units[i].CurrentHealth, health[i], "Adapters and the HP ledger agree for every unit.");
                if (retired.Count > 0)
                {
                    LedgerUnit stale = retired[sequence.Random.Next(retired.Count)];
                    float before = stale.CurrentHealth;
                    DamageResult late = rig.Engine.ApplyDamage(new DamageInfo { Victim = stale, Amount = 1f, DamageType = DamageType.Pure });
                    sequence.Close(late.AppliedAmount, 0f, "A stale unit object cannot receive late damage after ID replacement.");
                    sequence.Close(stale.CurrentHealth, before, "Stale entity health remains frozen.");
                }
                sequence.Require(damageEvents == expectedDamageEvents && healEvents == expectedHealEvents, "Committed HP facts each produce exactly one corresponding public event.");
            }
            rig.Engine.ClearRuntime();
            sequence.Require(rig.Engine.Modifiers.Count == 0 && rig.Engine.Projectiles.Count == 0 && rig.Engine.Thinkers.Count == 0, "Final combat cleanup has no runtime objects.");
        }

        private sealed class Sequence
        {
            internal readonly System.Random Random;
            private readonly int seed;
            private readonly Queue<string> trace = new Queue<string>();
            internal Sequence(int seed) { this.seed = seed; Random = new System.Random(seed); }
            internal void Note(int step, string operation) { trace.Enqueue("step=" + step + " " + operation); if (trace.Count > 24) trace.Dequeue(); }
            internal void Require(bool condition, string message)
            {
                if (!condition) throw new InvalidOperationException(message + "\nseed=" + seed + "\n" + string.Join("\n", trace));
            }
            internal void Close(float actual, float expected, string message) => Require(Math.Abs(actual - expected) < 0.0001f, message + " expected=" + expected + " actual=" + actual);
        }

        private sealed class ModifierExpected { internal IUnit Parent; internal float Armor; internal UnitState State; internal int Stacks; }

        private sealed class ModifierProbe : ModifierScript
        {
            internal int DestroyCalls, CallbacksAfterDestroy;
            private void Touch() { if (DestroyCalls != 0) CallbacksAfterDestroy++; }
            public override void OnDestroy() { DestroyCalls++; }
            public override void OnIntervalThink() { Touch(); }
            public override void OnEvent(ModifierEvent value) { Touch(); }
            public override float GetProperty(ModifierProperty property, ModifierPropertyContext context) { Touch(); return base.GetProperty(property, context); }
            public override bool CheckState(UnitState state) { Touch(); return base.CheckState(state); }
        }

        private sealed class AbilityProbe : AbilityScript
        {
            internal int RemoveCalls, SpellCalls, CallbacksAfterRemove;
            public override void OnRemoved() { RemoveCalls++; }
            public override void OnSpellStart(CastContext context) { if (RemoveCalls != 0) CallbacksAfterRemove++; SpellCalls++; }
        }

        private sealed class Resources : IResourceOwner
        {
            public float Mana { get; set; }
            internal int SpendCount;
            public bool HasMana(float amount) => amount <= Mana;
            public bool SpendMana(float amount) { if (!HasMana(amount)) return false; Mana -= amount; SpendCount++; return true; }
        }

        private sealed class LedgerUnit : ICombatUnit
        {
            internal LedgerUnit(int entityId, float health) { EntityId = entityId; Health = health; }
            internal float Health;
            public int EntityId { get; }
            public int TeamId => 2;
            public UnitType UnitType => UnitType.Hero;
            public bool IsAlive => Health > 0f;
            public bool IsMagicImmune => false;
            public bool IsInvulnerable { get; set; }
            public Vector3 Position => Vector3.zero;
            public float CurrentHealth => Health;
            public float MaxHealth => 200f;
            public float BaseArmor => 0f;
            public float BaseMagicResistancePercent => 0f;
            public float BasePhysicalDamageBlock => 0f;
            public bool IsVisibleToTeam(int teamId) => true;
            public void ApplyDamage(DamageResult result) { ApplyFinalDamage(result); }
            public void Heal(HealInfo info) { ApplyFinalHeal(info); }
            public DamageApplicationResult ApplyFinalDamage(DamageResult result)
            {
                float before = Health;
                Health = Math.Max(0f, before - result.FinalAmount);
                return new DamageApplicationResult(before - Health, before > 0f && Health <= 0f);
            }
            public HealApplicationResult ApplyFinalHeal(HealInfo info)
            {
                float before = Health;
                Health = Math.Min(200f, before + info.Amount);
                return new HealApplicationResult(Health - before);
            }
        }
    }
}
