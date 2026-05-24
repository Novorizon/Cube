using UnityEngine;

namespace Skill2
{
    public sealed class SkillAbility
    {
        public Skill2Engine Engine { get; }
        public SkillAbilityDefinition Definition { get; }
        public ISkill2Unit Owner { get; }
        public ISkill2ResourceOwner ResourceOwner { get; }
        public SkillAbilityScript Script { get; }
        public int Level { get; private set; }
        public bool IsActivated { get; private set; } = true;
        public bool IsToggleOn { get; private set; }
        public bool IsAutoCastOn { get; private set; }
        public float CooldownRemaining { get; private set; }
        public int Charges { get; private set; }
        public float ChargeRestoreRemaining { get; private set; }
        public SkillAbilityPhase Phase { get; private set; }
        public float CastPointRemaining { get; private set; }
        public float ChannelRemaining { get; private set; }

        private SkillCastOrder pendingOrder;
        private SkillModifier intrinsicModifier;

        internal SkillAbility(Skill2Engine engine, SkillAbilityDefinition definition, ISkill2Unit owner, ISkill2ResourceOwner resourceOwner, SkillAbilityScript script, int level)
        {
            Engine = engine;
            Definition = definition;
            Owner = owner;
            ResourceOwner = resourceOwner;
            Script = script ?? new DefaultSkillAbilityScript();
            Script.Bind(this);
            Level = Mathf.Clamp(level, 0, Mathf.Max(1, definition.MaxLevel));

            if (definition.Charges != null && definition.Charges.MaxCharges > 0)
            {
                Charges = definition.Charges.StartFull ? definition.Charges.MaxCharges : 0;
            }

            Script.OnCreated();
            ApplyIntrinsicModifier();
        }

        public float GetSpecialValue(string name)
        {
            return Definition.GetSpecialValue(name, Level);
        }

        public void SetLevel(int level)
        {
            int nextLevel = Mathf.Clamp(level, 0, Mathf.Max(1, Definition.MaxLevel));
            if (Level == nextLevel)
            {
                return;
            }

            Level = nextLevel;
            Script.OnUpgrade();
            ApplyIntrinsicModifier();
        }

        public void SetActivated(bool value)
        {
            IsActivated = value;
        }

        public void SetAutoCast(bool value)
        {
            if ((Definition.Behavior & SkillAbilityBehavior.AutoCast) == 0)
            {
                return;
            }

            IsAutoCastOn = value;
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

        public void StartCooldown(float cooldown)
        {
            StartCooldownInternal(cooldown);
        }

        public void EndCooldown()
        {
            CooldownRemaining = 0f;
            Engine.RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.CooldownFinished,
                Ability = this,
                Caster = Owner
            });
        }

