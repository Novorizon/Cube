using System;

namespace Game
{
    /// <summary>A definition identity, independent from Excel/JSON/code storage.</summary>
    public readonly struct AbilityKey
    {
        private AbilityKey(string value, bool stableId) { Value = value; IsStableId = stableId; }
        public string Value { get; }
        public bool IsStableId { get; }
        public bool IsValid => !string.IsNullOrWhiteSpace(Value);
        public static AbilityKey ByName(string name) => new AbilityKey(name, false);
        public static AbilityKey ByStableId(string stableId) => new AbilityKey(stableId, true);
        public static AbilityKey FromLegacyId(int skillId) => ByName("skill_" + skillId);
    }

    /// <summary>A battle-scoped actor reference. Old references cannot address a new battle.</summary>
    public readonly struct AbilityOwnerHandle : IEquatable<AbilityOwnerHandle>
    {
        internal AbilityOwnerHandle(int session, int entityId) { Session = session; EntityId = entityId; }
        internal int Session { get; }
        public int EntityId { get; }
        public bool IsValid => EntityId != 0;
        public bool Equals(AbilityOwnerHandle other) => Session == other.Session && EntityId == other.EntityId;
        public override bool Equals(object obj) => obj is AbilityOwnerHandle other && Equals(other);
        public override int GetHashCode() => (Session * 397) ^ EntityId;
    }

    /// <summary>Accepted means the order was accepted; its cast point/channel can still be interrupted.</summary>
    public enum AbilityCommandFailure
    {
        None, Unavailable, MissingAbility, InvalidTarget, OutOfRange, Restricted, Cooldown, NoCharges, InsufficientResource, Rejected
    }

    public readonly struct AbilityCommandResult
    {
        internal AbilityCommandResult(bool accepted, AbilityCommandFailure failure, string reason, string message, long castId, bool queued) { Accepted = accepted; Failure = failure; Reason = reason; Message = message; CastId = castId; Queued = queued; }
        public bool Accepted { get; }
        public AbilityCommandFailure Failure { get; }
        public string Reason { get; }
        public string Message { get; }
        public long CastId { get; }
        public bool Queued { get; }
    }

    public enum AbilityCommandStage { Started, Executed, ChannelStarted, ChannelFinished, Interrupted, Completed, Rejected }

    public readonly struct AbilityCommandUpdate
    {
        internal AbilityCommandUpdate(long castId, AbilityOwnerHandle owner, string name, AbilityCommandStage stage, AbilityCommandFailure failure, string message)
        {
            CastId = castId; Owner = owner; AbilityName = name; Stage = stage; Failure = failure; Message = message;
        }
        public long CastId { get; }
        public AbilityOwnerHandle Owner { get; }
        public string AbilityName { get; }
        public AbilityCommandStage Stage { get; }
        public AbilityCommandFailure Failure { get; }
        public string Message { get; }
    }

    public readonly struct AbilityAttackHandle
    {
        internal AbilityAttackHandle(int session, long attackId) { Session = session; AttackId = attackId; }
        internal int Session { get; }
        public long AttackId { get; }
        public bool IsValid => AttackId > 0;
    }

    public enum AbilityExecutionPhase { Idle, Casting, Channeling, Backswing }

    [Flags]
    public enum AbilityUsage
    {
        None = 0, NoTarget = 1, UnitTarget = 2, PositionTarget = 4, OptionalUnitTarget = 8,
        Directional = 16, Passive = 32, Hidden = 64, Toggle = 128, Channelled = 256, Immediate = 512, Area = 1024
    }

    /// <summary>Definition metadata at the requested level. Instance status includes script and modifier adjustments.</summary>
    public readonly struct AbilityMetadata
    {
        internal AbilityMetadata(string name, int maxLevel, AbilityUsage usage, float resourceCost, float castRange, float areaRadius)
        {
            Name = name; MaxLevel = maxLevel; Usage = usage; ResourceCost = resourceCost; CastRange = castRange; AreaRadius = areaRadius;
        }
        public string Name { get; }
        public int MaxLevel { get; }
        public AbilityUsage Usage { get; }
        public float ResourceCost { get; }
        public float CastRange { get; }
        public float AreaRadius { get; }
    }

    public readonly struct AbilityStatus
    {
        internal AbilityStatus(string name, int level, AbilityExecutionPhase phase, float remaining, float duration, int charges, bool activated, bool toggleEnabled, int maxCharges = 0, float chargeRestoreRemaining = 0f, float chargeRestoreDuration = 0f, float resourceCost = 0f, float castRange = 0f)
        {
            Name = name; Level = level; ExecutionPhase = phase; CooldownRemaining = remaining; CooldownDuration = duration;
            Charges = charges; Activated = activated; ToggleEnabled = toggleEnabled;
            MaxCharges = maxCharges; ChargeRestoreRemaining = chargeRestoreRemaining; ChargeRestoreDuration = chargeRestoreDuration; ResourceCost = resourceCost; CastRange = castRange;
        }
        public string Name { get; }
        public int Level { get; }
        public AbilityExecutionPhase ExecutionPhase { get; }
        // Compatibility for earlier consumers; fixed literals do not allocate on status polling.
        public string Phase => ExecutionPhase == AbilityExecutionPhase.Casting ? "Casting" : ExecutionPhase == AbilityExecutionPhase.Channeling ? "Channeling" : ExecutionPhase == AbilityExecutionPhase.Backswing ? "Backswing" : "Idle";
        public float CooldownRemaining { get; }
        public float CooldownDuration { get; }
        public int Charges { get; }
        public bool Activated { get; }
        public bool ToggleEnabled { get; }
        public int MaxCharges { get; }
        public float ChargeRestoreRemaining { get; }
        public float ChargeRestoreDuration { get; }
        public float ResourceCost { get; }
        public float CastRange { get; }
    }

    public readonly struct AbilityDamageOutcome
    {
        internal AbilityDamageOutcome(float amount, bool killed, string rejection) { AppliedAmount = amount; Killed = killed; Rejection = rejection; }
        public float AppliedAmount { get; }
        public bool Killed { get; }
        public string Rejection { get; }
    }

    public readonly struct AbilityEffectHandle
    {
        internal AbilityEffectHandle(int session, int id) { Session = session; Id = id; }
        internal int Session { get; }
        internal int Id { get; }
        public bool IsValid => Id != 0;
    }

    public readonly struct AbilityEffectStatus
    {
        internal AbilityEffectStatus(string name, int stacks, float remaining) { Name = name; Stacks = stacks; RemainingTime = remaining; }
        public string Name { get; }
        public int Stacks { get; }
        public float RemainingTime { get; }
    }
}
