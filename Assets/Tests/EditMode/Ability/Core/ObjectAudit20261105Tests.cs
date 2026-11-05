using System;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class ObjectAudit20261105Tests
    {
        [TestCase("modifier")]
        [TestCase("thinker")]
        [TestCase("projectile")]
        public void RuntimeObjectsRejectAbilityOwnedByAnotherEngine(string kind)
        {
            AbilityTestRig first = new AbilityTestRig();
            FakeUnit originalOwner = first.AddUnit(1, Vector3.zero);
            Game.Ability.Ability foreign = first.RegisterAndAddAbility(originalOwner, new AbilityDefinition { Name = "foreign" });
            AbilityTestRig second = new AbilityTestRig();
            FakeUnit localOwner = second.AddUnit(1, Vector3.zero);
            FakeUnit localTarget = second.AddUnit(2, Vector3.right);
            second.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "local.buff" });

            object created = kind == "modifier" ? (object)second.Engine.AddModifier(localOwner, localTarget, foreign, "local.buff")
                : kind == "thinker" ? second.Engine.CreateThinker(new ThinkerRequest { Ability = foreign, Caster = localOwner, Position = Vector3.zero, Duration = 1f })
                : second.Engine.CreateTrackingProjectile(foreign, localOwner, localTarget, new ProjectileDefinition { Speed = 10f });

            Assert.That(created, Is.Null, "A foreign engine's ability must not enter this runtime's object graph.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ArrivalPresentationThatKillsTargetDoesNotEmitProjectileHit(bool tracking)
        {
            AbilityTestRig rig = new AbilityTestRig();
            MovingPresentation presentation = new MovingPresentation();
            rig.Engine.Initialize(rig.World, presentation);
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            int hits = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ProjectileHit) hits++; };
            ProjectileDefinition definition = new ProjectileDefinition { Speed = 10f, Radius = 0.1f, Distance = 20f, EffectName = "moving" };
            Projectile projectile = tracking
                ? rig.Engine.CreateTrackingProjectile(null, caster, target, definition)
                : rig.Engine.CreateLinearProjectile(null, caster, Vector3.zero, Vector3.right, definition);
            presentation.Handle.OnPosition = position => target.IsAlive = false;

            rig.Tick(0.1f);

            Assert.That(hits, Is.Zero, "Presentation is an external callback; dead candidates must be checked again before hit callbacks.");
            if (tracking) Assert.That(projectile.Destroyed, Is.True);
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void TrackingProjectileDoesNotPublishNonFiniteTargetPosition(float coordinate)
        {
            AbilityTestRig rig = new AbilityTestRig();
            MovingPresentation presentation = new MovingPresentation();
            rig.Engine.Initialize(rig.World, presentation);
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            Projectile projectile = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition { Speed = 10f, EffectName = "moving" });
            target.Position = new Vector3(coordinate, 0f, 0f);

            rig.Tick(0.1f);

            Assert.That(projectile.Destroyed, Is.True, "An invalid moving target cannot poison the projectile or presentation position.");
            Assert.That(float.IsNaN(projectile.Position.x) || float.IsInfinity(projectile.Position.x), Is.False);
            Assert.That(presentation.Handle.InvalidPositions, Is.Zero);
        }

        [TestCase("modifier")]
        [TestCase("thinker")]
        [TestCase("projectile")]
        public void RuntimeObjectsRejectAbilityFromAnEarlierBattleGeneration(string kind)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            Game.Ability.Ability stale = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "old.battle" });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "local.buff" });
            rig.Engine.ClearRuntime();
            rig.Engine.RegisterUnit(caster);
            rig.Engine.RegisterUnit(target);

            object created = kind == "modifier" ? (object)rig.Engine.AddModifier(caster, target, stale, "local.buff")
                : kind == "thinker" ? rig.Engine.CreateThinker(new ThinkerRequest { Ability = stale, Caster = caster, Duration = 1f })
                : rig.Engine.CreateTrackingProjectile(stale, caster, target, new ProjectileDefinition());

            Assert.That(created, Is.Null, "Registering the same unit for a new battle cannot revive a stale ability's attribution.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ReentrantFactoryCannotLeakAnUnregisteredAbilityAsAValidSource(bool clearBattle)
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            AbilityScript script = null;
            rig.Engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "factory.leak" });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "factory.buff" });
            rig.Engine.RegisterAbilityScript("factory.leak", () =>
            {
                if (clearBattle) rig.Engine.ClearRuntime();
                else rig.Engine.RemoveUnit(caster);
                script = new DefaultAbilityScript();
                return script;
            });

            Game.Ability.Ability result = rig.Engine.AddAbility(caster, "factory.leak");
            Assert.That(result, Is.Null);
            Assert.That(script.Ability, Is.Not.Null, "The bound instance is observable through the caller's factory product.");
            rig.Engine.RegisterUnit(caster);
            rig.Engine.RegisterUnit(target);
            Game.Ability.Ability leaked = script.Ability;

            Assert.That(rig.Engine.AddModifier(caster, target, leaked, "factory.buff"), Is.Null);
            Assert.That(rig.Engine.CreateThinker(new ThinkerRequest { Ability = leaked, Caster = caster, Duration = 1f }), Is.Null);
            Assert.That(rig.Engine.CreateTrackingProjectile(leaked, caster, target, new ProjectileDefinition()), Is.Null);
        }

        [Test]
        public void ReentrantFactorySameNameGrantKeepsExactlyOneLiveRegistration()
        {
            foreach (bool removeInner in new[] { false, true })
            {
                AbilityTestRig rig = new AbilityTestRig();
                FakeUnit caster = rig.AddUnit(1, Vector3.zero);
                FakeUnit target = rig.AddUnit(2, Vector3.right);
                bool nested = false;
                Game.Ability.Ability inner = null;
                AbilityScript outerScript = null;
                rig.Engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "factory.duplicate", MaxLevel = 3 });
                rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "factory.buff" });
                rig.Engine.RegisterAbilityScript("factory.duplicate", () =>
                {
                    if (nested) return new DefaultAbilityScript();
                    nested = true;
                    inner = rig.Engine.AddAbility(caster, "factory.duplicate", 1);
                    if (removeInner) rig.Engine.RemoveAbility(caster, "factory.duplicate");
                    outerScript = new DefaultAbilityScript();
                    return outerScript;
                });

                Game.Ability.Ability result = rig.Engine.AddAbility(caster, "factory.duplicate", 3);

                Assert.That(rig.Engine.GetAbilities(caster).Count, Is.EqualTo(1));
                Assert.That(result.IsRemoved, Is.False);
                Assert.That(result.Level, Is.EqualTo(3));
                if (removeInner)
                {
                    Assert.That(inner.IsRemoved, Is.True);
                    Assert.That(result, Is.SameAs(outerScript.Ability));
                }
                else
                {
                    Assert.That(result, Is.SameAs(inner));
                    Assert.That(rig.Engine.AddModifier(caster, target, outerScript.Ability, "factory.buff"), Is.Null);
                }
            }
        }

        [Test]
        public void SnapshotSelfRemovingStateGetterDoesNotReportRemovedModifierOrSkipSurvivor()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit owner = rig.AddUnit(1, Vector3.zero);
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "self.removing" });
            rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "survivor" });
            RemoveSelfOnCheck script = null;
            rig.Engine.RegisterModifierScript("self.removing", () => script = new RemoveSelfOnCheck());
            rig.Engine.AddModifier(owner, owner, "self.removing");
            rig.Engine.AddModifier(owner, owner, "survivor");
            script.Armed = true;

            AbilityRuntimeSnapshot snapshot = rig.Engine.CreateRuntimeSnapshot();

            Assert.That(snapshot.Modifiers.Count, Is.EqualTo(1));
            Assert.That(snapshot.Modifiers[0].Name, Is.EqualTo("survivor"));
        }

        [Test]
        public void SnapshotOwnerGetterCanClearRuntimeWithoutInvalidatingDictionaryEnumeration()
        {
            AbilityTestRig rig = new AbilityTestRig();
            CallbackUnit owner = new CallbackUnit();
            rig.Engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "snapshot.owner" });
            rig.Engine.AddAbility(owner, "snapshot.owner");
            owner.OnTeam = rig.Engine.ClearRuntime;

            AbilityRuntimeSnapshot snapshot = rig.Engine.CreateRuntimeSnapshot();

            Assert.That(snapshot.Units, Is.Empty);
            Assert.That(rig.Engine.GetAbilities(owner), Is.Empty);
        }

        [Test]
        public void SnapshotOwnerGetterCanAddAnotherOwnerWithoutInvalidatingDictionaryEnumeration()
        {
            AbilityTestRig rig = new AbilityTestRig();
            CallbackUnit owner = new CallbackUnit();
            FakeUnit other = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterAbilityDefinition(new AbilityDefinition { Name = "snapshot.owner" });
            rig.Engine.AddAbility(owner, "snapshot.owner");
            owner.OnTeam = () => rig.Engine.AddAbility(other, "snapshot.owner");

            AbilityRuntimeSnapshot snapshot = rig.Engine.CreateRuntimeSnapshot();

            Assert.That(snapshot.Units.Count, Is.EqualTo(1), "A newly created owner is not retroactively part of the capture.");
            Assert.That(snapshot.Units[0].EntityId, Is.EqualTo(owner.EntityId));
        }

        [Test]
        public void ProjectileNaturalStopExceptionDoesNotLeaveDestroyedObjectRegistered()
        {
            AbilityTestRig rig = new AbilityTestRig();
            MovingPresentation presentation = new MovingPresentation();
            rig.Engine.Initialize(rig.World, presentation);
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right * 10f);
            int destroyed = 0;
            rig.Engine.EventRaised += value => { if (value.EventType == RuntimeEventType.ProjectileDestroyed) destroyed++; };
            Projectile projectile = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition { Speed = 0f, MaxLifetime = 0.1f, EffectName = "moving" });
            presentation.Handle.ThrowOnStop = true;

            bool threw = false;
            try { rig.Tick(0.2f); } catch (InvalidOperationException) { threw = true; }

            Assert.That(threw, Is.True);
            Assert.That(projectile.Destroyed, Is.True);
            Assert.That(rig.Engine.Projectiles, Is.Empty);
            Assert.That(destroyed, Is.EqualTo(1));
        }

        private sealed class RemoveSelfOnCheck : ModifierScript
        {
            internal bool Armed;
            public override bool CheckState(UnitState state)
            {
                if (Armed) Engine.RemoveModifier(Modifier);
                return false;
            }
        }

        private sealed class CallbackUnit : IUnit
        {
            internal Action OnTeam;
            public int EntityId => 501;
            public int TeamId { get { Action callback = OnTeam; OnTeam = null; callback?.Invoke(); return 1; } }
            public UnitType UnitType => UnitType.Hero;
            public bool IsAlive => true;
            public bool IsMagicImmune => false;
            public bool IsInvulnerable => false;
            public Vector3 Position => Vector3.zero;
            public bool IsVisibleToTeam(int teamId) => true;
            public void ApplyDamage(DamageResult result) { }
            public void Heal(HealInfo info) { }
        }

        private sealed class MovingPresentation : IPresentation, IProjectilePresentation
        {
            internal readonly MovingHandle Handle = new MovingHandle();
            public void PlayEffect(string name, Vector3 position) { }
            public void PlayEffect(string name, IUnit target) { }
            public void PlaySound(string name, Vector3 position) { }
            public IPresentationHandle PlayPersistentEffect(string name, IUnit target) => null;
            public IProjectilePresentationHandle PlayProjectileEffect(string name, Projectile projectile) => Handle;
        }

        private sealed class MovingHandle : IProjectilePresentationHandle
        {
            public bool IsActive { get; private set; } = true;
            internal Action<Vector3> OnPosition;
            internal int InvalidPositions;
            internal bool ThrowOnStop;
            public void Stop() { IsActive = false; if (ThrowOnStop) throw new InvalidOperationException("Presentation stop failed."); }
            public void SetPosition(Vector3 position)
            {
                if (float.IsNaN(position.x) || float.IsInfinity(position.x)) InvalidPositions++;
                OnPosition?.Invoke(position);
            }
        }
    }
}