        public void SetCharges(int value)
        {
            if (Definition.Charges == null || Definition.Charges.MaxCharges <= 0)
            {
                return;
            }

            Charges = Mathf.Clamp(value, 0, Definition.Charges.MaxCharges);
            if (Charges >= Definition.Charges.MaxCharges)
            {
                ChargeRestoreRemaining = 0f;
            }
            else if (ChargeRestoreRemaining <= 0f)
            {
                ChargeRestoreRemaining = Definition.Charges.RestoreTime;
            }

            Engine.RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ChargeChanged,
                Ability = this,
                Caster = Owner,
                Value = Charges
            });
        }

        public void RestoreFullCharges()
        {
            if (Definition.Charges == null || Definition.Charges.MaxCharges <= 0)
            {
                return;
            }

            SetCharges(Definition.Charges.MaxCharges);
        }

        public SkillCastResult IssueOrder(SkillCastOrder order)
        {
            SkillCastResult validation = ValidateOrder(order, false);
            if (!validation.Success)
            {
                Engine.RaiseCastFailed(this, order, validation);
                return validation;
            }

            if ((Definition.Behavior & SkillAbilityBehavior.Toggle) != 0)
            {
                return Toggle(order);
            }

            if (!SkillTargeting.BuildCastContext(Engine, this, order, out SkillCastContext context, out SkillCastResult targetResult))
            {
                Engine.RaiseCastFailed(this, order, targetResult);
                return targetResult;
            }

            SkillCastResult scriptResult = Script.CastFilter(context);
            if (!scriptResult.Success)
            {
                Engine.RaiseCastFailed(this, order, scriptResult);
                return scriptResult;
            }

            float castPoint = Script.GetCastPoint();
            if (castPoint > 0f && (Definition.Behavior & SkillAbilityBehavior.Immediate) == 0)
            {
                pendingOrder = order.Clone();
                Phase = SkillAbilityPhase.Casting;
                CastPointRemaining = castPoint;
                Engine.RaiseEvent(new SkillEvent
                {
                    EventType = SkillEventType.CastStarted,
                    Ability = this,
                    Caster = Owner,
                    Target = order.Target,
                    Position = context.TargetPosition
                });
                return SkillCastResult.Ok();
            }

            return CommitCast(context, order);
        }

        internal void Tick(float deltaTime)
        {
            TickCooldown(deltaTime);
            TickCharges(deltaTime);

            if (Phase == SkillAbilityPhase.Casting)
            {
                CastPointRemaining -= deltaTime;

                if (CastPointRemaining <= 0f)
                {
                    FinishCastPoint();
                }
            }
            else if (Phase == SkillAbilityPhase.Channeling)
            {
                ChannelRemaining -= deltaTime;
                Script.OnChannelThink(deltaTime);

                if (ChannelRemaining <= 0f)
                {
                    FinishChannel(false);
                }
            }
        }

        public bool Interrupt()
        {
            if (Phase == SkillAbilityPhase.Casting)
            {
                Phase = SkillAbilityPhase.Idle;
                CastPointRemaining = 0f;
                pendingOrder = null;
                return true;
            }

            if (Phase == SkillAbilityPhase.Channeling)
            {
                FinishChannel(true);
                return true;
            }

            return false;
        }

        internal void Remove()
        {
            if (intrinsicModifier != null)
            {
                Engine.RemoveModifier(intrinsicModifier);
                intrinsicModifier = null;
            }

            Script.OnRemoved();
        }

        private SkillCastResult ValidateOrder(SkillCastOrder order, bool allowCurrentCast)
        {
            if (order == null || order.Caster == null || order.Caster.EntityId != Owner.EntityId)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.InvalidTarget);
            }

            if ((Definition.Behavior & SkillAbilityBehavior.Hidden) != 0)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.AbilityHidden);
            }

            if ((Definition.Behavior & SkillAbilityBehavior.Passive) != 0 && (Definition.Behavior & SkillAbilityBehavior.Toggle) == 0)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.AbilityPassive);
            }

            if (Level <= 0)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.NotTrained);
            }

            if (!IsActivated)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.NotActivated);
            }

            if (!Owner.IsAlive)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.DeadCaster);
            }

            if (Engine.HasState(Owner, SkillUnitState.Stunned) || Engine.HasState(Owner, SkillUnitState.CommandRestricted))
            {
                return SkillCastResult.Fail(SkillCastFailureReason.Stunned);
            }

            if (Engine.HasState(Owner, SkillUnitState.Silenced))
            {
                return SkillCastResult.Fail(SkillCastFailureReason.Silenced);
            }

            if ((Definition.Behavior & SkillAbilityBehavior.RootDisables) != 0 && Engine.HasState(Owner, SkillUnitState.Rooted))
            {
                return SkillCastResult.Fail(SkillCastFailureReason.Rooted);
            }

            if (!allowCurrentCast && Phase == SkillAbilityPhase.Casting)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.Casting);
            }

            if (!allowCurrentCast && Phase == SkillAbilityPhase.Channeling)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.Channeling);
            }

            if (Definition.Charges != null && Definition.Charges.MaxCharges > 0)
            {
                if (Charges <= 0)
                {
                    return SkillCastResult.Fail(SkillCastFailureReason.NoCharges);
                }
            }
            else if (CooldownRemaining > 0f)
            {
                return SkillCastResult.Fail(SkillCastFailureReason.Cooldown);
            }

            float manaCost = Script.GetManaCost();
            if (manaCost > 0f && (ResourceOwner == null || !ResourceOwner.HasMana(manaCost)))
            {
                return SkillCastResult.Fail(SkillCastFailureReason.NotEnoughMana);
            }

            return SkillCastResult.Ok();
        }

        private SkillCastResult Toggle(SkillCastOrder order)
        {
            float manaCost = Script.GetManaCost();
            if (manaCost > 0f && (ResourceOwner == null || !ResourceOwner.SpendMana(manaCost)))
            {
                SkillCastResult result = SkillCastResult.Fail(SkillCastFailureReason.NotEnoughMana);
                Engine.RaiseCastFailed(this, order, result);
                return result;
            }

            IsToggleOn = !IsToggleOn;
            Script.OnToggle(IsToggleOn);
            Engine.RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ToggleChanged,
                Ability = this,
                Caster = Owner,
                Value = IsToggleOn ? 1f : 0f
            });
            return SkillCastResult.Ok();
        }

        private void FinishCastPoint()
        {
            SkillCastOrder order = pendingOrder;
            pendingOrder = null;
            Phase = SkillAbilityPhase.Idle;
            CastPointRemaining = 0f;

            SkillCastResult validation = ValidateOrder(order, true);
            if (!validation.Success)
            {
                Engine.RaiseCastFailed(this, order, validation);
                return;
            }

            if (!SkillTargeting.BuildCastContext(Engine, this, order, out SkillCastContext context, out SkillCastResult targetResult))
            {
                Engine.RaiseCastFailed(this, order, targetResult);
                return;
            }

            SkillCastResult scriptResult = Script.CastFilter(context);
            if (!scriptResult.Success)
            {
                Engine.RaiseCastFailed(this, order, scriptResult);
                return;
            }

            CommitCast(context, order);
        }

        private SkillCastResult CommitCast(SkillCastContext context, SkillCastOrder order)
        {
            float manaCost = Script.GetManaCost();
            if (manaCost > 0f && (ResourceOwner == null || !ResourceOwner.SpendMana(manaCost)))
            {
                SkillCastResult result = SkillCastResult.Fail(SkillCastFailureReason.NotEnoughMana);
                Engine.RaiseCastFailed(this, order, result);
                return result;
            }

            ConsumeCooldownOrCharge();

            Engine.DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.AbilityExecuted,
                Engine = Engine,
                Source = Owner,
                Target = context.Target,
                Ability = this,
                Position = context.TargetPosition
            });

            Engine.RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.SpellStarted,
                Ability = this,
                Caster = Owner,
                Target = context.Target,
                Position = context.TargetPosition
            });

            Script.OnSpellStart(context);

            float channelTime = Script.GetChannelTime();
            if ((Definition.Behavior & SkillAbilityBehavior.Channelled) != 0 && channelTime > 0f)
            {
                Phase = SkillAbilityPhase.Channeling;
                ChannelRemaining = channelTime;
                Engine.RaiseEvent(new SkillEvent
                {
                    EventType = SkillEventType.ChannelStarted,
                    Ability = this,
                    Caster = Owner,
                    Target = context.Target,
                    Position = context.TargetPosition
                });
            }
            else
            {
                Phase = SkillAbilityPhase.Idle;
            }

            Engine.DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.AbilityFullyCast,
                Engine = Engine,
                Source = Owner,
                Target = context.Target,
                Ability = this,
                Position = context.TargetPosition
            });

            Engine.RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.AbilityFullyCast,
                Ability = this,
                Caster = Owner,
                Target = context.Target,
                Position = context.TargetPosition
            });

            return SkillCastResult.Ok();
        }

        private void ConsumeCooldownOrCharge()
        {
            if (Definition.Charges != null && Definition.Charges.MaxCharges > 0)
            {
                Charges = Mathf.Max(0, Charges - 1);
                if (ChargeRestoreRemaining <= 0f && Charges < Definition.Charges.MaxCharges)
                {
                    ChargeRestoreRemaining = Definition.Charges.RestoreTime;
                }

                Engine.RaiseEvent(new SkillEvent
                {
                    EventType = SkillEventType.ChargeChanged,
                    Ability = this,
                    Caster = Owner,
                    Value = Charges
                });

                if (!Definition.Charges.UsesCooldown)
                {
                    return;
                }
            }

            StartCooldownInternal(Script.GetCooldown());
        }

        private void StartCooldownInternal(float cooldown)
        {
            float reduction = Engine.GetModifierProperty(Owner, SkillModifierProperty.CooldownReductionPercent, null);
            float multiplier = Mathf.Max(0f, 1f - reduction / 100f);
            CooldownRemaining = Mathf.Max(0f, cooldown * multiplier);

            if (CooldownRemaining > 0f)
            {
                Engine.RaiseEvent(new SkillEvent
                {
                    EventType = SkillEventType.CooldownStarted,
                    Ability = this,
                    Caster = Owner,
                    Value = CooldownRemaining
                });
            }
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
                Engine.RaiseEvent(new SkillEvent
                {
                    EventType = SkillEventType.CooldownFinished,
                    Ability = this,
                    Caster = Owner
                });
            }
        }

        private void TickCharges(float deltaTime)
        {
            if (Definition.Charges == null || Definition.Charges.MaxCharges <= 0 || Charges >= Definition.Charges.MaxCharges)
            {
                return;
            }

            if (ChargeRestoreRemaining <= 0f)
            {
                ChargeRestoreRemaining = Definition.Charges.RestoreTime;
            }

            ChargeRestoreRemaining -= deltaTime;

            if (ChargeRestoreRemaining > 0f)
            {
                return;
            }

            Charges++;
            ChargeRestoreRemaining = Charges < Definition.Charges.MaxCharges ? Definition.Charges.RestoreTime : 0f;
            Engine.RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ChargeChanged,
                Ability = this,
                Caster = Owner,
                Value = Charges
            });
        }

        private void FinishChannel(bool interrupted)
        {
            Phase = SkillAbilityPhase.Idle;
            ChannelRemaining = 0f;
            Script.OnChannelFinish(interrupted);
            Engine.DispatchModifierEvent(new SkillModifierEvent
            {
                EventType = SkillModifierEventType.ChannelFinished,
                Engine = Engine,
                Source = Owner,
                Ability = this,
                Value = interrupted ? 1f : 0f
            });
            Engine.RaiseEvent(new SkillEvent
            {
                EventType = SkillEventType.ChannelFinished,
                Ability = this,
                Caster = Owner,
                Value = interrupted ? 1f : 0f
            });
        }

        private void ApplyIntrinsicModifier()
        {
            if (Level <= 0)
            {
                return;
            }

            string modifierName = Script.GetIntrinsicModifierName();
            if (string.IsNullOrEmpty(modifierName))
            {
                return;
            }

            if (intrinsicModifier != null && intrinsicModifier.Name == modifierName)
            {
                return;
            }

            if (intrinsicModifier != null)
            {
                Engine.RemoveModifier(intrinsicModifier);
            }

            SkillModifierApplyOptions options = new SkillModifierApplyOptions();
            options.Duration = -1f;
            intrinsicModifier = Engine.AddModifier(Owner, Owner, this, modifierName, options);
        }
    }
}
