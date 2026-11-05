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
    public sealed class AuraTimingTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void CoveredChild_RetainsFullLingerRegardlessOfTickOrder(bool recipientFirst)
        {
            AuraRig aura = new AuraRig();
            if (recipientFirst) aura.ReverseTickOrder();

            for (int i = 0; i < 8; i++)
            {
                aura.Rig.Tick(0.25f);
                Assert.That(aura.Child.IsDestroyed, Is.False);
                Assert.That(aura.Child.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
                Assert.That(aura.CurrentChild(), Is.SameAs(aura.Child));
            }

            Assert.That(aura.Child.StackCount, Is.EqualTo(1));
            Assert.That(aura.Child.ElapsedTime, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(aura.Destroyed, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void LeavingCoverage_ExpiresAtExactlyConfiguredLinger(bool recipientFirst)
        {
            AuraRig aura = new AuraRig();
            if (recipientFirst) aura.ReverseTickOrder();
            aura.Rig.Tick(0.25f);
            aura.Receiver.Position = Vector3.right * 20f;

            aura.Rig.Tick(0.25f);
            Assert.That(aura.Child.IsDestroyed, Is.False);
            Assert.That(aura.Child.RemainingTime, Is.EqualTo(0.25f).Within(0.00001f));
            aura.Rig.Tick(0.25f);

            Assert.That(aura.Child.IsDestroyed, Is.True);
            Assert.That(aura.CurrentChild(), Is.Null);
            Assert.That(aura.Destroyed, Is.EqualTo(1));
        }

        [Test]
        public void CoveredChild_LongFrameRunsIntervalsAndRegenerationWithoutFalseDestroy()
        {
            AuraRig aura = new AuraRig(interval: 0.25f, regeneration: 10f);

            aura.Rig.Tick(2f);

            Assert.That(aura.Child.IsDestroyed, Is.False);
            Assert.That(aura.CurrentChild(), Is.SameAs(aura.Child));
            Assert.That(aura.Intervals, Is.EqualTo(8));
            Assert.That(aura.Receiver.HealingReceived, Is.EqualTo(20f).Within(0.00001f));
            Assert.That(aura.Child.ElapsedTime, Is.EqualTo(2f).Within(0.00001f));
            Assert.That(aura.Child.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(aura.Destroyed, Is.Zero);
        }

        [Test]
        public void CoveredChild_PeriodicCatchUpCapDoesNotConsumeLinger()
        {
            AuraRig aura = new AuraRig(interval: 0.01f);
            aura.Rig.Engine.MaxPeriodicCallbacksPerUpdate = 3;

            aura.Rig.Tick(10f);

            Assert.That(aura.Intervals, Is.EqualTo(3));
            Assert.That(aura.Child.IsDestroyed, Is.False);
            Assert.That(aura.Child.ElapsedTime, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(aura.Child.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
        }

        [Test]
        public void ReenteringCoverage_RenewsLeaseBeforeNextSourcePulse()
        {
            AuraRig aura = new AuraRig(thinkInterval: 2f);
            aura.Receiver.Position = Vector3.right * 20f;
            aura.Rig.Tick(0.25f);
            aura.Receiver.Position = Vector3.right;

            aura.Rig.Tick(0.1f);

            Assert.That(aura.CurrentChild(), Is.SameAs(aura.Child));
            Assert.That(aura.Child.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
        }

        [Test]
        public void TwoSources_KeepSeparateLeasesAndIndependentExitTimes()
        {
            AuraRig aura = new AuraRig();
            FakeUnit other = aura.Rig.AddUnit(1, Vector3.right * 2f);
            Modifier otherSource = aura.Rig.Engine.AddModifier(other, other, "aura_source");
            Modifier otherChild = aura.Rig.Engine.Modifiers.Single(modifier => modifier.Parent == aura.Receiver && modifier.SourceAura == otherSource);
            aura.Source.Position = Vector3.right * -20f;

            aura.Rig.Tick(0.5f);

            Assert.That(aura.Child.IsDestroyed, Is.True);
            Assert.That(otherChild.IsDestroyed, Is.False);
            Assert.That(otherChild.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
            Assert.That(otherChild.StackCount, Is.EqualTo(1));
        }

        [Test]
        public void MultipleAuraChild_RefreshesSameSourceAndKeepsDifferentSourcesSeparate()
        {
            AuraRig aura = new AuraRig(childAttributes: ModifierAttribute.Multiple);
            FakeUnit other = aura.Rig.AddUnit(1, Vector3.right * 2f);
            Modifier otherSource = aura.Rig.Engine.AddModifier(other, other, "aura_source");

            aura.Rig.Tick(0.35f);

            Modifier[] recipients = aura.Rig.Engine.Modifiers.Where(modifier => modifier.Parent == aura.Receiver && modifier.Name == "aura_child").ToArray();
            Assert.That(recipients.Length, Is.EqualTo(2));
            Assert.That(recipients.Single(modifier => modifier.SourceAura == aura.SourceModifier), Is.SameAs(aura.Child));
            Assert.That(recipients.Single(modifier => modifier.SourceAura == otherSource).StackCount, Is.EqualTo(1));
            Assert.That(aura.Child.StackCount, Is.EqualTo(1));
        }

        [Test]
        public void RemovedSource_StopsRenewingAndChildUsesFullLinger()
        {
            AuraRig aura = new AuraRig();
            aura.Rig.Tick(0.25f);
            aura.Rig.Engine.RemoveModifier(aura.SourceModifier);

            aura.Rig.Tick(0.25f);
            Assert.That(aura.Child.IsDestroyed, Is.False);
            Assert.That(aura.Child.RemainingTime, Is.EqualTo(0.25f).Within(0.00001f));
            aura.Rig.Tick(0.25f);

            Assert.That(aura.Child.IsDestroyed, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SourceExpiringDuringUpdate_StopsRenewingAtBoundaryWithoutOrderDependence(bool recipientFirst)
        {
            AuraRig aura = new AuraRig(sourceDuration: 0.25f);
            if (recipientFirst) aura.ReverseTickOrder();

            aura.Rig.Tick(0.25f);
            Assert.That(aura.SourceModifier.IsDestroyed, Is.True);
            Assert.That(aura.Child.IsDestroyed, Is.False);
            Assert.That(aura.Child.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
            aura.Rig.Tick(0.5f);

            Assert.That(aura.Child.IsDestroyed, Is.True);
        }

        [Test]
        public void DeadSourceAtBoundary_DoesNotGrantCoverageBeforeSourceTick()
        {
            AuraRig aura = new AuraRig();
            aura.ReverseTickOrder();
            aura.Source.IsAlive = false;

            aura.Rig.Tick(0.5f);

            Assert.That(aura.SourceModifier.IsDestroyed, Is.True);
            Assert.That(aura.Child.IsDestroyed, Is.True);
        }

        [Test]
        public void InvulnerableReceiverAtBoundary_LosesCoverageUnderTargetPolicy()
        {
            AuraRig aura = new AuraRig();
            aura.Receiver.IsInvulnerable = true;

            aura.Rig.Tick(0.5f);

            Assert.That(aura.Child.IsDestroyed, Is.True);
            Assert.That(aura.CurrentChild(), Is.Null);
        }

        [Test]
        public void OutOfGameReceiverAtBoundary_LosesCoverageUnderTargetPolicy()
        {
            AuraRig aura = new AuraRig();
            aura.Rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "out_of_game", States = UnitState.OutOfGame });
            aura.Rig.Engine.AddModifier(aura.Receiver, aura.Receiver, "out_of_game");

            aura.Rig.Tick(0.5f);

            Assert.That(aura.Child.IsDestroyed, Is.True);
            Assert.That(aura.CurrentChild(), Is.Null);
        }

        [Test]
        public void DeferredRefresh_ClearRuntimeFromCallbackDoesNotRetainQueuedWork()
        {
            AuraRig aura = new AuraRig();
            aura.OnRefresh = aura.Rig.Engine.ClearRuntime;

            aura.Rig.Tick(0.25f);

            Assert.That(aura.Rig.Engine.Modifiers, Is.Empty);
            aura.Rig.Tick(0.25f);
            Assert.That(aura.Rig.Engine.Modifiers, Is.Empty);
        }

        [Test]
        public void DeferredRefresh_PreservesExplicitStackPulseCount()
        {
            AuraRig aura = new AuraRig(incrementsStacks: true);

            aura.Rig.Tick(0.35f);

            Assert.That(aura.Child.StackCount, Is.EqualTo(4));
            Assert.That(aura.Child.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
        }

        [Test]
        public void NewReceiver_EntersOnPulseAndStartsAgingOnFollowingUpdate()
        {
            AuraRig aura = new AuraRig();
            FakeUnit entering = aura.Rig.AddUnit(1, Vector3.right * 20f);
            entering.Position = Vector3.right * 2f;

            aura.Rig.Tick(0.25f);
            Modifier child = aura.Rig.Engine.Modifiers.Single(modifier => modifier.Parent == entering && modifier.SourceAura == aura.SourceModifier);

            Assert.That(child.ElapsedTime, Is.Zero);
            Assert.That(child.RemainingTime, Is.EqualTo(0.5f).Within(0.00001f));
            aura.Rig.Tick(0.25f);
            Assert.That(child.ElapsedTime, Is.EqualTo(0.25f).Within(0.00001f));
        }

        private sealed class AuraRig
        {
            internal readonly AbilityTestRig Rig = new AbilityTestRig();
            internal readonly FakeUnit Source;
            internal readonly FakeUnit Receiver;
            internal readonly Modifier SourceModifier;
            internal readonly Modifier Child;
            internal int Destroyed;
            internal int Intervals;
            internal Action OnRefresh;

            internal AuraRig(float interval = 0f, float regeneration = 0f, float thinkInterval = 0.1f, float sourceDuration = -1f, bool incrementsStacks = false, ModifierAttribute childAttributes = ModifierAttribute.None)
            {
                Source = Rig.AddUnit(1, Vector3.zero);
                Receiver = Rig.AddUnit(1, Vector3.right);
                ModifierDefinition child = new ModifierDefinition { Name = "aura_child", Duration = 0.5f, MaxStack = 100, Interval = interval, Attributes = childAttributes };
                child.Properties[ModifierProperty.HealthRegen] = regeneration;
                Rig.Engine.RegisterModifierDefinition(child);
                Rig.Engine.RegisterModifierScript(child.Name, () => new AuraChildScript(this));
                Rig.Engine.RegisterModifierDefinition(new ModifierDefinition { Name = "aura_source", Duration = sourceDuration, AuraModifierName = child.Name, AuraRadius = 5f, AuraDuration = 0.5f, AuraThinkInterval = thinkInterval, AuraTargetTeam = TargetTeam.Friendly, AuraTargetType = UnitType.All, AuraTargetFlags = TargetFlags.ExcludeSelf, AuraIncrementsStacks = incrementsStacks });
                SourceModifier = Rig.Engine.AddModifier(Source, Source, "aura_source");
                Child = CurrentChild();
            }

            internal Modifier CurrentChild() => Rig.Engine.Modifiers.FirstOrDefault(modifier => modifier.Parent == Receiver && modifier.Name == "aura_child" && modifier.SourceAura == SourceModifier);

            // Reorder scheduling only; keep the same registered objects and index membership.
            // This protects the lease contract if runtime scheduling changes in the future.
            internal void ReverseTickOrder() => ((List<Modifier>)Rig.Engine.Modifiers).Reverse();

            private sealed class AuraChildScript : ModifierScript
            {
                private readonly AuraRig aura;
                internal AuraChildScript(AuraRig aura) { this.aura = aura; }
                public override void OnDestroy() { if (Parent == aura.Receiver) aura.Destroyed++; }
                public override void OnIntervalThink() { if (Parent == aura.Receiver) aura.Intervals++; }
                public override void OnRefresh(ModifierApplyOptions options) { aura.OnRefresh?.Invoke(); }
            }
        }
    }
}
