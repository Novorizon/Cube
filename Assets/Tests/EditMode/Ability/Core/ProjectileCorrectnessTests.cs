using System;
using System.Collections.Generic;
using Game.Ability;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.Ability
{
    [TestFixture]
    [Category("Ability")]
    public sealed class ProjectileCorrectnessTests
    {
        [Test]
        public void LinearSweep_StopsAtNearestIntersectionRegardlessOfWorldOrder()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit farther = rig.AddUnit(2, Vector3.right * 8f);
            FakeUnit nearer = rig.AddUnit(2, Vector3.right * 5f);
            List<int> hitIds = ObserveHits(rig.Engine);
            Projectile projectile = rig.Engine.CreateLinearProjectile(null, caster, Vector3.zero, Vector3.right, new ProjectileDefinition
            {
                Speed = 100f, Radius = 0.25f, Distance = 10f, DeleteOnHit = true
            });

            rig.Tick(0.1f);

            Assert.That(hitIds, Is.EqualTo(new[] { nearer.EntityId }));
            Assert.That(projectile.Position.x, Is.EqualTo(4.75f).Within(0.0001f));
            Assert.That(projectile.Destroyed, Is.True);
            Assert.That(rig.Engine.Projectiles, Is.Empty);
            Assert.That(farther.IsAlive, Is.True);
        }

        [Test]
        public void LinearSweep_ClipsMovementBeforeTestingHitsBeyondMaximumDistance()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            rig.AddUnit(2, Vector3.right * 20f);
            List<int> hits = ObserveHits(rig.Engine);
            Projectile projectile = rig.Engine.CreateLinearProjectile(null, caster, Vector3.zero, Vector3.right, new ProjectileDefinition
            {
                Speed = 100f, Radius = 0.25f, Distance = 10f
            });

            rig.Tick(1f);

            Assert.That(hits, Is.Empty);
            Assert.That(projectile.Position.x, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(projectile.Destroyed, Is.True);
        }

        [Test]
        public void LinearSweep_PiercesInDistanceOrderAndNeverHitsSameUnitTwice()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit farther = rig.AddUnit(2, Vector3.right * 8f);
            FakeUnit nearer = rig.AddUnit(2, Vector3.right * 5f);
            List<int> hits = ObserveHits(rig.Engine);
            Projectile projectile = rig.Engine.CreateLinearProjectile(null, caster, Vector3.zero, Vector3.right, new ProjectileDefinition
            {
                Speed = 100f, Radius = 0.25f, Distance = 20f, DeleteOnHit = false
            });

            rig.Tick(0.1f);
            nearer.Position = projectile.Position;
            rig.Tick(0.01f);

            Assert.That(hits, Is.EqualTo(new[] { nearer.EntityId, farther.EntityId }));
        }

        [Test]
        public void TrackingArrival_TerminatesEvenWhenLinearPiercingFlagIsEnabled()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            List<int> hits = ObserveHits(rig.Engine);
            Projectile projectile = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition
            {
                Speed = 10f, DeleteOnHit = false
            });

            rig.Tick(1f);
            rig.Tick(10f);

            Assert.That(hits, Is.EqualTo(new[] { target.EntityId }));
            Assert.That(projectile.Destroyed, Is.True);
            Assert.That(rig.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void TrackingProjectile_ExpiresEvenWhenItCannotMove()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            Projectile projectile = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition
            {
                Speed = 0f, MaxLifetime = 0.25f
            });

            rig.Tick(1f);

            Assert.That(projectile.Destroyed, Is.True);
            Assert.That(rig.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void TrackingProjectile_StopsAtMaximumTravelDistance()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right * 20f);
            List<int> hits = ObserveHits(rig.Engine);
            Projectile projectile = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition
            {
                Speed = 100f, Distance = 5f
            });

            rig.Tick(1f);

            Assert.That(hits, Is.Empty);
            Assert.That(projectile.Position.x, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(projectile.Destroyed, Is.True);
        }

        [Test]
        public void TrackingTargetRevalidation_IsExplicitAndSeparateFromArrival()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            List<int> hits = ObserveHits(rig.Engine);
            Projectile keep = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition
            {
                Speed = 10f, TrackingTargetPolicy = TrackingTargetPolicy.KeepTracking
            });
            Projectile revalidate = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition
            {
                Speed = 10f, TrackingTargetPolicy = TrackingTargetPolicy.RevalidateTarget
            });
            target.IsInvulnerable = true;

            rig.Tick(1f);

            Assert.That(hits, Is.EqualTo(new[] { target.EntityId }));
            Assert.That(keep.Destroyed && revalidate.Destroyed, Is.True);
        }

        [Test]
        public void ProjectilePresentation_FollowsAndStopsExactlyOnceOnExplicitRemoval()
        {
            AbilityTestRig rig = new AbilityTestRig();
            MovingPresentation presentation = new MovingPresentation();
            rig.Engine.Initialize(rig.World, presentation);
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right * 10f);
            Projectile projectile = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition
            {
                Speed = 2f, EffectName = "projectile_effect"
            });

            rig.Tick(0.5f);
            Assert.That(presentation.Handle.Position.x, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(rig.Engine.RemoveProjectile(projectile), Is.True);
            Assert.That(rig.Engine.RemoveProjectile(projectile), Is.False);

            Assert.That(presentation.Handle.StopCount, Is.EqualTo(1));
            Assert.That(presentation.Handle.IsActive, Is.False);
            Assert.That(presentation.OneShotCount, Is.Zero);
        }

        [Test]
        public void ProjectileHitCallback_RemovingCasterDestroysProjectileOnlyOnce()
        {
            AbilityTestRig rig = new AbilityTestRig();
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            rig.Engine.RegisterAbilityScript("host", () => new RemoveCasterOnHit());
            Game.Ability.Ability ability = rig.RegisterAndAddAbility(caster, new AbilityDefinition { Name = "host" });
            int destroyedCount = 0;
            List<int> hits = ObserveHits(rig.Engine);
            rig.Engine.EventRaised += e => { if (e.EventType == RuntimeEventType.ProjectileDestroyed) destroyedCount++; };
            Projectile projectile = rig.Engine.CreateTrackingProjectile(ability, caster, target, new ProjectileDefinition { Speed = 10f });

            rig.Tick(1f);

            Assert.That(projectile.Destroyed, Is.True);
            Assert.That(destroyedCount, Is.EqualTo(1));
            Assert.That(hits, Is.Empty);
            Assert.That(rig.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void ProjectilePresentation_CanRemoveDuringCreationAndStopsReturnedHandle()
        {
            AbilityTestRig rig = new AbilityTestRig();
            MovingPresentation presentation = new MovingPresentation();
            rig.Engine.Initialize(rig.World, presentation);
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            presentation.OnCreate = projectile => rig.Engine.RemoveProjectile(projectile);
            int createdCount = 0;
            int destroyedCount = 0;
            rig.Engine.EventRaised += e =>
            {
                if (e.EventType == RuntimeEventType.ProjectileCreated) createdCount++;
                if (e.EventType == RuntimeEventType.ProjectileDestroyed) destroyedCount++;
            };

            Projectile result = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition { EffectName = "fx" });

            Assert.That(result, Is.Null);
            Assert.That(presentation.Handle.StopCount, Is.EqualTo(1));
            Assert.That(createdCount, Is.Zero);
            Assert.That(destroyedCount, Is.EqualTo(1));
            Assert.That(rig.Engine.Projectiles, Is.Empty);
        }

        [Test]
        public void ProjectilePresentation_RemovalAtArrivalSuppressesHit()
        {
            AbilityTestRig rig = new AbilityTestRig();
            MovingPresentation presentation = new MovingPresentation();
            rig.Engine.Initialize(rig.World, presentation);
            FakeUnit caster = rig.AddUnit(1, Vector3.zero);
            FakeUnit target = rig.AddUnit(2, Vector3.right);
            List<int> hits = ObserveHits(rig.Engine);
            Projectile projectile = rig.Engine.CreateTrackingProjectile(null, caster, target, new ProjectileDefinition { Speed = 10f, EffectName = "fx" });
            presentation.Handle.OnPosition = position => rig.Engine.RemoveProjectile(projectile);

            rig.Tick(1f);

            Assert.That(projectile.Destroyed, Is.True);
            Assert.That(hits, Is.Empty);
            Assert.That(presentation.Handle.StopCount, Is.EqualTo(1));
            Assert.That(rig.Engine.Projectiles, Is.Empty);
        }

        private static List<int> ObserveHits(AbilitySystem engine)
        {
            List<int> ids = new List<int>();
            engine.EventRaised += e => { if (e.EventType == RuntimeEventType.ProjectileHit) ids.Add(e.Target.EntityId); };
            return ids;
        }

        private sealed class RemoveCasterOnHit : AbilityScript
        {
            public override bool OnProjectileHit(Projectile projectile, IUnit target, Vector3 position)
            {
                Engine.RemoveUnit(Caster);
                return false;
            }
        }

        private sealed class MovingPresentation : IPresentation, IProjectilePresentation
        {
            public MovingHandle Handle;
            public int OneShotCount;
            public Action<Projectile> OnCreate;
            public IProjectilePresentationHandle PlayProjectileEffect(string effectName, Projectile projectile)
            {
                Handle = new MovingHandle();
                OnCreate?.Invoke(projectile);
                return Handle;
            }
            public void PlayEffect(string effectName, Vector3 position) { OneShotCount++; }
            public void PlayEffect(string effectName, IUnit target) { OneShotCount++; }
            public IPresentationHandle PlayPersistentEffect(string effectName, IUnit target) { return null; }
            public void PlaySound(string soundName, Vector3 position) { }
        }

        private sealed class MovingHandle : IProjectilePresentationHandle
        {
            public bool IsActive { get; private set; } = true;
            public Vector3 Position;
            public int StopCount;
            public Action<Vector3> OnPosition;
            public void SetPosition(Vector3 position) { Position = position; OnPosition?.Invoke(position); }
            public void Stop() { if (!IsActive) return; IsActive = false; StopCount++; }
        }
    }
}
