using UnityEngine;

namespace Game.Ability
{
    /// <summary>
    /// Runtime ability instance owned by one unit.
    /// It stores level, cooldown, charges, cast point, channel state, and its C# script.
    /// </summary>
    public sealed class Ability
    {
        public AbilitySystem Engine { get; }
        public AbilityDefinition Definition { get; }
        public IUnit Owner { get; }
        public IResourceOwner ResourceOwner { get; }
        public AbilityScript Script { get; }
        public int Level { get; private set; }
        public AbilityPhase Phase { get; private set; }
        public bool Activated { get; private set; } = true;
        public bool ToggleEnabled { get; private set; }
        public float CooldownRemaining { get; private set; }
        public int Charges { get; private set; }

        private CastOrder pendingOrder;
        private CastContext channelContext;
        private float castPointRemaining;
        private float channelRemaining;
        private float chargeRestoreRemaining;
        // Intrinsic modifiers are tied to the ability lifetime and removed with the ability.
        private Modifier intrinsicModifier;

        internal Ability(AbilitySystem engine, AbilityDefinition definition, IUnit owner, IResourceOwner resourceOwner, AbilityScript script, int level)
        {
            Engine = engine;
            Definition = definition;
            Owner = owner;
            ResourceOwner = resourceOwner;
            Script = script ?? new DefaultAbilityScript();
            Level = Mathf.Clamp(level, 1, Mathf.Max(1, definition.MaxLevel));
            Charges = definition.Charges != null && definition.Charges.StartFull ? Mathf.Max(0, definition.Charges.MaxCharges) : Mathf.Max(0, definition.Charges != null ? 0 : -1);
            Script.Bind(this);
            Script.OnCreated();
            ApplyIntrinsicModifier();
        }

