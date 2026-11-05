using Game.Ability;
using UnityEngine;
using RuntimeAbility = Game.Ability.Ability;

namespace Game
{
    public sealed partial class AbilityManager
    {
        private readonly System.Collections.Generic.Dictionary<int, TdUnit> unitsByRuntimeId = new System.Collections.Generic.Dictionary<int, TdUnit>();
        private readonly System.Collections.Generic.Dictionary<int, Modifier> controlledEffects = new System.Collections.Generic.Dictionary<int, Modifier>();
        private readonly System.Collections.Generic.List<int> expiredEffectHandles = new System.Collections.Generic.List<int>();
        private int nextEffectHandle;
        public event System.Action<AbilityCommandUpdate> CommandUpdated;
        // Binding is explicit. UI/status reads below never create units, grant abilities or train levels.
        public AbilityOwnerHandle BindOwner(Npc npc) => ToHandle(GetUnit(npc));
        public AbilityOwnerHandle BindOwner(Tower tower) => ToHandle(GetUnit(tower));
        public AbilityOwnerHandle BindBaseOwner() => ToHandle(GetBaseUnit());
        public AbilityOwnerHandle FindOwner(Npc npc) => ToHandle(FindUnit(npc));
        public AbilityOwnerHandle FindOwner(Tower tower) => ToHandle(FindUnit(tower));
        public AbilityOwnerHandle FindBaseOwner() => ToHandle(IsAvailable ? baseUnit : null);
        internal TdUnit FindBoundBaseUnit() => IsAvailable && baseUnit != null && baseUnit.IsValidBinding ? baseUnit : null;

        public AbilityCommandResult TryCastTowerSkill(Tower tower, int skillId, Npc target)
        {
            TdUnit unit = GetUnit(tower);
            RuntimeAbility ability = EnsureAbility(unit, skillId);
            if (ability == null) return ToCommandResult(MissingOwnedAbility());
            AbilityBehavior behavior = ability.Definition.Behavior;
            if ((behavior & (AbilityBehavior.UnitTarget | AbilityBehavior.OptionalUnitTarget)) != 0) return ToCommandResult(Engine.CastAbilityOnTarget(unit, ability.Definition.Name, GetUnit(target)));
            if ((behavior & AbilityBehavior.PointTarget) != 0) return target != null ? ToCommandResult(Engine.CastAbilityOnPosition(unit, ability.Definition.Name, target.transform.position)) : ToCommandResult(CastResult.Fail(CastFailureReason.InvalidTarget));
            return ToCommandResult(Engine.CastAbility(unit, ability.Definition.Name));
        }

        /// <summary>Grant is idempotent. Use SetAbilityLevel for an intentional upgrade or downgrade.</summary>
        public bool GrantAbility(AbilityOwnerHandle owner, AbilityKey key, int level = 1, int resourceItemId = 0)
        {
            TdUnit unit = ResolveOwner(owner);
            if (unit == null || !TryResolveAbilityName(key, out string name)) return false;
            if (Engine.FindAbility(unit, name) != null) return true;
            return Engine.AddAbility(unit, name, Mathf.Max(0, level), resourceItemId > 0 ? new TdResourceOwner(resourceItemId) : null) != null;
        }

        public bool SetAbilityLevel(AbilityOwnerHandle owner, AbilityKey key, int level)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            if (ability == null) return false;
            ability.SetLevel(level);
            return true;
        }

