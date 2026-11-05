using System;
using System.Collections.Generic;
using System.Linq;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    public sealed class SpatialQueryAudit20261105Tests
    {
        [Test]
        public void BroadCandidatesAreFilteredByActualRadiusAndIdentity()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem();
            engine.Initialize(world);
            var inside = new FakeUnit(1, 2, Vector3.right);
            var edge = new FakeUnit(2, 2, new Vector3(3, 0, 0));
            world.Units.AddRange(new IUnit[] { inside, new FakeUnit(1, 2, Vector3.right), edge, new FakeUnit(3, 2, new Vector3(4, 0, 0)) });
            var results = new List<IUnit>();
            engine.FindUnits(Vector3.zero, 3, null, results);
            Assert.That(results, Is.EqualTo(new IUnit[] { inside, edge }));
        }

        [Test]
        public void ExistingResultsStayInPlaceAndDuplicateColliderWrappersAreNotAppended()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem();
            engine.Initialize(world);
            var first = new FakeUnit(1, 2, Vector3.zero);
            var results = new List<IUnit> { first };
            for (int i = 1; i <= 1000; i++)
            {
                world.Units.Add(new FakeUnit(i, 2, Vector3.zero));
                world.Units.Add(new FakeUnit(i, 2, Vector3.zero));
            }
            engine.FindUnits(Vector3.zero, 1, null, results);
            Assert.That(results.Count, Is.EqualTo(1000));
            Assert.That(results[0], Is.SameAs(first));
            Assert.That(results.Select(unit => unit.EntityId).Distinct().Count(), Is.EqualTo(1000));
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(-1f)]
        public void InvalidRadiusDoesNotCallWorldOrAppend(float radius)
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem();
            engine.Initialize(world);
            world.Units.Add(new FakeUnit(1, 2, Vector3.zero));
            var results = new List<IUnit>();
            engine.FindUnits(Vector3.zero, radius, null, results);
            Assert.That(world.Calls, Is.Zero);
            Assert.That(results, Is.Empty);
        }

        [Test]
        public void ZeroRadiusAndNonFiniteCandidatePositionsHaveDefinedResults()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem();
            engine.Initialize(world);
            var exact = new FakeUnit(1, 2, Vector3.zero);
            world.Units.AddRange(new IUnit[] { exact, new FakeUnit(2, 2, Vector3.right), new FakeUnit(3, 2, new Vector3(float.NaN, 0, 0)), new FakeUnit(4, 2, new Vector3(float.PositiveInfinity, 0, 0)) });
            var results = new List<IUnit>();
            engine.FindUnits(Vector3.zero, 0, null, results);
            Assert.That(results, Is.EqualTo(new IUnit[] { exact }));
        }

        [Test]
        public void QueryCallbackClearingBattleDoesNotCommitOldCandidates()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem();
            engine.Initialize(world);
            var existing = new FakeUnit(99, 1, Vector3.zero);
            var results = new List<IUnit> { existing };
            world.Units.Add(new FakeUnit(1, 2, Vector3.zero));
            world.DuringQuery = () => engine.ClearRuntime();
            engine.FindUnits(Vector3.zero, 1, null, results);
            Assert.That(results, Is.EqualTo(new IUnit[] { existing }));
        }

        [Test]
        public void NestedWorldQueryKeepsItsOwnScratchAndIdentityMembership()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem();
            engine.Initialize(world);
            world.Units.AddRange(new IUnit[] { new FakeUnit(1, 2, Vector3.zero), new FakeUnit(1, 2, Vector3.zero), new FakeUnit(2, 2, Vector3.zero) });
            var nested = new List<IUnit>();
            world.DuringQuery = () => engine.FindUnits(Vector3.zero, 1, null, nested);
            var results = new List<IUnit>();
            engine.FindUnits(Vector3.zero, 1, null, results);
            Assert.That(nested.Count, Is.EqualTo(2));
            Assert.That(results.Count, Is.EqualTo(2));
        }

        [Test]
        public void RemovedAndReplacedUnitsAreExcludedEvenWithoutAnExplicitTargetQuery()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem();
            engine.Initialize(world);
            var old = new FakeUnit(1, 2, Vector3.zero);
            engine.RegisterUnit(old);
            engine.RemoveUnit(old);
            var current = new FakeUnit(1, 2, Vector3.zero);
            engine.RegisterUnit(current);
            world.Units.AddRange(new IUnit[] { old, current });
            var results = new List<IUnit>();
            engine.FindUnits(Vector3.zero, 1, null, results);
            Assert.That(results, Is.EqualTo(new IUnit[] { current }));
        }

        [Test]
        public void ConfiguredAoeCannotDamageBroadPhaseUnitsOutsideItsRadius()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem();
            engine.Initialize(world);
            var caster = new FakeUnit(1, 1, Vector3.zero);
            var inside = new FakeUnit(2, 2, Vector3.right);
            var outside = new FakeUnit(3, 2, new Vector3(10, 0, 0));
            world.Units.AddRange(new IUnit[] { caster, inside, outside });
            var definition = new AbilityDefinition { Name = "spatial.nova", Behavior = AbilityBehavior.NoTarget | AbilityBehavior.Aoe, AoeRadius = LevelValue.Constant(3), TargetTeam = TargetTeam.Enemy };
            definition.Actions.Add(new ActionDefinition { ActionType = ActionType.Damage, Target = ActionTarget.ContextTargets, DamageType = DamageType.Pure, Value = LevelValue.Constant(20) });
            engine.RegisterAbilityDefinition(definition);
            engine.AddAbility(caster, definition.Name);
            Assert.That(engine.IssueOrder(new CastOrder { Caster = caster, AbilityName = definition.Name }).Success, Is.True);
            Assert.That(inside.DamageTaken, Is.EqualTo(20));
            Assert.That(outside.DamageTaken, Is.Zero);
        }

        [Test]
        public void ThousandStaticTargetsDoNotSpendTheScriptBudgetOnBuiltInStateReads()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem { MaxExecutionOperations = 64 };
            engine.Initialize(world);
            var caster = new FakeUnit(2000, 1, Vector3.zero);
            engine.RegisterModifierDefinition(new ModifierDefinition { Name = "stats" });
            int aborts = 0;
            engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ExecutionAborted) aborts++; };
            for (int i = 1; i <= 1000; i++)
            {
                var unit = new FakeUnit(i, 2, Vector3.zero);
                engine.AddModifier(unit, unit, "stats");
                world.Units.Add(unit);
                world.Units.Add(unit);
            }
            var results = new List<IUnit>();
            engine.FindUnits(Vector3.zero, 1, new TargetQuery { Engine = engine, Caster = caster, Team = TargetTeam.Enemy }, results);
            Assert.That(aborts, Is.Zero);
            Assert.That(results.Count, Is.EqualTo(1000));
        }

        [Test]
        public void RecursiveCustomStateStillAbortsAndQueryFailsClosed()
        {
            var world = new BroadWorld();
            var engine = new AbilitySystem { MaxExecutionDepth = 8, MaxExecutionOperations = 64 };
            engine.Initialize(world);
            var unit = new FakeUnit(1, 2, Vector3.zero);
            world.Units.Add(unit);
            var script = new RecursiveState();
            engine.RegisterModifierDefinition(new ModifierDefinition { Name = "recursive" });
            engine.RegisterModifierScript("recursive", () => script);
            engine.AddModifier(unit, unit, "recursive");
            int aborts = 0;
            engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ExecutionAborted) aborts++; };
            var results = new List<IUnit>();
            script.Armed = true;
            engine.FindUnits(Vector3.zero, 1, new TargetQuery { Engine = engine, Team = TargetTeam.Enemy }, results);
            Assert.That(aborts, Is.EqualTo(1));
            Assert.That(results, Is.Empty);
            script.Armed = false;
            engine.FindUnits(Vector3.zero, 1, new TargetQuery { Engine = engine, Team = TargetTeam.Enemy }, results);
            Assert.That(results.Count, Is.EqualTo(1));
        }

        private sealed class RecursiveState : ModifierScript
        {
            public bool Armed;
            public override bool CheckState(UnitState state) => Armed && Engine.HasState(Parent, state);
        }

        private sealed class BroadWorld : IWorld
        {
            public readonly List<IUnit> Units = new List<IUnit>();
            public Action DuringQuery;
            public int Calls;
            public float Time => 0;
            public bool HasLineOfSight(IUnit viewer, Vector3 position) => true;
            public void FindUnits(Vector3 center, float radius, TargetQuery query, IList<IUnit> results)
            {
                Calls++;
                Action callback = DuringQuery;
                DuringQuery = null;
                callback?.Invoke();
                foreach (IUnit unit in Units) results.Add(unit);
            }
        }
    }
}