        public void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 1, Mathf.Max(1, Definition.MaxLevel));
            Script.OnUpgrade();
            ApplyIntrinsicModifier();
        }

        public void SetActivated(bool activated)
        {
            Activated = activated;
        }

        public float GetSpecialValue(string name)
        {
            return Definition.GetSpecialValue(name, Level);
        }

        public float GetCooldown()
        {
            return Script.GetCooldown();
        }

        public float GetManaCost()
        {
            return Script.GetManaCost();
        }

        public float GetCastRange()
        {
            return Script.GetCastRange();
        }

        public float GetAoeRadius()
        {
            return Script.GetAoeRadius();
        }

        public void StartCooldown(float cooldown = -1f)
        {
            CooldownRemaining = Mathf.Max(0f, cooldown >= 0f ? cooldown : GetCooldown());
            if (CooldownRemaining > 0f)
            {
                Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.CooldownStarted, Ability = this, Caster = Owner, Value = CooldownRemaining });
            }
        }

        public void EndCooldown()
        {
            CooldownRemaining = 0f;
            Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.CooldownFinished, Ability = this, Caster = Owner });
        }

        public void SetCharges(int value)
        {
            if (Definition.Charges == null)
            {
                return;
            }

            Charges = Mathf.Clamp(value, 0, Mathf.Max(0, Definition.Charges.MaxCharges));
            Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ChargeChanged, Ability = this, Caster = Owner, Value = Charges });
        }

        public void RestoreFullCharges()
        {
            if (Definition.Charges != null)
            {
                SetCharges(Definition.Charges.MaxCharges);
            }
        }

        public CastResult IssueOrder(CastOrder order)
        {
            if (order == null)
            {
                return CastResult.Fail(CastFailureReason.InvalidTarget);
            }

            // Basic checks are engine-owned. AbilityScript.CastFilter handles custom rejection.
            CastResult result = CanCastBasic();
            if (!result.Success)
            {
                Engine.RaiseCastFailed(this, order, result);
                return result;
            }

            if (!Targeting.BuildCastContext(Engine, this, order, out CastContext context, out result))
            {
                Engine.RaiseCastFailed(this, order, result);
                return result;
            }

            result = Script.CastFilter(context);
            if (result == null)
            {
                result = CastResult.Ok();
            }

            if (!result.Success)
            {
                Engine.RaiseCastFailed(this, order, result);
                return result;
            }

            if ((Definition.Behavior & AbilityBehavior.Toggle) != 0)
            {
                // Toggle abilities change persistent state without entering cast point/channel phases.
                ToggleEnabled = !ToggleEnabled;
                Script.OnToggle(ToggleEnabled);
                Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ToggleChanged, Ability = this, Caster = Owner, Value = ToggleEnabled ? 1f : 0f });
                return CastResult.Ok();
            }

            float castPoint = Mathf.Max(0f, Script.GetCastPoint());
            if (castPoint > 0f)
            {
                // Store the order and rebuild the context when the cast point finishes.
                // This lets range/target validity be checked against the latest world state.
                Phase = AbilityPhase.Casting;
                castPointRemaining = castPoint;
                pendingOrder = order;
                Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.CastStarted, Ability = this, Caster = Owner, Target = order.Target, Position = order.HasTargetPosition ? order.TargetPosition : Owner.Position });
                return CastResult.Ok();
            }

            ExecuteCast(context);
            return CastResult.Ok();
        }

        public void Interrupt()
        {
            // Cast point interruption cancels the spell; channel interruption finishes with a flag.
            if (Phase == AbilityPhase.Casting)
            {
                Phase = AbilityPhase.Idle;
                pendingOrder = null;
                castPointRemaining = 0f;
                return;
            }

            if (Phase == AbilityPhase.Channeling)
            {
                FinishChannel(true);
            }
        }

        internal void Tick(float deltaTime)
        {
            TickCooldown(deltaTime);
            TickCharges(deltaTime);

            if (Phase == AbilityPhase.Casting)
            {
                TickCasting(deltaTime);
            }
            else if (Phase == AbilityPhase.Channeling)
            {
                TickChannel(deltaTime);
            }
        }

        internal void Remove()
        {
            Interrupt();
            if (intrinsicModifier != null)
            {
                // Removing the modifier is enough to remove all granted states and properties.
                Engine.RemoveModifier(intrinsicModifier);
                intrinsicModifier = null;
            }

            Script.OnRemoved();
        }

        private CastResult CanCastBasic()
        {
            if ((Definition.Behavior & AbilityBehavior.Hidden) != 0)
            {
                return CastResult.Fail(CastFailureReason.AbilityHidden);
            }

            if ((Definition.Behavior & AbilityBehavior.Passive) != 0)
            {
                return CastResult.Fail(CastFailureReason.AbilityPassive);
            }

            if (!Activated)
            {
                return CastResult.Fail(CastFailureReason.NotActivated);
            }

            if (Owner == null || !Owner.IsAlive)
            {
                return CastResult.Fail(CastFailureReason.DeadCaster);
            }

            if (Engine.HasState(Owner, UnitState.Stunned))
            {
                return CastResult.Fail(CastFailureReason.Stunned);
            }

            if (Engine.HasState(Owner, UnitState.Silenced))
            {
                return CastResult.Fail(CastFailureReason.Silenced);
            }

            if (Phase == AbilityPhase.Casting)
            {
                return CastResult.Fail(CastFailureReason.Casting);
            }

            if (Phase == AbilityPhase.Channeling)
            {
                return CastResult.Fail(CastFailureReason.Channeling);
            }

            if (CooldownRemaining > 0f)
            {
                return CastResult.Fail(CastFailureReason.Cooldown);
            }

            if (Definition.Charges != null && Charges <= 0)
            {
                return CastResult.Fail(CastFailureReason.NoCharges);
            }

            float manaCost = GetManaCost();
            if (manaCost > 0f && (ResourceOwner == null || !ResourceOwner.HasMana(manaCost)))
            {
                return CastResult.Fail(CastFailureReason.NotEnoughMana);
            }

            return CastResult.Ok();
        }

        private void ExecuteCast(CastContext context)
        {
            float manaCost = GetManaCost();
            if (manaCost > 0f && (ResourceOwner == null || !ResourceOwner.SpendMana(manaCost)))
            {
                Engine.RaiseCastFailed(this, null, CastResult.Fail(CastFailureReason.NotEnoughMana));
                return;
            }

            // Resources and charges are consumed only when the spell actually executes.
            if (Definition.Charges != null)
            {
                SetCharges(Charges - 1);
                if (Definition.Charges.UsesCooldown)
                {
                    StartCooldown();
                }
            }
            else
            {
                StartCooldown();
            }

            // Event order mirrors the useful Dota split between execution and fully-cast reactions.
            Engine.DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.AbilityExecuted, Engine = Engine, Source = Owner, Ability = this, Position = context.TargetPosition });
            Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.SpellStarted, Ability = this, Caster = Owner, Target = context.Target, Position = context.TargetPosition });
            Script.OnSpellStart(context);
            Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.AbilityFullyCast, Ability = this, Caster = Owner, Target = context.Target, Position = context.TargetPosition });
            Engine.DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.AbilityFullyCast, Engine = Engine, Source = Owner, Target = context.Target, Ability = this, Position = context.TargetPosition });

            float channelTime = Mathf.Max(0f, Script.GetChannelTime());
            if ((Definition.Behavior & AbilityBehavior.Channelled) != 0 && channelTime > 0f)
            {
                // Channel state is owned by the ability; ongoing effects should live in modifiers/thinkers.
                Phase = AbilityPhase.Channeling;
                channelRemaining = channelTime;
                channelContext = context;
                Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ChannelStarted, Ability = this, Caster = Owner, Target = context.Target, Position = context.TargetPosition, Value = channelTime });
            }
            else
            {
                Phase = AbilityPhase.Idle;
            }
        }

        private void TickCasting(float deltaTime)
        {
            castPointRemaining -= deltaTime;
            if (castPointRemaining > 0f)
            {
                return;
            }

            CastOrder order = pendingOrder;
            pendingOrder = null;
            Phase = AbilityPhase.Idle;
            if (Targeting.BuildCastContext(Engine, this, order, out CastContext context, out CastResult result))
            {
                ExecuteCast(context);
            }
            else
            {
                Engine.RaiseCastFailed(this, order, result);
            }
        }

        private void TickChannel(float deltaTime)
        {
            Script.OnChannelThink(deltaTime);
            channelRemaining -= deltaTime;
            if (channelRemaining <= 0f)
            {
                FinishChannel(false);
            }
        }

        private void FinishChannel(bool interrupted)
        {
            Phase = AbilityPhase.Idle;
            Script.OnChannelFinish(interrupted);
            Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.ChannelFinished, Ability = this, Caster = Owner, Target = channelContext != null ? channelContext.Target : null, Position = channelContext != null ? channelContext.TargetPosition : Owner.Position, Value = interrupted ? 1f : 0f });
            Engine.DispatchModifierEvent(new ModifierEvent { EventType = ModifierEventType.ChannelFinished, Engine = Engine, Source = Owner, Ability = this, Target = channelContext != null ? channelContext.Target : null });
            channelContext = null;
        }

        private void TickCooldown(float deltaTime)
        {
            if (CooldownRemaining <= 0f)
            {
                return;
            }

            CooldownRemaining -= deltaTime;
            if (CooldownRemaining <= 0f)
            {
                CooldownRemaining = 0f;
                Engine.RaiseEvent(new RuntimeEvent { EventType = RuntimeEventType.CooldownFinished, Ability = this, Caster = Owner });
            }
        }

        private void TickCharges(float deltaTime)
        {
            if (Definition.Charges == null || Charges >= Definition.Charges.MaxCharges || Definition.Charges.RestoreTime <= 0f)
            {
                return;
            }

            chargeRestoreRemaining -= deltaTime;
            if (chargeRestoreRemaining <= 0f)
            {
                SetCharges(Charges + 1);
                chargeRestoreRemaining = Definition.Charges.RestoreTime;
            }
        }

        private void ApplyIntrinsicModifier()
        {
            // Intrinsics are permanent modifiers attached to passive or owned abilities.
            string modifierName = Script.GetIntrinsicModifierName();
            if (string.IsNullOrEmpty(modifierName) || intrinsicModifier != null)
            {
                return;
            }

            intrinsicModifier = Engine.AddModifier(Owner, Owner, this, modifierName, new ModifierApplyOptions { Duration = -1f });
        }
    }
}