        public bool TrySetActivated(AbilityOwnerHandle owner, AbilityKey key, bool activated)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            if (ability == null) return false;
            ability.SetActivated(activated);
            return true;
        }

        public bool RemoveAbility(AbilityOwnerHandle owner, AbilityKey key)
        {
            TdUnit unit = ResolveOwner(owner);
            return unit != null && TryResolveAbilityName(key, out string name) && Engine.RemoveAbility(unit, name);
        }

        public AbilityEffectHandle ApplyStatusEffect(AbilityOwnerHandle target, string modifierName, AbilityOwnerHandle source = default, float duration = float.NaN, int level = 1)
        {
            PruneControlledEffects();
            TdUnit parent = ResolveOwner(target);
            TdUnit caster = source.IsValid ? ResolveOwner(source) : parent;
            if (parent == null || caster == null) return default;
            Modifier modifier = Engine.AddModifier(caster, parent, null, modifierName, new ModifierApplyOptions { Duration = duration, Level = Mathf.Max(1, level) });
            if (modifier == null || modifier.IsDestroyed) return default;
            foreach (var pair in controlledEffects) if (object.ReferenceEquals(pair.Value, modifier)) return new AbilityEffectHandle(battleSession, pair.Key);
            int id = ++nextEffectHandle;
            controlledEffects[id] = modifier;
            return new AbilityEffectHandle(battleSession, id);
        }

        public bool RemoveStatusEffect(AbilityEffectHandle handle)
        {
            Modifier modifier = ResolveEffect(handle);
            if (modifier == null) return false;
            controlledEffects.Remove(handle.Id);
            return Engine.RemoveModifier(modifier);
        }

        public bool TryGetStatusEffect(AbilityEffectHandle handle, out AbilityEffectStatus status)
        {
            Modifier modifier = ResolveEffect(handle);
            status = modifier == null ? default : new AbilityEffectStatus(modifier.Definition.Name, modifier.StackCount, modifier.RemainingTime);
            return modifier != null;
        }

        public bool TryGetAbilityStatus(AbilityOwnerHandle owner, AbilityKey key, out AbilityStatus status)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            status = ability == null ? default : new AbilityStatus(ability.Definition.Name, ability.Level, ToExecutionPhase(ability.Phase), Mathf.Max(0f, ability.CooldownRemaining), ability.CooldownDuration, ability.Charges, ability.Activated, ability.ToggleEnabled,
                ability.Definition.Charges?.MaxCharges ?? 0, ability.ChargeRestoreRemaining, ability.Definition.Charges?.RestoreTime ?? 0f, ability.GetManaCost(), Mathf.Max(0f, ability.GetCastRange() + Engine.GetModifierProperty(ability.Owner, ModifierProperty.CastRangeBonus, null)));
            return ability != null;
        }

        public bool TryGetAbilityMetadata(AbilityKey key, out AbilityMetadata metadata, int level = 1)
        {
            metadata = default;
            if (!TryResolveAbilityName(key, out string name) || !Engine.TryGetAbilityDefinition(name, out AbilityDefinition definition)) return false;
            int resolvedLevel = Mathf.Clamp(level, 1, Mathf.Max(1, definition.MaxLevel));
            metadata = new AbilityMetadata(definition.Name, definition.MaxLevel, ToUsage(definition.Behavior), definition.ManaCost.GetValue(resolvedLevel), definition.CastRange.GetValue(resolvedLevel), definition.AoeRadius.GetValue(resolvedLevel));
            return true;
        }

        public AbilityCommandResult CheckCast(AbilityOwnerHandle owner, AbilityKey key)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            return ToCommandResult(ability == null ? MissingOwnedAbility() : Engine.EvaluateOrder(new CastOrder { Caster = ability.Owner, AbilityName = ability.Definition.Name }));
        }

        public AbilityCommandResult CheckCastOnTarget(AbilityOwnerHandle owner, AbilityKey key, AbilityOwnerHandle target)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            if (ability == null) return ToCommandResult(MissingOwnedAbility());
            TdUnit targetUnit = ResolveOwner(target);
            return ToCommandResult(targetUnit == null ? CastResult.Fail(CastFailureReason.InvalidTarget) : Engine.EvaluateOrder(new CastOrder { Caster = ability.Owner, AbilityName = ability.Definition.Name, Target = targetUnit }));
        }

        public AbilityCommandResult CheckCastAtPosition(AbilityOwnerHandle owner, AbilityKey key, Vector3 position)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            return ToCommandResult(ability == null ? MissingOwnedAbility() : Engine.EvaluateOrder(new CastOrder { Caster = ability.Owner, AbilityName = ability.Definition.Name, TargetPosition = position, HasTargetPosition = true }));
        }

        public AbilityCommandResult Cast(AbilityOwnerHandle owner, AbilityKey key, bool queue = false)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            return ToCommandResult(ability == null ? MissingOwnedAbility() : Engine.IssueOrder(new CastOrder { Caster = ability.Owner, AbilityName = ability.Definition.Name, Queue = queue }));
        }

        public AbilityCommandResult CastOnTarget(AbilityOwnerHandle owner, AbilityKey key, AbilityOwnerHandle target, bool queue = false)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            TdUnit targetUnit = ResolveOwner(target);
            if (ability == null) return ToCommandResult(MissingOwnedAbility());
            if (targetUnit == null) return ToCommandResult(CastResult.Fail(CastFailureReason.InvalidTarget));
            return ToCommandResult(Engine.IssueOrder(new CastOrder { Caster = ability.Owner, AbilityName = ability.Definition.Name, Target = targetUnit, Queue = queue }));
        }

        public AbilityCommandResult CastAtPosition(AbilityOwnerHandle owner, AbilityKey key, Vector3 position, bool queue = false)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            return ToCommandResult(ability == null ? MissingOwnedAbility() : Engine.IssueOrder(new CastOrder { Caster = ability.Owner, AbilityName = ability.Definition.Name, TargetPosition = position, HasTargetPosition = true, Queue = queue }));
        }

        public AbilityCommandResult CastAtBestTarget(AbilityOwnerHandle owner, AbilityKey key)
        {
            RuntimeAbility ability = FindOwnedAbility(owner, key);
            return ToCommandResult(ability == null ? MissingOwnedAbility() : CastExistingAtBestTarget(ability));
        }

        public bool Stop(AbilityOwnerHandle owner)
        {
            TdUnit unit = ResolveOwner(owner);
            if (unit == null) return false;
            Engine.Stop(unit);
            return true;
        }

        public bool IsAttackRestricted(Npc npc) => IsAttackRestricted(FindUnit(npc));

        public bool IsUnitActionRestricted(Npc npc)
        {
            TdUnit unit = FindUnit(npc);
            return !IsAvailable || Engine.HasState(unit, UnitState.Stunned) || Engine.HasState(unit, UnitState.CommandRestricted) || Engine.HasState(unit, UnitState.OutOfGame);
        }

        public bool IsAbilityExecuting(Tower tower)
        {
            TdUnit unit = FindUnit(tower);
            if (!IsAvailable || unit == null) return false;
            var abilities = Engine.GetAbilities(unit);
            if (abilities == null) return false;
            for (int i = 0; i < abilities.Count; i++)
            {
                if (abilities[i].Phase == AbilityPhase.Casting || abilities[i].Phase == AbilityPhase.Channeling) return true;
            }
            return false;
        }

        public float GetAttackIntervalMultiplier(Npc npc)
        {
            TdUnit unit = FindUnit(npc);
            if (!IsAvailable || unit == null) return 1f;
            float bonus = Engine.GetModifierProperty(unit, ModifierProperty.AttackSpeedBonus, null);
            return 1f / Mathf.Max(0.01f, 1f + bonus / 100f);
        }

        public AbilityDamageOutcome Attack(Tower tower, Npc target, float amount)
        {
            DamageResult result = ApplyTowerAttackDamage(tower, target, amount);
            return result == null ? new AbilityDamageOutcome(0, false, "RuntimeUnavailableOrRestricted") : new AbilityDamageOutcome(result.AppliedAmount, result.Killed, result.BlockReason);
        }

        internal void NotifyNpcDeath(Npc npc)
        {
            TdUnit unit = FindUnit(npc);
            if (IsAvailable && unit != null) Engine.NotifyDeath(unit);
        }

        internal TdUnit FindUnit(Npc npc)
        {
            if (!IsAvailable || npc == null || !npcUnits.TryGetValue(npc.GetInstanceID(), out TdUnit unit)) return null;
            return unit != null && unit.Npc == npc && unit.IsValidBinding ? unit : null;
        }

        internal TdUnit FindUnit(Tower tower)
        {
            if (!IsAvailable || tower == null || !towerUnits.TryGetValue(tower.GetInstanceID(), out TdUnit unit)) return null;
            return unit != null && unit.Tower == tower && unit.IsValidBinding ? unit : null;
        }

        private static bool IsRegisteredNpc(Npc npc)
        {
            var actors = NpcManager.Instance.ActiveNpcs;
            if (actors == null) return false;
            for (int i = 0; i < actors.Count; i++) if (actors[i] == npc) return true;
            return false;
        }

        private static bool IsRegisteredTower(Tower tower)
        {
            var actors = TowerManager.Instance.ActiveTowers;
            if (actors == null) return false;
            for (int i = 0; i < actors.Count; i++) if (actors[i] == tower) return true;
            return false;
        }

        private bool IsAttackRestricted(TdUnit unit)
        {
            return !IsAvailable || unit == null || !unit.IsAlive || Engine.HasState(unit, UnitState.Stunned) || Engine.HasState(unit, UnitState.Disarmed) || Engine.HasState(unit, UnitState.Hexed) || Engine.HasState(unit, UnitState.CommandRestricted) || Engine.HasState(unit, UnitState.OutOfGame);
        }

        private AbilityOwnerHandle ToHandle(TdUnit unit) => IsAvailable && unit != null && unit.IsValidBinding ? new AbilityOwnerHandle(battleSession, unit.EntityId) : default;

        private TdUnit ResolveOwner(AbilityOwnerHandle handle)
        {
            if (!IsAvailable || !handle.IsValid || handle.Session != battleSession) return null;
            return unitsByRuntimeId.TryGetValue(handle.EntityId, out TdUnit unit) && unit.IsValidBinding ? unit : null;
        }

        private void IndexBinding(TdUnit unit) => unitsByRuntimeId[unit.EntityId] = unit;

        private static AbilityExecutionPhase ToExecutionPhase(AbilityPhase phase)
        {
            switch (phase)
            {
                case AbilityPhase.Casting: return AbilityExecutionPhase.Casting;
                case AbilityPhase.Channeling: return AbilityExecutionPhase.Channeling;
                case AbilityPhase.Backswing: return AbilityExecutionPhase.Backswing;
                default: return AbilityExecutionPhase.Idle;
            }
        }

        private static AbilityUsage ToUsage(AbilityBehavior behavior)
        {
            AbilityUsage usage = AbilityUsage.None;
            if ((behavior & AbilityBehavior.NoTarget) != 0) usage |= AbilityUsage.NoTarget;
            if ((behavior & AbilityBehavior.UnitTarget) != 0) usage |= AbilityUsage.UnitTarget;
            if ((behavior & AbilityBehavior.PointTarget) != 0) usage |= AbilityUsage.PositionTarget;
            if ((behavior & AbilityBehavior.OptionalUnitTarget) != 0) usage |= AbilityUsage.OptionalUnitTarget;
            if ((behavior & AbilityBehavior.Directional) != 0) usage |= AbilityUsage.Directional;
            if ((behavior & AbilityBehavior.Passive) != 0) usage |= AbilityUsage.Passive;
            if ((behavior & AbilityBehavior.Hidden) != 0) usage |= AbilityUsage.Hidden;
            if ((behavior & AbilityBehavior.Toggle) != 0) usage |= AbilityUsage.Toggle;
            if ((behavior & AbilityBehavior.Channelled) != 0) usage |= AbilityUsage.Channelled;
            if ((behavior & AbilityBehavior.Immediate) != 0) usage |= AbilityUsage.Immediate;
            if ((behavior & AbilityBehavior.Aoe) != 0) usage |= AbilityUsage.Area;
            return usage;
        }

        private bool TryResolveAbilityName(AbilityKey key, out string name)
        {
            name = null;
            if (!IsAvailable || !key.IsValid) return false;
            if (key.IsStableId) return DefinitionRegistry != null && DefinitionRegistry.TryGetAbilityNameByStableId(key.Value, out name);
            if (!Engine.TryGetAbilityDefinition(key.Value, out AbilityDefinition definition)) return false;
            name = definition.Name;
            return true;
        }

        private RuntimeAbility FindOwnedAbility(AbilityOwnerHandle owner, AbilityKey key)
        {
            TdUnit unit = ResolveOwner(owner);
            return unit != null && TryResolveAbilityName(key, out string name) ? Engine.FindAbility(unit, name) : null;
        }

        private Modifier ResolveEffect(AbilityEffectHandle handle)
        {
            if (!IsAvailable || !handle.IsValid || handle.Session != battleSession || !controlledEffects.TryGetValue(handle.Id, out Modifier modifier)) return null;
            if (modifier != null && !modifier.IsDestroyed && modifier.Parent != null && modifier.Parent is TdUnit unit && unit.IsValidBinding) return modifier;
            controlledEffects.Remove(handle.Id);
            return null;
        }

        private void PruneControlledEffects()
        {
            expiredEffectHandles.Clear();
            foreach (var pair in controlledEffects)
            {
                if (pair.Value == null || pair.Value.IsDestroyed || pair.Value.Parent is TdUnit unit && !unit.IsValidBinding) expiredEffectHandles.Add(pair.Key);
            }
            foreach (int id in expiredEffectHandles) controlledEffects.Remove(id);
        }

        private CastResult MissingOwnedAbility() => CastResult.Fail(IsAvailable ? CastFailureReason.MissingAbility : CastFailureReason.RuntimeUnavailable);
        private static AbilityCommandResult ToCommandResult(CastResult result)
        {
            AbilityCommandFailure failure;
            switch (result?.FailureReason ?? CastFailureReason.RuntimeUnavailable)
            {
                case CastFailureReason.None: failure = AbilityCommandFailure.None; break;
                case CastFailureReason.RuntimeUnavailable: failure = AbilityCommandFailure.Unavailable; break;
                case CastFailureReason.MissingAbility: failure = AbilityCommandFailure.MissingAbility; break;
                case CastFailureReason.InvalidTarget: failure = AbilityCommandFailure.InvalidTarget; break;
                case CastFailureReason.OutOfRange: failure = AbilityCommandFailure.OutOfRange; break;
                case CastFailureReason.Cooldown: failure = AbilityCommandFailure.Cooldown; break;
                case CastFailureReason.NoCharges: failure = AbilityCommandFailure.NoCharges; break;
                case CastFailureReason.NotEnoughMana: failure = AbilityCommandFailure.InsufficientResource; break;
                case CastFailureReason.Stunned:
                case CastFailureReason.Silenced:
                case CastFailureReason.Rooted:
                case CastFailureReason.CommandRestricted:
                case CastFailureReason.Channeling:
                case CastFailureReason.Casting: failure = AbilityCommandFailure.Restricted; break;
                default: failure = AbilityCommandFailure.Rejected; break;
            }
            return new AbilityCommandResult(result != null && result.Success, failure, result?.FailureReason.ToString() ?? "RuntimeUnavailable", result?.Message, result?.CastId ?? 0, result != null && result.Queued);
        }

        private void OnRuntimeEvent(RuntimeEvent runtimeEvent)
        {
            if (!IsAvailable || runtimeEvent == null || runtimeEvent.CastId <= 0 || !(runtimeEvent.Caster is TdUnit unit)) return;
            AbilityCommandStage stage;
            switch (runtimeEvent.EventType)
            {
                case RuntimeEventType.CastStarted: stage = AbilityCommandStage.Started; break;
                case RuntimeEventType.SpellStarted: stage = AbilityCommandStage.Executed; break;
                case RuntimeEventType.ChannelStarted: stage = AbilityCommandStage.ChannelStarted; break;
                case RuntimeEventType.ChannelFinished: stage = AbilityCommandStage.ChannelFinished; break;
                case RuntimeEventType.CastInterrupted: stage = AbilityCommandStage.Interrupted; break;
                case RuntimeEventType.CastCompleted: stage = AbilityCommandStage.Completed; break;
                case RuntimeEventType.CastFailed: stage = AbilityCommandStage.Rejected; break;
                default: return;
            }
            AbilityOwnerHandle owner = ToHandle(unit);
            if (!owner.IsValid) return;
            int session = battleSession;
            long generation = Engine.RuntimeGeneration;
            AbilityCommandFailure failure = ToCommandResult(new CastResult { FailureReason = runtimeEvent.FailureReason }).Failure;
            AbilityCommandUpdate update = new AbilityCommandUpdate(runtimeEvent.CastId, owner, runtimeEvent.Ability?.Definition.Name, stage, failure, runtimeEvent.Message);
            System.Action<AbilityCommandUpdate> listeners = CommandUpdated;
            if (listeners == null) return;
            foreach (System.Action<AbilityCommandUpdate> listener in listeners.GetInvocationList())
            {
                if (!IsAvailable || session != battleSession || generation != Engine.RuntimeGeneration || ResolveOwner(owner) == null) break;
                try { listener(update); }
                catch (System.Exception exception) { Debug.LogException(exception); }
            }
        }

        private CastResult CastExistingAtBestTarget(RuntimeAbility ability)
        {
            AbilityDefinition definition = ability.Definition;
            if ((definition.Behavior & AbilityBehavior.Passive) != 0) return CastResult.Fail(CastFailureReason.AbilityPassive);
            if ((definition.Behavior & (AbilityBehavior.UnitTarget | AbilityBehavior.OptionalUnitTarget | AbilityBehavior.PointTarget)) != 0)
            {
                IUnit target = FindNearestTarget(ability.Owner, ability);
                if (target == null) return (definition.Behavior & AbilityBehavior.OptionalUnitTarget) != 0 ? Engine.CastAbility(ability.Owner, definition.Name) : CastResult.Fail(CastFailureReason.InvalidTarget, "No valid target.");
                if ((definition.Behavior & (AbilityBehavior.UnitTarget | AbilityBehavior.OptionalUnitTarget)) != 0) return Engine.CastAbilityOnTarget(ability.Owner, definition.Name, target);
                return Engine.CastAbilityOnPosition(ability.Owner, definition.Name, target.Position);
            }
            return Engine.CastAbility(ability.Owner, definition.Name);
        }
    }
}
