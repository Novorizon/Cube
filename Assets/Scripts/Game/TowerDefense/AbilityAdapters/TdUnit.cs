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
    public sealed class TdUnit : IUnit
    {
        public const int PlayerTeamId = 1;
        public const int EnemyTeamId = 2;
        public const int BaseEntityId = -1000001;

        private readonly Npc npc;
        private readonly Tower tower;
        private readonly TdUnitKind kind;
        private readonly int entityId;

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

        private TdUnit()
        {
            entityId = BaseEntityId;
            kind = TdUnitKind.Base;
        }

        public static TdUnit CreateBaseUnit()
        {
            return new TdUnit();
        }

        public TdUnitKind Kind => kind;

        public Npc Npc => npc;

        public Tower Tower => tower;

        public int EntityId => entityId;

        public bool IsValidBinding
        {
            get
            {
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

        public bool IsAlive
        {
            get
            {
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

        public void ApplyDamage(DamageResult result)
        {
            if (result == null || result.Blocked)
            {
                return;
            }

            int damage = Mathf.RoundToInt(result.FinalAmount);
            if (damage <= 0)
            {
                return;
            }

            // The engine has already handled immunity and modifiers; apply the final integer damage.
            switch (kind)
            {
                case TdUnitKind.Npc:
                    NpcManager.Instance.TakeDamage(npc, damage);
                    break;

                case TdUnitKind.Base:
                    BaseManager.Instance.TakeDamage(damage);
                    break;
            }
        }

        public void Heal(HealInfo info)
        {
            if (info == null || info.Amount <= 0f)
            {
                return;
            }

            int amount = Mathf.RoundToInt(info.Amount);
            if (amount <= 0)
            {
                return;
            }

            // Healing is rounded at the adapter boundary to match existing integer HP data.
            switch (kind)
            {
                case TdUnitKind.Npc:
                    HealNpc(amount);
                    break;

                case TdUnitKind.Base:
                    BaseManager.Instance.Heal(amount);
                    break;
            }
        }

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

        private void HealNpc(int amount)
        {
            if (npc == null || npc.Data == null || npc.Data.Dead)
            {
                return;
            }

            int before = npc.Data.CurrentHp;
            npc.Data.CurrentHp = Mathf.Min(npc.Data.MaxHp, npc.Data.CurrentHp + amount);

            if (npc.Data.CurrentHp != before)
            {
                Debug.Log($"Npc healed. Id: {npc.Config?.Id}, Heal: {npc.Data.CurrentHp - before}, Hp: {npc.Data.CurrentHp}/{npc.Data.MaxHp}");
            }
        }
    }
}
