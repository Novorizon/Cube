using Game.Ability;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Business object categories currently exposed to the ability runtime.
    /// </summary>
    public enum TdUnitKind
    {
        None = 0,
        Npc = 1,
        Tower = 2,
        Base = 3
    }

    /// <summary>
    /// Adapter that exposes Npc, Tower, and Base as ability-system units.
    /// It is also the only place where damage/heal results mutate real TD game state.
    /// </summary>
    public sealed class TdUnit : IUnit, ICombatUnit
    {
        public const int PlayerTeamId = 1;
        public const int EnemyTeamId = 2;
        public const int BaseEntityId = -1000001;

        private readonly Npc npc;
        private readonly Tower tower;
        private readonly TdUnitKind kind;
        private readonly int entityId;
        private bool invalidated;

        public TdUnit(Npc npc)
            : this(npc != null ? npc.GetInstanceID() : 0, npc)
        {
        }

        internal TdUnit(int entityId, Npc npc)
        {
            this.entityId = entityId;
            this.npc = npc;
            kind = TdUnitKind.Npc;
        }

        public TdUnit(Tower tower)
            : this(tower != null ? tower.GetInstanceID() : 0, tower)
        {
        }

        internal TdUnit(int entityId, Tower tower)
        {
            this.entityId = entityId;
            this.tower = tower;
            kind = TdUnitKind.Tower;
        }

        private TdUnit(int runtimeEntityId)
        {
            entityId = runtimeEntityId;
            kind = TdUnitKind.Base;
        }

        public static TdUnit CreateBaseUnit(int runtimeEntityId = BaseEntityId)
        {
            return new TdUnit(runtimeEntityId);
        }

        public TdUnitKind Kind => kind;

        public Npc Npc => npc;

        public Tower Tower => tower;

        public int EntityId => entityId;

        public bool IsValidBinding
        {
            get
            {
                if (invalidated) return false;
                switch (kind)
                {
                    case TdUnitKind.Npc:
                        return npc != null;

                    case TdUnitKind.Tower:
                        return tower != null;

                    case TdUnitKind.Base:
                        return BaseManager.Instance.HasBaseObject;

                    default:
                        return false;
                }
            }
        }

        public int TeamId
        {
            get
            {
                if (kind == TdUnitKind.Npc && npc != null && npc.ActorType == ActorType.Enemy)
                {
                    return EnemyTeamId;
                }

                return PlayerTeamId;
            }
        }

        public UnitType UnitType
        {
            get
            {
                switch (kind)
                {
                    case TdUnitKind.Npc:
                        return GetNpcUnitType();

                    case TdUnitKind.Tower:
                    case TdUnitKind.Base:
                        return UnitType.Building;

                    default:
                        return UnitType.Other;
                }
            }
        }

        internal void Invalidate() => invalidated = true;

        public float CurrentHealth => kind == TdUnitKind.Npc ? npc?.Data?.CurrentHp ?? 0 : kind == TdUnitKind.Base ? BaseManager.Instance.CurrentLife : 0;
        public float MaxHealth => kind == TdUnitKind.Npc ? npc?.Data?.MaxHp ?? 0 : kind == TdUnitKind.Base ? BaseManager.Instance.MaxLife : 0;
        public float BaseArmor => 0f;
        public float BaseMagicResistancePercent => 0f;
        public float BasePhysicalDamageBlock => kind == TdUnitKind.Base ? BaseManager.Instance.Defense : 0f;

        public bool IsAlive
        {
            get
            {
                if (invalidated) return false;
                switch (kind)
                {
                    case TdUnitKind.Npc:
                        return npc != null && npc.Data != null && !npc.Data.Dead && npc.Data.CurrentHp > 0;

                    case TdUnitKind.Tower:
                        return tower != null && tower.gameObject != null && tower.gameObject.activeInHierarchy;

                    case TdUnitKind.Base:
                        return BaseManager.Instance.HasBaseObject && !BaseManager.Instance.IsDead;

                    default:
                        return false;
                }
            }
        }

        public bool IsMagicImmune => false;

        public bool IsInvulnerable => false;

        public Vector3 Position
        {
            get
            {
                switch (kind)
                {
                    case TdUnitKind.Npc:
                        return npc != null ? npc.transform.position : Vector3.zero;

                    case TdUnitKind.Tower:
                        return tower != null ? tower.transform.position : Vector3.zero;

                    case TdUnitKind.Base:
                        return BaseManager.Instance.BasePosition;

                    default:
                        return Vector3.zero;
                }
            }
        }

        public bool IsVisibleToTeam(int teamId)
        {
            return true;
        }

        public void ApplyDamage(DamageResult result) => ApplyFinalDamage(result);

        public DamageApplicationResult ApplyFinalDamage(DamageResult result)
        {
            if (!IsValidBinding || !IsAlive || result == null || result.Blocked || float.IsNaN(result.FinalAmount) || float.IsInfinity(result.FinalAmount)) return new DamageApplicationResult(0f);
            int damage = RoundHealthAmount(result.FinalAmount);
            if (damage <= 0) return new DamageApplicationResult(0f);
            int applied = 0;
            bool killed = false;
            switch (kind)
            {
                case TdUnitKind.Npc:
                    applied = NpcManager.Instance.ApplyFinalDamage(npc, damage, out killed);
                    break;
                case TdUnitKind.Base:
                    applied = BaseManager.Instance.ApplyFinalDamage(damage, out killed);
                    break;
            }
            return new DamageApplicationResult(applied, killed);
        }

        public void Heal(HealInfo info) => ApplyFinalHeal(info);

        public HealApplicationResult ApplyFinalHeal(HealInfo info)
        {
            if (!IsValidBinding || !IsAlive || info == null || info.Amount <= 0f || float.IsNaN(info.Amount) || float.IsInfinity(info.Amount)) return new HealApplicationResult(0f);
            int amount = RoundHealthAmount(info.Amount);
            if (amount <= 0) return new HealApplicationResult(0f);
            int applied = 0;
            switch (kind)
            {
                case TdUnitKind.Npc:
                    applied = HealNpc(amount);
                    break;
                case TdUnitKind.Base:
                    applied = BaseManager.Instance.ApplyFinalHeal(amount);
                    break;
            }
            return new HealApplicationResult(applied);
        }

        private static int RoundHealthAmount(float amount) => (int)System.Math.Min(int.MaxValue, System.Math.Max(0d, System.Math.Round((double)amount)));

        private UnitType GetNpcUnitType()
        {
            if (npc == null)
            {
                return UnitType.Basic;
            }

            if (npc.ActorType == ActorType.Hero)
            {
                return UnitType.Hero | UnitType.Basic;
            }

            if (npc.ActorType == ActorType.Enemy)
            {
                return UnitType.Creep | UnitType.Basic;
            }

            return UnitType.Basic;
        }

        private int HealNpc(int amount)
        {
            if (npc == null || npc.Data == null || npc.Data.Dead)
            {
                return 0;
            }

            int before = npc.Data.CurrentHp;
            npc.Data.CurrentHp = (int)System.Math.Min(npc.Data.MaxHp, (long)npc.Data.CurrentHp + amount);

            int applied = npc.Data.CurrentHp - before;
            if (applied > 0)
            {
                Debug.Log($"Npc healed. Id: {npc.Config?.Id}, Heal: {npc.Data.CurrentHp - before}, Hp: {npc.Data.CurrentHp}/{npc.Data.MaxHp}");
            }
            return applied;
        }
    }
}

